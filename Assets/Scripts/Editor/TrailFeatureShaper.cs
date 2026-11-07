using System.Collections.Generic;
using UnityEngine;
using Feature = RealMapTrails.Feature;
using Line = TrailTerrainCarver.Line;

/// <summary>
/// Le da forma a los saltos y obstáculos sobre el perfil ya tallado de un sendero (la bajada sigue siendo bajada):
/// drops donde la bajada se aplana y cae de golpe, rampas, mesas, dobles, lomas, gaps sobre el camino y la cerca,
/// pedregales que vibran, y peraltes en las curvas cerradas. Las rectas largas se llenan solas de saltos.
/// Las recepciones son largas y empinadas: se cae bien a cualquier velocidad (más rápido = más lejos).
/// </summary>
public static class TrailFeatureShaper
{
    private const float Sp = TrailTerrainCarver.Spacing;
    private const float EndClear = 22f;      // Sin saltos cerca de los extremos del tramo (cruces de salida/llegada).
    private const float PinClear = 26f;      // Ni cerca de cruces con otras rutas y puentes.
    private const float FeatureGap = 22f;    // Espacio mínimo entre dos saltos (m).
    private const float AutoSpacing = 42f;   // En las rectas, un salto cada tanto.
    private const float ApproachGrade = 0.02f; // La entrada a un drop casi plana: se junta altura para caer.

    private static readonly Feature[] AutoCycle = { Feature.Tabletop, Feature.Rollers, Feature.Kicker, Feature.Drop, Feature.Doubles };

    public static void Shape(Line line, int lineIndex, List<int> fixedPoints, List<Vector2> roadCrossings, TrailTerrainCarver.Result result)
    {
        int n = line.Points.Length;
        bool[] busy = new bool[n];
        Mark(busy, 0, Idx(EndClear));
        Mark(busy, n - 1 - Idx(EndClear), n - 1);
        foreach (int p in fixedPoints) Mark(busy, p - Idx(PinClear), p + Idx(PinClear));
        foreach (Vector2 road in roadCrossings) Mark(busy, (int)road.x - Idx(10f), (int)road.x + Idx(10f));
        bool[] protectedPoints = (bool[])busy.Clone();
        List<TrailTerrainCarver.Placed> placedHere = new List<TrailTerrainCarver.Placed>();

        // Los pedidos a mano, en orden a lo largo del tramo.
        List<KeyValuePair<int, TrailTerrainCarver.LocalSpot>> spots = new List<KeyValuePair<int, TrailTerrainCarver.LocalSpot>>();
        foreach (TrailTerrainCarver.LocalSpot spot in line.Spots) spots.Add(new KeyValuePair<int, TrailTerrainCarver.LocalSpot>(NearestIndex(line.Points, spot.At), spot));
        spots.Sort((a, b) => a.Key.CompareTo(b.Key));
        foreach (KeyValuePair<int, TrailTerrainCarver.LocalSpot> pair in spots)
        {
            TrailTerrainCarver.Placed placed = Place(line, pair.Value.Type, pair.Key, pair.Value.Size, roadCrossings, protectedPoints);
            if (placed == null) continue;
            placed.Line = lineIndex;
            placedHere.Add(placed);
            Mark(busy, placed.Start - Idx(FeatureGap), placed.End + Idx(FeatureGap));
        }

        // Rectas largas: se aburre uno, así que se llenan de saltos (alternando el tipo).
        if (line.Source.AutoJumps) AutoJumps(line, lineIndex, busy, protectedPoints, placedHere);

        result.Features.AddRange(placedHere);
        ComputeBank(line, fixedPoints, placedHere);
        if (line.Source.Clay) ClayRanges(line, lineIndex, result);
    }

    // =====================================================================
    // Saltos
    // =====================================================================

    private static TrailTerrainCarver.Placed Place(Line line, Feature type, int at, float size, List<Vector2> roadCrossings, bool[] protectedPoints)
    {
        float[] h = line.Heights;
        int n = h.Length;
        TrailTerrainCarver.Placed placed = new TrailTerrainCarver.Placed { Type = type, Size = size, Lip = at };
        switch (type)
        {
            case Feature.Kicker: Jump(h, at, 1.2f * size, 4.5f * Mathf.Sqrt(size), 0f, 6f, 0.6f, 9f * size, placed); break;
            case Feature.Tabletop: Jump(h, at, 1.4f * size, 5.5f, 4.5f * size, 6f, 0.6f, 10f * size, placed); break;
            case Feature.Drop: Jump(h, at, 0.3f, 2.5f, 0f, 16f * size, 3.2f * size, 11f * size, placed); break;
            case Feature.Mirador: Jump(h, at, 0.8f, 3f, 0f, 13f * size, 3.5f * size, 12f * size, placed); break;
            case Feature.RockDrop: Jump(h, at, 0.25f, 2f, 0f, 9f * size, 1.8f * size, 8f * size, placed); break;
            case Feature.BigJump: Jump(h, at, 2.3f * size, 8.5f, 0f, 26f * size, 5f * size, 24f * size, placed); break;
            case Feature.FenceGap:
                Jump(h, at, 1.4f * size, 5.5f, 0f, 10f, 1.2f, 13f, placed);
                placed.Extra = 3f;   // La cerca va 3 m después del borde de la rampa.
                break;
            case Feature.RoadGap:
            {
                // El borde de la rampa queda justo antes del camino; la recepción, pasando el camino.
                int best = -1;
                float roadHalf = 3f;
                foreach (Vector2 road in roadCrossings)
                {
                    if (best < 0 || Mathf.Abs(road.x - at) < Mathf.Abs(best - at)) { best = (int)road.x; roadHalf = road.y; }
                }
                if (best < 0 || Mathf.Abs(best - at) > Idx(40f)) return null;
                RoadGap(h, best, roadHalf, size, placed);
                placed.Extra = roadHalf;
                break;
            }
            case Feature.Doubles: Doubles(h, at, size, placed); break;
            case Feature.Rollers: Rollers(h, at, size, placed); break;
            case Feature.RockGarden: Rough(line, at, 40f * size, 0.17f * size, placed); break;
            case Feature.Roots: Rough(line, at, 30f * size, 0.1f * size, placed); break;
        }
        placed.Start = Mathf.Clamp(placed.Start, 0, n - 1);
        placed.End = Mathf.Clamp(placed.End, 0, n - 1);
        placed.Lip = Mathf.Clamp(placed.Lip, 0, n - 1);
        return placed;
    }

    /// <summary>
    /// Salto general: entrada que se aplana (junta altura sobre la bajada), rampa cóncava de "kickH" metros,
    /// mesa opcional y recepción que cae fuerte al principio y se suaviza al final (como una buena recepción).
    /// Nunca queda por debajo del perfil original: no hay que subir para salir.
    /// </summary>
    private static void Jump(float[] h, int lip, float kickH, float kickLen, float tableLen, float flatLen, float surplus, float landLen,
        TrailTerrainCarver.Placed placed)
    {
        int n = h.Length;
        int kick0 = Mathf.Max(1, lip - Idx(kickLen));
        int flat0 = Mathf.Max(0, kick0 - Idx(flatLen));
        int tableEnd = Mathf.Min(n - 2, lip + Idx(tableLen));
        // La entrada baja menos que el cerro, lo justo para juntar "surplus" metros sobre la bajada (no un acantilado).
        float approachLength = Mathf.Max(Sp, (kick0 - flat0) * Sp);
        float baseGrade = (h[flat0] - h[kick0]) / approachLength;
        float grade = Mathf.Max(ApproachGrade, baseGrade - surplus / approachLength);
        float[] p = (float[])h.Clone();
        float a0 = h[flat0];
        for (int i = flat0; i <= tableEnd; i++) p[i] = Mathf.Max(h[i], a0 - grade * Sp * (i - flat0));
        for (int i = kick0; i <= lip; i++)
        {
            float t = (i - kick0) / (float)Mathf.Max(1, lip - kick0);
            p[i] += kickH * t * t;
        }
        for (int i = lip + 1; i <= tableEnd; i++) p[i] += kickH;
        float top = p[tableEnd];
        // Recepción: lo bastante larga para que no pase de unos 45° al principio.
        int probe = Mathf.Min(n - 1, tableEnd + Idx(10f));
        float landGrade = probe > tableEnd ? (h[tableEnd] - h[probe]) / ((probe - tableEnd) * Sp) : 0f;
        float offset = top - h[tableEnd];
        float land = Mathf.Max(Mathf.Max(3f, landLen), 2f * offset / Mathf.Max(0.3f, 0.95f - landGrade));
        int end = Mathf.Min(n - 1, tableEnd + Idx(land));
        // La altura de más se va gastando (cuadrática): fuerte al principio, suave al final, siempre sobre el cerro.
        for (int i = tableEnd + 1; i <= end; i++)
        {
            float t = (i - tableEnd) / (float)Mathf.Max(1, end - tableEnd);
            p[i] = h[i] + offset * (1f - t) * (1f - t);
        }
        System.Array.Copy(p, h, n);
        placed.Start = flat0;
        placed.Lip = lip;
        placed.End = end;
    }

    /// <summary>Gap sobre el camino: rampa antes, el camino abajo y una loma de recepción pasándolo.</summary>
    private static void RoadGap(float[] h, int crossing, float roadHalf, float size, TrailTerrainCarver.Placed placed)
    {
        int n = h.Length;
        int lip = crossing - Idx(roadHalf + 1.5f);
        int kick0 = Mathf.Max(1, lip - Idx(5.5f));
        int flat0 = Mathf.Max(0, kick0 - Idx(10f));
        float[] p = (float[])h.Clone();
        float a0 = h[flat0];
        float grade = Mathf.Max(ApproachGrade, (h[flat0] - h[kick0]) / Mathf.Max(Sp, (kick0 - flat0) * Sp) - 1.2f / 10f);
        for (int i = flat0; i <= lip; i++) p[i] = Mathf.Max(h[i], a0 - grade * Sp * (i - flat0));
        for (int i = kick0; i <= lip; i++)
        {
            float t = (i - kick0) / (float)Mathf.Max(1, lip - kick0);
            p[i] += 1.5f * size * t * t;
        }
        // Detrás de la rampa cae enseguida al nivel del camino.
        int back = Mathf.Min(n - 1, lip + Idx(1f));
        for (int i = lip + 1; i <= back; i++) p[i] = Mathf.Lerp(p[lip], h[i], (i - lip) / (float)Mathf.Max(1, back - lip));
        // Loma de recepción después del camino.
        int land0 = Mathf.Min(n - 1, crossing + Idx(roadHalf + 1f));
        int landTop = Mathf.Min(n - 1, land0 + Idx(2.5f));
        int end = Mathf.Min(n - 1, landTop + Idx(11f));
        float mound = 0.9f * size;
        for (int i = land0; i <= landTop; i++)
        {
            float t = (i - land0) / (float)Mathf.Max(1, landTop - land0);
            p[i] = h[i] + mound * Mathf.Sqrt(t);
        }
        for (int i = landTop + 1; i <= end; i++)
        {
            float t = (i - landTop) / (float)Mathf.Max(1, end - landTop);
            p[i] = h[i] + mound * (1f - t) * (1f - t);
        }
        System.Array.Copy(p, h, n);
        placed.Start = flat0;
        placed.Lip = lip;
        placed.End = end;
    }

    /// <summary>Tres dobles seguidos: rampa, hueco al nivel del sendero y loma de recepción.</summary>
    private static void Doubles(float[] h, int at, float size, TrailTerrainCarver.Placed placed)
    {
        int n = h.Length;
        int cursor = at;
        placed.Start = Mathf.Max(0, at - Idx(4f));
        for (int k = 0; k < 3; k++)
        {
            int kick0 = cursor, lip = Mathf.Min(n - 2, kick0 + Idx(4f));
            float kickH = 1.1f * size;
            for (int i = kick0; i <= lip; i++)
            {
                float t = (i - kick0) / (float)Mathf.Max(1, lip - kick0);
                h[i] += kickH * t * t;
            }
            int back = Mathf.Min(n - 1, lip + Idx(1f));
            for (int i = lip + 1; i <= back; i++) h[i] += kickH * (1f - (i - lip) / (float)Mathf.Max(1, back - lip));
            if (k == 0) placed.Lip = lip;
            int land0 = Mathf.Min(n - 1, back + Idx(4.5f));
            int landTop = Mathf.Min(n - 1, land0 + Idx(2f));
            int end = Mathf.Min(n - 1, landTop + Idx(5.5f));
            float mound = 0.9f * size;
            for (int i = land0; i <= landTop; i++) h[i] += mound * Mathf.Sqrt((i - land0) / (float)Mathf.Max(1, landTop - land0));
            for (int i = landTop + 1; i <= end; i++)
            {
                float t = (i - landTop) / (float)Mathf.Max(1, end - landTop);
                h[i] += mound * (1f - t) * (1f - t);
            }
            cursor = end + Idx(3f);
            placed.End = end;
            if (cursor >= n - 2) break;
        }
    }

    /// <summary>Cinco lomas seguidas (para bombear o saltar de una a otra).</summary>
    private static void Rollers(float[] h, int at, float size, TrailTerrainCarver.Placed placed)
    {
        int n = h.Length;
        const float wave = 7.5f;
        int start = Mathf.Max(0, at - Idx(wave)), end = Mathf.Min(n - 1, start + Idx(wave * 5f));
        for (int i = start; i <= end; i++)
        {
            float s = Mathf.Sin(Mathf.PI * (i - start) * Sp / wave);
            h[i] += 0.55f * size * s * s;
        }
        placed.Start = start;
        placed.Lip = at;
        placed.End = end;
    }

    /// <summary>Pedregal o raíces: el piso queda con serrucho (la bici rebota y vibra).</summary>
    private static void Rough(Line line, int at, float length, float amplitude, TrailTerrainCarver.Placed placed)
    {
        int n = line.Points.Length;
        int start = Mathf.Max(0, at - Idx(length * 0.5f)), end = Mathf.Min(n - 1, at + Idx(length * 0.5f));
        int ramp = Idx(5f);
        for (int i = start; i <= end; i++)
        {
            float edge = Mathf.Min(i - start, end - i) / (float)Mathf.Max(1, ramp);
            line.Rough[i] = Mathf.Max(line.Rough[i], amplitude * Mathf.Clamp01(edge));
        }
        placed.Start = start;
        placed.Lip = at;
        placed.End = end;
    }

    private static void AutoJumps(Line line, int lineIndex, bool[] busy, bool[] protectedPoints, List<TrailTerrainCarver.Placed> placedHere)
    {
        Vector2[] pts = line.Points;
        float[] h = line.Heights;
        int n = pts.Length, k = Idx(22f), cycle = lineIndex % AutoCycle.Length;
        int i = k;
        while (i < n - k)
        {
            float chord = Vector2.Distance(pts[i - k], pts[i + k]);
            float straight = chord / (2f * k * Sp);
            float grade = (h[i - k] - h[i + k]) / (2f * k * Sp);
            if (straight < 0.97f || grade < 0.06f || grade > 0.75f) { i += 2; continue; }
            Feature type = AutoCycle[cycle % AutoCycle.Length];
            float before = type == Feature.Drop ? 22f : 14f;
            float after = type == Feature.Doubles ? 60f : (type == Feature.Rollers ? 40f : 22f);
            if (!Free(busy, i - Idx(before), i + Idx(after))) { i += 2; continue; }
            TrailTerrainCarver.Placed placed = Place(line, type, i, 1f, null, protectedPoints);
            if (placed != null)
            {
                placed.Line = lineIndex;
                placedHere.Add(placed);
                Mark(busy, placed.Start - Idx(FeatureGap), placed.End + Idx(FeatureGap));
                cycle++;
            }
            i += Idx(AutoSpacing);
        }
    }

    // =====================================================================
    // Peraltes y arcilla
    // =====================================================================

    /// <summary>
    /// Peralte en las curvas cerradas (radio menor a ~34 m): el lado de afuera sube, así se puede doblar
    /// rápido apoyado en la berma. No se peralta encima de los saltos ni en los cruces.
    /// </summary>
    private static void ComputeBank(Line line, List<int> fixedPoints, List<TrailTerrainCarver.Placed> placed)
    {
        Vector2[] p = line.Points;
        int n = p.Length, k = Idx(6f);
        float[] bank = new float[n];
        for (int i = k; i < n - k; i++)
        {
            Vector2 a = p[i - k], b = p[i], c = p[i + k];
            float ab = Vector2.Distance(a, b), bc = Vector2.Distance(b, c), ca = Vector2.Distance(c, a);
            float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) * 0.5f;
            float radius = area > 1e-4f ? ab * bc * ca / (4f * area) : 1e6f;
            float slope = 0.5f * Mathf.Clamp01((34f - radius) / 24f);
            // Girando a la izquierda (cross > 0) el lado de afuera es el derecho: se sube con peralte negativo.
            bank[i] = cross > 0f ? -slope : slope;
        }
        // Suavizado, para que el peralte entre y salga de a poco.
        int w = Idx(5f);
        float[] tmp = new float[n];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int j = Mathf.Max(0, i - w); j <= Mathf.Min(n - 1, i + w); j++) { sum += bank[j]; count++; }
                tmp[i] = sum / count;
            }
            System.Array.Copy(tmp, bank, n);
        }
        for (int i = 0; i < n; i++)
        {
            float fade = Mathf.Clamp01(Mathf.Min(i, n - 1 - i) * Sp / 10f);
            foreach (int f in fixedPoints) fade = Mathf.Min(fade, Mathf.Clamp01((Mathf.Abs(i - f) * Sp - 8f) / 10f));
            foreach (TrailTerrainCarver.Placed j in placed)
            {
                if (j.Type == Feature.RockGarden || j.Type == Feature.Roots) continue;
                if (i >= j.Start - Idx(4f) && i <= j.End + Idx(4f)) fade = 0f;
            }
            line.Bank[i] = bank[i] * fade;
        }
    }

    /// <summary>Tramos de curva bien peraltada: ahí el suelo es arcilla (rojiza y resbalosa).</summary>
    private static void ClayRanges(Line line, int lineIndex, TrailTerrainCarver.Result result)
    {
        int n = line.Points.Length, start = -1, gap = 0;
        for (int i = 0; i <= n; i++)
        {
            bool clay = i < n && Mathf.Abs(line.Bank[i]) > 0.2f;
            if (clay) { if (start < 0) start = i; gap = 0; continue; }
            if (start < 0) continue;
            if (i < n && ++gap < Idx(4f)) continue;
            int end = i - gap;
            if ((end - start) * Sp > 6f) result.Clay.Add(new Vector3Int(lineIndex, start, end));
            start = -1;
            gap = 0;
        }
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    private static int Idx(float meters) => Mathf.Max(1, Mathf.RoundToInt(meters / Sp));

    private static void Mark(bool[] busy, int from, int to)
    {
        for (int i = Mathf.Max(0, from); i <= Mathf.Min(busy.Length - 1, to); i++) busy[i] = true;
    }

    private static bool Free(bool[] busy, int from, int to)
    {
        if (from < 0 || to >= busy.Length) return false;
        for (int i = from; i <= to; i++) if (busy[i]) return false;
        return true;
    }

    private static int NearestIndex(Vector2[] points, Vector2 target)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = (points[i] - target).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }
}
