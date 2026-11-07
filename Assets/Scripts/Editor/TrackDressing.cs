using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Feature = RealMapTrails.Feature;
using Line = TrailTerrainCarver.Line;

/// <summary>
/// Ambientación de una pista terminada (estilo carrera de downhill): cinta "DOWNHILL RACE" a los dos lados,
/// pancartas de patrocinadores en saltos y curvas, rampas de madera, miradores con drop, el puente de madera,
/// la cerca de piedra, rocas y raíces, bosque cerrado que hay que esquivar, curvas de arcilla resbalosas,
/// público en la recta final, arcos en los checkpoints, una casa de campo y la ciudad allá abajo.
/// No toca las partes reales (tramos Locked): esas las edita el usuario.
/// </summary>
public static class TrackDressing
{
    private const float Sp = TrailTerrainCarver.Spacing;
    private static readonly string[] Sponsors = { "DOWNHILL RACE", "TURI BIKE PARK", "CUENCA GRAVITY", "ANDES RIDERS", "PIEDRA ROJA", "VOLCAN CYCLES" };
    private static readonly Color[] SponsorColors =
    {
        new Color(0.85f, 0.12f, 0.1f), new Color(0.1f, 0.35f, 0.8f), new Color(0.1f, 0.1f, 0.12f),
        new Color(0.95f, 0.55f, 0.05f), new Color(0.55f, 0.15f, 0.1f), new Color(0.1f, 0.55f, 0.25f)
    };

    /// <summary>Lo que comparten todas las partes de la ambientación.</summary>
    private class Ctx
    {
        public Terrain Terrain;
        public Vector3 Origin;
        public RealMapTrails.Set Set;
        public List<Line> Lines;
        public TrailTerrainCarver.Result Carved;
        public float HeightOffset;      // Altura del terreno = altura tallada + esto.
        public float[,] Edge;           // Distancia a la orilla del sendero o camino más cercano.
        public string Prefix;
        public System.Random Rng;
        public Transform Root;
        public Material Wood, White, Rock, Bark, Skin, Tape;
        public Material[] SponsorMats;
        public Mesh Banner, Cube, Capsule, Sphere;
        public List<Mesh> Rocks = new List<Mesh>(), Houses = new List<Mesh>(), Bushes = new List<Mesh>();
        public int MeshCount;
    }

    public static void Build(Terrain terrain, RealMapTrails.Set set, List<Line> lines, TrailTerrainCarver.Result carved,
        List<Vector3[]> routes, HeightmapMapBuilder.Settings s, float heightOffset)
    {
        if (lines == null || carved == null) return;
        Ctx c = new Ctx
        {
            Terrain = terrain, Origin = terrain.transform.position, Set = set, Lines = lines, Carved = carved,
            HeightOffset = heightOffset, Prefix = s.MapName, Rng = new System.Random(s.Seed * 97 + 13)
        };
        TerrainData data = terrain.terrainData;
        c.Edge = TrailTerrainCarver.EdgeDistance(lines.FindAll(l => l.Sink <= 0f), 2049, data.size.x, data.size.z, 6f);
        LoadKit(c);
        c.Root = new GameObject("Pista terminada").transform;

        Tape(c);
        Banners(c);
        FeatureDressing(c);
        Bridges(c);
        ForestHazard(c);
        ClayZones(c);
        Crowd(c);
        Gates(c);
        Farms(c);
        City(c);

        foreach (Transform t in c.Root.GetComponentsInChildren<Transform>())
        {
            if (t.GetComponent<Collider>() != null && t.GetComponent<Collider>().isTrigger) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
        Debug.Log("DownhillRace: pista terminada (" + carved.Features.Count + " saltos y obstáculos, " + carved.Clay.Count + " curvas de arcilla).");
    }

    // =====================================================================
    // Cinta a los dos lados
    // =====================================================================

    /// <summary>
    /// Cinta "DOWNHILL RACE" a los dos lados de todos los senderos nuevos (no en las partes reales).
    /// Se corta sola donde hay otro sendero o camino cerca (cruces, salidas de atajos) y sobre el puente.
    /// </summary>
    private static void Tape(Ctx c)
    {
        Transform parent = Group(c, "Cinta");
        for (int k = 0; k < c.Lines.Count; k++)
        {
            Line line = c.Lines[k];
            if (!IsDressable(line)) continue;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tapeTris = new List<int>(), postTris = new List<int>();
            int n = line.Points.Length, margin = Idx(9f);
            for (int side = -1; side <= 1; side += 2)
            {
                List<Vector3> run = new List<Vector3>();
                for (int i = margin; i < n - margin; i += 4)
                {
                    float offset = line.HalfWidth + 0.8f;
                    Vector3 post = Beside(c, line, i, side * offset);
                    bool ok = !line.NoCarve[i] && EdgeAt(c, post) > offset - line.HalfWidth - 0.3f;
                    if (ok) run.Add(post);
                    else
                    {
                        RealMapDecor.AddTapeRun(run, vertices, uvs, tapeTris, postTris);
                        run.Clear();
                    }
                }
                RealMapDecor.AddTapeRun(run, vertices, uvs, tapeTris, postTris);
            }
            if (vertices.Count == 0) continue;
            Mesh mesh = new Mesh { name = c.Prefix + "_CintaFinal" + k, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tapeTris, 0);
            mesh.SetTriangles(postTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMesh(c, mesh, "CintaFinal" + k);
            GameObject go = Place("Cinta " + line.Name, parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, c.Tape, c.White);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // =====================================================================
    // Pancartas de patrocinadores
    // =====================================================================

    /// <summary>Pancartas antes de cada salto, por fuera de las curvas cerradas y de vez en cuando en las rectas.</summary>
    private static void Banners(Ctx c)
    {
        Transform parent = Group(c, "Pancartas");
        int sponsor = 0;
        // Saltos: a los dos lados, mirando al que viene.
        foreach (TrailTerrainCarver.Placed f in c.Carved.Features)
        {
            if (f.Type == Feature.RockGarden || f.Type == Feature.Roots) continue;
            Line line = c.Lines[f.Line];
            int i = Mathf.Max(1, f.Lip - Idx(7f));
            for (int side = -1; side <= 1; side += 2)
            {
                TryBanner(c, parent, line, i, side, line.HalfWidth + 2.4f, 0.55f, sponsor++);
            }
        }
        // Curvas cerradas: por fuera, en el vértice.
        foreach (Line line in c.Lines)
        {
            if (!IsDressable(line)) continue;
            int n = line.Points.Length, lastCurve = -1000, lastStraight = 0;
            for (int i = Idx(10f); i < n - Idx(10f); i++)
            {
                float b = line.Bank[i];
                bool apex = Mathf.Abs(b) > 0.28f && Mathf.Abs(b) >= Mathf.Abs(line.Bank[i - 1]) && Mathf.Abs(b) >= Mathf.Abs(line.Bank[i + 1]);
                if (apex && i - lastCurve > Idx(25f))
                {
                    // Peralte negativo = sube el lado derecho = la curva es a la izquierda: afuera es la derecha.
                    int outer = b < 0f ? -1 : 1;
                    if (TryBanner(c, parent, line, i, outer, line.HalfWidth + 2.6f, 0f, sponsor++)) lastCurve = i;
                }
                if (i - lastStraight > Idx(95f) && Mathf.Abs(b) < 0.05f)
                {
                    if (TryBanner(c, parent, line, i, (i / 7) % 2 == 0 ? 1 : -1, line.HalfWidth + 2.2f, 0.25f, sponsor++)) lastStraight = i;
                }
            }
        }
    }

    /// <summary>Pancarta al costado del punto i, girada hacia el sendero ("toward" > 0 = mira además hacia el que viene).</summary>
    private static bool TryBanner(Ctx c, Transform parent, Line line, int i, int side, float lateral, float toward, int sponsor)
    {
        Vector3 p = Beside(c, line, i, side * lateral);
        if (EdgeAt(c, p) < lateral - line.HalfWidth - 0.5f || Steep(c, p) > 32f) return false;
        Vector2 t = TrailTerrainCarver.Tangent(line.Points, i);
        Vector3 forward = new Vector3(t.x, 0f, t.y);
        Vector3 inward = (Center(c, line, i) - p);
        inward.y = 0f;
        Vector3 face = (inward.normalized - forward * toward).normalized;
        Place("Pancarta", parent, c.Banner, p - Vector3.up * 0.1f, Quaternion.LookRotation(-face), Vector3.one,
            c.SponsorMats[sponsor % c.SponsorMats.Length], c.White);
        return true;
    }

    // =====================================================================
    // Saltos: madera, rocas, raíces, cerca
    // =====================================================================

    private static void FeatureDressing(Ctx c)
    {
        Transform parent = Group(c, "Saltos");
        foreach (TrailTerrainCarver.Placed f in c.Carved.Features)
        {
            Line line = c.Lines[f.Line];
            switch (f.Type)
            {
                case Feature.Kicker: WoodDeck(c, parent, "Rampa de madera", line, f.Lip - Idx(4.5f), f.Lip, line.HalfWidth * 0.8f, false); break;
                case Feature.BigJump: WoodDeck(c, parent, "Salto final", line, f.Lip - Idx(8.5f), f.Lip, line.HalfWidth * 0.9f, false); break;
                case Feature.RoadGap:
                    WoodDeck(c, parent, "Rampa del atajo", line, f.Lip - Idx(5.5f), f.Lip, line.HalfWidth * 0.9f, false);
                    Sign(c, parent, line, Mathf.Max(1, f.Start), "ATAJO", new Color(0.95f, 0.45f, 0.05f));
                    break;
                case Feature.FenceGap:
                    WoodDeck(c, parent, "Rampa de la cerca", line, f.Lip - Idx(5.5f), f.Lip, line.HalfWidth * 0.85f, false);
                    StoneFence(c, parent, line, Mathf.Min(line.Points.Length - 1, f.Lip + Idx(f.Extra)));
                    break;
                case Feature.Mirador:
                    WoodDeck(c, parent, "Mirador", line, f.Lip - Idx(12f), f.Lip, line.HalfWidth + 0.4f, true);
                    Sign(c, parent, line, Mathf.Max(1, f.Lip - Idx(9f)), "MIRADOR", new Color(0.1f, 0.35f, 0.8f));
                    break;
                case Feature.RockDrop: RockDrop(c, parent, line, f.Lip); break;
                case Feature.RockGarden: RockGarden(c, parent, line, f.Start, f.End); break;
                case Feature.Roots: Roots(c, parent, line, f.Start, f.End); break;
            }
        }
    }

    /// <summary>
    /// Tablas sobre la rampa (siguen la forma tallada, 6 cm arriba), con faldones de madera a los lados y al frente.
    /// El mirador además lleva barandas, pilares bajo el borde y un cartel.
    /// </summary>
    private static void WoodDeck(Ctx c, Transform parent, string name, Line line, int from, int to, float halfWidth, bool mirador)
    {
        int n = line.Points.Length;
        from = Mathf.Clamp(from, 0, n - 2);
        to = Mathf.Clamp(to, from + 1, n - 1);
        const int across = 5;
        MeshBuilderLite top = new MeshBuilderLite(1);
        int rows = to - from + 1;
        Vector3[,] grid = new Vector3[rows, across];
        for (int r = 0; r < rows; r++)
        {
            for (int a = 0; a < across; a++)
            {
                float lateral = Mathf.Lerp(-halfWidth, halfWidth, a / (float)(across - 1));
                grid[r, a] = Beside(c, line, from + r, lateral) + Vector3.up * 0.06f;
            }
        }
        for (int r = 0; r < rows - 1; r++)
        {
            for (int a = 0; a < across - 1; a++)
            {
                top.Quad(0, grid[r, a], grid[r + 1, a], grid[r + 1, a + 1], grid[r, a + 1],
                    new Vector2(a * halfWidth * 0.5f, r * Sp * 0.8f), new Vector2(a * halfWidth * 0.5f, (r + 1) * Sp * 0.8f),
                    new Vector2((a + 1) * halfWidth * 0.5f, (r + 1) * Sp * 0.8f), new Vector2((a + 1) * halfWidth * 0.5f, r * Sp * 0.8f), Vector3.up);
            }
        }
        // Faldones: de las tablas hasta el suelo, a los lados y bajo el borde de la rampa.
        MeshBuilderLite skirt = new MeshBuilderLite(1);
        for (int r = 0; r < rows - 1; r++)
        {
            for (int s = 0; s < 2; s++)
            {
                int a = s == 0 ? 0 : across - 1;
                Vector3 p0 = grid[r, a], p1 = grid[r + 1, a];
                Vector3 g0 = Ground(c, p0) - Vector3.up * 0.3f, g1 = Ground(c, p1) - Vector3.up * 0.3f;
                Vector3 outward = p0 - grid[r, across / 2];
                outward.y = 0f;
                skirt.Quad(0, g0, p0, p1, g1, Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), outward);
            }
        }
        int last = rows - 1;
        Vector3 ahead = Beside(c, line, Mathf.Min(n - 1, to + Idx(0.8f)), 0f);
        float frontBottom = Mathf.Min(ahead.y, grid[last, across / 2].y) - 0.3f;
        for (int a = 0; a < across - 1; a++)
        {
            Vector3 p0 = grid[last, a], p1 = grid[last, a + 1];
            Vector3 front = ahead - grid[last, across / 2];
            front.y = 0f;
            skirt.Quad(0, new Vector3(p0.x, frontBottom, p0.z), p0, p1, new Vector3(p1.x, frontBottom, p1.z),
                Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), front);
        }
        Mesh topMesh = SaveMesh(c, top.ToMesh(name), name.Replace(" ", "") + c.MeshCount);
        GameObject deck = Place(name, parent, topMesh, Vector3.zero, Quaternion.identity, Vector3.one, c.Wood);
        deck.AddComponent<MeshCollider>().sharedMesh = topMesh;
        Mesh skirtMesh = SaveMesh(c, skirt.ToMesh(name + " faldon"), name.Replace(" ", "") + "Faldon" + c.MeshCount);
        Place(name + " (faldón)", deck.transform, skirtMesh, Vector3.zero, Quaternion.identity, Vector3.one, c.Wood);

        if (!mirador) return;
        // Barandas a los lados (sin choque: solo marcan), pilares bajo el borde y una bandera.
        for (int r = 0; r < rows - 1; r += 3)
        {
            for (int s = 0; s < 2; s++)
            {
                Vector3 p = grid[r, s == 0 ? 0 : across - 1];
                Place("Baranda", deck.transform, c.Cube, p + Vector3.up * 0.55f, Quaternion.identity, new Vector3(0.1f, 1.1f, 0.1f), c.Wood);
                if (r + 3 < rows)
                {
                    Vector3 q = grid[r + 3, s == 0 ? 0 : across - 1];
                    Vector3 mid = (p + q) * 0.5f + Vector3.up * 1.05f;
                    Place("Pasamanos", deck.transform, c.Cube, mid, Quaternion.LookRotation(q - p), new Vector3(0.08f, 0.08f, Vector3.Distance(p, q)), c.Wood);
                }
            }
        }
        for (int a = 0; a < across; a += 2)
        {
            Vector3 p = grid[last, a];
            float bottom = frontBottom - 0.5f;
            Place("Pilar", deck.transform, c.Cube, new Vector3(p.x, (p.y + bottom) * 0.5f, p.z), Quaternion.identity,
                new Vector3(0.22f, Mathf.Max(0.3f, p.y - bottom), 0.22f), c.Wood);
        }
    }

    /// <summary>Cartel grande al costado (ATAJO, MIRADOR).</summary>
    private static void Sign(Ctx c, Transform parent, Line line, int i, string text, Color color)
    {
        Texture2D texture = VisualAssetFactory.SaveTexture(ProceduralTextures.Banner(1024, 256, text, new ProceduralTextures.Rgb(color.r, color.g, color.b), false),
            "Cartel" + text, VisualAssetFactory.TextureKind.Color, 1024);
        Material material = VisualAssetFactory.Lit("Cartel" + text, Color.white, texture, null, 0.2f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 p = Beside(c, line, i, side * (line.HalfWidth + 2f));
            if (EdgeAt(c, p) < 1.2f) continue;
            Vector3 inward = Center(c, line, i) - p;
            inward.y = 0f;
            Vector2 t = TrailTerrainCarver.Tangent(line.Points, i);
            Vector3 face = (inward.normalized - new Vector3(t.x, 0f, t.y) * 0.8f).normalized;
            Place("Cartel " + text, parent, c.Banner, p, Quaternion.LookRotation(-face), new Vector3(1.4f, 1.4f, 1f), material, c.White);
            return;
        }
    }

    /// <summary>Cerca de piedra cruzando el sendero (se salta con la rampa); deja un paso angosto a un lado para el que va lento.</summary>
    private static void StoneFence(Ctx c, Transform parent, Line line, int i)
    {
        Vector2 t = TrailTerrainCarver.Tangent(line.Points, i);
        Vector2 across = new Vector2(-t.y, t.x);
        float hw = line.HalfWidth;
        MeshBuilderLite wall = new MeshBuilderLite(1);
        const float step = 0.8f, reach = 26f;
        for (float lateral = -hw - reach; lateral < hw + reach; lateral += step)
        {
            // Paso angosto pegado a la orilla derecha.
            if (lateral > -hw - 0.4f && lateral < -hw + 1.4f) continue;
            Vector2 a2 = line.Points[i] + across * lateral, b2 = line.Points[i] + across * (lateral + step);
            Vector3 a = Ground(c, c.Origin + new Vector3(a2.x, 0f, a2.y)), b = Ground(c, c.Origin + new Vector3(b2.x, 0f, b2.y));
            // Fuera del sendero se corta si llega a otro sendero o camino.
            if (Mathf.Abs(lateral) > hw + 1f && (EdgeAt(c, a) < 1.2f || EdgeAt(c, b) < 1.2f)) continue;
            float height = 0.95f + (float)c.Rng.NextDouble() * 0.2f;
            Vector3 dir = (b - a);
            dir.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * 0.32f;
            Vector3 a0 = a - Vector3.up * 0.4f, b0 = b - Vector3.up * 0.4f;
            Vector3 a1 = a + Vector3.up * height, b1 = b + Vector3.up * (height + (float)c.Rng.NextDouble() * 0.1f);
            wall.Box(0, a0 - side, a0 + side, b0 + side, b0 - side, a1 - side, a1 + side, b1 + side, b1 - side);
        }
        Mesh mesh = SaveMesh(c, wall.ToMesh("Cerca de piedra"), "CercaPiedra" + c.MeshCount);
        GameObject go = Place("Cerca de piedra", parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, c.Rock);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static void RockDrop(Ctx c, Transform parent, Line line, int lip)
    {
        if (c.Rocks.Count == 0) return;
        float hw = line.HalfWidth;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int k = 0; k < 2; k++)
            {
                Vector3 p = Beside(c, line, Mathf.Max(0, lip - k * 2), side * (hw + 0.5f + (float)c.Rng.NextDouble()));
                Rock(c, parent, p, 0.9f + (float)c.Rng.NextDouble() * 0.6f, true);
            }
        }
        // Lajas en la cara del drop (planas, sin choque): se ve el escalón de roca.
        for (int k = 0; k < 5; k++)
        {
            int i = Mathf.Min(line.Points.Length - 1, lip + 1 + c.Rng.Next(3));
            Vector3 p = Beside(c, line, i, Range(c, -hw * 0.8f, hw * 0.8f));
            Rock(c, parent, p - Vector3.up * 0.25f, 0.5f, false, 0.35f);
        }
    }

    private static void RockGarden(Ctx c, Transform parent, Line line, int from, int to)
    {
        if (c.Rocks.Count == 0) return;
        float hw = line.HalfWidth;
        // Piedras sueltas al 40 % de antes (una cada ~4 m): que no se llene el sendero de piedras.
        for (int i = from; i <= to; i += 5)
        {
            Vector3 inside = Beside(c, line, i, Range(c, -hw + 0.3f, hw - 0.3f));
            Rock(c, parent, inside - Vector3.up * 0.12f, Range(c, 0.22f, 0.42f), false, 0.55f);
            if (i % 15 == 0)
            {
                Vector3 edge = Beside(c, line, i, (c.Rng.NextDouble() < 0.5 ? -1 : 1) * (hw + Range(c, 0.2f, 1f)));
                Rock(c, parent, edge, Range(c, 0.6f, 1.1f), true);
            }
        }
    }

    private static void Roots(Ctx c, Transform parent, Line line, int from, int to)
    {
        float hw = line.HalfWidth;
        for (int i = from; i <= to; i += 2 + c.Rng.Next(2))
        {
            Vector3 center = Beside(c, line, i, Range(c, -0.5f, 0.5f));
            Vector2 t = TrailTerrainCarver.Tangent(line.Points, i);
            Quaternion along = Quaternion.LookRotation(new Vector3(t.x, 0f, t.y));
            Quaternion rotation = along * Quaternion.Euler(0f, Range(c, -35f, 35f), 90f);
            Place("Raiz", parent, c.Capsule, center - Vector3.up * 0.05f, rotation, new Vector3(0.14f, hw + 0.8f, 0.14f), c.Bark);
        }
    }

    // =====================================================================
    // Puente de madera
    // =====================================================================

    /// <summary>Tablero de madera sobre la ruta de abajo, con barandas (chocar contra ellas es caída) y pilares en los extremos.</summary>
    private static void Bridges(Ctx c)
    {
        Transform parent = Group(c, "Puente");
        foreach (TrailTerrainCarver.Crossing crossing in c.Carved.Crossings)
        {
            if (!crossing.Bridge) continue;
            Line upper = c.Lines[crossing.Upper];
            int n = upper.Points.Length;
            int from = Mathf.Max(0, crossing.UpperFrom - Idx(2f)), to = Mathf.Min(n - 1, crossing.UpperTo + Idx(2f));
            float half = upper.HalfWidth + 0.35f;
            MeshBuilderLite deck = new MeshBuilderLite(1), rails = new MeshBuilderLite(1);
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            float along = 0f;
            for (int i = from; i <= to; i++)
            {
                Vector2 t = TrailTerrainCarver.Tangent(upper.Points, i);
                Vector3 right = new Vector3(t.y, 0f, -t.x) * half;
                // Altura del tablero: el perfil tallado de la ruta de arriba (en el centro va 0,3 m más hondo por el surco).
                Vector3 center = c.Origin + new Vector3(upper.Points[i].x, upper.Heights[i] + c.HeightOffset - 0.22f, upper.Points[i].y);
                Vector3 l = center - right, r = center + right;
                if (i > from)
                {
                    along += Vector3.Distance(center, (prevL + prevR) * 0.5f);
                    deck.Quad(0, prevL, l, r, prevR, new Vector2(0f, along - Sp), new Vector2(0f, along), new Vector2(half, along), new Vector2(half, along - Sp), Vector3.up);
                    Vector3 down = Vector3.up * 0.35f;
                    deck.Quad(0, prevR - down, r - down, l - down, prevL - down, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.down);
                    deck.Quad(0, prevL - down, l - down, l, prevL, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, -right);
                    deck.Quad(0, r - down, prevR - down, prevR, r, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, right);
                    for (int s = 0; s < 2; s++)
                    {
                        Vector3 a = s == 0 ? prevL : prevR, b = s == 0 ? l : r;
                        foreach (float h in new[] { 0.55f, 1.05f }) Beam(rails, a + Vector3.up * h, b + Vector3.up * h, 0.06f);
                    }
                }
                if ((i - from) % 2 == 0)
                {
                    Beam(rails, l, l + Vector3.up * 1.12f, 0.07f);
                    Beam(rails, r, r + Vector3.up * 1.12f, 0.07f);
                }
                prevL = l;
                prevR = r;
            }
            Mesh deckMesh = SaveMesh(c, deck.ToMesh("Puente"), "Puente" + c.MeshCount);
            GameObject bridge = Place("Puente de madera (" + upper.Name + " sobre " + c.Lines[crossing.Lower].Name + ")", parent, deckMesh,
                Vector3.zero, Quaternion.identity, Vector3.one, c.Wood);
            bridge.AddComponent<MeshCollider>().sharedMesh = deckMesh;
            Mesh railMesh = SaveMesh(c, rails.ToMesh("Barandas"), "PuenteBarandas" + c.MeshCount);
            GameObject railGo = Place("Barandas", bridge.transform, railMesh, Vector3.zero, Quaternion.identity, Vector3.one, c.Wood);
            railGo.AddComponent<MeshCollider>().sharedMesh = railMesh;
            // Pilares en los extremos del tramo libre, del tablero al suelo.
            foreach (int i in new[] { crossing.UpperFrom + Idx(1f), crossing.UpperTo - Idx(1f) })
            {
                Vector2 t = TrailTerrainCarver.Tangent(upper.Points, i);
                Vector3 right = new Vector3(t.y, 0f, -t.x) * (half - 0.2f);
                Vector3 center = c.Origin + new Vector3(upper.Points[i].x, upper.Heights[i] + c.HeightOffset - 0.6f, upper.Points[i].y);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 topP = center + right * s;
                    // Ningún pilar dentro de la ruta de abajo.
                    if (DistanceToLine(c.Lines[crossing.Lower], topP - c.Origin) < c.Lines[crossing.Lower].HalfWidth + 2f) continue;
                    float ground = Ground(c, topP).y - 1f;
                    float height = Mathf.Max(0.5f, topP.y - ground);
                    Place("Pilar", bridge.transform, c.Cube, new Vector3(topP.x, ground + height * 0.5f, topP.z), Quaternion.LookRotation(new Vector3(t.x, 0f, t.y)),
                        new Vector3(0.35f, height, 0.35f), c.Wood);
                }
            }
        }
    }

    // =====================================================================
    // Bosque cerrado, arcilla, público, arcos, casa, ciudad
    // =====================================================================

    /// <summary>Árboles pegados al sendero en los tramos de bosque: hay que ir fino (chocar con un tronco es caída).</summary>
    private static void ForestHazard(Ctx c)
    {
        TerrainData data = c.Terrain.terrainData;
        List<int> kinds = new List<int>();
        for (int k = 0; k < data.treePrototypes.Length; k++)
        {
            GameObject prefab = data.treePrototypes[k].prefab;
            if (prefab != null && (prefab.name.StartsWith("Pino") || prefab.name.StartsWith("Arbol"))) kinds.Add(k);
        }
        if (kinds.Count == 0) return;
        List<TreeInstance> trees = new List<TreeInstance>(data.treeInstances);
        int added = 0;
        foreach (Line line in c.Lines)
        {
            if (!IsDressable(line) || !line.Source.Forest) continue;
            float hw = line.HalfWidth;
            for (int i = Idx(12f); i < line.Points.Length - Idx(12f); i += 3)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float near = Range(c, 0.9f, 3.2f), far = Range(c, 4f, 9f);
                    foreach (float lateral in new[] { near, far })
                    {
                        if (c.Rng.NextDouble() > (lateral < 3.5f ? 0.55 : 0.45)) continue;
                        Vector3 p = Beside(c, line, i, side * (hw + lateral));
                        if (EdgeAt(c, p) < lateral - 0.3f || Steep(c, p) > 40f) continue;
                        Vector3 local = p - c.Origin;
                        float scale = Range(c, 0.85f, 1.3f);
                        trees.Add(new TreeInstance
                        {
                            prototypeIndex = kinds[c.Rng.Next(kinds.Count)],
                            position = new Vector3(local.x / data.size.x, 0f, local.z / data.size.z),
                            widthScale = scale,
                            heightScale = scale * Range(c, 0.95f, 1.15f),
                            rotation = Range(c, 0f, Mathf.PI * 2f),
                            color = Color.white,
                            lightmapColor = Color.white
                        });
                        added++;
                    }
                }
            }
        }
        data.SetTreeInstances(trees.ToArray(), true);
        Debug.Log("DownhillRace: bosque cerrado con " + added + " árboles junto al sendero.");
    }

    /// <summary>Zonas resbalosas en las curvas de arcilla (tramos de 6 m, sin encimarse).</summary>
    private static void ClayZones(Ctx c)
    {
        Transform parent = Group(c, "Arcilla");
        foreach (Vector3Int range in c.Carved.Clay)
        {
            Line line = c.Lines[range.x];
            int piece = Idx(6f);
            for (int i = range.y; i + 1 < range.z; i += piece)
            {
                int j = Mathf.Min(range.z, i + piece);
                Vector3 a = Center(c, line, i), b = Center(c, line, j);
                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) continue;
                GameObject go = new GameObject("Curva de arcilla");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(dir));
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(line.HalfWidth * 2f + 1.5f, 4f, dir.magnitude);
                box.center = new Vector3(0f, 1f, 0f);
                go.AddComponent<GripZone>().Configure(0.38f);
            }
        }
    }

    /// <summary>Recta final: vallas con pancartas a los dos lados y público detrás (gente simple de colores).</summary>
    private static void Crowd(Ctx c)
    {
        Transform parent = Group(c, "Publico");
        Material[] shirts =
        {
            VisualAssetFactory.Lit("PublicoRojo", new Color(0.85f, 0.15f, 0.12f), null, null, 0.2f),
            VisualAssetFactory.Lit("PublicoAzul", new Color(0.15f, 0.3f, 0.8f), null, null, 0.2f),
            VisualAssetFactory.Lit("PublicoAmarillo", new Color(0.95f, 0.8f, 0.15f), null, null, 0.2f),
            VisualAssetFactory.Lit("PublicoVerde", new Color(0.15f, 0.6f, 0.3f), null, null, 0.2f),
            VisualAssetFactory.Lit("PublicoBlanco", new Color(0.9f, 0.9f, 0.9f), null, null, 0.2f),
            VisualAssetFactory.Lit("PublicoNegro", new Color(0.12f, 0.12f, 0.14f), null, null, 0.2f),
        };
        List<CombineInstance>[] bodies = new List<CombineInstance>[shirts.Length];
        for (int k = 0; k < bodies.Length; k++) bodies[k] = new List<CombineInstance>();
        List<CombineInstance> heads = new List<CombineInstance>();
        int sponsor = 0;
        foreach (Line line in c.Lines)
        {
            if (!IsDressable(line) || !line.Source.Crowd) continue;
            float hw = line.HalfWidth;
            int n = line.Points.Length;
            for (int i = Idx(6f); i < n - Idx(4f); i += 4)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    // Valla de pancartas.
                    if (i % 8 == 0) TryBanner(c, parent, line, i, side, hw + 1.7f, 0f, sponsor++);
                    // Dos o tres filas de gente detrás de la valla.
                    for (int row = 0; row < 3; row++)
                    {
                        if (c.Rng.NextDouble() < 0.25) continue;
                        float lateral = hw + 2.8f + row * 1.1f + Range(c, -0.2f, 0.2f);
                        Vector3 p = Beside(c, line, i, side * lateral) + new Vector3(Range(c, -0.4f, 0.4f), 0f, Range(c, -0.4f, 0.4f));
                        if (EdgeAt(c, p) < lateral - hw - 0.5f || Steep(c, p) > 35f) continue;
                        float height = Range(c, 0.9f, 1.1f);
                        Matrix4x4 body = Matrix4x4.TRS(p + Vector3.up * 0.82f * height, Quaternion.identity, new Vector3(0.48f, 0.82f * height, 0.4f));
                        Matrix4x4 head = Matrix4x4.TRS(p + Vector3.up * 1.78f * height, Quaternion.identity, Vector3.one * 0.27f);
                        bodies[c.Rng.Next(bodies.Length)].Add(new CombineInstance { mesh = c.Capsule, transform = body });
                        heads.Add(new CombineInstance { mesh = c.Sphere, transform = head });
                    }
                }
            }
        }
        for (int k = 0; k < bodies.Length; k++) CombineInto(c, parent, "Publico " + shirts[k].name, bodies[k], shirts[k]);
        CombineInto(c, parent, "Publico (cabezas)", heads, c.Skin);
    }

    /// <summary>Arco con pancarta sobre cada checkpoint (sin choque).</summary>
    private static void Gates(Ctx c)
    {
        GameObject group = GameObject.Find("Checkpoints");
        if (group == null) return;
        Transform parent = Group(c, "Arcos");
        int sponsor = 0;
        foreach (TrackTrigger trigger in group.GetComponentsInChildren<TrackTrigger>())
        {
            if (trigger.Type != TrackTrigger.TriggerType.Checkpoint) continue;
            Vector3 p = trigger.transform.position;
            float hw = NearestHalfWidth(c, p);
            float half = hw + 1.3f;
            Quaternion rotation = trigger.transform.rotation;
            Vector3 right = rotation * Vector3.right;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 foot = Ground(c, p + right * half * s);
                Place("Poste", parent, c.Cube, foot + Vector3.up * 2.2f, rotation, new Vector3(0.18f, 4.6f, 0.18f), c.White);
            }
            Vector3 topCenter = Ground(c, p) + Vector3.up * 3.6f;
            Place("Arco checkpoint", parent, c.Banner, topCenter - Vector3.up * 0.3f, rotation,
                new Vector3(half * 2f / 2.8f, 1.1f, 1f), c.SponsorMats[sponsor++ % c.SponsorMats.Length], c.White);
        }
    }

    /// <summary>Casa de campo al final del camino de tierra, con unos arbustos.</summary>
    private static void Farms(Ctx c)
    {
        if (c.Set.Farms == null || c.Houses.Count == 0) return;
        Transform parent = Group(c, "Casa de campo");
        TerrainData data = c.Terrain.terrainData;
        Material wall = LoadMaterial("CasaPared"), roof = LoadMaterial("CasaTeja"), window = LoadMaterial("CasaVentana");
        Material leaves = LoadMaterial("Hojas");
        foreach (Vector2 farm in c.Set.Farms)
        {
            Vector2 local = TrailTerrainCarver.ImageToLocal(farm, new Vector2(0f, 1f), new Vector2(0f, 1f), data.size.x, data.size.z);
            Vector3 p = Ground(c, c.Origin + new Vector3(local.x + 9f, 0f, local.y + 4f));
            Place("Casa", parent, c.Houses[0], p - Vector3.up * 0.2f, Quaternion.Euler(0f, 250f, 0f), Vector3.one, wall, roof, window);
            for (int k = 0; k < 5 && c.Bushes.Count > 0; k++)
            {
                Vector3 b = Ground(c, p + new Vector3(Range(c, -12f, 12f), 0f, Range(c, -12f, 12f)));
                if (EdgeAt(c, b) < 1.5f) continue;
                Place("Arbusto", parent, c.Bushes[c.Rng.Next(c.Bushes.Count)], b, Quaternion.Euler(0f, Range(c, 0f, 360f), 0f), Vector3.one * Range(c, 1f, 1.6f), c.Bark, leaves);
            }
        }
    }

    /// <summary>
    /// La ciudad allá abajo, al norte (fuera del mapa): manzanas de casas blancas con techos de teja,
    /// más ralas lejos del centro. Se ve desde lo alto del cerro, entre la neblina.
    /// </summary>
    private static void City(Ctx c)
    {
        if (c.Set.ViewDistance <= 0f) return;
        TerrainData data = c.Terrain.terrainData;
        float edgeMin = float.MaxValue;
        for (int k = 0; k <= 40; k++)
        {
            Vector3 p = c.Origin + new Vector3(data.size.x * k / 40f, 0f, data.size.z - 1f);
            edgeMin = Mathf.Min(edgeMin, c.Terrain.SampleHeight(p) + c.Origin.y);
        }
        float baseY = edgeMin - 25f;
        float x0 = c.Origin.x - 900f, x1 = c.Origin.x + data.size.x + 900f;
        float z0 = c.Origin.z + data.size.z + 60f, z1 = z0 + 2400f;
        Transform parent = Group(c, "Ciudad");
        // Suelo de la ciudad (debajo del borde norte del mapa).
        MeshBuilderLite ground = new MeshBuilderLite(1);
        ground.Quad(0, new Vector3(x0 - 2000f, baseY, z0 - 120f), new Vector3(x0 - 2000f, baseY, z1 + 2000f), new Vector3(x1 + 2000f, baseY, z1 + 2000f),
            new Vector3(x1 + 2000f, baseY, z0 - 120f), Vector2.zero, new Vector2(0f, 400f), new Vector2(400f, 400f), new Vector2(400f, 0f), Vector3.up);
        Material valley = LoadMaterial("Valle") ?? LoadMaterial("Pasto");
        Place("Suelo de la ciudad", parent, SaveMesh(c, ground.ToMesh("Suelo ciudad"), "CiudadSuelo"), Vector3.zero, Quaternion.identity, Vector3.one, valley);

        MeshBuilderLite city = new MeshBuilderLite(2);
        const float cell = 34f;
        float cx = (x0 + x1) * 0.5f;
        for (float z = z0 + 40f; z < z1; z += cell)
        {
            for (float x = x0; x < x1; x += cell)
            {
                float far = Mathf.Abs(x - cx) / (x1 - x0) + (z - z0) / (z1 - z0) * 0.5f;
                if (c.Rng.NextDouble() < 0.15 + far * 0.6) continue;   // calles, parques y afueras
                int count = 1 + c.Rng.Next(3);
                for (int b = 0; b < count; b++)
                {
                    float w = Range(c, 9f, 16f), d = Range(c, 9f, 16f);
                    float h = Range(c, 6f, 14f) * (c.Rng.NextDouble() < 0.06 ? 3f : 1f);
                    Vector3 o = new Vector3(x + Range(c, 2f, cell - w - 2f), baseY, z + Range(c, 2f, cell - d - 2f));
                    BuildingBox(city, o, w, d, h);
                }
            }
        }
        Mesh mesh = city.ToMesh("Ciudad");
        mesh.indexFormat = IndexFormat.UInt32;
        mesh = SaveMesh(c, mesh, "Ciudad");
        GameObject go = Place("Ciudad", parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, LoadMaterial("CasaPared"), LoadMaterial("CasaTeja"));
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static void BuildingBox(MeshBuilderLite m, Vector3 o, float w, float d, float h)
    {
        Vector3 a = o, b = o + new Vector3(w, 0f, 0f), cc = o + new Vector3(w, 0f, d), dd = o + new Vector3(0f, 0f, d);
        Vector3 up = Vector3.up * h;
        Vector3[] floor = { a, b, cc, dd };
        Vector3 center = (a + cc) * 0.5f;
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = floor[k], q = floor[(k + 1) % 4];
            m.Quad(0, p, p + up, q + up, q, Vector2.zero, new Vector2(0f, h / 3f), new Vector2(w / 3f, h / 3f), new Vector2(w / 3f, 0f), (p + q) * 0.5f - center);
        }
        m.Quad(1, a + up, dd + up, cc + up, b + up, Vector2.zero, new Vector2(0f, d / 4f), new Vector2(w / 4f, d / 4f), new Vector2(w / 4f, 0f), Vector3.up);
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    private static bool IsDressable(Line line)
    {
        return line.Source != null && !line.Locked && !line.IsRoad && line.Sink <= 0f && line.Bank != null;
    }

    private static void LoadKit(Ctx c)
    {
        c.Wood = LoadMaterial("Rampa");
        c.White = LoadMaterial("Blanco");
        c.Rock = LoadMaterial("Roca");
        c.Bark = LoadMaterial("Tronco");
        c.Skin = LoadMaterial("CorredorPiel") ?? c.White;
        c.Tape = RealMapDecor.CourseTapeMaterial();
        c.SponsorMats = new Material[Sponsors.Length];
        for (int k = 0; k < Sponsors.Length; k++)
        {
            Color col = SponsorColors[k];
            Texture2D texture = VisualAssetFactory.SaveTexture(ProceduralTextures.Banner(1024, 256, Sponsors[k], new ProceduralTextures.Rgb(col.r, col.g, col.b), false),
                "Pancarta" + (k + 1), VisualAssetFactory.TextureKind.Color, 1024);
            c.SponsorMats[k] = VisualAssetFactory.Lit("Pancarta" + (k + 1), Color.white, texture, null, 0.2f);
        }
        c.Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        c.Capsule = Resources.GetBuiltinResource<Mesh>("Capsule.fbx");
        c.Sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        c.Banner = BannerMesh(c);
        for (int k = 1; k <= 4; k++) AddMesh(c.Rocks, "Roca" + k);
        for (int k = 1; k <= 3; k++) AddMesh(c.Houses, "Casa" + k);
        for (int k = 1; k <= 3; k++) AddMesh(c.Bushes, "Arbusto" + k);
    }

    /// <summary>Pancarta de 2,8 × 0,85 m con dos postes; se ve de los dos lados (el texto se lee bien de frente).</summary>
    private static Mesh BannerMesh(Ctx c)
    {
        MeshBuilderLite m = new MeshBuilderLite(2);
        const float w = 1.4f, bottom = 0.3f, top = 1.15f;
        // Frente (mira hacia -Z) y espalda.
        m.Quad(0, new Vector3(-w, bottom, 0f), new Vector3(-w, top, 0f), new Vector3(w, top, 0f), new Vector3(w, bottom, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.back);
        m.Quad(0, new Vector3(w, bottom, 0.02f), new Vector3(w, top, 0.02f), new Vector3(-w, top, 0.02f), new Vector3(-w, bottom, 0.02f),
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.forward);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 x = new Vector3(s * (w + 0.03f), 0f, 0.01f);
            Beam(m, x - Vector3.up * 0.3f, x + Vector3.up * (top + 0.05f), 0.04f, 1);
        }
        return SaveMesh(c, m.ToMesh("Pancarta"), "Pancarta");
    }

    private static void Beam(MeshBuilderLite m, Vector3 a, Vector3 b, float r, int submesh = 0)
    {
        Vector3 dir = (b - a).normalized;
        Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized * r;
        Vector3 up = Vector3.Cross(side, dir).normalized * r;
        m.Box(submesh, a - side - up, a + side - up, b + side - up, b - side - up, a - side + up, a + side + up, b + side + up, b - side + up);
    }

    private static void CombineInto(Ctx c, Transform parent, string name, List<CombineInstance> parts, Material material)
    {
        if (parts.Count == 0) return;
        Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        mesh = SaveMesh(c, mesh, name.Replace(" ", "").Replace("(", "").Replace(")", ""));
        Place(name, parent, mesh, Vector3.zero, Quaternion.identity, Vector3.one, material);
    }

    private static void Rock(Ctx c, Transform parent, Vector3 position, float size, bool collider, float flat = 1f)
    {
        Mesh mesh = c.Rocks[c.Rng.Next(c.Rocks.Count)];
        Vector3 scale = new Vector3(size * Range(c, 0.9f, 1.4f), size * Range(c, 0.6f, 1f) * flat, size * Range(c, 0.9f, 1.3f));
        GameObject rock = Place("Roca", parent, mesh, position - Vector3.up * scale.y * 0.2f, Quaternion.Euler(Range(c, -8f, 8f), Range(c, 0f, 360f), Range(c, -8f, 8f)),
            scale, c.Rock);
        if (!collider) return;
        MeshCollider mc = rock.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = true;
    }

    private static float DistanceToLine(Line line, Vector3 local)
    {
        float best = float.MaxValue;
        Vector2 p = new Vector2(local.x, local.z);
        foreach (Vector2 q in line.Points) best = Mathf.Min(best, (q - p).sqrMagnitude);
        return Mathf.Sqrt(best);
    }

    private static float NearestHalfWidth(Ctx c, Vector3 world)
    {
        float best = float.MaxValue, half = 3f;
        Vector2 p = new Vector2(world.x - c.Origin.x, world.z - c.Origin.z);
        foreach (Line line in c.Lines)
        {
            if (line.Sink > 0f || line.IsRoad) continue;
            for (int i = 0; i < line.Points.Length; i += 4)
            {
                float d = (line.Points[i] - p).sqrMagnitude;
                if (d < best) { best = d; half = line.HalfWidth; }
            }
        }
        return half;
    }

    /// <summary>Punto al costado del sendero ("lateral" metros, + = izquierda), apoyado en el terreno.</summary>
    private static Vector3 Beside(Ctx c, Line line, int i, float lateral)
    {
        i = Mathf.Clamp(i, 0, line.Points.Length - 1);
        Vector2 t = TrailTerrainCarver.Tangent(line.Points, i);
        Vector2 p = line.Points[i] + new Vector2(-t.y, t.x) * lateral;
        return Ground(c, c.Origin + new Vector3(p.x, 0f, p.y));
    }

    private static Vector3 Center(Ctx c, Line line, int i) => Beside(c, line, i, 0f);

    private static Vector3 Ground(Ctx c, Vector3 p)
    {
        p.y = c.Terrain.SampleHeight(p) + c.Origin.y;
        return p;
    }

    private static float EdgeAt(Ctx c, Vector3 world)
    {
        TerrainData data = c.Terrain.terrainData;
        int res = c.Edge.GetLength(0);
        // Interpolado: la cuadrícula es de 86 cm y los postes van a 80 cm de la orilla.
        float fx = Mathf.Clamp01((world.x - c.Origin.x) / data.size.x) * (res - 1), fz = Mathf.Clamp01((world.z - c.Origin.z) / data.size.z) * (res - 1);
        int x0 = Mathf.Min(Mathf.FloorToInt(fx), res - 2), z0 = Mathf.Min(Mathf.FloorToInt(fz), res - 2);
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(Far(c.Edge[z0, x0]), Far(c.Edge[z0, x0 + 1]), tx);
        float b = Mathf.Lerp(Far(c.Edge[z0 + 1, x0]), Far(c.Edge[z0 + 1, x0 + 1]), tx);
        return Mathf.Lerp(a, b, tz);
    }

    // "Lejos" (fuera del alcance) vale poco más que el alcance, para poder interpolar.
    private static float Far(float e)
    {
        return e > 1e6f ? 8f : e;
    }

    private static float Steep(Ctx c, Vector3 world)
    {
        TerrainData data = c.Terrain.terrainData;
        return data.GetSteepness((world.x - c.Origin.x) / data.size.x, (world.z - c.Origin.z) / data.size.z);
    }

    private static Transform Group(Ctx c, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(c.Root, false);
        return t;
    }

    private static Mesh SaveMesh(Ctx c, Mesh mesh, string name)
    {
        c.MeshCount++;
        return VisualAssetFactory.SaveMeshAsset(mesh, VisualAssetFactory.ModelsFolder + "/" + c.Prefix + "_" + name + ".asset");
    }

    private static void AddMesh(List<Mesh> list, string name)
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(VisualAssetFactory.ModelsFolder + "/" + name + ".asset");
        if (mesh != null) list.Add(mesh);
    }

    private static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(VisualAssetFactory.MaterialsFolder + "/" + name + ".mat");

    private static float Range(Ctx c, float a, float b) => a + (float)c.Rng.NextDouble() * (b - a);

    private static int Idx(float meters) => Mathf.Max(1, Mathf.RoundToInt(meters / Sp));

    private static GameObject Place(string name, Transform parent, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, params Material[] materials)
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

    /// <summary>Armador de mallas sencillo (cuadros y cajas) con varias submallas.</summary>
    private class MeshBuilderLite
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] triangles;

        public MeshBuilderLite(int submeshes)
        {
            triangles = new List<int>[submeshes];
            for (int k = 0; k < submeshes; k++) triangles[k] = new List<int>();
        }

        /// <summary>
        /// Cuadro a-b-c-d. "face" es hacia dónde debe mirar: si los puntos vienen al revés se da vuelta el orden
        /// (Unity solo dibuja la cara de adelante).
        /// </summary>
        public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 face)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            Vector3 normal = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(normal, face) >= 0f) triangles[submesh].AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            else triangles[submesh].AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
        }

        /// <summary>Caja con base a0-b0-c0-d0 y tapa a1-b1-c1-d1 (las caras miran hacia afuera solas).</summary>
        public void Box(int submesh, Vector3 a0, Vector3 b0, Vector3 c0, Vector3 d0, Vector3 a1, Vector3 b1, Vector3 c1, Vector3 d1)
        {
            Vector2 u0 = Vector2.zero, u1 = new Vector2(0f, 1f), u2 = Vector2.one, u3 = new Vector2(1f, 0f);
            Vector3 center = (a0 + b0 + c0 + d0 + a1 + b1 + c1 + d1) * 0.125f;
            Side(submesh, a1, d1, c1, b1, center, u0, u1, u2, u3);   // tapa
            Side(submesh, a0, b0, c0, d0, center, u0, u1, u2, u3);   // base
            Side(submesh, a0, a1, b1, b0, center, u0, u1, u2, u3);
            Side(submesh, b0, b1, c1, c0, center, u0, u1, u2, u3);
            Side(submesh, c0, c1, d1, d0, center, u0, u1, u2, u3);
            Side(submesh, d0, d1, a1, a0, center, u0, u1, u2, u3);
        }

        private void Side(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
        {
            Quad(submesh, a, b, c, d, u0, u1, u2, u3, (a + b + c + d) * 0.25f - center);
        }

        public Mesh ToMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int k = 0; k < triangles.Length; k++) mesh.SetTriangles(triangles[k], k);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
