using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Arma la escena de la pista de BMX La Concordia (Cuenca) con la forma de BmxTrackShape: terreno con los saltos,
/// peraltes y terrazas; colores como la pista real (rojo, asfalto negro, líneas blancas, cemento y muros de piedra);
/// portón de salida, checkpoints, meta y la bici con manejo de BMX.
/// Menú: DownhillRace → Crear pista BMX: La Concordia.
/// </summary>
public static class BmxTrackBuilder
{
    public const string SceneName = "BMX_Concordia";

    private const int Resolution = 1025;          // ~11 cm por punto.
    private const int AlphaResolution = 2048;     // ~5 cm por punto de color: las líneas blancas salen nítidas.
    private const string RedColor = "#CF5F5A";    // Rojo coral de las rectas (medido en la foto).
    private const string AsphaltColor = "#3A3B3D";
    private const string WhiteColor = "#F2F2F0";

    [MenuItem("DownhillRace/Crear pista BMX: La Concordia", priority = 30)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string scenePath = "Assets/Scenes/" + SceneName + ".unity";
        if (File.Exists(scenePath) && !EditorUtility.DisplayDialog("Pista BMX",
                "Ya existe " + scenePath + ".\nSi la creas de nuevo se borra lo que hayas agregado a mano.", "Crear de nuevo", "Cancelar"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Pista BMX", "Saltos, peraltes y terrazas", 0.1f);
            BmxTrackShape shape = new BmxTrackShape();
            float[,] heights = new float[Resolution, Resolution];
            bool[,] onTrack = new bool[Resolution, Resolution];
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    Vector2 p = new Vector2(x / (float)(Resolution - 1) * shape.SizeX, z / (float)(Resolution - 1) * shape.SizeZ);
                    heights[z, x] = shape.HeightAt(p);
                    shape.SurfaceAt(p, out float track);
                    onTrack[z, x] = track > 0.01f;
                }
            }
            SoftenTerraces(heights, onTrack);
            float maxH = 0f;
            foreach (float h in heights) maxH = Mathf.Max(maxH, h);
            float sizeY = maxH + 1f;
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++) heights[z, x] /= sizeY;
            }

            EditorUtility.DisplayProgressBar("Pista BMX", "Colores de la pista", 0.45f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VisualAssetFactory.EnsureFolder(HeightmapMapBuilder.MapsFolder);
            string dataPath = HeightmapMapBuilder.MapsFolder + "/" + SceneName + "_Terreno.asset";
            AssetDatabase.DeleteAsset(dataPath);
            TerrainData data = new TerrainData { heightmapResolution = Resolution };
            data.size = new Vector3(shape.SizeX, sizeY, shape.SizeZ);
            data.SetHeights(0, 0, heights);
            float[,,] alphamaps = Paint(data, shape);
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Pista BMX";
            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.materialTemplate = VisualAssetFactory.TerrainMaterial("Terreno");
            terrain.heightmapPixelError = 1.5f;  // Saltos chicos: más detalle de cerca.
            terrain.basemapDistance = 250f;
            terrain.drawInstanced = true;
            GameObjectUtility.SetStaticEditorFlags(terrainGo, StaticEditorFlags.BatchingStatic);

            EditorUtility.DisplayProgressBar("Pista BMX", "Bici, portón y meta", 0.8f);
            Vector3[] route = WorldPoints(terrain, shape.Points);
            int startIndex = shape.IndexAt(3f);
            Vector3 forward = route[startIndex + 4] - route[startIndex];
            forward.y = 0f;
            BikeController bike = PrototypeSceneBuilder.AddRaceSetup(SceneName, TrackLayout.Theme.BosqueNublado, route[startIndex],
                Quaternion.LookRotation(forward), -15f, true);
            TuneBikeForBmx(bike);
            BuildGate(route, shape.IndexAt(7f));
            BuildRaceTriggers(route, shape);

            VisualAssetFactory.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            HeightmapMapBuilder.KeepPaint(data, alphamaps);
            Selection.activeGameObject = bike.gameObject;
            Debug.Log("DownhillRace: pista BMX '" + SceneName + "' creada (" + shape.Length.ToString("0") +
                      " m). Se juega desde el menú: BMX (o con Play en esta escena).");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Suaviza solo el suelo de al lado (no la pista): el cambio de nivel entre una recta y la de abajo
    /// queda como un talud parejo (el muro de piedra) en vez de escalones.
    /// </summary>
    private static void SoftenTerraces(float[,] heights, bool[,] onTrack)
    {
        int n = heights.GetLength(0);
        const int radius = 4; // ~45 cm
        for (int pass = 0; pass < 2; pass++)
        {
            float[,] copy = (float[,])heights.Clone();
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    if (onTrack[z, x]) continue;
                    float sum = 0f;
                    int count = 0;
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int zz = Mathf.Clamp(z + dz, 0, n - 1);
                        for (int dx = -radius; dx <= radius; dx += 2)
                        {
                            sum += copy[zz, Mathf.Clamp(x + dx, 0, n - 1)];
                            count++;
                        }
                    }
                    heights[z, x] = sum / count;
                }
            }
        }
    }

    /// <summary>Rojo en rectas y saltos, asfalto negro en curvas, líneas blancas, cemento en la salida, piedra en los muros.</summary>
    private static float[,,] Paint(TerrainData data, BmxTrackShape shape)
    {
        TerrainLayer grass = HeightmapMapBuilder.Layer(SceneName + "_CapaPasto", "Pasto", 6f);
        TerrainLayer red = ColorLayer("Rojo", RedColor, 3f);
        TerrainLayer asphalt = ColorLayer("Asfalto", AsphaltColor, 3f);
        TerrainLayer white = ColorLayer("Blanco", WhiteColor, 3f);
        TerrainLayer concrete = HeightmapMapBuilder.Layer(SceneName + "_CapaCemento", "Concreto", 4f);
        TerrainLayer stone = HeightmapMapBuilder.Layer(SceneName + "_CapaPiedra", "Roca", 2.5f);
        data.terrainLayers = new[] { grass, red, asphalt, white, concrete, stone };
        data.alphamapResolution = AlphaResolution;
        float[,,] alpha = new float[AlphaResolution, AlphaResolution, 6];
        for (int y = 0; y < AlphaResolution; y++)
        {
            for (int x = 0; x < AlphaResolution; x++)
            {
                float u = x / (float)(AlphaResolution - 1), v = y / (float)(AlphaResolution - 1);
                BmxTrackShape.Surface surface = shape.SurfaceAt(new Vector2(u * shape.SizeX, v * shape.SizeZ), out float track);
                int layer = surface switch
                {
                    BmxTrackShape.Surface.Red => 1,
                    BmxTrackShape.Surface.Asphalt => 2,
                    BmxTrackShape.Surface.White => 3,
                    BmxTrackShape.Surface.Concrete => 4,
                    _ => 0,
                };
                // Fuera de la pista: los taludes entre terrazas son muros de piedra.
                float stoneAmount = layer == 0 ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(25f, 40f, data.GetSteepness(u, v))) : 0f;
                alpha[y, x, 0] = layer == 0 ? 1f - stoneAmount : 1f - track;
                alpha[y, x, 5] = stoneAmount;
                if (layer != 0) alpha[y, x, layer] = track;
            }
        }
        data.SetAlphamaps(0, 0, alpha);
        return alpha;
    }

    /// <summary>Capa de un color liso (cada una con su propia textura, para que no se pisen).</summary>
    private static TerrainLayer ColorLayer(string name, string hex, float tile)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        Texture2D texture = HeightmapMapBuilder.TrailColorTexture(SceneName + "_" + name, color);
        return VisualAssetFactory.TerrainLayerAsset(SceneName + "_Capa" + name, texture,
            AssetDatabase.LoadAssetAtPath<Texture2D>(VisualAssetFactory.TexturesFolder + "/ConcretoNormal.png"), tile);
    }

    /// <summary>
    /// Manejo de BMX: acelera más rápido pero llega a menos velocidad, salta más alto y gira más cerrado.
    /// </summary>
    private static void TuneBikeForBmx(BikeController bike)
    {
        SerializedObject so = new SerializedObject(bike);
        void Set(string field, float value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }
        Set("maxSpeed", 15f);
        Set("maxSpeedCrouched", 17f);
        Set("pedalAcceleration", 10f);
        Set("pedalTopSpeed", 14f);
        Set("jumpSpeed", 5.5f);
        Set("turnRateFast", 85f);
        Set("lateralGrip", 13f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Portón rojo al borde de la plataforma de salida: cae con el ¡YA!.</summary>
    private static void BuildGate(Vector3[] route, int index)
    {
        Vector3 forward = route[Mathf.Min(route.Length - 1, index + 2)] - route[index];
        forward.y = 0f;
        GameObject pivot = new GameObject("Porton");
        pivot.transform.SetPositionAndRotation(route[index], Quaternion.LookRotation(forward));
        GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "Hoja";
        Object.DestroyImmediate(leaf.GetComponent<Collider>()); // La bici no se mueve hasta el ¡YA!.
        leaf.transform.SetParent(pivot.transform, false);
        leaf.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        leaf.transform.localScale = new Vector3(BmxTrackShape.HalfWidth * 2f, 0.56f, 0.06f);
        Material redMat = AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/Rojo.mat");
        if (redMat != null) leaf.GetComponent<Renderer>().sharedMaterial = redMat;
        pivot.AddComponent<StartGate>();
    }

    /// <summary>Checkpoint al salir de cada curva, la meta al final de la última recta y su línea blanca.</summary>
    private static void BuildRaceTriggers(Vector3[] route, BmxTrackShape shape)
    {
        Transform group = new GameObject("Checkpoints").transform;
        float[] checkpoints = { 103f, 182f, 265f, 354f };
        float width = BmxTrackShape.HalfWidth * 2f + 4f;
        for (int k = 0; k < checkpoints.Length; k++)
        {
            HeightmapMapBuilder.Trigger("Checkpoint " + (k + 1), group, route, shape.IndexAt(checkpoints[k]), TrackTrigger.TriggerType.Checkpoint, k + 1, width);
        }
        int finish = shape.IndexAt(shape.Length - 6f);
        HeightmapMapBuilder.Trigger("Meta", group, route, finish, TrackTrigger.TriggerType.Meta, 0, width + 2f);

        Vector3 forward = route[Mathf.Min(route.Length - 1, finish + 2)] - route[Mathf.Max(0, finish - 2)];
        forward.y = 0f;
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "LineaDeMeta";
        Object.DestroyImmediate(stripe.GetComponent<Collider>());
        stripe.transform.SetParent(group, false);
        stripe.transform.SetPositionAndRotation(route[finish] + Vector3.up * 0.02f, Quaternion.LookRotation(forward));
        stripe.transform.localScale = new Vector3(BmxTrackShape.HalfWidth * 2f, 0.04f, 0.6f);
        Material whiteMat = AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/Blanco.mat");
        if (whiteMat != null) stripe.GetComponent<Renderer>().sharedMaterial = whiteMat;
    }

    private static Vector3[] WorldPoints(Terrain terrain, Vector2[] points)
    {
        Vector3[] world = new Vector3[points.Length];
        Vector3 origin = terrain.transform.position;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 p = origin + new Vector3(points[i].x, 0f, points[i].y);
            p.y = terrain.SampleHeight(p) + origin.y;
            world[i] = p;
        }
        return world;
    }
}
