using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talla senderos en las alturas de un terreno sin aplanar la bajada: el sendero sigue la pendiente
/// del cerro (con acantilados y todo) y solo se nivela de lado a lado, como un sendero cavado en la ladera.
/// También calcula la distancia a los senderos para pintar la tierra y apartar los árboles.
/// </summary>
public static class TrailTerrainCarver
{
    private const float Spacing = 0.75f;         // Separación entre puntos de la línea central (m).
    private const float Shoulder = 4f;           // Borde que se mezcla con el terreno a cada lado (m).
    private const float RutDepth = 0.3f;         // El centro queda un poco hundido, como un sendero gastado.
    private const float ProfileSmoothing = 4f;   // Suavizado de la línea central a lo largo (m): quita serrucho, no curvas.
    // Subida máxima permitida (8 %, se sube pedaleando). Las lomas más empinadas se rebajan arriba
    // y se rellenan antes, repartiendo el movimiento de tierra en vez de cavar una trinchera.
    private const float MaxUphillGrade = 0.08f;
    // Peso de los puntos fijos (cruces): obliga a que los tramos que se juntan empaten en altura.
    private const float PinWeight = 10000f;
    private const float JunctionBlend = 15f;     // Largo de la rampa que empata un tramo con su cruce (m).

    /// <summary>Línea central de un tramo, en metros desde la esquina suroeste del terreno (x = este, y = norte).</summary>
    public class Line
    {
        public string Name;
        public float HalfWidth;
        public float MaxClimb;
        public Vector2[] Points;
        public float[] Heights;   // Altura del centro (m) después de tallar.
    }

    /// <summary>Pasa los tramos (coordenadas de imagen) al terreno recortado, redondea las esquinas y remuestrea.</summary>
    public static List<Line> BuildLines(List<RealMapTrails.Segment> segments, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ)
    {
        List<Line> lines = new List<Line>();
        foreach (RealMapTrails.Segment segment in segments)
        {
            List<Vector2> points = new List<Vector2>();
            foreach (Vector2 p in segment.Image)
            {
                float tu = (p.x - cropX.x) / Mathf.Max(0.01f, cropX.y - cropX.x);
                float tz = ((1f - p.y) - cropZ.x) / Mathf.Max(0.01f, cropZ.y - cropZ.x);
                points.Add(new Vector2(tu * sizeX, tz * sizeZ));
            }
            // Las esquinas del trazo se vuelven curvas (tres pasadas de Chaikin).
            for (int pass = 0; pass < 3; pass++) points = Chaikin(points);
            lines.Add(new Line { Name = segment.Name, HalfWidth = segment.HalfWidth, MaxClimb = segment.MaxClimb, Points = Resample(points, Spacing) });
        }
        return lines;
    }

    /// <summary>Talla todas las líneas en "heights" (en metros, [z, x], cubre sizeX × sizeZ).</summary>
    public static void Carve(float[,] heights, float sizeX, float sizeZ, List<Line> lines)
    {
        int res = heights.GetLength(0);
        float[,] original = (float[,])heights.Clone();

        // Perfil de cada línea. Donde una empieza al final de otra (cruces) arranca a la misma altura.
        List<Vector2> junctions = new List<Vector2>();
        List<float> junctionHeights = new List<float>();
        float maxHalf = 0f;
        foreach (Line line in lines)
        {
            line.Heights = CenterProfile(original, sizeX, sizeZ, line, junctions, junctionHeights);
            maxHalf = Mathf.Max(maxHalf, line.HalfWidth);
        }

        Nearest(lines, res, sizeX, sizeZ, maxHalf + Shoulder, out float[,] edge, out float[,] center, out float[,] half);
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float e = edge[z, x];
                if (e >= Shoulder) continue;
                float target = center[z, x];
                float weight;
                if (e <= 0f)
                {
                    float across = (e + half[z, x]) / Mathf.Max(0.1f, half[z, x]);
                    target -= RutDepth * (1f - across * across);
                    weight = 1f;
                }
                else
                {
                    weight = 1f - Mathf.SmoothStep(0f, 1f, e / Shoulder);
                }
                heights[z, x] = Mathf.Lerp(original[z, x], target, weight);
            }
        }
    }

    /// <summary>
    /// Distancia (m) desde cada celda de una cuadrícula res × res al borde del sendero más cercano.
    /// Negativo = dentro del sendero; float.MaxValue = más lejos que "reach".
    /// </summary>
    public static float[,] EdgeDistance(List<Line> lines, int res, float sizeX, float sizeZ, float reach)
    {
        Nearest(lines, res, sizeX, sizeZ, reach, out float[,] edge, out _, out _);
        return edge;
    }

    private static float[] CenterProfile(float[,] heights, float sizeX, float sizeZ, Line line,
        List<Vector2> junctions, List<float> junctionHeights)
    {
        int n = line.Points.Length;
        float[] h = new float[n];
        for (int i = 0; i < n; i++) h[i] = SampleBilinear(heights, line.Points[i].x / sizeX, line.Points[i].y / sizeZ);

        // Suavizado a lo largo: el heightmap tiene ruido que se sentiría como serrucho bajo las ruedas.
        int window = Mathf.Max(1, Mathf.RoundToInt(ProfileSmoothing / Spacing));
        float[] tmp = new float[n];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -window; k <= window; k++)
                {
                    int j = i + k;
                    if (j < 0 || j >= n) continue;
                    sum += h[j];
                    count++;
                }
                tmp[i] = sum / count;
            }
            System.Array.Copy(tmp, h, n);
        }

        // Donde el tramo empieza o termina en un cruce ya calculado, se fija esa altura.
        int start = FindJunction(junctions, line.Points[0]);
        int end = FindJunction(junctions, line.Points[n - 1]);
        float[] weights = new float[n];
        for (int i = 0; i < n; i++) weights[i] = 1f;
        if (start >= 0) { h[0] = junctionHeights[start]; weights[0] = PinWeight; }
        if (end >= 0) { h[n - 1] = junctionHeights[end]; weights[n - 1] = PinWeight; }

        h = FitDescending(h, weights, Spacing, line.MaxClimb > 0f ? line.MaxClimb : MaxUphillGrade);
        // En los cruces la altura fija puede quedar distinta del terreno de al lado: se reparte en una rampa.
        if (start >= 0) BlendEnd(h, true);
        if (end >= 0) BlendEnd(h, false);

        if (start < 0) { junctions.Add(line.Points[0]); junctionHeights.Add(h[0]); }
        if (end < 0) { junctions.Add(line.Points[n - 1]); junctionHeights.Add(h[n - 1]); }
        return h;
    }

    /// <summary>Rampa suave de JunctionBlend metros entre la altura fija del cruce y el resto del perfil.</summary>
    private static void BlendEnd(float[] h, bool atStart)
    {
        int n = h.Length;
        int k = Mathf.Min(n - 1, Mathf.RoundToInt(JunctionBlend / Spacing));
        if (k < 2) return;
        int pin = atStart ? 0 : n - 1, far = atStart ? k : n - 1 - k, step = atStart ? 1 : -1;
        for (int i = 1; i < k; i++)
        {
            int index = pin + step * i;
            float t = i / (float)k;
            float straight = Mathf.Lerp(h[pin], h[far], t);
            h[index] = Mathf.Lerp(straight, h[index], Mathf.SmoothStep(0f, 1f, t));
        }
    }

    /// <summary>
    /// El perfil más parecido al terreno (mínimos cuadrados) que nunca sube más que "maxGrade".
    /// Se resuelve con regresión isotónica (Pool Adjacent Violators) sobre h − maxGrade·distancia,
    /// que debe ir siempre hacia abajo. Las bajadas, incluso las muy empinadas, quedan tal cual.
    /// </summary>
    private static float[] FitDescending(float[] h, float[] weights, float spacing, float maxGrade)
    {
        int n = h.Length;
        // Bloques de puntos promediados: valor medio, peso total y cuántos puntos abarcan.
        float[] blockValue = new float[n], blockWeight = new float[n];
        int[] blockCount = new int[n];
        int blocks = 0;
        for (int i = 0; i < n; i++)
        {
            blockValue[blocks] = h[i] - maxGrade * spacing * i;
            blockWeight[blocks] = weights[i];
            blockCount[blocks] = 1;
            blocks++;
            // Si un bloque queda más alto que el anterior (subida de más), se juntan.
            while (blocks > 1 && blockValue[blocks - 1] > blockValue[blocks - 2])
            {
                float w = blockWeight[blocks - 2] + blockWeight[blocks - 1];
                blockValue[blocks - 2] = (blockValue[blocks - 2] * blockWeight[blocks - 2] + blockValue[blocks - 1] * blockWeight[blocks - 1]) / w;
                blockWeight[blocks - 2] = w;
                blockCount[blocks - 2] += blockCount[blocks - 1];
                blocks--;
            }
        }
        float[] result = new float[n];
        int index = 0;
        for (int b = 0; b < blocks; b++)
        {
            for (int k = 0; k < blockCount[b]; k++, index++) result[index] = blockValue[b] + maxGrade * spacing * index;
        }
        return result;
    }

    private static int FindJunction(List<Vector2> junctions, Vector2 point)
    {
        for (int i = 0; i < junctions.Count; i++)
        {
            if ((junctions[i] - point).sqrMagnitude < 4f) return i;
        }
        return -1;
    }

    /// <summary>Para cada celda: distancia al borde del sendero más cercano, la altura de su centro y su medio ancho.</summary>
    private static void Nearest(List<Line> lines, int res, float sizeX, float sizeZ, float reach,
        out float[,] edge, out float[,] center, out float[,] half)
    {
        edge = new float[res, res];
        center = new float[res, res];
        half = new float[res, res];
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++) edge[z, x] = float.MaxValue;
        }
        float cellX = sizeX / (res - 1), cellZ = sizeZ / (res - 1);
        foreach (Line line in lines)
        {
            float radius = reach + line.HalfWidth;
            int rx = Mathf.CeilToInt(radius / cellX), rz = Mathf.CeilToInt(radius / cellZ);
            for (int i = 0; i < line.Points.Length; i++)
            {
                Vector2 p = line.Points[i];
                int cx = Mathf.RoundToInt(p.x / cellX), cz = Mathf.RoundToInt(p.y / cellZ);
                for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(res - 1, cz + rz); z++)
                {
                    float dz = z * cellZ - p.y;
                    for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(res - 1, cx + rx); x++)
                    {
                        float dx = x * cellX - p.x;
                        float e = Mathf.Sqrt(dx * dx + dz * dz) - line.HalfWidth;
                        if (e > reach || e >= edge[z, x]) continue;
                        edge[z, x] = e;
                        center[z, x] = line.Heights != null ? line.Heights[i] : 0f;
                        half[z, x] = line.HalfWidth;
                    }
                }
            }
        }
    }

    private static float SampleBilinear(float[,] grid, float u, float v)
    {
        int res = grid.GetLength(0);
        float fx = Mathf.Clamp01(u) * (res - 1), fz = Mathf.Clamp01(v) * (res - 1);
        int x0 = Mathf.Min(Mathf.FloorToInt(fx), res - 2), z0 = Mathf.Min(Mathf.FloorToInt(fz), res - 2);
        float tx = fx - x0, tz = fz - z0;
        float a = Mathf.Lerp(grid[z0, x0], grid[z0, x0 + 1], tx);
        float b = Mathf.Lerp(grid[z0 + 1, x0], grid[z0 + 1, x0 + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    private static List<Vector2> Chaikin(List<Vector2> points)
    {
        if (points.Count < 3) return points;
        List<Vector2> result = new List<Vector2> { points[0] };
        for (int i = 0; i < points.Count - 1; i++)
        {
            result.Add(Vector2.Lerp(points[i], points[i + 1], 0.25f));
            result.Add(Vector2.Lerp(points[i], points[i + 1], 0.75f));
        }
        result.Add(points[points.Count - 1]);
        return result;
    }

    /// <summary>Puntos a distancia fija a lo largo de la polilínea (conserva el primero y el último).</summary>
    public static Vector2[] Resample(List<Vector2> points, float spacing)
    {
        List<Vector2> result = new List<Vector2> { points[0] };
        float carried = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 a = points[i - 1], b = points[i];
            float length = Vector2.Distance(a, b);
            float t = spacing - carried;
            while (t <= length)
            {
                result.Add(Vector2.Lerp(a, b, t / length));
                t += spacing;
            }
            carried = length - (t - spacing);
        }
        if ((result[result.Count - 1] - points[points.Count - 1]).sqrMagnitude > 0.01f) result.Add(points[points.Count - 1]);
        return result.ToArray();
    }
}
