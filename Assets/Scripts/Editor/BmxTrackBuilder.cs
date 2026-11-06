using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pista de BMX Racing «La Concordia» (Parque La Concordia, Cuenca). Un heightmap no sirve aquí (los datos de
/// altura satelitales no ven saltos de 1–2 m), así que el terreno se arma por código desde el trazado marcado
/// sobre la foto aérea: rampa de salida con portón, rectas con lomas, dobles y mesas, y peraltes en las curvas.
/// En la segunda recta hay dos líneas: la pro (saltos grandes) y la fácil (lomas suaves).
/// Menú: DownhillRace → Crear pista BMX: La Concordia.
/// </summary>
public static class BmxTrackBuilder
{
    public const string SceneName = "BMX_Concordia";

    private const float MetersPerPixel = 0.1052f;  // Medido con la longitud que da Google Earth (316 m).
    // La pista real es muy corta para la velocidad del juego: se agranda pareja (largo, ancho y saltos)
    // para que entre rampa y rampa haya tiempo de tomar velocidad, sin cambiar la forma.
    private const float GameScale = 1.35f;
    private const float Margin = 10f;              // Terreno alrededor de la pista (m).
    private const int Resolution = 1025;           // ~8 cm por punto: los saltos salen con su forma.
    private const int AlphaResolution = 1024;
    private const float Step = 0.25f;              // Separación de los puntos de la línea central (m).
    private const float Shoulder = 2.2f;           // Borde que baja de la pista al suelo (m).
    private const float BermHeight = 1.7f;         // Altura del borde de afuera de los peraltes (m).
    private const string ClayColor = "#C8693A";    // Arcilla naranja de las zonas de saltos (como en la foto).

    private enum Kind { StartHill, Rollers, Double, Table }

    private class Feature
    {
        public Kind Kind;
        public float From, To, Height, Period;
    }

    private class Line
    {
        public string Name;
        public float HalfWidth;
        public int[] Pixels;
        public List<Feature> Features = new List<Feature>();
        public Vector2[] Points;   // Metros en el terreno (x este, y norte).
        public float[] Distance;
        public float[] Curvature;  // Con signo: + gira a la izquierda.
    }

    // =====================================================================
    // Trazado (píxeles de la foto aérea marcada, con el norte arriba)
    // =====================================================================

    private static List<Line> Layout()
    {
        Line main = new Line
        {
            Name = "Pista", HalfWidth = 3.5f,
            Pixels = new[] { 646,75, 465,97, 428,101, 371,92, 341,102, 300,102, 261,104, 208,111, 153,125, 100,145, 98,188,
                150,244, 216,244, 262,215, 331,212, 391,212, 455,213, 508,213, 565,216, 597,231, 613,247, 603,293, 589,309,
                551,331, 488,338, 409,341, 343,345, 178,351, 104,376, 118,438, 190,440, 272,445, 343,446, 411,444, 467,436,
                545,430, 634,427, 683,455, 677,495, 593,520, 535,528, 464,521, 410,521, 260,526, 187,530, 107,528, 38,529 },
        };
        // Recta 1 (salida): rampa con portón, un doble y lomas.
        main.Features.Add(new Feature { Kind = Kind.StartHill, From = 0f, To = 15f, Height = 4.5f, Period = 5f });
        main.Features.Add(new Feature { Kind = Kind.Double, From = 20f, To = 30f, Height = 1.3f });
        main.Features.Add(new Feature { Kind = Kind.Rollers, From = 33f, To = 49f, Height = 0.7f, Period = 4f });
        // Recta 2, línea pro: doble, mesa y doble.
        main.Features.Add(new Feature { Kind = Kind.Double, From = 81f, To = 91f, Height = 1.6f });
        main.Features.Add(new Feature { Kind = Kind.Table, From = 94f, To = 104f, Height = 1.4f });
        main.Features.Add(new Feature { Kind = Kind.Double, From = 106f, To = 115f, Height = 1.1f });
        // Recta 3: sección rítmica (6 lomas seguidas) y un doble.
        main.Features.Add(new Feature { Kind = Kind.Rollers, From = 139f, To = 163f, Height = 0.9f, Period = 4f });
        main.Features.Add(new Feature { Kind = Kind.Double, From = 165f, To = 175f, Height = 1.2f });
        // Recta 4: mesa y dos dobles.
        main.Features.Add(new Feature { Kind = Kind.Table, From = 200f, To = 212f, Height = 1.3f });
        main.Features.Add(new Feature { Kind = Kind.Double, From = 216f, To = 226f, Height = 1.5f });
        main.Features.Add(new Feature { Kind = Kind.Double, From = 229f, To = 239f, Height = 1.3f });
        // Recta 5 (llegada): lomas, un doble y el sprint a la meta.
        main.Features.Add(new Feature { Kind = Kind.Rollers, From = 266f, To = 282f, Height = 0.8f, Period = 4f });
        main.Features.Add(new Feature { Kind = Kind.Double, From = 285f, To = 295f, Height = 1.4f });

        // Recta 2, línea fácil: más abajo, solo lomas suaves; se separa al salir de la curva 1 y vuelve antes de la 2.
        Line easy = new Line
        {
            Name = "Línea fácil", HalfWidth = 2.5f,
            Pixels = new[] { 150,244, 200,268, 290,280, 430,262, 570,238, 597,231 },
        };
        easy.Features.Add(new Feature { Kind = Kind.Rollers, From = 8f, To = 44f, Height = 0.6f, Period = 4.5f });
        return new List<Line> { main, easy };
    }

    // =====================================================================
    // Crear la escena
    // =====================================================================

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
            EditorUtility.DisplayProgressBar("Pista BMX", "Trazado", 0.05f);
            List<Line> lines = Layout();
            foreach (Line line in lines)
            {
                line.HalfWidth *= GameScale;
                foreach (Feature f in line.Features)
                {
                    f.From *= GameScale;
                    f.To *= GameScale;
                    f.Period *= GameScale;
                }
            }
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (Line line in lines)
            {
                for (int i = 0; i < line.Pixels.Length; i += 2)
                {
                    minX = Mathf.Min(minX, line.Pixels[i]); maxX = Mathf.Max(maxX, line.Pixels[i]);
                    minY = Mathf.Min(minY, line.Pixels[i + 1]); maxY = Mathf.Max(maxY, line.Pixels[i + 1]);
                }
            }
            float sizeX = (maxX - minX) * MetersPerPixel * GameScale + Margin * 2f, sizeZ = (maxY - minY) * MetersPerPixel * GameScale + Margin * 2f;
            foreach (Line line in lines) Prepare(line, minX, maxY);

            EditorUtility.DisplayProgressBar("Pista BMX", "Saltos, lomas y peraltes", 0.25f);
            SampleGrid grid = new SampleGrid(lines, sizeX, sizeZ);
            float[,] heights = new float[Resolution, Resolution];
            float maxH = 0f;
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float h = HeightAt(grid, new Vector2(x / (float)(Resolution - 1) * sizeX, z / (float)(Resolution - 1) * sizeZ));
                    heights[z, x] = h;
                    maxH = Mathf.Max(maxH, h);
                }
            }
            float sizeY = maxH + 1f;
            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++) heights[z, x] /= sizeY;
            }

            EditorUtility.DisplayProgressBar("Pista BMX", "Terreno y colores", 0.5f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            VisualAssetFactory.EnsureFolder(HeightmapMapBuilder.MapsFolder);
            string dataPath = HeightmapMapBuilder.MapsFolder + "/" + SceneName + "_Terreno.asset";
            AssetDatabase.DeleteAsset(dataPath);
            TerrainData data = new TerrainData { heightmapResolution = Resolution };
            data.size = new Vector3(sizeX, sizeY, sizeZ);
            data.SetHeights(0, 0, heights);
            Paint(data, grid, sizeX, sizeZ);
            AssetDatabase.CreateAsset(data, dataPath);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Pista BMX";
            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.materialTemplate = VisualAssetFactory.TerrainMaterial("Terreno");
            terrain.heightmapPixelError = 2f;   // Los saltos son chicos: más detalle de cerca.
            terrain.basemapDistance = 200f;
            terrain.drawInstanced = true;
            GameObjectUtility.SetStaticEditorFlags(terrainGo, StaticEditorFlags.BatchingStatic);

            EditorUtility.DisplayProgressBar("Pista BMX", "Bici, portón, cinta y meta", 0.75f);
            Line mainLine = lines[0];
            Vector3[] route = WorldPoints(terrain, mainLine.Points);
            int startIndex = IndexAt(mainLine, 2.5f);
            Vector3 forward = route[startIndex + 4] - route[startIndex];
            forward.y = 0f;
            BikeController bike = PrototypeSceneBuilder.AddRaceSetup(SceneName, TrackLayout.Theme.BosqueNublado, route[startIndex],
                Quaternion.LookRotation(forward), -15f, true);
            TuneBikeForBmx(bike);
            BuildGate(route, IndexAt(mainLine, 5f), mainLine.HalfWidth);
            BuildRaceTriggers(route, mainLine);
            BuildTape(terrain, lines);

            VisualAssetFactory.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = bike.gameObject;
            Debug.Log("DownhillRace: pista BMX '" + SceneName + "' creada (" + mainLine.Distance[mainLine.Distance.Length - 1].ToString("0") +
                      " m). Se juega desde el menú: BMX (o con Play en esta escena).");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // =====================================================================
    // Forma de la pista
    // =====================================================================

    /// <summary>Pasa los píxeles a metros, redondea las esquinas, remuestrea y calcula la curvatura.</summary>
    private static void Prepare(Line line, int minX, int maxY)
    {
        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < line.Pixels.Length; i += 2)
        {
            points.Add(new Vector2((line.Pixels[i] - minX) * MetersPerPixel * GameScale + Margin, (maxY - line.Pixels[i + 1]) * MetersPerPixel * GameScale + Margin));
        }
        for (int pass = 0; pass < 3; pass++) points = Chaikin(points);
        line.Points = TrailTerrainCarver.Resample(points, Step);
        int n = line.Points.Length;
        line.Distance = new float[n];
        for (int i = 1; i < n; i++) line.Distance[i] = line.Distance[i - 1] + Vector2.Distance(line.Points[i - 1], line.Points[i]);

        float[] raw = new float[n];
        const int k = 8; // 2 m antes y después
        for (int i = 0; i < n; i++)
        {
            Vector2 a = line.Points[Mathf.Max(0, i - k)], b = line.Points[i], c = line.Points[Mathf.Min(n - 1, i + k)];
            Vector2 v1 = b - a, v2 = c - b;
            float length = (v1.magnitude + v2.magnitude) * 0.5f;
            if (length < 0.01f) continue;
            float angle = Mathf.Atan2(v1.x * v2.y - v1.y * v2.x, Vector2.Dot(v1, v2));
            raw[i] = angle / length;
        }
        // Suavizado de 3 m: el peralte entra y sale de a poco.
        line.Curvature = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            int count = 0;
            for (int j = Mathf.Max(0, i - 12); j <= Mathf.Min(n - 1, i + 12); j++) { sum += raw[j]; count++; }
            line.Curvature[i] = sum / count;
        }
    }

    /// <summary>Cuánto de peralte hay en una curva (0 en recta, 1 en curva cerrada).</summary>
    private static float BermWeight(float curvature)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f / 45f, 1f / 12f, Mathf.Abs(curvature)));
    }

    /// <summary>Altura de los saltos y lomas en el punto "s" (m) a lo largo de la línea.</summary>
    private static float FeatureHeight(Line line, float s)
    {
        float h = 0f;
        foreach (Feature f in line.Features)
        {
            if (s < f.From || s > f.To) continue;
            float t = s - f.From, length = f.To - f.From, u = t / length;
            float v = 0f;
            switch (f.Kind)
            {
                case Kind.StartHill:
                    // Plataforma arriba y bajada recta y empinada, redondeada abajo.
                    v = t <= f.Period ? f.Height : f.Height * (1f - Mathf.SmoothStep(0f, 1f, (t - f.Period) / (length - f.Period)));
                    break;
                case Kind.Rollers:
                    v = f.Height * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * t / f.Period));
                    break;
                case Kind.Table:
                    if (u < 0.3f) v = f.Height * Mathf.Pow(u / 0.3f, 1.4f);            // Subida cóncava (impulsa hacia arriba).
                    else if (u < 0.7f) v = f.Height;                                   // Mesa plana.
                    else v = f.Height * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));
                    break;
                case Kind.Double:
                    if (u < 0.35f) v = f.Height * Mathf.Pow(u / 0.35f, 1.6f);           // Despegue con labio.
                    else if (u < 0.42f) v = f.Height * (1f - 0.75f * (u - 0.35f) / 0.07f);
                    else if (u < 0.58f) v = f.Height * 0.25f;                          // Hueco entre las dos lomas.
                    else if (u < 0.65f) v = f.Height * (0.25f + 0.75f * (u - 0.58f) / 0.07f);
                    else v = f.Height * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.65f) / 0.35f)); // Aterrizaje largo.
                    break;
            }
            h = Mathf.Max(h, v);
        }
        return h;
    }

    /// <summary>Altura del terreno: la pista (saltos + peralte) y un borde que baja al suelo plano.</summary>
    private static float HeightAt(SampleGrid grid, Vector2 p)
    {
        if (!grid.Nearest(p, out Line line, out int i, out float lateral)) return 0f;
        float half = line.HalfWidth;
        float clamped = Mathf.Clamp(lateral, -half, half);
        float surface = FeatureHeight(line, line.Distance[i]) + Bank(line, i, clamped);
        float edge = Mathf.Abs(lateral) - half;
        if (edge <= 0f) return surface;
        // Más alto el borde (peralte o salida), más largo el talud.
        return surface * (1f - Mathf.SmoothStep(0f, 1f, edge / (Shoulder + surface * 1.2f)));
    }

    /// <summary>Peralte: el lado de afuera de la curva sube hasta BermHeight.</summary>
    private static float Bank(Line line, int i, float lateral)
    {
        float curvature = line.Curvature[i];
        float weight = BermWeight(curvature);
        if (weight <= 0f) return 0f;
        // Girando a la izquierda (+), el lado de afuera es la derecha (lateral +).
        float outside = curvature > 0f ? lateral : -lateral;
        float half = line.HalfWidth;
        return BermHeight * weight * Mathf.SmoothStep(0f, 1f, (outside + half * 0.5f) / (half * 1.5f));
    }

    /// <summary>
    /// Colores como en la foto: pasto alrededor, cemento gris en curvas, rectas planas y la rampa de salida,
    /// y arcilla naranja justo donde están los saltos y las lomas.
    /// </summary>
    private static void Paint(TerrainData data, SampleGrid grid, float sizeX, float sizeZ)
    {
        TerrainLayer grass = HeightmapMapBuilder.Layer(SceneName + "_CapaPasto", "Pasto", 6f);
        ColorUtility.TryParseHtmlString(ClayColor, out Color clayColor);
        TerrainLayer clay = VisualAssetFactory.TerrainLayerAsset(SceneName + "_CapaArcilla", HeightmapMapBuilder.TrailColorTexture(SceneName, clayColor),
            AssetDatabase.LoadAssetAtPath<Texture2D>(VisualAssetFactory.TexturesFolder + "/TierraNormal.png"), 3f);
        TerrainLayer concrete = HeightmapMapBuilder.Layer(SceneName + "_CapaCemento", "Concreto", 4f);
        data.terrainLayers = new[] { grass, clay, concrete };
        data.alphamapResolution = AlphaResolution;
        float[,,] alpha = new float[AlphaResolution, AlphaResolution, 3];
        for (int y = 0; y < AlphaResolution; y++)
        {
            for (int x = 0; x < AlphaResolution; x++)
            {
                Vector2 p = new Vector2(x / (float)(AlphaResolution - 1) * sizeX, y / (float)(AlphaResolution - 1) * sizeZ);
                float track = 0f, jumps = 0f;
                if (grid.Nearest(p, out Line line, out int i, out float lateral))
                {
                    track = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 0.5f, Mathf.Abs(lateral) - line.HalfWidth));
                    jumps = JumpZone(line, line.Distance[i]) * (1f - BermWeight(line.Curvature[i]));
                }
                alpha[y, x, 0] = 1f - track;
                alpha[y, x, 1] = track * jumps;
                alpha[y, x, 2] = track * (1f - jumps);
            }
        }
        data.SetAlphamaps(0, 0, alpha);
    }

    /// <summary>1 dentro de una zona de saltos o lomas (con 1,5 m de transición), 0 fuera. La rampa de salida no cuenta.</summary>
    private static float JumpZone(Line line, float s)
    {
        float zone = 0f;
        foreach (Feature f in line.Features)
        {
            if (f.Kind == Kind.StartHill) continue;
            float inside = Mathf.Min(s - (f.From - 1.5f), (f.To + 1.5f) - s);
            zone = Mathf.Max(zone, Mathf.Clamp01(inside / 1.5f));
        }
        return zone;
    }

    // =====================================================================
    // Carrera
    // =====================================================================

    /// <summary>
    /// Manejo de BMX: acelera más rápido pero llega a menos velocidad, salta más alto y gira más cerrado.
    /// La pista es casi plana: se gana velocidad pedaleando y en las bajadas de las lomas.
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

    /// <summary>Portón de salida al borde de la plataforma: cae con el ¡YA!.</summary>
    private static void BuildGate(Vector3[] route, int index, float halfWidth)
    {
        Vector3 forward = route[Mathf.Min(route.Length - 1, index + 2)] - route[index];
        forward.y = 0f;
        GameObject pivot = new GameObject("Porton");
        pivot.transform.SetPositionAndRotation(route[index], Quaternion.LookRotation(forward));
        GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "Hoja";
        Object.DestroyImmediate(leaf.GetComponent<Collider>()); // La bici no se mueve hasta el ¡YA!; no hace falta que choque.
        leaf.transform.SetParent(pivot.transform, false);
        leaf.transform.localPosition = new Vector3(0f, 0.28f, 0f);
        leaf.transform.localScale = new Vector3(halfWidth * 2f + 0.4f, 0.56f, 0.06f);
        Material red = AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/Rojo.mat");
        if (red != null) leaf.GetComponent<Renderer>().sharedMaterial = red;
        pivot.AddComponent<StartGate>();
    }

    /// <summary>Checkpoint al salir de cada curva y la meta al final de la última recta.</summary>
    private static void BuildRaceTriggers(Vector3[] route, Line line)
    {
        Transform group = new GameObject("Checkpoints").transform;
        float[] checkpoints = { 76f * GameScale, 135f * GameScale, 196f * GameScale, 262f * GameScale };
        for (int k = 0; k < checkpoints.Length; k++)
        {
            HeightmapMapBuilder.Trigger("Checkpoint " + (k + 1), group, route, IndexAt(line, checkpoints[k]),
                TrackTrigger.TriggerType.Checkpoint, k + 1, line.HalfWidth * 2f + 4f);
        }
        float total = line.Distance[line.Distance.Length - 1];
        int finish = IndexAt(line, total - 4f);
        HeightmapMapBuilder.Trigger("Meta", group, route, finish, TrackTrigger.TriggerType.Meta, 0, line.HalfWidth * 2f + 6f);

        // Línea blanca de meta pintada en el suelo.
        Vector3 forward = route[Mathf.Min(route.Length - 1, finish + 2)] - route[Mathf.Max(0, finish - 2)];
        forward.y = 0f;
        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "LineaDeMeta";
        Object.DestroyImmediate(stripe.GetComponent<Collider>());
        stripe.transform.SetParent(group, false);
        stripe.transform.SetPositionAndRotation(route[finish] + Vector3.up * 0.02f, Quaternion.LookRotation(forward));
        stripe.transform.localScale = new Vector3(line.HalfWidth * 2f, 0.04f, 0.6f);
        Material white = AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/Blanco.mat");
        if (white != null) stripe.GetComponent<Renderer>().sharedMaterial = white;
    }

    /// <summary>Cinta «DOWNHILL RACE» a los dos lados de toda la pista (se corta donde se separa la línea fácil).</summary>
    private static void BuildTape(Terrain terrain, List<Line> lines)
    {
        Line main = lines[0];
        List<Vector2> flat = new List<Vector2>(main.Points);
        Vector2[] sparse = TrailTerrainCarver.Resample(flat, 2f);
        Vector3[] route = WorldPoints(terrain, sparse);
        float[] half = new float[route.Length];
        for (int i = 0; i < half.Length; i++) half[i] = main.HalfWidth;
        List<Vector3[]> avoid = new List<Vector3[]>();
        for (int k = 1; k < lines.Count; k++) avoid.Add(WorldPoints(terrain, lines[k].Points));
        float total = main.Distance[main.Distance.Length - 1];
        List<RealMapTrails.Zone> zones = new List<RealMapTrails.Zone>
        {
            // Desde el pie de la rampa: arriba en la plataforma no hace falta.
            new RealMapTrails.Zone { Name = "Cinta de la pista", Style = RealMapTrails.Decor.Cinta, From = 16f * GameScale, To = total - 1f, BothSides = true },
        };
        RealMapDecor.Build(terrain, route, half, zones, false, 1, avoid, SceneName);
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    private static int IndexAt(Line line, float distance)
    {
        for (int i = 0; i < line.Distance.Length; i++)
        {
            if (line.Distance[i] >= distance) return i;
        }
        return line.Distance.Length - 1;
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

    private static List<Vector2> Chaikin(List<Vector2> points)
    {
        List<Vector2> result = new List<Vector2> { points[0] };
        for (int i = 0; i < points.Count - 1; i++)
        {
            result.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
            result.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
        }
        result.Add(points[points.Count - 1]);
        return result;
    }

    /// <summary>Busca rápido el punto de línea central más cercano (cuadrícula de celdas de 2 m).</summary>
    private class SampleGrid
    {
        private const float Cell = 2f;
        private readonly List<(Line line, int index)>[] cells;
        private readonly int width, height;
        private readonly int reach;

        public SampleGrid(List<Line> lines, float sizeX, float sizeZ)
        {
            width = Mathf.CeilToInt(sizeX / Cell) + 1;
            height = Mathf.CeilToInt(sizeZ / Cell) + 1;
            cells = new List<(Line, int)>[width * height];
            float maxHalf = 0f;
            foreach (Line line in lines)
            {
                maxHalf = Mathf.Max(maxHalf, line.HalfWidth);
                for (int i = 0; i < line.Points.Length; i++)
                {
                    int cx = Mathf.Clamp((int)(line.Points[i].x / Cell), 0, width - 1), cz = Mathf.Clamp((int)(line.Points[i].y / Cell), 0, height - 1);
                    int c = cz * width + cx;
                    if (cells[c] == null) cells[c] = new List<(Line, int)>();
                    cells[c].Add((line, i));
                }
            }
            // Alcance: medio ancho + talud de la salida (la parte más alta).
            reach = Mathf.CeilToInt((maxHalf + Shoulder + 4.5f * 1.2f + 1f) / Cell);
        }

        /// <summary>Línea más cercana (por borde), índice del punto y distancia lateral con signo (+ = derecha).</summary>
        public bool Nearest(Vector2 p, out Line best, out int bestIndex, out float lateral)
        {
            best = null;
            bestIndex = 0;
            lateral = 0f;
            float bestEdge = float.MaxValue, bestDist = 0f;
            int cx = (int)(p.x / Cell), cz = (int)(p.y / Cell);
            for (int z = Mathf.Max(0, cz - reach); z <= Mathf.Min(height - 1, cz + reach); z++)
            {
                for (int x = Mathf.Max(0, cx - reach); x <= Mathf.Min(width - 1, cx + reach); x++)
                {
                    List<(Line line, int index)> bucket = cells[z * width + x];
                    if (bucket == null) continue;
                    foreach ((Line line, int index) in bucket)
                    {
                        float d = Vector2.Distance(p, line.Points[index]);
                        float edge = d - line.HalfWidth;
                        if (edge < bestEdge) { bestEdge = edge; best = line; bestIndex = index; bestDist = d; }
                    }
                }
            }
            if (best == null || bestEdge > reach * Cell) return false;
            Vector2 a = best.Points[Mathf.Max(0, bestIndex - 1)], b = best.Points[Mathf.Min(best.Points.Length - 1, bestIndex + 1)];
            Vector2 tangent = (b - a).normalized;
            Vector2 right = new Vector2(tangent.y, -tangent.x);
            lateral = Vector2.Dot(p - best.Points[bestIndex], right);
            return true;
        }
    }
}
