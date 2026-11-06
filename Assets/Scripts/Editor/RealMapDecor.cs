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
    public static void Build(Terrain terrain, Vector3[] route, float[] halfWidths, List<RealMapTrails.Zone> zones, bool dry, int seed)
    {
        if (zones == null || zones.Count == 0 || route.Length < 3) return;
        Kit kit = new Kit(dry);
        System.Random rng = new System.Random(seed * 31 + 7);
        Transform root = new GameObject("Ambientacion").transform;
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
        public readonly Mesh Pole;
        public readonly Material Wall, Roof, Zinc, Window, PoleMat, Bark, Leaves, RockMat, AgaveMat;

        public Kit(bool dry)
        {
            Load(Houses, "Casa", 3);
            Load(Bushes, "Arbusto", 3);
            Load(Rocks, "Roca", 4);
            Load(Agaves, "Agave", 2);
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
