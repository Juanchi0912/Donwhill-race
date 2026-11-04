using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Construye mallas por código a partir de formas simples (cajas, tubos, cápsulas, elipsoides, toroides).
/// Usa System.Numerics y no UnityEngine, para poder probarlo y previsualizarlo fuera del Editor;
/// VisualAssetFactory la convierte después en una Mesh de Unity.
/// Convención de Unity: x = derecha, y = arriba, z = adelante; las caras visibles tienen
/// la normal cross(b - a, c - a) hacia afuera. Cada forma corrige sola el orden de sus triángulos.
/// </summary>
public sealed class MeshBuilder
{
    public readonly List<Vector3> Vertices = new List<Vector3>();
    public readonly List<Vector2> Uvs = new List<Vector2>();
    public readonly List<List<int>> Submeshes = new List<List<int>>();

    // Posiciones en "mundo" (antes de pasar al marco local), usadas para orientar los triángulos.
    private readonly List<Vector3> world = new List<Vector3>();
    private Matrix4x4 toLocal = Matrix4x4.Identity;

    public MeshBuilder(int submeshCount)
    {
        for (int i = 0; i < submeshCount; i++) Submeshes.Add(new List<int>());
    }

    public int TriangleCount
    {
        get
        {
            int count = 0;
            foreach (List<int> s in Submeshes) count += s.Count / 3;
            return count;
        }
    }

    /// <summary>
    /// Define el pivote del objeto que llevará la malla: las formas se describen en coordenadas
    /// del modelo completo y se guardan relativas a este pivote.
    /// </summary>
    public void SetFrame(Vector3 origin, Quaternion rotation)
    {
        Matrix4x4 frame = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(origin);
        Matrix4x4.Invert(frame, out toLocal);
    }

    public int AddVertex(Vector3 position, Vector2 uv)
    {
        world.Add(position);
        Vertices.Add(Vector3.Transform(position, toLocal));
        Uvs.Add(uv);
        return Vertices.Count - 1;
    }

    /// <summary>Agrega un triángulo; si su normal no apunta hacia "outward", invierte el orden.</summary>
    public void AddTriangle(int submesh, int a, int b, int c, Vector3 outward)
    {
        Vector3 n = Vector3.Cross(world[b] - world[a], world[c] - world[a]);
        if (n.LengthSquared() < 1e-14f) return; // Triángulo degenerado (por ejemplo, en los polos).
        List<int> list = Submeshes[submesh];
        if (Vector3.Dot(n, outward) >= 0f)
        {
            list.Add(a); list.Add(b); list.Add(c);
        }
        else
        {
            list.Add(a); list.Add(c); list.Add(b);
        }
    }

    public void AddQuad(int submesh, int a, int b, int c, int d, Vector3 outward)
    {
        AddTriangle(submesh, a, b, c, outward);
        AddTriangle(submesh, a, c, d, outward);
    }

    // =====================================================================
    // Formas
    // =====================================================================

    /// <summary>Caja definida por su centro y tres ejes (medios tamaños incluidos en cada eje).</summary>
    public void AddBoxAxes(int submesh, Vector3 center, Vector3 halfX, Vector3 halfY, Vector3 halfZ, float tile)
    {
        Vector3[] axes = { halfX, halfY, halfZ };
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 n = axes[axis];
            Vector3 s = axes[(axis + 1) % 3];
            Vector3 t = axes[(axis + 2) % 3];
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 c = center + n * sign;
                float su = tile > 0f ? s.Length() * 2f / tile : 1f;
                float sv = tile > 0f ? t.Length() * 2f / tile : 1f;
                int a = AddVertex(c - s - t, new Vector2(0f, 0f));
                int b = AddVertex(c + s - t, new Vector2(su, 0f));
                int d = AddVertex(c + s + t, new Vector2(su, sv));
                int e = AddVertex(c - s + t, new Vector2(0f, sv));
                AddQuad(submesh, a, b, d, e, n * sign);
            }
        }
    }

    public void AddBox(int submesh, Vector3 center, Quaternion rotation, Vector3 size, float tile = 0f)
    {
        AddBoxAxes(submesh, center,
            Vector3.Transform(Vector3.UnitX, rotation) * size.X * 0.5f,
            Vector3.Transform(Vector3.UnitY, rotation) * size.Y * 0.5f,
            Vector3.Transform(Vector3.UnitZ, rotation) * size.Z * 0.5f, tile);
    }

    /// <summary>Viga de sección rectangular entre dos puntos (tubos "hidroformados" del cuadro).</summary>
    public void AddBeam(int submesh, Vector3 from, Vector3 to, float width, float height, Vector3 upHint, float tile = 0.5f)
    {
        Vector3 f = to - from;
        float length = f.Length();
        f /= length;
        Vector3 right = SafeNormalize(Vector3.Cross(upHint, f), Perpendicular(f));
        Vector3 up = Vector3.Cross(f, right);
        AddBoxAxes(submesh, (from + to) * 0.5f, right * width * 0.5f, up * height * 0.5f, f * length * 0.5f, tile);
    }

    /// <summary>
    /// Superficie de revolución alrededor de un eje: cada punto del perfil es (distancia a lo largo
    /// del eje, radio). Sirve para tubos, conos, cápsulas y las capas de los pinos.
    /// </summary>
    public void AddLathe(int submesh, Vector3 origin, Vector3 axis, IList<Vector2> profile, int segments,
        float uRepeat = 1f, float vTile = 0f, float flatten = 1f, Vector3 widthAxis = default, float[] radiusJitter = null, bool inward = false)
    {
        axis = Vector3.Normalize(axis);
        Vector3 p = widthAxis == default ? Perpendicular(axis) : SafeNormalize(widthAxis - axis * Vector3.Dot(widthAxis, axis), Perpendicular(axis));
        Vector3 q = Vector3.Cross(axis, p);

        float total = 0f;
        float[] along = new float[profile.Count];
        for (int k = 1; k < profile.Count; k++)
        {
            total += Vector2.Distance(profile[k], profile[k - 1]);
            along[k] = total;
        }

        int[,] index = new int[profile.Count, segments + 1];
        for (int k = 0; k < profile.Count; k++)
        {
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * MathF.PI * 2f;
                float jitter = radiusJitter != null ? radiusJitter[i % segments] : 1f;
                float r = profile[k].Y * jitter;
                Vector3 radial = p * MathF.Cos(angle) * r + q * MathF.Sin(angle) * r * flatten;
                float v = vTile > 0f ? along[k] / vTile : (total > 0f ? along[k] / total : 0f);
                index[k, i] = AddVertex(origin + axis * profile[k].X + radial, new Vector2(i / (float)segments * uRepeat, v));
            }
        }

        for (int k = 0; k < profile.Count - 1; k++)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) / segments * MathF.PI * 2f;
                Vector3 outward = p * MathF.Cos(angle) + q * MathF.Sin(angle);
                // En perfiles que se cierran (radio que baja a 0) la normal se inclina hacia el eje.
                float dr = profile[k + 1].Y - profile[k].Y;
                float dx = profile[k + 1].X - profile[k].X;
                outward = outward * MathF.Abs(dx) - axis * dr * MathF.Sign(dx == 0f ? 1f : dx);
                if (outward.LengthSquared() < 1e-10f) outward = axis * (profile[k].X <= profile[0].X ? -1f : 1f);
                if (inward) outward = -outward; // Superficies que se ven desde adentro (interior del aro).
                AddQuad(submesh, index[k, i], index[k, i + 1], index[k + 1, i + 1], index[k + 1, i], outward);
            }
        }
    }

    /// <summary>Tubo (o cono truncado) entre dos puntos, con tapas opcionales.</summary>
    public void AddTube(int submesh, Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments,
        bool caps = true, float tile = 0.5f, float flatten = 1f, Vector3 widthAxis = default)
    {
        Vector3 axis = to - from;
        float length = axis.Length();
        float circumference = MathF.PI * (radiusFrom + radiusTo);
        float uRepeat = tile > 0f ? MathF.Max(1f, MathF.Round(circumference / tile)) : 1f;
        AddLathe(submesh, from, axis, new[] { new Vector2(0f, radiusFrom), new Vector2(length, radiusTo) }, segments, uRepeat, tile, flatten, widthAxis);
        if (caps)
        {
            AddDisc(submesh, from, -axis, radiusFrom, segments, flatten, widthAxis);
            AddDisc(submesh, to, axis, radiusTo, segments, flatten, widthAxis);
        }
    }

    /// <summary>Cápsula entre dos puntos (extremidades del corredor).</summary>
    public void AddCapsule(int submesh, Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int segments,
        float flatten = 1f, Vector3 widthAxis = default)
    {
        Vector3 axis = to - from;
        float length = axis.Length();
        List<Vector2> profile = new List<Vector2>();
        const int capSteps = 4;
        for (int s = 0; s <= capSteps; s++)
        {
            float a = -MathF.PI / 2f + s / (float)capSteps * MathF.PI / 2f;
            profile.Add(new Vector2(MathF.Sin(a) * radiusFrom, MathF.Cos(a) * radiusFrom));
        }
        for (int s = 0; s <= capSteps; s++)
        {
            float a = s / (float)capSteps * MathF.PI / 2f;
            profile.Add(new Vector2(length + MathF.Sin(a) * radiusTo, MathF.Cos(a) * radiusTo));
        }
        AddLathe(submesh, from, axis, profile, segments, 1f, 0f, flatten, widthAxis);
    }

    /// <summary>Disco plano (tapa) mirando hacia "normal".</summary>
    public void AddDisc(int submesh, Vector3 center, Vector3 normal, float radius, int segments, float flatten = 1f, Vector3 widthAxis = default)
    {
        normal = Vector3.Normalize(normal);
        Vector3 p = widthAxis == default ? Perpendicular(normal) : SafeNormalize(widthAxis - normal * Vector3.Dot(widthAxis, normal), Perpendicular(normal));
        Vector3 q = Vector3.Cross(normal, p);
        int c = AddVertex(center, new Vector2(0.5f, 0.5f));
        int first = -1, prev = -1;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * MathF.PI * 2f;
            int v = i == segments ? first : AddVertex(center + p * MathF.Cos(a) * radius + q * MathF.Sin(a) * radius * flatten,
                new Vector2(0.5f + MathF.Cos(a) * 0.5f, 0.5f + MathF.Sin(a) * 0.5f));
            if (i == 0) first = v;
            if (prev >= 0) AddTriangle(submesh, c, prev, v, normal);
            prev = v;
        }
    }

    /// <summary>Anillo plano (como el costado de un aro) en el plano perpendicular a "normal".</summary>
    public void AddAnnulus(int submesh, Vector3 center, Vector3 normal, float innerRadius, float outerRadius, int segments, float uRepeat = 1f)
    {
        normal = Vector3.Normalize(normal);
        Vector3 p = Perpendicular(normal);
        Vector3 q = Vector3.Cross(normal, p);
        int prevIn = -1, prevOut = -1;
        for (int i = 0; i <= segments; i++)
        {
            float a = i / (float)segments * MathF.PI * 2f;
            Vector3 dir = p * MathF.Cos(a) + q * MathF.Sin(a);
            float u = i / (float)segments * uRepeat;
            int vin = AddVertex(center + dir * innerRadius, new Vector2(u, 0f));
            int vout = AddVertex(center + dir * outerRadius, new Vector2(u, 1f));
            if (prevIn >= 0) AddQuad(submesh, prevIn, prevOut, vout, vin, normal);
            prevIn = vin;
            prevOut = vout;
        }
    }

    /// <summary>Elipsoide con mapeo latitud-longitud (u = longitud, v = 0 abajo a 1 arriba).</summary>
    public void AddEllipsoid(int submesh, Vector3 center, Quaternion rotation, Vector3 radii, int segmentsU, int segmentsV)
    {
        int[,] index = new int[segmentsV + 1, segmentsU + 1];
        for (int j = 0; j <= segmentsV; j++)
        {
            float lat = -MathF.PI / 2f + j / (float)segmentsV * MathF.PI;
            for (int i = 0; i <= segmentsU; i++)
            {
                float lon = i / (float)segmentsU * MathF.PI * 2f;
                // Longitud 0 hacia atrás (-z), para que la costura del mapa quede en la nuca.
                Vector3 local = new Vector3(MathF.Cos(lat) * -MathF.Sin(lon) * radii.X, MathF.Sin(lat) * radii.Y, MathF.Cos(lat) * -MathF.Cos(lon) * radii.Z);
                index[j, i] = AddVertex(center + Vector3.Transform(local, rotation), new Vector2(i / (float)segmentsU, j / (float)segmentsV));
            }
        }
        for (int j = 0; j < segmentsV; j++)
        {
            for (int i = 0; i < segmentsU; i++)
            {
                Vector3 mid = (world[index[j, i]] + world[index[j + 1, i + 1]]) * 0.5f;
                AddQuad(submesh, index[j, i], index[j, i + 1], index[j + 1, i + 1], index[j + 1, i], mid - center);
            }
        }
    }

    /// <summary>
    /// Toroide (o parte de uno) alrededor de "axis". Sirve para neumáticos, el resorte del amortiguador,
    /// la mentonera del casco y la correa de las antiparras.
    /// </summary>
    public void AddTorus(int submesh, Vector3 center, Vector3 axis, float majorRadius, float minorRadius, int segmentsU, int segmentsV,
        float startAngle = 0f, float endAngle = MathF.PI * 2f, float uRepeat = 1f, Vector3 startDirection = default)
    {
        axis = Vector3.Normalize(axis);
        Vector3 p = startDirection == default ? Perpendicular(axis) : SafeNormalize(startDirection - axis * Vector3.Dot(startDirection, axis), Perpendicular(axis));
        Vector3 q = Vector3.Cross(axis, p);
        int[,] index = new int[segmentsU + 1, segmentsV + 1];
        Vector3[] ringCenters = new Vector3[segmentsU + 1];
        for (int i = 0; i <= segmentsU; i++)
        {
            float a = startAngle + (endAngle - startAngle) * i / segmentsU;
            Vector3 dir = p * MathF.Cos(a) + q * MathF.Sin(a);
            ringCenters[i] = center + dir * majorRadius;
            for (int j = 0; j <= segmentsV; j++)
            {
                float b = j / (float)segmentsV * MathF.PI * 2f;
                Vector3 offset = dir * MathF.Cos(b) * minorRadius + axis * MathF.Sin(b) * minorRadius;
                index[i, j] = AddVertex(ringCenters[i] + offset, new Vector2(i / (float)segmentsU * uRepeat, j / (float)segmentsV));
            }
        }
        for (int i = 0; i < segmentsU; i++)
        {
            for (int j = 0; j < segmentsV; j++)
            {
                Vector3 mid = (world[index[i, j]] + world[index[i + 1, j + 1]]) * 0.5f;
                Vector3 ring = (ringCenters[i] + ringCenters[i + 1]) * 0.5f;
                AddQuad(submesh, index[i, j], index[i + 1, j], index[i + 1, j + 1], index[i, j + 1], mid - ring);
            }
        }
    }

    /// <summary>Rectángulo plano de una cara (calcomanías). UV de 0 a 1.</summary>
    public void AddPanel(int submesh, Vector3 center, Vector3 right, Vector3 up, float width, float height)
    {
        right = Vector3.Normalize(right) * width * 0.5f;
        up = Vector3.Normalize(up) * height * 0.5f;
        Vector3 normal = Vector3.Cross(up, right); // Mira hacia el lado desde el que el texto se lee bien.
        int a = AddVertex(center - right - up, new Vector2(0f, 0f));
        int b = AddVertex(center + right - up, new Vector2(1f, 0f));
        int c = AddVertex(center + right + up, new Vector2(1f, 1f));
        int d = AddVertex(center - right + up, new Vector2(0f, 1f));
        AddQuad(submesh, a, b, c, d, normal);
    }

    /// <summary>
    /// Esfera facetada y deformada con ruido (rocas, copas de árboles). Cada triángulo tiene
    /// vértices propios, así el sombreado queda "low poly".
    /// </summary>
    public void AddLumpySphere(int submesh, Vector3 center, Vector3 radii, int subdivisions, float roughness, int seed, float flattenBottom = 0f)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int[]> faces = new List<int[]>();
        Icosahedron(verts, faces);
        for (int s = 0; s < subdivisions; s++) Subdivide(verts, faces);

        Vector3[] displaced = new Vector3[verts.Count];
        for (int i = 0; i < verts.Count; i++)
        {
            Vector3 d = Vector3.Normalize(verts[i]);
            float n = ValueNoise3(d * 2.2f, seed) * 0.65f + ValueNoise3(d * 5f, seed + 17) * 0.35f;
            Vector3 point = d * (1f + (n - 0.5f) * 2f * roughness);
            point = new Vector3(point.X * radii.X, point.Y * radii.Y, point.Z * radii.Z);
            if (flattenBottom > 0f && point.Y < -radii.Y * flattenBottom) point.Y = -radii.Y * flattenBottom;
            displaced[i] = point;
        }

        foreach (int[] f in faces)
        {
            Vector3 a = displaced[f[0]], b = displaced[f[1]], c = displaced[f[2]];
            int ia = AddVertex(center + a, SphereUv(a));
            int ib = AddVertex(center + b, SphereUv(b));
            int ic = AddVertex(center + c, SphereUv(c));
            AddTriangle(submesh, ia, ib, ic, (a + b + c) / 3f);
        }
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    public static Vector3 Perpendicular(Vector3 v)
    {
        Vector3 other = MathF.Abs(v.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        return Vector3.Normalize(Vector3.Cross(other, v));
    }

    public static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
    {
        float len = v.Length();
        return len > 1e-6f ? v / len : fallback;
    }

    public static Quaternion AxisAngle(Vector3 axis, float degrees)
    {
        return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), degrees * MathF.PI / 180f);
    }

    private static Vector2 SphereUv(Vector3 p)
    {
        Vector3 d = Vector3.Normalize(p);
        return new Vector2(MathF.Atan2(d.X, d.Z) / (MathF.PI * 2f) + 0.5f, d.Y * 0.5f + 0.5f);
    }

    private static void Icosahedron(List<Vector3> verts, List<int[]> faces)
    {
        float t = (1f + MathF.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        foreach (Vector3 p in v) verts.Add(Vector3.Normalize(p));
        int[][] f =
        {
            new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
            new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
            new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
            new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
        };
        faces.AddRange(f);
    }

    private static void Subdivide(List<Vector3> verts, List<int[]> faces)
    {
        Dictionary<long, int> cache = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int m)) return m;
            verts.Add(Vector3.Normalize((verts[a] + verts[b]) * 0.5f));
            cache[key] = verts.Count - 1;
            return verts.Count - 1;
        }
        List<int[]> result = new List<int[]>();
        foreach (int[] f in faces)
        {
            int ab = Mid(f[0], f[1]), bc = Mid(f[1], f[2]), ca = Mid(f[2], f[0]);
            result.Add(new[] { f[0], ab, ca });
            result.Add(new[] { f[1], bc, ab });
            result.Add(new[] { f[2], ca, bc });
            result.Add(new[] { ab, bc, ca });
        }
        faces.Clear();
        faces.AddRange(result);
    }

    /// <summary>Ruido de valor 3D en [0,1], suficiente para deformar rocas.</summary>
    public static float ValueNoise3(Vector3 p, int seed)
    {
        int x0 = (int)MathF.Floor(p.X), y0 = (int)MathF.Floor(p.Y), z0 = (int)MathF.Floor(p.Z);
        float fx = p.X - x0, fy = p.Y - y0, fz = p.Z - z0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        fz = fz * fz * (3f - 2f * fz);
        float Corner(int x, int y, int z) => Hash01(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435);
        float Lerp(float a, float b, float t) => a + (b - a) * t;
        float c00 = Lerp(Corner(x0, y0, z0), Corner(x0 + 1, y0, z0), fx);
        float c10 = Lerp(Corner(x0, y0 + 1, z0), Corner(x0 + 1, y0 + 1, z0), fx);
        float c01 = Lerp(Corner(x0, y0, z0 + 1), Corner(x0 + 1, y0, z0 + 1), fx);
        float c11 = Lerp(Corner(x0, y0 + 1, z0 + 1), Corner(x0 + 1, y0 + 1, z0 + 1), fx);
        return Lerp(Lerp(c00, c10, fy), Lerp(c01, c11, fy), fz);
    }

    private static float Hash01(int h)
    {
        unchecked
        {
            h = (h ^ 61) ^ (h >> 16);
            h *= 9;
            h ^= h >> 4;
            h *= 0x27d4eb2d;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
