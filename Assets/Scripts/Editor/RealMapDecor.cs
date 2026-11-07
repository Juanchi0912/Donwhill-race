using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Decor = RealMapTrails.Decor;

/// <summary>
/// Ambientación de un mapa real a lo largo de su ruta, por zonas (pueblo, ladera abierta, matorral, pedregal),
/// con los modelos que ya usan las otras pistas. Va después de crear el terreno y la carrera,
/// porque necesita la altura del terreno y los modelos que genera la paleta de la pista.
/// </summary>
public static class RealMapDecor
{
    private const float Step = 2f;   // Separación de los puntos de la ruta (m), igual que TrailPath.

    /// <summary>
    /// "route" son los puntos de la línea central (cada 2 m) y "halfWidths" el medio ancho del sendero en cada uno.
    /// </summary>
    public static void Build(Terrain terrain, Vector3[] route, float[] halfWidths, List<RealMapTrails.Zone> zones, bool dry, int seed,
        List<Vector3[]> avoid = null, string assetPrefix = "Mapa")
    {
        if (zones == null || zones.Count == 0 || route.Length < 3) return;
        Kit kit = new Kit(dry);
        System.Random rng = new System.Random(seed * 31 + 7);
        Transform root = new GameObject("Ambientacion").transform;
        int tapeCount = 0;
        foreach (RealMapTrails.Zone zone in zones)
        {
            Transform parent = new GameObject(zone.Name).transform;
            parent.SetParent(root, false);
            int i0 = Mathf.Clamp(Mathf.RoundToInt(zone.From / Step), 1, route.Length - 2);
            int i1 = Mathf.Clamp(Mathf.RoundToInt(zone.To / Step), 1, route.Length - 2);
            switch (zone.Style)
            {
                case Decor.Pueblo: Village(terrain, route, halfWidths, i0, i1, rng, kit, parent); break;
                case Decor.Matorral: Thicket(terrain, route, halfWidths, i0, i1, rng, kit, parent); break;
                case Decor.Pedregal: RockField(terrain, route, halfWidths, i0, i1, rng, kit, parent); break;
                case Decor.LaderaAbierta: OpenSlope(terrain, route, halfWidths, i0, i1, rng, kit, parent); break;
                case Decor.Arboleda: Grove(terrain, route, halfWidths, i0, i1, rng); break;
                case Decor.Cinta:
                    Tape(terrain, route, halfWidths, i0, i1, zone.BothSides, avoid, kit, parent,
                        VisualAssetFactory.ModelsFolder + "/" + assetPrefix + "_Cinta" + (++tapeCount) + ".asset");
                    break;
            }
        }
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
    }

    /// <summary>Casas que miran al camino y postes de luz en un lado, como en la salida del pueblo.</summary>
    private static void Village(Terrain terrain, Vector3[] route, float[] hw, int i0, int i1, System.Random rng, Kit kit, Transform parent)
    {
        if (kit.Houses.Count > 0)
        {
            for (int i = i0 + Rand(rng, 1, 4); i < i1; i += Rand(rng, 6, 10))
            {
                int side = rng.NextDouble() < 0.5 ? -1 : 1;
                Vector3 position = Beside(terrain, route, i, side * (hw[i] + Range(rng, 7f, 12f)));
                // La fachada (con la puerta) mira hacia el camino.
                Vector3 toRoad = Flat(route[i] - position);
                Quaternion rotation = Quaternion.LookRotation(-toRoad) * Quaternion.Euler(0f, Range(rng, -10f, 10f), 0f);
                bool shed = rng.NextDouble() < 0.3;
                Place(shed ? "Galpon" : "Casa", parent, kit.Houses[rng.Next(kit.Houses.Count)], position - Vector3.up * 0.15f, rotation,
                    Vector3.one, kit.Wall, shed ? kit.Zinc : kit.Roof, kit.Window);
            }
        }
        if (kit.Pole != null)
        {
            for (int i = i0 + 2; i < i1; i += 18)
            {
                Place("PosteLuz", parent, kit.Pole, Beside(terrain, route, i, hw[i] + 2.5f), Quaternion.LookRotation(Flat(Forward(route, i))), Vector3.one, kit.PoleMat);
            }
        }
    }

    /// <summary>Arbustos secos cerrando los dos lados: el sendero se siente angosto aunque no lo sea tanto.</summary>
    private static void Thicket(Terrain terrain, Vector3[] route, float[] hw, int i0, int i1, System.Random rng, Kit kit, Transform parent)
    {
        if (kit.Bushes.Count == 0) return;
        for (int i = i0; i < i1; i++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() < 0.35) continue;
                Vector3 position = Beside(terrain, route, i, side * (hw[i] + Range(rng, 0.8f, 3.5f)));
                if (!ClearOfRoute(route, hw, position, i, 0.6f)) continue;
                float scale = Range(rng, 1f, 1.9f);
                Place("Arbusto", parent, kit.Bushes[rng.Next(kit.Bushes.Count)], position - Vector3.up * 0.05f, RandomYaw(rng),
                    Vector3.one * scale, kit.Bark, kit.Leaves);
            }
        }
    }

    /// <summary>Rocas en los bordes (con colisión) y piedritas sueltas dentro del sendero, más agaves.</summary>
    private static void RockField(Terrain terrain, Vector3[] route, float[] hw, int i0, int i1, System.Random rng, Kit kit, Transform parent)
    {
        if (kit.Rocks.Count == 0) return;
        for (int i = i0; i < i1; i += 2)
        {
            int side = rng.NextDouble() < 0.5 ? -1 : 1;
            // Roca del borde: se puede usar de rampa natural o hay que esquivarla.
            if (rng.NextDouble() < 0.7)
            {
                float size = Range(rng, 0.5f, 1.4f);
                Vector3 position = Beside(terrain, route, i, side * (hw[i] + Range(rng, -0.3f, 1.8f)));
                Rock(parent, kit, rng, position, size, true);
            }
            // Piedras sueltas en el camino: chicas, sin colisión (se pisan y sacuden la cámara de tierra).
            if (rng.NextDouble() < 0.35)
            {
                Vector3 position = Beside(terrain, route, i, Range(rng, -hw[i] + 0.4f, hw[i] - 0.4f));
                Rock(parent, kit, rng, position, Range(rng, 0.2f, 0.4f), false);
            }
            if (kit.Agaves.Count > 0 && rng.NextDouble() < 0.18)
            {
                Vector3 position = Beside(terrain, route, i, -side * (hw[i] + Range(rng, 1.5f, 5f)));
                if (ClearOfRoute(route, hw, position, i, 0.8f))
                {
                    Place("Agave", parent, kit.Agaves[rng.Next(kit.Agaves.Count)], position, RandomYaw(rng), Vector3.one * Range(rng, 1f, 1.6f), kit.AgaveMat);
                }
            }
        }
    }

    /// <summary>Ladera de pasto seco con vista: se ralean los árboles cerca del sendero y quedan agaves sueltos.</summary>
    private static void OpenSlope(Terrain terrain, Vector3[] route, float[] hw, int i0, int i1, System.Random rng, Kit kit, Transform parent)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        const float clearRadius = 45f;
        List<TreeInstance> kept = new List<TreeInstance>();
        foreach (TreeInstance tree in data.treeInstances)
        {
            Vector3 world = origin + Vector3.Scale(tree.position, data.size);
            bool near = false;
            for (int i = i0; i <= i1 && !near; i += 3)
            {
                Vector3 d = world - route[i];
                d.y = 0f;
                near = d.sqrMagnitude < clearRadius * clearRadius;
            }
            // Se quitan casi todos: queda alguno suelto para que no parezca cortado a cuchillo.
            if (!near || rng.NextDouble() < 0.12) kept.Add(tree);
        }
        data.SetTreeInstances(kept.ToArray(), true);

        if (kit.Agaves.Count == 0) return;
        for (int i = i0; i < i1; i += Rand(rng, 6, 14))
        {
            int side = rng.NextDouble() < 0.5 ? -1 : 1;
            Vector3 position = Beside(terrain, route, i, side * (hw[i] + Range(rng, 2f, 9f)));
            if (!ClearOfRoute(route, hw, position, i, 1f)) continue;
            Place("Agave", parent, kit.Agaves[rng.Next(kit.Agaves.Count)], position, RandomYaw(rng), Vector3.one * Range(rng, 0.9f, 1.5f), kit.AgaveMat);
        }
    }

    /// <summary>Más árboles a los dos lados (partes anchas): árboles del terreno, con tronco que choca.</summary>
    private static void Grove(Terrain terrain, Vector3[] route, float[] hw, int i0, int i1, System.Random rng)
    {
        TerrainData data = terrain.terrainData;
        List<int> treeKinds = new List<int>();
        TreePrototype[] prototypes = data.treePrototypes;
        for (int k = 0; k < prototypes.Length; k++)
        {
            string name = prototypes[k].prefab != null ? prototypes[k].prefab.name : string.Empty;
            if (name.StartsWith("Arbol") || name.StartsWith("Pino")) treeKinds.Add(k);
        }
        if (treeKinds.Count == 0) return;
        Vector3 origin = terrain.transform.position;
        List<TreeInstance> trees = new List<TreeInstance>(data.treeInstances);
        for (int i = i0; i < i1; i += 2)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                if (rng.NextDouble() < 0.45) continue;
                Vector3 position = Beside(terrain, route, i, side * (hw[i] + Range(rng, 3f, 18f)));
                if (!ClearOfRoute(route, hw, position, i, 3f)) continue;
                Vector3 local = position - origin;
                float u = local.x / data.size.x, v = local.z / data.size.z;
                if (u < 0f || u > 1f || v < 0f || v > 1f || data.GetSteepness(u, v) > 40f) continue;
                float scale = Range(rng, 0.8f, 1.25f);
                trees.Add(new TreeInstance
                {
                    prototypeIndex = treeKinds[rng.Next(treeKinds.Count)],
                    position = new Vector3(u, 0f, v),
                    widthScale = scale,
                    heightScale = scale * Range(rng, 0.9f, 1.1f),
                    rotation = Range(rng, 0f, Mathf.PI * 2f),
                    color = Color.white,
                    lightmapColor = Color.white
                });
            }
        }
        data.SetTreeInstances(trees.ToArray(), true);
    }

    // =====================================================================
    // Cinta de carrera
    // =====================================================================

    /// <summary>Material de la cinta "DOWNHILL RACE" (lo usa también la ambientación de las pistas terminadas).</summary>
    internal static Material CourseTapeMaterial() => Kit.TapeMaterial();

    private const float TapeOffset = 0.7f;      // Distancia de los postes al borde del sendero (m).
    private const float TapeBottom = 0.78f, TapeTop = 0.94f;
    private const float TapeRepeat = 3f;        // Cada 3 m se repite el texto.
    private const float CurveRadius = 45f;      // En lo ancho, solo se acordona por fuera de curvas más cerradas que esto.

    /// <summary>
    /// Cinta "DOWNHILL RACE" con postes cada 4 m: a los dos lados donde el sendero es angosto (o si se pide),
    /// y por fuera de las curvas cerradas en lo ancho, que es donde uno se sale o se pierde. No choca:
    /// solo marca por dónde va la pista. Se corta donde salen las líneas alternativas y en el río.
    /// </summary>
    private static void Tape(Terrain terrain, Vector3[] route, float[] hw, int i0, int i1, bool bothSides, List<Vector3[]> avoid,
        Kit kit, Transform parent, string meshPath)
    {
        if (kit.Tape == null) return;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tapeTris = new List<int>(), postTris = new List<int>();
        for (int side = -1; side <= 1; side += 2)
        {
            List<Vector3> run = new List<Vector3>();
            for (int i = i0; i <= i1; i += 2)
            {
                bool want = bothSides || hw[i] < 4f || OuterOfTightCurve(route, i, side);
                Vector3 post = Beside(terrain, route, i, side * (hw[i] + TapeOffset));
                bool ok = want && ClearOfRoute(route, hw, post, i, 0.4f) && !NearAny(avoid, post, 4f);
                if (ok) run.Add(post);
                else
                {
                    AddTapeRun(run, vertices, uvs, tapeTris, postTris);
                    run.Clear();
                }
            }
            AddTapeRun(run, vertices, uvs, tapeTris, postTris);
        }
        if (vertices.Count == 0) return;

        Mesh mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(meshPath) };
        mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(tapeTris, 0);
        mesh.SetTriangles(postTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh = VisualAssetFactory.SaveMeshAsset(mesh, meshPath);
        GameObject go = Place("Cinta", parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, kit.Tape, kit.PostMat);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    /// <summary>Una tira de cinta entre postes seguidos, con sus postes.</summary>
    internal static void AddTapeRun(List<Vector3> run, List<Vector3> vertices, List<Vector2> uvs, List<int> tapeTris, List<int> postTris)
    {
        if (run.Count < 2) return;
        int start = vertices.Count;
        float along = 0f;
        for (int k = 0; k < run.Count; k++)
        {
            if (k > 0) along += Vector3.Distance(run[k - 1], run[k]);
            vertices.Add(run[k] + Vector3.up * TapeBottom);
            vertices.Add(run[k] + Vector3.up * TapeTop);
            uvs.Add(new Vector2(along / TapeRepeat, 0f));
            uvs.Add(new Vector2(along / TapeRepeat, 1f));
            if (k > 0)
            {
                int a = start + (k - 1) * 2;
                tapeTris.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
        }
        foreach (Vector3 p in run) AddPost(p, vertices, uvs, postTris);
    }

    /// <summary>Poste fino de 1 m (caja de 4 cm).</summary>
    private static void AddPost(Vector3 foot, List<Vector3> vertices, List<Vector2> uvs, List<int> tris)
    {
        const float r = 0.02f, h = 1.02f;
        Vector3[] corners =
        {
            new Vector3(-r, 0f, -r), new Vector3(r, 0f, -r), new Vector3(r, 0f, r), new Vector3(-r, 0f, r),
            new Vector3(-r, h, -r), new Vector3(r, h, -r), new Vector3(r, h, r), new Vector3(-r, h, r),
        };
        int[][] faces = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }, new[] { 4, 5, 6, 7 } };
        foreach (int[] f in faces)
        {
            int b = vertices.Count;
            foreach (int c in f)
            {
                vertices.Add(foot - Vector3.up * 0.15f + corners[c]);
                uvs.Add(Vector2.zero);
            }
            // Caras hacia afuera: vistas desde fuera van en sentido horario.
            tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }
    }

    /// <summary>True si "side" es el lado de afuera de una curva más cerrada que CurveRadius.</summary>
    private static bool OuterOfTightCurve(Vector3[] route, int i, int side)
    {
        const int k = 6; // 12 m antes y después
        if (i - k < 0 || i + k >= route.Length) return false;
        Vector3 a = route[i - k], b = route[i], c = route[i + k];
        Vector3 v1 = Flat(b - a), v2 = Flat(c - b);
        float cross = v1.x * v2.z - v1.z * v2.x;
        float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c), ca = Vector3.Distance(c, a);
        float area = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x)) * 0.5f;
        float radius = area > 1e-4f ? ab * bc * ca / (4f * area) : float.MaxValue;
        // Girando a la derecha (cross < 0) el lado de afuera es el izquierdo (-1), y al revés.
        int outer = cross < 0f ? -1 : 1;
        return radius < CurveRadius && side == outer;
    }

    private static bool NearAny(List<Vector3[]> lines, Vector3 p, float distance)
    {
        if (lines == null) return false;
        foreach (Vector3[] line in lines)
        {
            foreach (Vector3 q in line)
            {
                Vector3 d = p - q;
                d.y = 0f;
                if (d.sqrMagnitude < distance * distance) return true;
            }
        }
        return false;
    }

    // =====================================================================
    // Zonas verdes de costa
    // =====================================================================

    /// <summary>
    /// Zona verde de costa: matas de caña de azúcar en grupos, árboles chicos de cacao y arbustos verdes,
    /// sin tapar el sendero ni el río. "center" está en el mundo; "radius" en metros.
    /// </summary>
    public static void CoastPatch(Terrain terrain, string name, Vector3 center, float radius, Vector3[] route, float[] hw,
        List<Vector3[]> riverPoints, List<float> riverHalfWidths, int seed)
    {
        Kit kit = new Kit(false);
        System.Random rng = new System.Random(seed * 53 + 11);
        Transform parent = new GameObject(name).transform;
        Transform root = GameObject.Find("Ambientacion") != null ? GameObject.Find("Ambientacion").transform : new GameObject("Ambientacion").transform;
        parent.SetParent(root, false);
        int count = Mathf.RoundToInt(Mathf.PI * radius * radius / 9f); // ~1 planta cada 9 m²
        for (int n = 0; n < count; n++)
        {
            // Más denso al centro, se abre hacia el borde del círculo.
            float r = radius * Mathf.Sqrt((float)rng.NextDouble()) * 1.05f;
            float a = Range(rng, 0f, Mathf.PI * 2f);
            Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            if (!ClearOfAll(route, hw, p, 1.2f) || InRiver(riverPoints, riverHalfWidths, p, 1.5f)) continue;
            double pick = rng.NextDouble();
            if (pick < 0.55 && kit.Canes.Count > 0)
            {
                // Caña de azúcar: varias matas juntas, altas.
                for (int c = 0; c < 3; c++)
                {
                    Vector3 q = p + new Vector3(Range(rng, -0.8f, 0.8f), 0f, Range(rng, -0.8f, 0.8f));
                    q.y = terrain.SampleHeight(q) + terrain.transform.position.y - 0.05f;
                    Place("Cana", parent, kit.Canes[rng.Next(kit.Canes.Count)], q, RandomYaw(rng), Vector3.one * Range(rng, 1f, 1.4f), kit.CaneMat);
                }
            }
            else if (pick < 0.8 && kit.Broadleaf.Count > 0)
            {
                // Cacao: árbol chico de hoja verde y brillante.
                GameObject tree = Place("Cacao", parent, kit.Broadleaf[rng.Next(kit.Broadleaf.Count)], p - Vector3.up * 0.1f, RandomYaw(rng),
                    Vector3.one * Range(rng, 0.45f, 0.62f), kit.Bark, kit.GreenLeaves);
                CapsuleCollider trunk = tree.AddComponent<CapsuleCollider>();
                trunk.radius = 0.25f;
                trunk.height = 2f;
                trunk.center = new Vector3(0f, 1f, 0f);
            }
            else if (kit.Bushes.Count > 0)
            {
                Place("ArbustoVerde", parent, kit.Bushes[rng.Next(kit.Bushes.Count)], p - Vector3.up * 0.05f, RandomYaw(rng),
                    Vector3.one * Range(rng, 1f, 1.7f), kit.Bark, kit.GreenLeaves);
            }
        }
        foreach (Transform t in parent.GetComponentsInChildren<Transform>())
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
    }

    // =====================================================================
    // Río rocoso
    // =====================================================================

    /// <summary>
    /// Agua que corre sobre el cauce ya tallado, rocas grandes en el cauce (con colisión), piedras chicas
    /// donde cruza el sendero y una zona que frena la bici. "points" es la línea central en el mundo.
    /// </summary>
    public static void RockyRiver(Terrain terrain, string name, Vector3[] points, float halfWidth, Vector3[] route, float[] hw,
        string meshPath, bool dry, int seed)
    {
        if (points.Length < 2) return;
        Kit kit = new Kit(dry);
        System.Random rng = new System.Random(seed * 71 + 5);
        Transform parent = new GameObject(name).transform;
        float waterOffset = 0.45f;

        // --- Agua: una franja que sigue el cauce (puede ir en bajada, como un río de verdad).
        int n = points.Length;
        Vector3[] vertices = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        int[] triangles = new int[(n - 1) * 6];
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) along += Vector3.Distance(points[i - 1], points[i]);
            Vector3 right = Vector3.Cross(Vector3.up, Flat(Forward(points, i))).normalized * halfWidth;
            Vector3 c = points[i];
            c.y = terrain.SampleHeight(c) + terrain.transform.position.y + waterOffset;
            vertices[i * 2] = c - right;
            vertices[i * 2 + 1] = c + right;
            uvs[i * 2] = new Vector2(0f, along / 6f);
            uvs[i * 2 + 1] = new Vector2(halfWidth * 2f / 6f, along / 6f);
            if (i < n - 1)
            {
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
        }
        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        if (mesh.normals[0].y < 0f)
        {
            // Si quedó mirando hacia abajo se invierte el orden de los triángulos (si no, no se ve desde arriba).
            for (int t = 0; t < triangles.Length; t += 3) (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        mesh = VisualAssetFactory.SaveMeshAsset(mesh, meshPath);
        Material water = LoadMaterialStatic("Agua");
        GameObject waterGo = Place("Agua", parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, water);
        waterGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        waterGo.AddComponent<WaterFlow>(); // Las ondas se desplazan: el agua corre.

        // --- El agua frena (una sola zona, para que no se sume dos veces).
        Vector3 a = vertices[0] * 0.5f + vertices[1] * 0.5f, b = vertices[n * 2 - 2] * 0.5f + vertices[n * 2 - 1] * 0.5f;
        GameObject zone = new GameObject("ZonaAgua");
        zone.transform.SetParent(parent, false);
        zone.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(Flat(b - a)));
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(halfWidth * 2f, Mathf.Abs(b.y - a.y) + 4f, Vector3.Distance(a, b));
        box.center = new Vector3(0f, 0.5f, 0f);
        zone.AddComponent<SlowZone>().Configure(6f);

        // --- Rocas: grandes en el cauce y las orillas; donde cruza el sendero solo piedras chicas.
        if (kit.Rocks.Count == 0) return;
        for (int k = 0; k < 70; k++)
        {
            int i = rng.Next(n);
            float lateral = Range(rng, -halfWidth - 2f, halfWidth + 2f);
            Vector3 right = Vector3.Cross(Vector3.up, Flat(Forward(points, i))).normalized;
            Vector3 p = points[i] + right * lateral;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            bool onTrail = !ClearOfAll(route, hw, p, 1.5f);
            float size = onTrail ? Range(rng, 0.2f, 0.4f) : Range(rng, 0.5f, 1.8f);
            Rock(parent, kit, rng, p, size, !onTrail);
        }
        foreach (Transform t in parent.GetComponentsInChildren<Transform>())
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
    }

    private static Material LoadMaterialStatic(string name) => AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/" + name + ".mat");

    private static bool ClearOfAll(Vector3[] route, float[] hw, Vector3 p, float margin)
    {
        for (int j = 0; j < route.Length; j++)
        {
            Vector3 d = p - route[j];
            d.y = 0f;
            float limit = hw[j] + margin;
            if (d.sqrMagnitude < limit * limit) return false;
        }
        return true;
    }

    private static bool InRiver(List<Vector3[]> rivers, List<float> halfWidths, Vector3 p, float margin)
    {
        if (rivers == null) return false;
        for (int r = 0; r < rivers.Count; r++)
        {
            float limit = halfWidths[r] + margin;
            foreach (Vector3 q in rivers[r])
            {
                Vector3 d = p - q;
                d.y = 0f;
                if (d.sqrMagnitude < limit * limit) return true;
            }
        }
        return false;
    }

    private static void Rock(Transform parent, Kit kit, System.Random rng, Vector3 position, float size, bool collider)
    {
        Mesh mesh = kit.Rocks[rng.Next(kit.Rocks.Count)];
        Vector3 scale = new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.7f, 1.0f), size * Range(rng, 0.9f, 1.3f));
        Quaternion rotation = Quaternion.Euler(Range(rng, -10f, 10f), Range(rng, 0f, 360f), Range(rng, -10f, 10f));
        GameObject rock = Place("Roca", parent, mesh, position - Vector3.up * scale.y * 0.2f, rotation, scale, kit.RockMat);
        if (collider)
        {
            // Collider convexo con la forma de la roca (la malla es facetada y simple).
            MeshCollider meshCollider = rock.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
        }
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    /// <summary>Punto al costado de la ruta ("lateral" metros, + = derecha) apoyado en el terreno.</summary>
    private static Vector3 Beside(Terrain terrain, Vector3[] route, int i, float lateral)
    {
        Vector3 right = Vector3.Cross(Vector3.up, Flat(Forward(route, i))).normalized;
        Vector3 p = route[i] + right * lateral;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p;
    }

    /// <summary>False si el punto cae dentro del sendero en otra parte de la ruta (zigzags que pasan cerca).</summary>
    private static bool ClearOfRoute(Vector3[] route, float[] hw, Vector3 p, int around, float margin)
    {
        int from = Mathf.Max(0, around - 150), to = Mathf.Min(route.Length - 1, around + 150);
        for (int j = from; j <= to; j++)
        {
            Vector3 d = p - route[j];
            d.y = 0f;
            float limit = hw[j] + margin;
            if (d.sqrMagnitude < limit * limit) return false;
        }
        return true;
    }

    private static Vector3 Forward(Vector3[] route, int i)
    {
        Vector3 f = route[Mathf.Min(route.Length - 1, i + 1)] - route[Mathf.Max(0, i - 1)];
        return f.sqrMagnitude > 1e-6f ? f : Vector3.forward;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    private static Quaternion RandomYaw(System.Random rng) => Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
    private static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
    private static int Rand(System.Random rng, int a, int b) => rng.Next(a, b);

    private static GameObject Place(string name, Transform parent, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale,
        params Material[] materials)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        return go;
    }

    /// <summary>Modelos y materiales de las otras pistas (los genera la paleta al armar la carrera).</summary>
    private class Kit
    {
        public readonly List<Mesh> Houses = new List<Mesh>(), Bushes = new List<Mesh>(), Rocks = new List<Mesh>(), Agaves = new List<Mesh>();
        public readonly List<Mesh> Canes = new List<Mesh>(), Broadleaf = new List<Mesh>();
        public readonly Mesh Pole;
        public readonly Material Wall, Roof, Zinc, Window, PoleMat, Bark, Leaves, RockMat, AgaveMat, CaneMat, GreenLeaves, Tape, PostMat;

        public Kit(bool dry)
        {
            Load(Houses, "Casa", 3);
            Load(Bushes, "Arbusto", 3);
            Load(Rocks, "Roca", 4);
            Load(Agaves, "Agave", 2);
            Load(Canes, "Cana", 2);
            Load(Broadleaf, "ArbolFrondoso", 2);
            CaneMat = LoadMaterial("Cana");
            GreenLeaves = LoadMaterial("HojasVerdes");
            PostMat = LoadMaterial("Blanco");
            Tape = TapeMaterial();
            Pole = LoadMesh("PosteLuz");
            Wall = LoadMaterial("CasaPared");
            Roof = LoadMaterial("CasaTeja");
            Zinc = LoadMaterial("CasaZinc");
            Window = LoadMaterial("CasaVentana");
            PoleMat = LoadMaterial("PosteConcreto");
            Bark = LoadMaterial("Tronco");
            Leaves = LoadMaterial(dry ? "HojasSecas" : "Hojas");
            RockMat = LoadMaterial(dry ? "RocaSeca" : "Roca");
            AgaveMat = LoadMaterial("Agave");
        }

        /// <summary>Material de la cinta "DOWNHILL RACE" (se ve de los dos lados).</summary>
        internal static Material TapeMaterial()
        {
            Texture2D texture = VisualAssetFactory.SaveTexture(ProceduralTextures.CourseTape(1024, 96, "DOWNHILL RACE"), "CintaCarrera",
                VisualAssetFactory.TextureKind.Color, 1024, 4);
            return VisualAssetFactory.Cutout("CintaCarrera", texture, Color.white, 0.25f, true);
        }

        private static void Load(List<Mesh> list, string prefix, int count)
        {
            for (int i = 1; i <= count; i++)
            {
                Mesh mesh = LoadMesh(prefix + i);
                if (mesh != null) list.Add(mesh);
            }
        }

        private static Mesh LoadMesh(string name) => AssetDatabase.LoadAssetAtPath<Mesh>(VisualAssetFactory.ModelsFolder + "/" + name + ".asset");
        private static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/" + name + ".mat");
    }
}
