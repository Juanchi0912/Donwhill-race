using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Convierte un heightmap (PNG exportado de una página de mapas) en un terreno de Unity a escala real,
/// en una escena aparte que no entra al juego: con pasto, roca, una capa de tierra para pintar senderos
/// y árboles repartidos en las laderas. Se usa desde la ventana DownhillRace → Crear mapa desde heightmap.
/// </summary>
public static class HeightmapMapBuilder
{
    public const string MapsFolder = "Assets/Mapas";
    private const string PrefabsFolder = "Assets/Prefabs/Vegetacion";

    // Por encima de esta pendiente no crecen árboles (es roca o barranco).
    private const float MaxTreeSlope = 38f;
    // Peso mínimo de la capa de tierra para considerar que un punto es sendero.
    private const float TrailThreshold = 0.25f;

    /// <summary>Ajustes de la ventana; se guardan entre sesiones.</summary>
    [System.Serializable]
    public class Settings
    {
        public string MapName = "MapaReal";
        public string HeightmapPath = MapsFolder + "/MapaReal_Alturas.png";
        public float Width = 1500f;            // Ancho real de la imagen completa (oeste → este), en metros.
        public float Length = 1500f;           // Largo real de la imagen completa (sur → norte), en metros.
        public float HeightDifference = 331f;  // Del punto más bajo al más alto de la imagen completa.
        public Vector2 CropX = new Vector2(0f, 1f);
        public Vector2 CropZ = new Vector2(0f, 1f);
        public int Resolution = 1025;
        public float SmoothMeters = 6f;
        public TrackLayout.Theme Ambience = TrackLayout.Theme.BosqueNublado;
        public float TreesPerHectare = 20f;
        public int Seed = 1;

        public float CroppedWidth => Width * Mathf.Max(0.01f, CropX.y - CropX.x);
        public float CroppedLength => Length * Mathf.Max(0.01f, CropZ.y - CropZ.x);
    }

    // =====================================================================
    // Crear el mapa
    // =====================================================================

    /// <summary>Crea (o reemplaza) la escena Assets/Scenes/&lt;nombre&gt;.unity con el terreno del heightmap.</summary>
    public static void Build(Settings s)
    {
        if (string.IsNullOrWhiteSpace(s.MapName) || s.MapName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            EditorUtility.DisplayDialog("Crear mapa", "Escribe un nombre de mapa válido (sin / \\ : * ? \" < > |).", "Entendido");
            return;
        }
        if (!File.Exists(s.HeightmapPath))
        {
            EditorUtility.DisplayDialog("Crear mapa", "No encuentro el heightmap en " + s.HeightmapPath + ".", "Entendido");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string scenePath = "Assets/Scenes/" + s.MapName + ".unity";
        if (File.Exists(scenePath) && !EditorUtility.DisplayDialog("Crear mapa",
                "Ya existe " + scenePath + ".\nSi lo creas de nuevo se borran los senderos y árboles que hayas pintado en él.",
                "Crear de nuevo", "Cancelar"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Crear mapa", "Leyendo el heightmap", 0.05f);
            float[,] image = PngHeightmapReader.Read(s.HeightmapPath, out int bitDepth);

            EditorUtility.DisplayProgressBar("Crear mapa", "Recortando y suavizando las alturas", 0.2f);
            int res = s.Resolution;
            float sizeX = s.CroppedWidth, sizeZ = s.CroppedLength;
            float[,] heights = Resample(image, s.CropX, s.CropZ, res);
            float metersPerSample = Mathf.Max(sizeX, sizeZ) / (res - 1);
            Smooth(heights, Mathf.RoundToInt(s.SmoothMeters / metersPerSample));

            // Se conserva la escala real: el recorte mide solo el desnivel que tiene dentro.
            float min = float.MaxValue, max = float.MinValue;
            foreach (float h in heights)
            {
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            float range = Mathf.Max(max - min, 1e-4f);
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++) heights[z, x] = (heights[z, x] - min) / range;
            }
            float sizeY = Mathf.Max(1f, s.HeightDifference * range);

            EditorUtility.DisplayProgressBar("Crear mapa", "Creando el terreno", 0.4f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VisualAssetFactory.EnsureFolder(MapsFolder);
            string dataPath = MapsFolder + "/" + s.MapName + "_Terreno.asset";
            AssetDatabase.DeleteAsset(dataPath);
            TerrainData data = new TerrainData();
            data.heightmapResolution = res;
            data.size = new Vector3(sizeX, sizeY, sizeZ);
            data.SetHeights(0, 0, heights);

            EditorUtility.DisplayProgressBar("Crear mapa", "Pintando pasto y roca", 0.55f);
            PaintLayers(data, s);

            EditorUtility.DisplayProgressBar("Crear mapa", "Plantando árboles", 0.7f);
            int trees = ScatterTrees(data, s, null);
            AssetDatabase.CreateAsset(data, dataPath);

            EditorUtility.DisplayProgressBar("Crear mapa", "Luz, cielo y cámara", 0.85f);
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Mapa";
            // Centrado en el origen: así es más fácil ubicarse en la vista de escena.
            go.transform.position = new Vector3(-sizeX * 0.5f, 0f, -sizeZ * 0.5f);
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = VisualAssetFactory.TerrainMaterial("Terreno");
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 400f;
            terrain.drawInstanced = true;
            terrain.treeDistance = 1000f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            BuildEnvironment(s, sizeX, sizeY, sizeZ);

            VisualAssetFactory.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = go;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0f, sizeY * 0.5f, 0f), new Vector3(sizeX, sizeY, sizeZ)), false);
            }
            Debug.Log("DownhillRace: mapa '" + s.MapName + "' creado en " + scenePath + " (" + sizeX.ToString("0") + " × " +
                      sizeZ.ToString("0") + " m, " + sizeY.ToString("0") + " m de desnivel, heightmap de " + bitDepth +
                      " bits, " + trees + " árboles). No está en el juego: es solo para diseñar.");
        }
        catch (InvalidDataException e)
        {
            EditorUtility.DisplayDialog("Crear mapa", "No pude leer el heightmap: " + e.Message, "Entendido");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Pasa la imagen a la cuadrícula del terreno. La fila 0 de la imagen es el norte (arriba);
    /// en el terreno la fila 0 es el sur, por eso se invierte.
    /// </summary>
    private static float[,] Resample(float[,] image, Vector2 cropX, Vector2 cropZ, int res)
    {
        int rows = image.GetLength(0), cols = image.GetLength(1);
        float[,] result = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            float north = Mathf.Lerp(cropZ.x, cropZ.y, z / (float)(res - 1));
            float row = (1f - north) * (rows - 1);
            int r0 = Mathf.Clamp(Mathf.FloorToInt(row), 0, rows - 1), r1 = Mathf.Min(r0 + 1, rows - 1);
            float fr = row - r0;
            for (int x = 0; x < res; x++)
            {
                float col = Mathf.Lerp(cropX.x, cropX.y, x / (float)(res - 1)) * (cols - 1);
                int c0 = Mathf.Clamp(Mathf.FloorToInt(col), 0, cols - 1), c1 = Mathf.Min(c0 + 1, cols - 1);
                float fc = col - c0;
                float top = Mathf.Lerp(image[r0, c0], image[r0, c1], fc);
                float bottom = Mathf.Lerp(image[r1, c0], image[r1, c1], fc);
                result[z, x] = Mathf.Lerp(top, bottom, fr);
            }
        }
        return result;
    }

    /// <summary>
    /// Tres pasadas de promedio móvil (se parece a un desenfoque gaussiano). Borra los escalones
    /// de los heightmaps de 8 bits, que solo tienen 256 alturas distintas.
    /// </summary>
    private static void Smooth(float[,] heights, int radius)
    {
        if (radius <= 0) return;
        int n = heights.GetLength(0);
        float[] line = new float[n];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++) line[x] = heights[z, x];
                for (int x = 0; x < n; x++) heights[z, x] = BoxAverage(line, x, radius);
            }
            for (int x = 0; x < n; x++)
            {
                for (int z = 0; z < n; z++) line[z] = heights[z, x];
                for (int z = 0; z < n; z++) heights[z, x] = BoxAverage(line, z, radius);
            }
        }
    }

    private static float BoxAverage(float[] line, int center, int radius)
    {
        // En los bordes se repite el último valor, para que la orilla del mapa no se hunda.
        float sum = 0f;
        for (int k = -radius; k <= radius; k++) sum += line[Mathf.Clamp(center + k, 0, line.Length - 1)];
        return sum / (radius * 2 + 1);
    }

    // =====================================================================
    // Texturas: pasto, roca y tierra para los senderos
    // =====================================================================

    private static void PaintLayers(TerrainData data, Settings s)
    {
        bool dry = s.Ambience == TrackLayout.Theme.ValleSeco;
        TerrainLayer grass = Layer(s.MapName + "_CapaPasto", dry ? "PastoSeco" : "Pasto", 8f);
        TerrainLayer rock = Layer(s.MapName + "_CapaRoca", dry ? "RocaSeca" : "Roca", 6f);
        TerrainLayer dirt = Layer(s.MapName + "_CapaTierra", dry ? "TierraSeca" : "Tierra", 4f);
        // La tierra queda en 0: es la que se pinta a mano para marcar los senderos.
        data.terrainLayers = new[] { grass, rock, dirt };

        // Más resolución en mapas grandes para que un sendero de 2-3 m se vea nítido.
        int alphaRes = Mathf.Max(data.size.x, data.size.z) > 1200f ? 2048 : 1024;
        data.alphamapResolution = alphaRes;
        float[,,] alpha = new float[alphaRes, alphaRes, 3];
        for (int y = 0; y < alphaRes; y++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                float steep = data.GetSteepness(x / (float)(alphaRes - 1), y / (float)(alphaRes - 1));
                float rockAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 45f, steep));
                alpha[y, x, 0] = 1f - rockAmount;
                alpha[y, x, 1] = rockAmount;
            }
        }
        data.SetAlphamaps(0, 0, alpha);
    }

    private static TerrainLayer Layer(string name, string texture, float tileSize)
    {
        string folder = VisualAssetFactory.TexturesFolder + "/";
        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + texture + ".png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + texture + "Normal.png");
        if (albedo == null)
        {
            Debug.LogWarning("DownhillRace: falta la textura " + texture + ". Construye una pista (DownhillRace → Crear escena...) para generarla.");
        }
        return VisualAssetFactory.TerrainLayerAsset(name, albedo, normal, tileSize);
    }

    // =====================================================================
    // Árboles
    // =====================================================================

    /// <summary>
    /// Reparte árboles y arbustos en manchas de bosque, sin ponerlos en lo muy empinado
    /// ni sobre los senderos (si se pasan los pesos de la capa de tierra). Devuelve cuántos plantó.
    /// </summary>
    private static int ScatterTrees(TerrainData data, Settings s, float[,] trail)
    {
        bool dry = s.Ambience == TrackLayout.Theme.ValleSeco;
        List<GameObject> tall = new List<GameObject>(), broad = new List<GameObject>(), bushes = new List<GameObject>();
        Material bark = LoadMaterial("Tronco");
        Material leaves = LoadMaterial(dry ? "HojasSecas" : "Hojas");
        if (!dry)
        {
            Material needles = LoadMaterial("Agujas");
            for (int i = 1; i <= 4; i++) tall.Add(VegetationPrefab("Pino" + i, "Pino" + i, true, bark, needles));
        }
        for (int i = 1; i <= 2; i++) broad.Add(VegetationPrefab((dry ? "ArbolSeco" : "ArbolFrondoso") + i, "ArbolFrondoso" + i, true, bark, leaves));
        for (int i = 1; i <= 3; i++) bushes.Add(VegetationPrefab((dry ? "ArbustoSeco" : "Arbusto") + i, "Arbusto" + i, false, bark, leaves));

        List<GameObject> all = new List<GameObject>();
        all.AddRange(tall);
        all.AddRange(broad);
        all.AddRange(bushes);
        all.RemoveAll(prefab => prefab == null);
        if (all.Count == 0)
        {
            Debug.LogWarning("DownhillRace: no hay modelos de árboles. Construye una pista (DownhillRace → Crear escena...) para generarlos.");
            data.treeInstances = new TreeInstance[0];
            return 0;
        }
        TreePrototype[] prototypes = new TreePrototype[all.Count];
        for (int i = 0; i < all.Count; i++) prototypes[i] = new TreePrototype { prefab = all[i], bendFactor = 0f };
        data.treePrototypes = prototypes;

        System.Random rng = new System.Random(s.Seed);
        float hectares = data.size.x * data.size.z / 10000f;
        int target = Mathf.RoundToInt(hectares * Mathf.Max(0f, s.TreesPerHectare));
        List<TreeInstance> trees = new List<TreeInstance>(target);
        float noiseOffset = s.Seed * 17.31f;
        for (int attempt = 0; attempt < target * 6 && trees.Count < target; attempt++)
        {
            float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
            float steep = data.GetSteepness(u, v);
            if (steep > MaxTreeSlope) continue;
            if (trail != null && OnTrail(trail, u, v, 2.5f / data.size.x * trail.GetLength(1))) continue;

            // Manchas de bosque con claros entre ellas, más ralas al acercarse a lo empinado.
            float patch = Mathf.PerlinNoise(u * data.size.x * 0.006f + noiseOffset, v * data.size.z * 0.006f + noiseOffset);
            float chance = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.65f, patch)) * (1f - steep / MaxTreeSlope * 0.6f);
            if (rng.NextDouble() > chance) continue;

            // En lo alto y abierto hay más arbustos; en el bosque, más árboles.
            double pick = rng.NextDouble();
            List<GameObject> group;
            if (dry) group = pick < 0.4 ? broad : bushes;
            else group = pick < 0.55 ? tall : (pick < 0.8 ? broad : bushes);
            group = group.FindAll(prefab => prefab != null);
            if (group.Count == 0) continue;
            GameObject chosen = group[rng.Next(group.Count)];

            float scale = Mathf.Lerp(0.8f, 1.25f, (float)rng.NextDouble());
            trees.Add(new TreeInstance
            {
                prototypeIndex = all.IndexOf(chosen),
                position = new Vector3(u, 0f, v),
                widthScale = scale,
                heightScale = scale * Mathf.Lerp(0.9f, 1.1f, (float)rng.NextDouble()),
                rotation = (float)(rng.NextDouble() * Mathf.PI * 2.0),
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        data.SetTreeInstances(trees.ToArray(), true);
        return trees.Count;
    }

    /// <summary>Prefab de una planta para el terreno. Los árboles llevan collider (la bici choca); los arbustos no.</summary>
    private static GameObject VegetationPrefab(string name, string meshName, bool collider, params Material[] materials)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(VisualAssetFactory.ModelsFolder + "/" + meshName + ".asset");
        if (mesh == null) return null;
        VisualAssetFactory.EnsureFolder(PrefabsFolder);
        GameObject go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        if (collider)
        {
            // Solo el tronco: las ramas no frenan la bici.
            CapsuleCollider trunk = go.AddComponent<CapsuleCollider>();
            trunk.radius = 0.3f;
            trunk.height = 4f;
            trunk.center = new Vector3(0f, 2f, 0f);
        }
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabsFolder + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static Material LoadMaterial(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/" + name + ".mat");
    }

    // =====================================================================
    // Herramientas para el mapa abierto
    // =====================================================================

    /// <summary>Quita los árboles y arbustos que quedaron sobre lo pintado con la capa de tierra (los senderos).</summary>
    public static void ClearTreesOnTrails(Terrain terrain, float marginMeters)
    {
        TerrainData data = terrain.terrainData;
        float[,] trail = TrailWeights(data);
        if (trail == null)
        {
            EditorUtility.DisplayDialog("Quitar árboles", "Este terreno no tiene la capa de tierra (…_CapaTierra). Créalo con la ventana de mapas.", "Entendido");
            return;
        }
        float radius = marginMeters / data.size.x * trail.GetLength(1);
        List<TreeInstance> kept = new List<TreeInstance>();
        int removed = 0;
        foreach (TreeInstance tree in data.treeInstances)
        {
            if (OnTrail(trail, tree.position.x, tree.position.z, radius)) removed++;
            else kept.Add(tree);
        }
        Undo.RegisterCompleteObjectUndo(data, "Quitar árboles de los senderos");
        data.SetTreeInstances(kept.ToArray(), true);
        EditorUtility.SetDirty(data);
        EditorUtility.DisplayDialog("Quitar árboles", removed + " árboles y arbustos quitados de los senderos.", "Listo");
    }

    /// <summary>Vuelve a repartir los árboles con los ajustes actuales, respetando los senderos ya pintados.</summary>
    public static void RescatterTrees(Terrain terrain, Settings s)
    {
        TerrainData data = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, "Repartir árboles");
        int count = ScatterTrees(data, s, TrailWeights(data));
        EditorUtility.SetDirty(data);
        Debug.Log("DownhillRace: " + count + " árboles repartidos en '" + terrain.name + "'.");
    }

    /// <summary>Pesos de la capa de tierra [fila, columna] (0 = nada, 1 = sendero), o null si no existe.</summary>
    private static float[,] TrailWeights(TerrainData data)
    {
        TerrainLayer[] layers = data.terrainLayers;
        int index = -1;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] != null && layers[i].name.EndsWith("_CapaTierra")) index = i;
        }
        if (index < 0) return null;
        int res = data.alphamapResolution;
        float[,,] maps = data.GetAlphamaps(0, 0, res, res);
        float[,] trail = new float[res, res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++) trail[y, x] = maps[y, x, index];
        }
        return trail;
    }

    /// <summary>True si hay sendero en (u, v) o a menos de "radius" píxeles del mapa de texturas.</summary>
    private static bool OnTrail(float[,] trail, float u, float v, float radius)
    {
        int res = trail.GetLength(0);
        int cx = Mathf.RoundToInt(u * (res - 1)), cy = Mathf.RoundToInt(v * (res - 1));
        int r = Mathf.Max(0, Mathf.CeilToInt(radius));
        for (int dy = -r; dy <= r; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= res) continue;
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= res || dx * dx + dy * dy > r * r) continue;
                if (trail[y, x] > TrailThreshold) return true;
            }
        }
        return false;
    }

    // =====================================================================
    // Luz, cielo y cámara
    // =====================================================================

    private static void BuildEnvironment(Settings s, float sizeX, float sizeY, float sizeZ)
    {
        bool dry = s.Ambience == TrackLayout.Theme.ValleSeco;
        GameObject sun = new GameObject("Sol");
        sun.transform.rotation = Quaternion.Euler(dry ? 52f : 42f, -30f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = dry ? new Color(1f, 0.95f, 0.86f) : new Color(0.95f, 0.97f, 1f);
        light.intensity = dry ? 1.4f : 1.1f;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;

        Material sky = LoadMaterial(dry ? "CieloValle" : "CieloNublado");
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.60f, 0.64f, 0.70f);
        RenderSettings.ambientEquatorColor = new Color(0.50f, 0.52f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.26f, 0.23f, 0.19f);

        // Niebla lejana: deja ver todo el mapa desde arriba pero da profundidad.
        float extent = Mathf.Max(sizeX, sizeZ);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = dry ? new Color(0.82f, 0.80f, 0.74f) : new Color(0.70f, 0.73f, 0.76f);
        RenderSettings.fogStartDistance = extent * 0.4f;
        RenderSettings.fogEndDistance = extent * 2.2f;

        // Cámara que mira todo el mapa desde el sur: es lo que se ve al darle Play.
        GameObject cameraGo = new GameObject("CamaraVista");
        cameraGo.tag = "MainCamera";
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.farClipPlane = extent * 3f;
        camera.nearClipPlane = 0.5f;
        cameraGo.transform.position = new Vector3(0f, sizeY + extent * 0.45f, -sizeZ * 0.5f - extent * 0.45f);
        cameraGo.transform.LookAt(new Vector3(0f, sizeY * 0.3f, 0f));
    }
}
