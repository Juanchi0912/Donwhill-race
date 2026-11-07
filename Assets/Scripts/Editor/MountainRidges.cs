using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lomas empinadas entre las rutas (como límites): así no se ve de una ruta a la otra y cada una se siente
/// como su propio valle. Cada loma sigue el trazo dibujado, pero se corre sola al medio entre las rutas,
/// y cerca de cualquier sendero baja en pendiente fuerte hasta el nivel del sendero: nunca lo tapa.
/// Solo usa UnityEngine (se puede probar fuera de Unity). Va después de tallar los senderos.
/// </summary>
public static class MountainRidges
{
    private const float TrailClearance = 5f;   // Metros planos junto a la orilla del sendero antes de que suba la loma.
    private const float WallSlope = 1f;        // Pendiente máxima de la loma junto a un sendero (45°).
    private const float LockedFade = 110f;      // Cerca de las partes reales (círculos grises) no hay lomas.
    private const float BridgeClear = 60f;      // Ni cerca de un puente.
    private const float SearchReach = 55f;     // Cuánto puede correrse la loma para quedar al medio de las rutas.
    private const int FieldResolution = 1025;  // Los campos de distancia se calculan más gruesos y se interpolan.

    /// <summary>"heights" en metros [z, x]. "trails" son todas las líneas talladas (senderos y caminos).</summary>
    public static void Apply(float[,] heights, float sizeX, float sizeZ, RealMapTrails.Set set, List<TrailTerrainCarver.Line> trails,
        Vector2 cropX, Vector2 cropZ)
    {
        if (set == null || set.Ridges.Count == 0) return;
        List<TrailTerrainCarver.Line> solid = trails.FindAll(l => l.Sink <= 0f);
        // Puntos de los senderos (cada 3 m) para buscar el medio entre rutas.
        List<Vector3> trailPoints = new List<Vector3>();   // (x, y, medio ancho)
        foreach (TrailTerrainCarver.Line line in solid)
        {
            for (int i = 0; i < line.Points.Length; i += 4) trailPoints.Add(new Vector3(line.Points[i].x, line.Points[i].y, line.HalfWidth));
        }

        int res = FieldResolution;
        float[,] trailEdge = TrailTerrainCarver.EdgeDistance(solid, res, sizeX, sizeZ, 90f);
        List<TrailTerrainCarver.Line> locked = solid.FindAll(l => l.Locked);
        // Alrededor de los puentes no hay loma: debajo del tablero tiene que pasar la otra ruta.
        float[,] bridgeFar = BridgeDistance(solid, res, sizeX, sizeZ);
        float[,] lockedEdge = locked.Count > 0 ? TrailTerrainCarver.EdgeDistance(locked, res, sizeX, sizeZ, LockedFade) : null;
        float[,] ridgeDistance = new float[res, res];
        float[,] ridgeHeight = new float[res, res];
        float[,] ridgeWidth = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++) ridgeDistance[z, x] = float.MaxValue;
        }
        float cellX = sizeX / (res - 1), cellZ = sizeZ / (res - 1);
        foreach (RealMapTrails.Ridge ridge in set.Ridges)
        {
            List<Vector2> local = new List<Vector2>();
            foreach (Vector2 p in ridge.Image) local.Add(TrailTerrainCarver.ImageToLocal(p, cropX, cropZ, sizeX, sizeZ));
            Vector2[] line = Centered(TrailTerrainCarver.Resample(local, 8f), trailPoints);
            Vector2[] dense = TrailTerrainCarver.Resample(new List<Vector2>(line), 3f);
            float reach = ridge.HalfWidth;
            int rx = Mathf.CeilToInt(reach / cellX), rz = Mathf.CeilToInt(reach / cellZ);
            foreach (Vector2 p in dense)
            {
                int cx = Mathf.RoundToInt(p.x / cellX), cz = Mathf.RoundToInt(p.y / cellZ);
                for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(res - 1, cz + rz); z++)
                {
                    float dz = z * cellZ - p.y;
                    for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(res - 1, cx + rx); x++)
                    {
                        float dx = x * cellX - p.x;
                        float d = Mathf.Sqrt(dx * dx + dz * dz) / reach;   // 0 en la cumbre, 1 al pie
                        if (d >= 1f || d >= ridgeDistance[z, x]) continue;
                        ridgeDistance[z, x] = d;
                        ridgeHeight[z, x] = ridge.Height;
                        ridgeWidth[z, x] = reach;
                    }
                }
            }
        }

        int n = heights.GetLength(0);
        for (int z = 0; z < n; z++)
        {
            float v = z / (float)(n - 1);
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)(n - 1);
                float d = Sample(ridgeDistance, u, v);
                if (d >= 1f) continue;
                float mx = u * sizeX, mz = v * sizeZ;
                // Ancho que cambia (espolones y entradas), cumbres y portezuelos, quebradas y roca suelta: que parezca cerro.
                float wide = 0.75f + 0.5f * Mathf.PerlinNoise(mx * 0.012f + 3.3f, mz * 0.012f + 8.8f);
                float dd = d / wide;
                if (dd >= 1f) continue;
                float peaks = 0.6f + 0.8f * Mathf.PerlinNoise(mx * 0.006f + 31.7f, mz * 0.006f + 12.3f);
                float gullies = (Mathf.PerlinNoise(mx * 0.025f + 51.3f, mz * 0.025f + 2.9f) - 0.5f) * 14f;
                float rough = (Mathf.PerlinNoise(mx * 0.08f + 5.1f, mz * 0.08f + 77.7f) - 0.5f) * 4f;
                float shape = Mathf.Pow(Mathf.Max(0f, 1f - dd * dd), 1.3f);
                float add = (Sample(ridgeHeight, u, v) * peaks + gullies + rough) * shape;
                if (lockedEdge != null) add *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LockedFade * 0.4f, LockedFade, Sample(lockedEdge, u, v)));
                if (bridgeFar != null) add *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BridgeClear * 0.6f, BridgeClear, Sample(bridgeFar, u, v)));
                // Cerca de un sendero la loma baja como pared hasta su orilla.
                float edge = Sample(trailEdge, u, v);
                add = Mathf.Min(add, Mathf.Max(0f, edge - TrailClearance) * WallSlope);
                if (add > 0f) heights[z, x] += add;
            }
        }
    }

    /// <summary>Distancia (m) a los tramos de puente (puntos sin tallar), o null si no hay puentes.</summary>
    private static float[,] BridgeDistance(List<TrailTerrainCarver.Line> lines, int res, float sizeX, float sizeZ)
    {
        List<Vector2> deck = new List<Vector2>();
        foreach (TrailTerrainCarver.Line line in lines)
        {
            if (line.NoCarve == null) continue;
            for (int i = 0; i < line.Points.Length; i++) if (line.NoCarve[i]) deck.Add(line.Points[i]);
        }
        if (deck.Count == 0) return null;
        float[,] field = new float[res, res];
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) field[z, x] = BridgeClear;
        float cellX = sizeX / (res - 1), cellZ = sizeZ / (res - 1);
        int rx = Mathf.CeilToInt(BridgeClear / cellX), rz = Mathf.CeilToInt(BridgeClear / cellZ);
        foreach (Vector2 p in deck)
        {
            int cx = Mathf.RoundToInt(p.x / cellX), cz = Mathf.RoundToInt(p.y / cellZ);
            for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(res - 1, cz + rz); z++)
            {
                for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(res - 1, cx + rx); x++)
                {
                    float dx = x * cellX - p.x, dz = z * cellZ - p.y;
                    field[z, x] = Mathf.Min(field[z, x], Mathf.Sqrt(dx * dx + dz * dz));
                }
            }
        }
        return field;
    }

    /// <summary>Corre cada punto de la loma (de lado) a donde queda más lejos de todas las rutas: al medio entre ellas.</summary>
    private static Vector2[] Centered(Vector2[] line, List<Vector3> trailPoints)
    {
        int n = line.Length;
        float[] offsets = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 tangent = (line[Mathf.Min(n - 1, i + 1)] - line[Mathf.Max(0, i - 1)]).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float best = float.MinValue, bestOffset = 0f;
            for (float off = -SearchReach; off <= SearchReach; off += 2.5f)
            {
                Vector2 c = line[i] + normal * off;
                float clear = float.MaxValue;
                foreach (Vector3 t in trailPoints)
                {
                    float dx = t.x - c.x, dy = t.y - c.y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 < (clear + t.z) * (clear + t.z)) clear = Mathf.Sqrt(d2) - t.z;
                }
                float score = Mathf.Min(clear, 60f) - 0.08f * Mathf.Abs(off);
                if (score > best) { best = score; bestOffset = off; }
            }
            offsets[i] = bestOffset;
        }
        // Suavizado de los corrimientos: la loma sigue siendo una línea continua.
        float[] smooth = new float[n];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int j = Mathf.Max(0, i - 2); j <= Mathf.Min(n - 1, i + 2); j++) { sum += offsets[j]; count++; }
                smooth[i] = sum / count;
            }
            System.Array.Copy(smooth, offsets, n);
        }
        Vector2[] result = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2 tangent = (line[Mathf.Min(n - 1, i + 1)] - line[Mathf.Max(0, i - 1)]).normalized;
            result[i] = line[i] + new Vector2(-tangent.y, tangent.x) * offsets[i];
        }
        return result;
    }

    private static float Sample(float[,] grid, float u, float v)
    {
        int res = grid.GetLength(0);
        float fx = Mathf.Clamp01(u) * (res - 1), fz = Mathf.Clamp01(v) * (res - 1);
        int x0 = Mathf.Min(Mathf.FloorToInt(fx), res - 2), z0 = Mathf.Min(Mathf.FloorToInt(fz), res - 2);
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(Clamp(grid[z0, x0]), Clamp(grid[z0, x0 + 1]), tx);
        float b = Mathf.Lerp(Clamp(grid[z0 + 1, x0]), Clamp(grid[z0 + 1, x0 + 1]), tx);
        return Mathf.Lerp(a, b, tz);
    }

    // Los campos usan float.MaxValue para "lejos": se recorta para poder interpolar.
    private static float Clamp(float v) => v > 1e6f ? 1e6f : v;
}
