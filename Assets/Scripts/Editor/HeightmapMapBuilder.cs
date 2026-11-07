using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Convierte un heightmap (PNG exportado de una página de mapas) en un terreno de Unity a escala real,
/// en su propia escena: relieve exagerado para que las bajadas se sientan extremas, los senderos del mapa
/// (Turi Bike Park, La Paz…) tallados y pintados, pasto, roca y bosque a los lados de las rutas, y si es
/// jugable, la bici, el cronómetro, los checkpoints y la meta.
/// Se usa desde la ventana DownhillRace → Crear mapa desde heightmap.
/// </summary>
public static class HeightmapMapBuilder
{
    public const string MapsFolder = "Assets/Mapas";
    private const string PrefabsFolder = "Assets/Prefabs/Vegetacion";

    // Por encima de esta pendiente no crecen árboles (es roca o barranco).
    private const float MaxTreeSlope = 38f;
    // Peso mínimo de la capa de tierra para considerar que un punto es sendero.
    private const float TrailThreshold = 0.25f;
    // Radio del relieve "de fondo" al acentuar: se resaltan lomas y quebradas más chicas que esto.
    private const float AccentRadius = 60f;
    // Árboles: espacio libre a cada lado del sendero, franja de bosque más denso y claro en salida y meta.
    private const float TreeClearance = 2f;
    private const float ForestBand = 30f;
    private const float OpenAreaRadius = 30f;
    // Carrera: un checkpoint cada tanto a lo largo de cada ruta y la caída que hace reaparecer.
    private const float CheckpointSpacing = 250f;
    private const float StartOffset = 4f;
    private const float FallBelowTerrain = 40f;

    /// <summary>Ajustes de la ventana; se guardan entre sesiones.</summary>
    [System.Serializable]
    public class Settings
    {
        public string MapName = "MapaReal";
        public string HeightmapPath = MapsFolder + "/MapaReal_Alturas.png";
        public float Width = 1770f;            // Ancho real de la imagen completa (oeste → este), en metros. Turi–Ictocruz: 1,77 km.
        public float Length = 1770f;           // Largo real de la imagen completa (sur → norte), en metros.
        public float HeightDifference = 331f;  // Del punto más bajo al más alto de la imagen completa.
        public float ReliefExaggeration = 1.6f; // Multiplica el desnivel: la bajada real se sentiría suave en el juego.
        public float ReliefAccent = 0.35f;     // Hace más hondas las quebradas y más marcadas las lomas.
        public bool AddTrails = true;          // Senderos del mapa (solo calzan con su propio heightmap).
        public bool Playable = true;           // Bici, cronómetro y checkpoints; se juega desde el menú Carrera.
        public string TrailColor = "";         // Color liso del suelo del sendero (#RRGGBB); vacío = textura de tierra.
        public string GroundTint = "";         // Tiñe el pasto de alrededor (#RRGGBB, multiplica); vacío = como es.
        public bool HotDay;                    // Sol fuerte y cielo de desierto (día caluroso).
        public Vector2 CropX = new Vector2(0f, 1f);
        public Vector2 CropZ = new Vector2(0f, 1f);
        public int Resolution = 2049;
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

    /// <summary>
    /// Crea (o reemplaza) la escena Assets/Scenes/&lt;nombre&gt;.unity con el terreno del heightmap.
    /// "trailSet" son los senderos del mapa (null = solo el terreno).
    /// </summary>
    public static void Build(Settings s, RealMapTrails.Set trailSet)
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

            EditorUtility.DisplayProgressBar("Crear mapa", "Recortando, suavizando y exagerando el relieve", 0.15f);
            int res = s.Resolution;
            float sizeX = s.CroppedWidth, sizeZ = s.CroppedLength;
            float[,] heights = Resample(image, s.CropX, s.CropZ, res);
            float metersPerSample = Mathf.Max(sizeX, sizeZ) / (res - 1);
            Smooth(heights, Mathf.RoundToInt(s.SmoothMeters / metersPerSample));
            if (s.ReliefAccent > 0f) AccentRelief(heights, Mathf.RoundToInt(AccentRadius / metersPerSample), s.ReliefAccent);

            // A metros: la imagen completa (0–1) mide el desnivel real, multiplicado por la exageración.
            float verticalScale = s.HeightDifference * Mathf.Max(0.1f, s.ReliefExaggeration);
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++) heights[z, x] *= verticalScale;
            }

            List<TrailTerrainCarver.Line> trails = null, rivers = null, roads = null, carve = null;
            TrailTerrainCarver.Result carved = null;
            if (s.AddTrails && trailSet != null && trailSet.Segments.Count > 0)
            {
                // Retoques del relieve (zonas suavizadas, pasos abiertos) e inclinación hacia el norte: todo baja.
                TerrainSculptor.Apply(heights, trailSet, s.CropX, s.CropZ, sizeX, sizeZ, s.Length / Mathf.Max(1f, s.Width));
                EditorUtility.DisplayProgressBar("Crear mapa", "Tallando los senderos de " + trailSet.MapName, 0.3f);
                trails = TrailTerrainCarver.BuildLines(trailSet.Segments, s.CropX, s.CropZ, sizeX, sizeZ);
                // Los caminos van primero: los senderos que los cruzan empatan su altura.
                roads = TrailTerrainCarver.BuildRoadLines(trailSet.Roads, s.CropX, s.CropZ, sizeX, sizeZ);
                carve = new List<TrailTerrainCarver.Line>(roads);
                carve.AddRange(trails);
                if (trailSet.Rivers.Count > 0)
                {
                    // Los ríos se tallan junto con los senderos: donde se cruzan, el cauce manda.
                    rivers = TrailTerrainCarver.BuildRiverLines(trailSet.Rivers, s.CropX, s.CropZ, sizeX, sizeZ);
                    carve.AddRange(rivers);
                }
                carved = TrailTerrainCarver.Carve(heights, sizeX, sizeZ, carve, trailSet.Bridges);
                if (trailSet.Ridges.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("Crear mapa", "Lomas empinadas entre las rutas", 0.4f);
                    MountainRidges.Apply(heights, sizeX, sizeZ, trailSet, carve, s.CropX, s.CropZ);
                }
            }

            float min = float.MaxValue, max = float.MinValue;
            foreach (float h in heights)
            {
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }
            float sizeY = Mathf.Max(1f, max - min);
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++) heights[z, x] = (heights[z, x] - min) / sizeY;
            }

            EditorUtility.DisplayProgressBar("Crear mapa", "Creando el terreno", 0.45f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VisualAssetFactory.EnsureFolder(MapsFolder);
            string dataPath = MapsFolder + "/" + s.MapName + "_Terreno.asset";
            AssetDatabase.DeleteAsset(dataPath);
            TerrainData data = new TerrainData();
            data.heightmapResolution = res;
            data.size = new Vector3(sizeX, sizeY, sizeZ);
            data.SetHeights(0, 0, heights);

            EditorUtility.DisplayProgressBar("Crear mapa", "Pintando pasto, roca y tierra de los senderos", 0.55f);
            int alphaRes = Mathf.Max(sizeX, sizeZ) > 1200f ? 2048 : 1024;
            float[,] trailEdge = trails != null ? TrailTerrainCarver.EdgeDistance(trails, alphaRes, sizeX, sizeZ, ForestBand) : null;
            float[,] riverEdge = rivers != null ? TrailTerrainCarver.EdgeDistance(rivers, alphaRes, sizeX, sizeZ, 6f) : null;
            List<Vector3> patches = LocalPatches(trails != null ? trailSet : null, s, sizeX, sizeZ);
            List<Vector2> bounds = LocalBounds(trails != null ? trailSet : null, s, sizeX, sizeZ);
            float[,] roadEdge = roads != null && roads.Count > 0 ? TrailTerrainCarver.EdgeDistance(roads, alphaRes, sizeX, sizeZ, 4f) : null;
            float[,] clayEdge = carved != null && carved.Clay.Count > 0 ? TrailTerrainCarver.EdgeDistance(ClayLines(carve, carved), alphaRes, sizeX, sizeZ, 2f) : null;
            bool marked = trails != null && trailSet.Dressing;
            float[,,] alphamaps = PaintLayers(data, s, alphaRes, trailEdge, riverEdge, patches, roadEdge, clayEdge, marked);

            EditorUtility.DisplayProgressBar("Crear mapa", "Plantando árboles", 0.7f);
            List<Vector2> openAreas = new List<Vector2>();
            if (trails != null && trailSet.Routes.Count > 0)
            {
                // Salida y meta despejadas (como el potrero de la salida en las fotos), según la primera ruta:
                // el último tramo de la lista puede ser una línea alternativa, no la llegada.
                int[] main = trailSet.Routes[0].Segments;
                openAreas.Add(trails[main[0]].Points[0]);
                TrailTerrainCarver.Line last = trails[main[main.Length - 1]];
                openAreas.Add(last.Points[last.Points.Length - 1]);
            }
            int trees = ScatterTrees(data, s, null, trailEdge, openAreas, riverEdge, patches, bounds, roadEdge);
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayProgressBar("Crear mapa", "Luz, cielo y cámara", 0.85f);
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Mapa";
            // Centrado en el origen: así es más fácil ubicarse en la vista de escena.
            go.transform.position = new Vector3(-sizeX * 0.5f, 0f, -sizeZ * 0.5f);
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = VisualAssetFactory.TerrainMaterial("Terreno");
            terrain.heightmapPixelError = 4f;
            terrain.basemapDistance = 400f;
            terrain.drawInstanced = true;
            terrain.treeDistance = 1000f;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            List<float[]> routeWidths = null;
            List<Vector3[]> routes = trails != null ? BuildTrailObjects(terrain, trails, trailSet, out routeWidths) : null;
            bool playable = s.Playable && routes != null && routes.Count > 0;
            BikeController bike = null;
            if (playable)
            {
                EditorUtility.DisplayProgressBar("Crear mapa", "Bici, cronómetro, checkpoints y meta", 0.9f);
                bike = BuildRace(s, routes, trailSet, bounds, go.transform.position, carve, carved);
            }
            else
            {
                BuildEnvironment(s, sizeX, sizeY, sizeZ);
            }
            // Ambientación por zonas (pueblo, matorral, pedregal…) a lo largo de la primera ruta.
            if (routes != null && routes.Count > 0 && trailSet.Zones.Count > 0)
            {
                EditorUtility.DisplayProgressBar("Crear mapa", "Ambientación: pueblo, matorral, pedregal", 0.93f);
                // La cinta se corta donde salen las líneas alternativas y donde pasa el río.
                List<Vector3[]> avoid = new List<Vector3[]>();
                Vector3 o = go.transform.position;
                HashSet<int> inRoutes = new HashSet<int>();
                foreach (RealMapTrails.Route r in trailSet.Routes) foreach (int k in r.Segments) inRoutes.Add(k);
                for (int k = 0; k < trails.Count; k++)
                {
                    if (!inRoutes.Contains(k)) avoid.Add(System.Array.ConvertAll(trails[k].Points, p => o + new Vector3(p.x, 0f, p.y)));
                }
                if (rivers != null)
                {
                    foreach (TrailTerrainCarver.Line r in rivers) avoid.Add(System.Array.ConvertAll(r.Points, p => o + new Vector3(p.x, 0f, p.y)));
                }
                RealMapDecor.Build(terrain, routes[0], routeWidths[0], trailSet.Zones, s.Ambience == TrackLayout.Theme.ValleSeco, s.Seed, avoid, s.MapName);
            }
            if (routes != null && routes.Count > 0)
            {
                Vector3 origin = go.transform.position;
                List<Vector3[]> riverWorld = new List<Vector3[]>();
                List<float> riverHalf = new List<float>();
                if (rivers != null)
                {
                    EditorUtility.DisplayProgressBar("Crear mapa", "Río rocoso", 0.95f);
                    for (int i = 0; i < rivers.Count; i++)
                    {
                        Vector2[] sparse = TrailTerrainCarver.Resample(new List<Vector2>(rivers[i].Points), 2f);
                        Vector3[] world = System.Array.ConvertAll(sparse, p => origin + new Vector3(p.x, 0f, p.y));
                        riverWorld.Add(world);
                        riverHalf.Add(rivers[i].HalfWidth);
                        RealMapDecor.RockyRiver(terrain, rivers[i].Name, world, rivers[i].HalfWidth, routes[0], routeWidths[0],
                            VisualAssetFactory.ModelsFolder + "/" + s.MapName + "_Rio" + (i + 1) + ".asset", s.Ambience == TrackLayout.Theme.ValleSeco, s.Seed + i);
                    }
                }
                for (int i = 0; i < patches.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Crear mapa", "Zona verde de costa", 0.96f);
                    RealMapDecor.CoastPatch(terrain, trailSet.Patches[i].Name, origin + new Vector3(patches[i].x, 0f, patches[i].y), patches[i].z,
                        routes[0], routeWidths[0], riverWorld, riverHalf, s.Seed + i);
                }
            }

            if (routes != null && trailSet.Dressing)
            {
                EditorUtility.DisplayProgressBar("Crear mapa", "Pista terminada: cintas, pancartas, puente, saltos de madera, público", 0.97f);
                TrackDressing.Build(terrain, trailSet, carve, carved, routes, s, -min);
            }

            VisualAssetFactory.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            // Solo la escena jugable entra a la lista del juego (el menú la carga por su nombre).
            if (playable) BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            KeepPaint(data, alphamaps);

            Selection.activeGameObject = bike != null ? bike.gameObject : go;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0f, sizeY * 0.5f, 0f), new Vector3(sizeX, sizeY, sizeZ)), false);
            }
            Debug.Log("DownhillRace: mapa '" + s.MapName + "' creado en " + scenePath + " (" + sizeX.ToString("0") + " × " +
                      sizeZ.ToString("0") + " m, " + sizeY.ToString("0") + " m de desnivel, heightmap de " + bitDepth +
                      " bits, " + trees + " árboles" + (trails != null ? ", senderos de " + trailSet.MapName : "") + "). " +
                      (playable ? "Se juega desde el menú: Carrera → " + trailSet.MapName + " (o con Play en esta escena)." : "No está en el juego: es solo para diseñar."));
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
        float[] line = new float[n], blurred = new float[n];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++) line[x] = heights[z, x];
                BoxBlurLine(line, blurred, radius);
                for (int x = 0; x < n; x++) heights[z, x] = blurred[x];
            }
            for (int x = 0; x < n; x++)
            {
                for (int z = 0; z < n; z++) line[z] = heights[z, x];
                BoxBlurLine(line, blurred, radius);
                for (int z = 0; z < n; z++) heights[z, x] = blurred[z];
            }
        }
    }

    /// <summary>Promedio móvil con suma acumulada: cuesta lo mismo con radio 2 que con radio 80.</summary>
    private static void BoxBlurLine(float[] source, float[] result, int radius)
    {
        int n = source.Length;
        // En los bordes se repite el último valor, para que la orilla del mapa no se hunda.
        float sum = 0f;
        for (int k = -radius; k <= radius; k++) sum += source[Mathf.Clamp(k, 0, n - 1)];
        float inverse = 1f / (radius * 2 + 1);
        for (int i = 0; i < n; i++)
        {
            result[i] = sum * inverse;
            sum += source[Mathf.Min(i + radius + 1, n - 1)] - source[Mathf.Max(i - radius, 0)];
        }
    }

    /// <summary>
    /// Resalta el relieve local: a cada punto se le suma su diferencia con el relieve de fondo.
    /// Las quebradas quedan más hondas y las lomas más altas, así las bajadas se sienten más empinadas.
    /// </summary>
    private static void AccentRelief(float[,] heights, int radius, float amount)
    {
        if (radius <= 0) return;
        float[,] background = (float[,])heights.Clone();
        Smooth(background, radius);
        int n = heights.GetLength(0);
        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++) heights[z, x] += amount * (heights[z, x] - background[z, x]);
        }
    }

    // =====================================================================
    // Texturas: pasto, roca y tierra de los senderos
    // =====================================================================

    /// <summary>
    /// Vuelve a aplicar la pintura al final y la guarda. Si después de pintar se crean otros recursos
    /// (bici, materiales, árboles), Unity puede volver a cargar el terreno sin los mapas de color
    /// (quedaba todo de pasto). Al final se comprueba que haya quedado pintado.
    /// </summary>
    internal static void KeepPaint(TerrainData data, float[,,] alphamaps)
    {
        data.SetAlphamaps(0, 0, alphamaps);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        int res = data.alphamapResolution, layers = data.alphamapLayers, painted = 0, samples = 0;
        float[,,] check = data.GetAlphamaps(0, 0, res, res);
        for (int y = 0; y < res; y += 16)
        {
            for (int x = 0; x < res; x += 16)
            {
                samples++;
                if (check[y, x, 0] < 0.5f) painted++;
            }
        }
        if (layers > 1 && painted == 0) Debug.LogWarning("DownhillRace: los colores del terreno no quedaron guardados (todo es la primera capa).");
        else Debug.Log("DownhillRace: colores del terreno guardados (" + (100f * painted / Mathf.Max(1, samples)).ToString("0") + " % no es la capa de fondo).");
    }

    private static float[,,] PaintLayers(TerrainData data, Settings s, int alphaRes, float[,] trailEdge, float[,] riverEdge, List<Vector3> patches,
        float[,] roadEdge = null, float[,] clayEdge = null, bool marked = false)
    {
        bool dry = s.Ambience == TrackLayout.Theme.ValleSeco;
        TerrainLayer grass = Layer(s.MapName + "_CapaPasto", dry ? "PastoSeco" : "Pasto", 8f);
        if (!string.IsNullOrWhiteSpace(s.GroundTint) && ColorUtility.TryParseHtmlString(s.GroundTint.Trim(), out Color tint))
        {
            // Pasto teñido (más oscuro / oliva) para que el sendero de color claro se distinga bien.
            Texture2D tinted = TintedTexture(dry ? "PastoSeco" : "Pasto", s.MapName + "_Pasto", tint);
            if (tinted != null) grass.diffuseTexture = tinted;
            EditorUtility.SetDirty(grass);
        }
        TerrainLayer rock = Layer(s.MapName + "_CapaRoca", dry ? "RocaSeca" : "Roca", 6f);
        bool plainTrail = TryTrailColor(s, out Color trailColor);
        TerrainLayer dirt = plainTrail
            ? VisualAssetFactory.TerrainLayerAsset(s.MapName + "_CapaTierra", TrailColorTexture(s.MapName, trailColor),
                AssetDatabase.LoadAssetAtPath<Texture2D>(VisualAssetFactory.TexturesFolder + "/TierraSecaNormal.png"), 4f)
            : Layer(s.MapName + "_CapaTierra", dry ? "TierraSeca" : "Tierra", 4f);
        bool green = patches != null && patches.Count > 0;
        // Capas: pasto, roca, tierra, [pasto verde], [orilla oscura], [arcilla], [camino].
        List<TerrainLayer> list = new List<TerrainLayer> { grass, rock, dirt };
        int greenIndex = -1, edgeIndex = -1, clayIndex = -1, roadIndex = -1;
        if (green)
        {
            greenIndex = list.Count;
            list.Add(Layer(s.MapName + "_CapaPastoVerde", "Pasto", 8f));
        }
        if (marked)
        {
            // Orilla de tierra oscura y compacta: la línea del sendero se ve clarito desde lejos.
            edgeIndex = list.Count;
            list.Add(VisualAssetFactory.TerrainLayerAsset(s.MapName + "_CapaOrilla", TrailColorTexture(s.MapName + "_Orilla", new Color(0.36f, 0.25f, 0.16f)),
                AssetDatabase.LoadAssetAtPath<Texture2D>(VisualAssetFactory.TexturesFolder + "/TierraNormal.png"), 3f));
        }
        if (clayEdge != null)
        {
            // Arcilla rojiza en las curvas cerradas con peralte.
            clayIndex = list.Count;
            list.Add(VisualAssetFactory.TerrainLayerAsset(s.MapName + "_CapaArcilla", TrailColorTexture(s.MapName + "_Arcilla", new Color(0.66f, 0.33f, 0.2f)),
                AssetDatabase.LoadAssetAtPath<Texture2D>(VisualAssetFactory.TexturesFolder + "/TierraNormal.png"), 3f));
        }
        if (roadEdge != null)
        {
            roadIndex = list.Count;
            list.Add(Layer(s.MapName + "_CapaCamino", "Carretera", 5f));
        }
        int layers = list.Count;
        data.terrainLayers = list.ToArray();

        // Más resolución en mapas grandes para que un sendero de 6 m se vea nítido.
        data.alphamapResolution = alphaRes;
        float[,,] alpha = new float[alphaRes, alphaRes, layers];
        float texelX = data.size.x / (alphaRes - 1), texelZ = data.size.z / (alphaRes - 1);
        for (int y = 0; y < alphaRes; y++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                float steep = data.GetSteepness(x / (float)(alphaRes - 1), y / (float)(alphaRes - 1));
                float rockAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(30f, 45f, steep));
                float trail = 0f, trailRock = 0f, rim = 0f;
                if (trailEdge != null && trailEdge[y, x] < 2f)
                {
                    if (marked)
                    {
                        // Borde nítido (casi sin mordidas del pasto) y una franja oscura en la orilla.
                        float wobble = (Mathf.PerlinNoise(x * texelX * 0.3f, y * texelZ * 0.3f) - 0.5f) * 0.35f;
                        float e = trailEdge[y, x] + wobble;
                        trail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, 0.35f, e));
                        rim = Mathf.Clamp01(1f - Mathf.Abs(e + 0.45f) / 0.6f);
                    }
                    else
                    {
                        // Borde irregular: el pasto se come un poco la orilla, como en un sendero real.
                        float wobble = (Mathf.PerlinNoise(x * texelX * 0.15f, y * texelZ * 0.15f) - 0.5f) * 1.4f;
                        trail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.8f, 0.9f, trailEdge[y, x] + wobble));
                    }
                    // En lo empinado del sendero asoma la roca (lajas, como en las fotos de la pista).
                    // Con color liso, el suelo por donde pasa la bici queda todo de ese color.
                    if (!plainTrail) trailRock = trail * 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 34f, steep)) * (marked ? 0.2f : 1f);
                }
                float clay = 0f;
                if (clayEdge != null && clayEdge[y, x] < 1f) clay = trail * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.5f, 0.8f, clayEdge[y, x])));
                float road = 0f;
                if (roadEdge != null && roadEdge[y, x] < 1.5f) road = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.9f, roadEdge[y, x]))) * (1f - trail);

                float g = (1f - rockAmount) * (1f - trail) * (1f - road);
                float rk = rockAmount * (1f - trail) * (1f - road) + trailRock;
                float dt = trail - trailRock;
                float edgeW = 0f;
                if (marked)
                {
                    edgeW = dt * rim * 0.85f;
                    dt -= edgeW;
                }
                float clayW = Mathf.Min(clay, dt + edgeW);
                if (clayW > 0f)
                {
                    float fromDirt = Mathf.Min(dt, clayW);
                    dt -= fromDirt;
                    edgeW -= clayW - fromDirt;
                }
                // Cauce del río: piedras (también donde lo cruza el sendero).
                if (riverEdge != null && riverEdge[y, x] < 3f)
                {
                    float river = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 2.5f, riverEdge[y, x]));
                    g *= 1f - river;
                    dt *= 1f - river;
                    edgeW *= 1f - river;
                    clayW *= 1f - river;
                    rk = rk * (1f - river) + river;
                }
                alpha[y, x, 0] = g;
                alpha[y, x, 1] = rk;
                alpha[y, x, 2] = dt;
                if (edgeIndex >= 0) alpha[y, x, edgeIndex] = edgeW;
                if (clayIndex >= 0) alpha[y, x, clayIndex] = clayW;
                if (roadIndex >= 0) alpha[y, x, roadIndex] = road;
                if (green)
                {
                    float lush = 0f;
                    Vector2 m = new Vector2(x * texelX, y * texelZ);
                    float wobble = (Mathf.PerlinNoise(m.x * 0.05f + 3f, m.y * 0.05f + 9f) - 0.5f) * 0.3f;
                    foreach (Vector3 patch in patches)
                    {
                        float d = Vector2.Distance(m, new Vector2(patch.x, patch.y)) / patch.z;
                        lush = Mathf.Max(lush, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.15f, d + wobble)));
                    }
                    alpha[y, x, greenIndex] = g * lush;
                    alpha[y, x, 0] = g * (1f - lush);
                }
            }
        }
        data.SetAlphamaps(0, 0, alpha);
        return alpha;
    }

    /// <summary>Las curvas de arcilla como líneas cortas (para pintarlas y poner zonas resbalosas).</summary>
    internal static List<TrailTerrainCarver.Line> ClayLines(List<TrailTerrainCarver.Line> lines, TrailTerrainCarver.Result carved)
    {
        List<TrailTerrainCarver.Line> result = new List<TrailTerrainCarver.Line>();
        foreach (Vector3Int c in carved.Clay)
        {
            TrailTerrainCarver.Line line = lines[c.x];
            int count = c.z - c.y + 1;
            if (count < 2) continue;
            Vector2[] points = new Vector2[count];
            System.Array.Copy(line.Points, c.y, points, 0, count);
            result.Add(new TrailTerrainCarver.Line { Name = line.Name + " (arcilla)", HalfWidth = line.HalfWidth, Points = points });
        }
        return result;
    }

    private static bool TryTrailColor(Settings s, out Color color)
    {
        color = Color.white;
        return !string.IsNullOrWhiteSpace(s.TrailColor) && ColorUtility.TryParseHtmlString(s.TrailColor.Trim(), out color);
    }

    /// <summary>
    /// Textura del suelo del sendero: el color pedido con un poco de grano (ruido que se repite sin cortes),
    /// para que no se vea plástico. Se guarda como PNG junto a las demás texturas.
    /// </summary>
    internal static Texture2D TrailColorTexture(string mapName, Color color)
    {
        const int size = 256;
        string path = VisualAssetFactory.TexturesFolder + "/" + mapName + "_Sendero.png";
        System.Random rng = new System.Random(5);
        float[] coarse = new float[16 * 16], fine = new float[64 * 64];
        for (int i = 0; i < coarse.Length; i++) coarse[i] = (float)rng.NextDouble();
        for (int i = 0; i < fine.Length; i++) fine[i] = (float)rng.NextDouble();
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = (TileNoise(coarse, 16, x, y, size) - 0.5f) * 0.16f + (TileNoise(fine, 64, x, y, size) - 0.5f) * 0.10f
                          + ((float)rng.NextDouble() - 0.5f) * 0.05f;
                texture.SetPixel(x, y, new Color(color.r * (1f + n), color.g * (1f + n), color.b * (1f + n)));
            }
        }
        VisualAssetFactory.EnsureFolder(VisualAssetFactory.TexturesFolder);
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Copia de una textura multiplicada por un color (se guarda como PNG aparte; la original no cambia).</summary>
    private static Texture2D TintedTexture(string source, string name, Color tint)
    {
        string folder = VisualAssetFactory.TexturesFolder + "/";
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string sourceFile = Path.Combine(projectRoot, folder + source + ".png");
        if (!File.Exists(sourceFile)) return null;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(sourceFile))) return null;
        Color32[] pixels = texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            pixels[i] = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
        }
        texture.SetPixels32(pixels);
        string path = folder + name + ".png";
        File.WriteAllBytes(Path.Combine(projectRoot, path), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Ruido de valores interpolados que se repite: el borde derecho empata con el izquierdo.</summary>
    private static float TileNoise(float[] grid, int cells, int x, int y, int size)
    {
        float fx = x / (float)size * cells, fy = y / (float)size * cells;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
        float G(int gx, int gy) => grid[((gy % cells + cells) % cells) * cells + ((gx % cells + cells) % cells)];
        return Mathf.Lerp(Mathf.Lerp(G(x0, y0), G(x0 + 1, y0), tx), Mathf.Lerp(G(x0, y0 + 1), G(x0 + 1, y0 + 1), tx), ty);
    }

    /// <summary>Zonas verdes en metros del terreno: (x, z, radio).</summary>
    private static List<Vector3> LocalPatches(RealMapTrails.Set set, Settings s, float sizeX, float sizeZ)
    {
        List<Vector3> result = new List<Vector3>();
        if (set == null) return result;
        foreach (RealMapTrails.Patch patch in set.Patches)
        {
            Vector2 c = TrailTerrainCarver.ImageToLocal(patch.Center, s.CropX, s.CropZ, sizeX, sizeZ);
            result.Add(new Vector3(c.x, c.y, patch.Radius));
        }
        return result;
    }

    /// <summary>Límite de la pista en metros del terreno, o null si el mapa no tiene.</summary>
    private static List<Vector2> LocalBounds(RealMapTrails.Set set, Settings s, float sizeX, float sizeZ)
    {
        if (set == null || set.Bounds == null || set.Bounds.Length < 3) return null;
        List<Vector2> result = new List<Vector2>();
        foreach (Vector2 p in set.Bounds) result.Add(TrailTerrainCarver.ImageToLocal(p, s.CropX, s.CropZ, sizeX, sizeZ));
        return result;
    }

    private static bool InPolygon(List<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    internal static TerrainLayer Layer(string name, string texture, float tileSize)
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
    /// Reparte árboles y arbustos en manchas de bosque, sin ponerlos en lo muy empinado ni sobre los senderos.
    /// "dirt" (pesos de la capa de tierra) o "trailEdge" (distancia al sendero en m) marcan dónde no plantar;
    /// con "trailEdge" además el bosque se espesa a los lados de las rutas. Devuelve cuántos plantó.
    /// </summary>
    private static int ScatterTrees(TerrainData data, Settings s, float[,] dirt, float[,] trailEdge, List<Vector2> openAreas,
        float[,] riverEdge, List<Vector3> patches, List<Vector2> bounds, float[,] roadEdge = null)
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
            if (dirt != null && OnTrail(dirt, u, v, 2.5f / data.size.x * dirt.GetLength(1))) continue;
            Vector2 meters = new Vector2(u * data.size.x, v * data.size.z);
            if (InOpenArea(openAreas, meters)) continue;
            if (riverEdge != null && Sample(riverEdge, u, v) < 4f) continue;
            if (roadEdge != null && Sample(roadEdge, u, v) < 2f) continue;
            // En las zonas de costa no hay bosque: ahí van caña y cacao.
            if (patches != null && patches.Exists(p => (new Vector2(p.x, p.y) - meters).sqrMagnitude < p.z * p.z * 1.2f)) continue;

            // Manchas de bosque con claros entre ellas, más ralas al acercarse a lo empinado.
            float patch = Mathf.PerlinNoise(u * data.size.x * 0.006f + noiseOffset, v * data.size.z * 0.006f + noiseOffset);
            float chance = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.65f, patch)) * (1f - steep / MaxTreeSlope * 0.6f);
            if (trailEdge != null)
            {
                float edge = Sample(trailEdge, u, v);
                if (edge < TreeClearance) continue;
                // Bosque cerrado a los lados del sendero, como en las fotos de la pista.
                if (edge < ForestBand) chance = Mathf.Max(chance, 0.55f) * 2.2f;
            }
            // Fuera del límite de la pista quedan pocos árboles: solo para que el paisaje no se vea pelado.
            if (bounds != null && !InPolygon(bounds, meters)) chance *= 0.25f;
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

    private static bool InOpenArea(List<Vector2> openAreas, Vector2 meters)
    {
        if (openAreas == null) return false;
        foreach (Vector2 center in openAreas)
        {
            if ((center - meters).sqrMagnitude < OpenAreaRadius * OpenAreaRadius) return true;
        }
        return false;
    }

    private static float Sample(float[,] grid, float u, float v)
    {
        int res = grid.GetLength(0);
        return grid[Mathf.Clamp(Mathf.RoundToInt(v * (res - 1)), 0, res - 1), Mathf.Clamp(Mathf.RoundToInt(u * (res - 1)), 0, res - 1)];
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
    // Rutas en la escena
    // =====================================================================

    /// <summary>
    /// Un objeto por ruta (con su línea dibujada en la vista de escena) y marcas de salida y meta,
    /// para ubicarse al poner rampas y puentes.
    /// </summary>
    private static List<Vector3[]> BuildTrailObjects(Terrain terrain, List<TrailTerrainCarver.Line> lines, RealMapTrails.Set set,
        out List<float[]> halfWidths)
    {
        List<Vector3[]> routes = new List<Vector3[]>();
        halfWidths = new List<float[]>();
        Transform parent = new GameObject("Senderos").transform;
        Vector3 origin = terrain.transform.position;
        List<RealMapTrails.Segment> segments = set.Segments;
        Vector3[] first = null;
        foreach (RealMapTrails.Route route in set.Routes)
        {
            List<Vector2> flat = new List<Vector2>();
            List<float> flatHalf = new List<float>();
            float width = 0f;
            foreach (int index in route.Segments)
            {
                Vector2[] points = lines[index].Points;
                for (int i = flat.Count > 0 ? 1 : 0; i < points.Length; i++)
                {
                    flat.Add(points[i]);
                    flatHalf.Add(segments[index].HalfWidth);
                }
                width = Mathf.Max(width, segments[index].HalfWidth * 2f);
            }
            Vector2[] sparse = TrailTerrainCarver.Resample(flat, 2f);
            Vector3[] world = new Vector3[sparse.Length];
            float[] half = new float[sparse.Length];
            int cursor = 0;
            for (int i = 0; i < sparse.Length; i++)
            {
                Vector3 p = origin + new Vector3(sparse[i].x, 0f, sparse[i].y);
                p.y = terrain.SampleHeight(p) + origin.y;
                world[i] = p;
                // El medio ancho del punto original más cercano que viene después (los puntos van en orden).
                while (cursor < flat.Count - 1 && (flat[cursor] - sparse[i]).sqrMagnitude > (flat[cursor + 1] - sparse[i]).sqrMagnitude) cursor++;
                half[i] = flatHalf[cursor];
            }
            halfWidths.Add(half);
            GameObject go = new GameObject("Ruta " + route.Name);
            go.transform.SetParent(parent, false);
            go.AddComponent<TrailPath>().Configure(route.Name, route.Color, width, world);
            routes.Add(world);
            if (first == null) first = world;
        }

        if (first != null && first.Length > 1)
        {
            Marker("Salida", parent, first[0], first[1] - first[0]);
            Marker("Meta", parent, first[first.Length - 1], first[first.Length - 1] - first[first.Length - 2]);
        }
        return routes;
    }

    // =====================================================================
    // Carrera (contrarreloj)
    // =====================================================================

    /// <summary>
    /// Bici en la salida, GameManager, cámara, luz y HUD (los mismos de las otras pistas),
    /// checkpoints a lo largo de cada ruta para reaparecer, y la meta que detiene el cronómetro.
    /// </summary>
    private static BikeController BuildRace(Settings s, List<Vector3[]> routes, RealMapTrails.Set set, List<Vector2> bounds, Vector3 origin,
        List<TrailTerrainCarver.Line> carve, TrailTerrainCarver.Result carved)
    {
        string mapName = set.MapName;
        Vector3[] main = routes[0];
        int startIndex = PointAtDistance(main, StartOffset);
        Vector3 start = main[startIndex];
        Vector3 ahead = main[Mathf.Min(main.Length - 1, startIndex + 2)] - start;
        ahead.y = 0f;
        BikeController bike = PrototypeSceneBuilder.AddRaceSetup(s.MapName, s.Ambience, start,
            Quaternion.LookRotation(ahead.sqrMagnitude > 0.001f ? ahead : Vector3.forward), -FallBelowTerrain, s.HotDay);
        if (bounds != null)
        {
            // Si el borde deja fuera parte de la ruta (dibujo mal leído), no se pone: si no, la bici quedaría
            // "fuera de pista" en plena ruta y reaparecería sin parar.
            int outside = 0;
            foreach (Vector3 p in main)
            {
                if (!InPolygon(bounds, new Vector2(p.x - origin.x, p.z - origin.z))) outside++;
            }
            if (outside <= main.Length / 50) CreateBounds(bike, bounds, origin);
            else Debug.LogWarning("DownhillRace: el límite de pista deja fuera " + outside + " de " + main.Length +
                                  " puntos de la ruta; no se puso. Revisa el borde en el trazado del mapa.");
        }

        if (set.Crashes)
        {
            // Chocar fuerte es caída: "¡CAÍDA!" en pantalla y se reaparece en el último checkpoint.
            bike.EnableCrashes(true);
            GameObject ui = GameObject.Find("UI");
            if (ui != null)
            {
                Text crash = UiBuilder.Label("Caida", ui.transform, UiBuilder.DefaultFont, "¡CAÍDA!", 72, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 90f), new Color(1f, 0.25f, 0.15f), FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(900f, 110f));
                ui.AddComponent<CrashNotice>().Configure(bike, crash);
            }
        }
        if (set.ViewDistance > 0f)
        {
            // Niebla más lejos: desde arriba se ve la ciudad.
            RenderSettings.fogStartDistance = set.ViewDistance * 0.3f;
            RenderSettings.fogEndDistance = set.ViewDistance;
            Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
            if (cam != null) cam.farClipPlane = set.ViewDistance + 300f;
        }

        // Puntos donde no va un checkpoint: el puente y los cruces (la caja tomaría al corredor de la otra ruta).
        List<Vector3> noCheckpoint = new List<Vector3>();
        if (carved != null)
        {
            foreach (TrailTerrainCarver.Crossing c in carved.Crossings)
            {
                Vector2 p = carve[c.A].Points[c.IndexA];
                noCheckpoint.Add(origin + new Vector3(p.x, 0f, p.y));
            }
        }
        // Checkpoints por distancia (más seguidos en las pistas con caídas): en los tramos compartidos coinciden, así que no se repiten.
        float spacing = set.Crashes ? 150f : CheckpointSpacing;
        Transform group = new GameObject("Checkpoints").transform;
        List<Vector3> placed = new List<Vector3>();
        int created = 0;
        foreach (Vector3[] route in routes)
        {
            float total = RouteLength(route);
            for (int k = 1; k * spacing < total - 80f; k++)
            {
                int i = PointAtDistance(route, k * spacing);
                if (placed.Exists(p => (p - route[i]).sqrMagnitude < 15f * 15f)) continue;
                if (noCheckpoint.Exists(p => new Vector2(p.x - route[i].x, p.z - route[i].z).sqrMagnitude < 30f * 30f))
                {
                    i = PointAtDistance(route, k * spacing + 40f);
                    if (noCheckpoint.Exists(p => new Vector2(p.x - route[i].x, p.z - route[i].z).sqrMagnitude < 30f * 30f)) continue;
                }
                placed.Add(route[i]);
                Trigger("Checkpoint " + k, group, route, i, TrackTrigger.TriggerType.Checkpoint, k, 12f);
                created++;
            }
        }
        // Meta común unos metros antes del final, ancha para que entren todas las rutas.
        Trigger("Meta", group, main, PointAtDistance(main, RouteLength(main) - 8f), TrackTrigger.TriggerType.Meta, 0, 18f);
        Debug.Log("DownhillRace: " + created + " checkpoints y la meta en " + mapName + ".");
        return bike;
    }

    /// <summary>Límite de la pista: fuera de él aparece "¡FUERA DE PISTA!" y la bici reaparece a los pocos segundos.</summary>
    private static void CreateBounds(BikeController bike, List<Vector2> local, Vector3 origin)
    {
        GameObject ui = GameObject.Find("UI");
        Text warning = null;
        if (ui != null)
        {
            warning = UiBuilder.Label("FueraDePista", ui.transform, UiBuilder.DefaultFont, "¡FUERA DE PISTA!", 64, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 170f), new Color(1f, 0.38f, 0.2f), FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(1200f, 100f));
        }
        GameObject go = new GameObject("LimiteDePista");
        go.transform.position = new Vector3(0f, bike.transform.position.y, 0f);
        Vector2[] world = local.ConvertAll(p => new Vector2(p.x + origin.x, p.y + origin.z)).ToArray();
        go.AddComponent<TrackBounds>().Configure(bike, warning, world);
    }

    internal static void Trigger(string name, Transform parent, Vector3[] route, int index, TrackTrigger.TriggerType type, int order, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Vector3 forward = route[Mathf.Min(route.Length - 1, index + 1)] - route[Mathf.Max(0, index - 1)];
        forward.y = 0f;
        // El origen va sobre el sendero mirando hacia la bajada: ahí reaparece la bici.
        go.transform.SetPositionAndRotation(route[index], Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward));
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width, 10f, 3f);
        box.center = new Vector3(0f, 4f, 0f);
        go.AddComponent<TrackTrigger>().Configure(type, order);
    }

    private static float RouteLength(Vector3[] route)
    {
        float length = 0f;
        for (int i = 1; i < route.Length; i++) length += Vector3.Distance(route[i - 1], route[i]);
        return length;
    }

    private static int PointAtDistance(Vector3[] route, float distance)
    {
        float travelled = 0f;
        for (int i = 1; i < route.Length; i++)
        {
            travelled += Vector3.Distance(route[i - 1], route[i]);
            if (travelled >= distance) return i;
        }
        return route.Length - 1;
    }

    private static void Marker(string name, Transform parent, Vector3 position, Vector3 forward)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        forward.y = 0f;
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward));
        // Ícono visible en la vista de escena (los objetos vacíos no se ven).
        Texture2D icon = EditorGUIUtility.IconContent(name == "Salida" ? "sv_label_3" : "sv_label_6").image as Texture2D;
        if (icon != null) EditorGUIUtility.SetIconForObject(go, icon);
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
        int count = ScatterTrees(data, s, TrailWeights(data), null, null, null, null, null);
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
