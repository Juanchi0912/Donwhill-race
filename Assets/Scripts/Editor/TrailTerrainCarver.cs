using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talla senderos en las alturas de un terreno sin aplanar la bajada: el sendero sigue la pendiente
/// del cerro (con acantilados y todo) y solo se nivela de lado a lado, como un sendero cavado en la ladera.
/// En las pistas terminadas además: perfil que siempre baja, saltos y drops (TrailFeatureShaper),
/// peraltes en las curvas cerradas, bordo marcado en la orilla, piedras que hacen vibrar,
/// cruces con caminos de tierra y puentes donde una ruta pasa sobre otra.
/// También calcula la distancia a los senderos para pintar la tierra y apartar los árboles.
/// </summary>
public static class TrailTerrainCarver
{
    public const float Spacing = 0.75f;          // Separación entre puntos de la línea central (m).
    private const float Shoulder = 4f;           // Borde que se mezcla con el terreno a cada lado (m).
    // Donde hay que cortar o rellenar mucho, el talud se ensancha (1,8 m por metro de corte, unos 29°)
    // para que quede una ladera y no una trinchera de paredes verticales.
    private const float ShoulderPerMeter = 1.8f;
    private const float MaxShoulder = 22f;
    private const float RutDepth = 0.3f;         // El centro queda un poco hundido, como un sendero gastado.
    private const float ProfileSmoothing = 4f;   // Suavizado de la línea central a lo largo (m): quita serrucho, no curvas.
    private const float RoadSmoothing = 14f;     // Los caminos son más parejos que los senderos.
    // Subida máxima permitida (8 %, se sube pedaleando). Las lomas más empinadas se rebajan arriba
    // y se rellenan antes, repartiendo el movimiento de tierra en vez de cavar una trinchera.
    private const float MaxUphillGrade = 0.08f;
    // Peso de los puntos fijos (cruces): obliga a que los tramos que se juntan empaten en altura.
    private const float PinWeight = 10000f;
    private const float JunctionBlend = 15f;     // Largo de la rampa que empata un tramo con su cruce (m).
    private const float RiverSink = 1.2f;        // El cauce queda 1,2 m bajo el terreno de alrededor.
    private const float RiverDepth = 0.8f;       // Y más hondo al centro: el agua junta en el medio.
    private const float BridgeClearance = 7.5f;  // Separación de las dos rutas en el cruce (m), antes de ajustar el tramo.
    private const float BridgeHeadroom = 5.5f;   // Altura libre bajo el tablero en todo el tramo (pasa el corredor parado y sobra).
    private const float BridgeGrade = 0.2f;      // El tablero baja suave (20 %) aunque el cerro sea mucho más empinado.
    private const float DeckDepth = 0.6f;        // Del perfil al fondo del tablero (tablas 0,35 m y el surco).
    private const float StepSmoothing = 0.6f;    // Cuánto se suavizan los escalones del sendero (0,6 = quedan al 40 %).
    private const float RimHeight = 0.16f;       // Bordo en la orilla: se nota dónde termina el sendero.

    /// <summary>Línea central de un tramo, en metros desde la esquina suroeste del terreno (x = este, y = norte).</summary>
    public class Line
    {
        public string Name;
        public float HalfWidth;
        public float MaxClimb;
        public float Sink;               // Cuánto se hunde el centro bajo el terreno (cauce de un río).
        public float Depth = RutDepth;   // Hondura de la sección de lado a lado.
        public Vector2[] Points;
        public float[] Heights;          // Altura del centro (m) después de tallar.
        public bool Locked;              // Parte real que no se retoca (círculos grises).
        public bool IsRoad;              // Camino de tierra: sigue el terreno suavizado, sin saltos.
        public bool Rim;                 // Bordo marcado en la orilla.
        public int Segment = -1;         // Índice del tramo en el conjunto (o del camino).
        public RealMapTrails.Segment Source;
        public float[] Bank;             // Peralte (pendiente de lado a lado, + = sube el lado izquierdo).
        public float[] Rough;            // Piedras y raíces: amplitud del serrucho (m).
        public bool[] NoCarve;           // Puntos que no se tallan: bajo un puente pasa otra ruta.
        public List<LocalSpot> Spots = new List<LocalSpot>();
    }

    /// <summary>Salto u obstáculo pedido, ya en metros del terreno.</summary>
    public class LocalSpot
    {
        public RealMapTrails.Feature Type;
        public Vector2 At;
        public float Size;
    }

    /// <summary>Salto u obstáculo ya tallado, para ponerle la ambientación (madera, rocas, pancartas).</summary>
    public class Placed
    {
        public RealMapTrails.Feature Type;
        public int Line;
        public int Start, Lip, End;      // Índices de puntos: dónde empieza, el borde de la rampa y dónde termina.
        public float Size;
        public float Extra;              // RoadGap: medio ancho del camino. FenceGap: distancia de la cerca al borde (m).
    }

    /// <summary>Donde dos líneas se cruzan (camino, puente o cruce a nivel).</summary>
    public class Crossing
    {
        public int A, B;                 // A va antes en la lista (B se ajusta a A).
        public int IndexA, IndexB;
        public bool Bridge;
        public int Upper = -1, Lower = -1, UpperFrom, UpperTo;   // Puente: la línea de arriba y su tramo sin tallar.
    }

    public class Result
    {
        public readonly List<Placed> Features = new List<Placed>();
        public readonly List<Crossing> Crossings = new List<Crossing>();
        public readonly List<Vector3Int> Clay = new List<Vector3Int>();   // (línea, desde, hasta): curvas de arcilla.
    }

    /// <summary>Pasa los tramos (coordenadas de imagen) al terreno recortado, redondea las esquinas y remuestrea.</summary>
    public static List<Line> BuildLines(List<RealMapTrails.Segment> segments, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ)
    {
        List<Line> lines = new List<Line>();
        for (int k = 0; k < segments.Count; k++)
        {
            RealMapTrails.Segment segment = segments[k];
            Line line = new Line
            {
                Name = segment.Name, HalfWidth = segment.HalfWidth, MaxClimb = segment.MaxClimb, Segment = k, Source = segment,
                Locked = segment.Locked, Rim = !segment.Locked, Points = Smoothed(segment.Image, cropX, cropZ, sizeX, sizeZ)
            };
            foreach (RealMapTrails.Spot spot in segment.Spots)
            {
                line.Spots.Add(new LocalSpot { Type = spot.Type, Size = spot.Size, At = ImageToLocal(spot.At, cropX, cropZ, sizeX, sizeZ) });
            }
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Caminos de tierra: planos de lado a lado y sin límite de subida (siguen el terreno suavizado).</summary>
    public static List<Line> BuildRoadLines(List<RealMapTrails.Segment> roads, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ)
    {
        List<Line> lines = new List<Line>();
        for (int k = 0; k < roads.Count; k++)
        {
            lines.Add(new Line { Name = roads[k].Name, HalfWidth = roads[k].HalfWidth, MaxClimb = 100f, Depth = 0.05f, IsRoad = true, Segment = k,
                Source = roads[k], Points = Smoothed(roads[k].Image, cropX, cropZ, sizeX, sizeZ) });
        }
        return lines;
    }

    /// <summary>
    /// Ríos como líneas para tallar: el cauce va hundido bajo el terreno y bien cóncavo,
    /// y no tiene límite de subida (el agua corre para cualquier lado del trazo).
    /// </summary>
    public static List<Line> BuildRiverLines(List<RealMapTrails.River> rivers, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ)
    {
        List<Line> lines = new List<Line>();
        foreach (RealMapTrails.River river in rivers)
        {
            lines.Add(new Line { Name = river.Name, HalfWidth = river.HalfWidth, MaxClimb = 100f, Sink = RiverSink, Depth = RiverDepth,
                Points = Smoothed(river.Image, cropX, cropZ, sizeX, sizeZ) });
        }
        return lines;
    }

    /// <summary>Punto de la imagen (0–1, y desde el norte) a metros dentro del terreno recortado.</summary>
    public static Vector2 ImageToLocal(Vector2 p, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ)
    {
        float tu = (p.x - cropX.x) / Mathf.Max(0.01f, cropX.y - cropX.x);
        float tz = ((1f - p.y) - cropZ.x) / Mathf.Max(0.01f, cropZ.y - cropZ.x);
        return new Vector2(tu * sizeX, tz * sizeZ);
    }

    private static Vector2[] Smoothed(Vector2[] image, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ)
    {
        List<Vector2> points = new List<Vector2>();
        foreach (Vector2 p in image) points.Add(ImageToLocal(p, cropX, cropZ, sizeX, sizeZ));
        // Las esquinas del trazo se vuelven curvas (tres pasadas de Chaikin).
        for (int pass = 0; pass < 3; pass++) points = Chaikin(points);
        return Resample(points, Spacing);
    }

    /// <summary>Talla todas las líneas en "heights" (en metros, [z, x], cubre sizeX × sizeZ).</summary>
    public static void Carve(float[,] heights, float sizeX, float sizeZ, List<Line> lines)
    {
        Carve(heights, sizeX, sizeZ, lines, null);
    }

    /// <summary>
    /// Talla todas las líneas. Los caminos van primero en la lista; los senderos que los cruzan empatan su altura.
    /// "bridges" son los puentes del conjunto (índices de tramos). Devuelve los saltos y cruces para la ambientación.
    /// </summary>
    public static Result Carve(float[,] heights, float sizeX, float sizeZ, List<Line> lines, List<RealMapTrails.Bridge> bridges)
    {
        Result result = new Result();
        int res = heights.GetLength(0);
        float[,] original = (float[,])heights.Clone();

        // Primera pasada: perfil de cada línea. Con ella se ven las alturas donde se cruzan.
        ComputeProfiles(original, sizeX, sizeZ, lines, null);
        Dictionary<int, List<Vector2>> pins = new Dictionary<int, List<Vector2>>();   // línea → (índice, altura)
        foreach (Crossing c in FindCrossings(lines, bridges))
        {
            result.Crossings.Add(c);
            if (c.Bridge)
            {
                int iu = c.Upper == c.A ? c.IndexA : c.IndexB, il = c.Upper == c.A ? c.IndexB : c.IndexA;
                float hu = lines[c.Upper].Heights[iu], hl = lines[c.Lower].Heights[il];
                if (hu - hl < BridgeClearance + 0.6f)
                {
                    // Se separan las dos: la de arriba sube en un terraplén y la de abajo pasa en un corte.
                    float mid = (hu + hl) * 0.5f, half = (BridgeClearance + 0.6f) * 0.5f;
                    hu = mid + half;
                    hl = mid - half;
                }
                AddPin(pins, c.Upper, iu, hu);
                AddPin(pins, c.Lower, il, hl);
            }
            else
            {
                AddPin(pins, c.B, c.IndexB, lines[c.A].Heights[c.IndexA]);
            }
        }

        // Segunda pasada con los cruces fijos.
        if (pins.Count > 0) ComputeProfiles(original, sizeX, sizeZ, lines, pins);

        // Puentes: el tablero va casi plano en todo su largo y la ruta de abajo pasa en un corte con altura libre
        // en todo el tramo que queda bajo las tablas (no solo en el punto del cruce: se cruzan en diagonal).
        bool bridges2 = false;
        foreach (Crossing c in result.Crossings)
        {
            if (!c.Bridge) continue;
            Line upper = lines[c.Upper], lower = lines[c.Lower];
            int iu = c.Upper == c.A ? c.IndexA : c.IndexB, il = c.Upper == c.A ? c.IndexB : c.IndexA;
            BridgeSpan(c, upper, lower, iu, il);
            float hu = upper.Heights[iu];
            pins[c.Upper].RemoveAll(q => (int)q.x == iu);
            for (int k = c.UpperFrom; k <= c.UpperTo; k++) AddPin(pins, c.Upper, k, hu - BridgeGrade * (k - iu) * Spacing);
            float minRequired = float.MaxValue;
            int k0 = -1, k1 = -1;
            for (int k = 0; k < lower.Points.Length; k++)
            {
                for (int j = c.UpperFrom; j <= c.UpperTo; j++)
                {
                    if ((upper.Points[j] - lower.Points[k]).sqrMagnitude > (upper.HalfWidth + 1.5f) * (upper.HalfWidth + 1.5f)) continue;
                    float deck = hu - BridgeGrade * (j - iu) * Spacing;
                    minRequired = Mathf.Min(minRequired, deck - DeckDepth - BridgeHeadroom + RutDepth);
                    if (k0 < 0) k0 = k;
                    k1 = k;
                }
            }
            if (k0 < 0) continue;
            pins[c.Lower].RemoveAll(q => (int)q.x == il);
            k0 = Mathf.Max(0, k0 - 3);
            k1 = Mathf.Min(lower.Points.Length - 1, k1 + 3);
            for (int k = k0; k <= k1; k++) AddPin(pins, c.Lower, k, minRequired - 0.04f * (k - k0) * Spacing);
            bridges2 = true;
        }
        if (bridges2) ComputeProfiles(original, sizeX, sizeZ, lines, pins);

        // Saltos, drops, peraltes y pedregales (solo en las pistas terminadas, no en las partes reales).
        for (int k = 0; k < lines.Count; k++)
        {
            Line line = lines[k];
            int n = line.Points.Length;
            line.Bank = new float[n];
            line.Rough = new float[n];
            line.NoCarve = new bool[n];
            if (line.Locked || line.IsRoad || line.Sink > 0f || line.Source == null) continue;
            List<int> fixedPoints = new List<int>();
            List<Vector2> roadCrossings = new List<Vector2>();   // (índice, medio ancho del camino)
            if (pins.TryGetValue(k, out List<Vector2> own)) foreach (Vector2 p in own) fixedPoints.Add((int)p.x);
            foreach (Crossing c in result.Crossings)
            {
                if (c.B == k && lines[c.A].IsRoad) roadCrossings.Add(new Vector2(c.IndexB, lines[c.A].HalfWidth));
                if (c.A == k) fixedPoints.Add(c.IndexA);
            }
            TrailFeatureShaper.Shape(line, k, fixedPoints, roadCrossings, result);
        }

        // Bajo el puente manda la ruta de abajo: la de arriba no se talla ahí (va sobre las tablas).
        foreach (Crossing c in result.Crossings)
        {
            if (!c.Bridge) continue;
            Line upper = lines[c.Upper], lower = lines[c.Lower];
            for (int i = c.UpperFrom; i <= c.UpperTo; i++) upper.NoCarve[i] = true;
        }

        float maxHalf = 0f;
        foreach (Line line in lines) maxHalf = Mathf.Max(maxHalf, line.HalfWidth);
        Nearest(lines, res, sizeX, sizeZ, maxHalf + MaxShoulder, true, out float[,] edge, out int[,] nearest);
        float cellX = sizeX / (res - 1), cellZ = sizeZ / (res - 1);
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                float e = edge[z, x];
                if (e > MaxShoulder) continue;
                Line line = lines[nearest[z, x] >> 24];
                int i = nearest[z, x] & 0xFFFFFF;
                float half = line.HalfWidth;
                bool simple = line.Locked || line.Sink > 0f || line.Bank == null;
                Vector2 cell = new Vector2(x * cellX, z * cellZ);
                // En lo empinado el sendero queda en escalones cada 75 cm (le da carácter, se sienten las piedras).
                // Se dejan al 40 % de su altura: sigue vibrando pero la bici ya no salta sola a cada rato.
                int j = NeighborAlong(line.Points, i, cell, out float t);
                float target = Mathf.Lerp(line.Heights[i], Mathf.Lerp(line.Heights[i], line.Heights[j], t), StepSmoothing);
                float rough = simple ? 0f : Mathf.Lerp(line.Rough[i], line.Rough[j], t);

                if (!simple)
                {
                    // Peralte: el lado de afuera de la curva sube, el de adentro baja un poco.
                    Vector2 tangent = Tangent(line.Points, i);
                    float lateral = Vector2.Dot(cell - line.Points[i], new Vector2(-tangent.y, tangent.x));
                    target += Mathf.Lerp(line.Bank[i], line.Bank[j], t) * Mathf.Clamp(lateral, -half - 1f, half + 1f);
                }

                float weight;
                if (e <= 0f)
                {
                    float across = (e + half) / Mathf.Max(0.1f, half);
                    target -= line.Depth * (1f - across * across);
                    if (!simple)
                    {
                        if (line.Rim) target += RimHeight * Mathf.Clamp01(1f - Mathf.Abs(e + 0.35f) / 0.45f);
                        if (rough > 0f) target += rough * RockNoise(x * cellX, z * cellZ);
                    }
                    weight = 1f;
                }
                else
                {
                    float shoulder = simple ? Shoulder : Mathf.Clamp(Mathf.Abs(target - original[z, x]) * ShoulderPerMeter, Shoulder, MaxShoulder);
                    if (e >= shoulder) continue;
                    weight = 1f - Mathf.SmoothStep(0f, 1f, e / shoulder);
                }
                heights[z, x] = Mathf.Lerp(original[z, x], target, weight);
            }
        }
        return result;
    }

    /// <summary>
    /// Distancia (m) desde cada celda de una cuadrícula res × res al borde del sendero más cercano.
    /// Negativo = dentro del sendero; float.MaxValue = más lejos que "reach".
    /// </summary>
    public static float[,] EdgeDistance(List<Line> lines, int res, float sizeX, float sizeZ, float reach)
    {
        Nearest(lines, res, sizeX, sizeZ, reach, false, out float[,] edge, out _);
        return edge;
    }

    /// <summary>Dirección de la línea en el punto i (normalizada).</summary>
    public static Vector2 Tangent(Vector2[] points, int i)
    {
        Vector2 d = points[Mathf.Min(points.Length - 1, i + 2)] - points[Mathf.Max(0, i - 2)];
        return d.sqrMagnitude > 1e-8f ? d.normalized : new Vector2(0f, 1f);
    }

    /// <summary>
    /// Tramo de la ruta de arriba que va sobre tablas. Si se cruzan en diagonal, la ruta de abajo ocupa más largo
    /// bajo el puente: el tablero se alarga.
    /// </summary>
    private static void BridgeSpan(Crossing c, Line upper, Line lower, int iu, int il)
    {
        Vector2 tu = Tangent(upper.Points, iu), tl = Tangent(lower.Points, il);
        float sin = Mathf.Max(0.35f, Mathf.Abs(tu.x * tl.y - tu.y * tl.x));
        float cut = BridgeHeadroom * 0.5f;
        int span = Mathf.CeilToInt((lower.HalfWidth + Mathf.Max(Shoulder, cut * ShoulderPerMeter) + 3f) / sin / Spacing);
        c.UpperFrom = Mathf.Max(0, iu - span);
        c.UpperTo = Mathf.Min(upper.Points.Length - 1, iu + span);
    }

    /// <summary>El punto vecino (antes o después de i) hacia donde cae la celda, y cuánto avanza hacia él (0–1).</summary>
    private static int NeighborAlong(Vector2[] points, int i, Vector2 cell, out float t)
    {
        t = 0f;
        Vector2 d = cell - points[i];
        if (i + 1 < points.Length)
        {
            Vector2 seg = points[i + 1] - points[i];
            float along = Vector2.Dot(d, seg) / Mathf.Max(1e-6f, seg.sqrMagnitude);
            if (along > 0f) { t = Mathf.Min(1f, along); return i + 1; }
        }
        if (i > 0)
        {
            Vector2 seg = points[i - 1] - points[i];
            float along = Vector2.Dot(d, seg) / Mathf.Max(1e-6f, seg.sqrMagnitude);
            if (along > 0f) { t = Mathf.Min(1f, along); return i - 1; }
        }
        return i;
    }

    private static void AddPin(Dictionary<int, List<Vector2>> pins, int line, int index, float height)
    {
        if (!pins.TryGetValue(line, out List<Vector2> list)) pins[line] = list = new List<Vector2>();
        list.Add(new Vector2(index, height));
    }

    private static void ComputeProfiles(float[,] original, float sizeX, float sizeZ, List<Line> lines, Dictionary<int, List<Vector2>> pins)
    {
        List<Vector2> junctions = new List<Vector2>();
        List<float> junctionHeights = new List<float>();
        for (int k = 0; k < lines.Count; k++)
        {
            List<Vector2> own = null;
            if (pins != null) pins.TryGetValue(k, out own);
            lines[k].Heights = CenterProfile(original, sizeX, sizeZ, lines, k, junctions, junctionHeights, own);
        }
    }

    private static float[] CenterProfile(float[,] heights, float sizeX, float sizeZ, List<Line> lines, int lineIndex,
        List<Vector2> junctions, List<float> junctionHeights, List<Vector2> pins)
    {
        Line line = lines[lineIndex];
        int n = line.Points.Length;
        float[] h = new float[n];
        for (int i = 0; i < n; i++) h[i] = SampleBilinear(heights, line.Points[i].x / sizeX, line.Points[i].y / sizeZ);

        // Suavizado a lo largo: el heightmap tiene ruido que se sentiría como serrucho bajo las ruedas.
        int window = Mathf.Max(1, Mathf.RoundToInt((line.IsRoad ? RoadSmoothing : ProfileSmoothing) / Spacing));
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
        if (line.IsRoad) return h;

        // Donde el tramo empieza o termina en un cruce ya calculado (o sobre otra línea), se fija esa altura.
        bool startPinned = TryJunction(lines, lineIndex, junctions, junctionHeights, line.Points[0], out float startHeight);
        bool endPinned = TryJunction(lines, lineIndex, junctions, junctionHeights, line.Points[n - 1], out float endHeight);
        float[] weights = new float[n];
        for (int i = 0; i < n; i++) weights[i] = 1f;
        if (startPinned) { h[0] = startHeight; weights[0] = PinWeight; }
        if (endPinned) { h[n - 1] = endHeight; weights[n - 1] = PinWeight; }
        if (pins != null)
        {
            foreach (Vector2 pin in pins)
            {
                int i = Mathf.Clamp((int)pin.x, 0, n - 1);
                h[i] = pin.y;
                weights[i] = PinWeight;
            }
        }

        h = FitDescending(h, weights, Spacing, line.MaxClimb);
        // En los cruces la altura fija puede quedar distinta del terreno de al lado: se reparte en una rampa.
        if (startPinned) BlendEnd(h, true);
        if (endPinned) BlendEnd(h, false);
        if (pins != null)
        {
            foreach (Vector2 pin in pins)
            {
                // Solo se suaviza en los bordes de un grupo de puntos fijos seguidos (el tablero de un puente).
                int i = (int)pin.x;
                bool before = pins.Exists(q => (int)q.x == i - 1), after = pins.Exists(q => (int)q.x == i + 1);
                if (before && after) continue;
                // Al terminar un tablero alto sobre una bajada empinada: recepción larga, como bajar de un drop.
                if (before && i + 1 < n && h[i] - h[i + 1] > 0.5f) LandAfter(h, i);
                else BlendAround(h, i);
            }
            foreach (Vector2 pin in pins) h[Mathf.Clamp((int)pin.x, 0, n - 1)] = pin.y;
        }

        if (!startPinned) { junctions.Add(line.Points[0]); junctionHeights.Add(h[0]); }
        if (!endPinned) { junctions.Add(line.Points[n - 1]); junctionHeights.Add(h[n - 1]); }
        if (line.Sink > 0f)
        {
            for (int i = 0; i < n; i++) h[i] -= line.Sink;
        }
        return h;
    }

    /// <summary>Altura de un cruce ya calculado en ese punto, o de otra línea (atajos que salen de una ruta).</summary>
    private static bool TryJunction(List<Line> lines, int lineIndex, List<Vector2> junctions, List<float> junctionHeights, Vector2 point, out float height)
    {
        int j = FindJunction(junctions, point);
        if (j >= 0)
        {
            height = junctionHeights[j];
            return true;
        }
        for (int k = 0; k < lineIndex; k++)
        {
            Line other = lines[k];
            if (other.IsRoad || other.Sink > 0f || other.Heights == null) continue;
            for (int i = 0; i < other.Points.Length; i++)
            {
                if ((other.Points[i] - point).sqrMagnitude < 4f * 4f)
                {
                    height = other.Heights[i];
                    return true;
                }
            }
        }
        height = 0f;
        return false;
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

    /// <summary>Suaviza alrededor de un punto fijo en medio del tramo (cruce o puente): sin escalones.</summary>
    private static void BlendAround(float[] h, int pin)
    {
        int n = h.Length, k = Mathf.RoundToInt(JunctionBlend / Spacing);
        int a = Mathf.Max(0, pin - k), b = Mathf.Min(n - 1, pin + k);
        if (pin - a < 2 || b - pin < 2) return;
        float ha = h[a], hp = h[pin], hb = h[b];
        for (int i = a + 1; i < b; i++)
        {
            if (i == pin) continue;
            float straight = i < pin ? Mathf.Lerp(ha, hp, (i - a) / (float)(pin - a)) : Mathf.Lerp(hp, hb, (i - pin) / (float)(b - pin));
            float t = i < pin ? (pin - i) / (float)(pin - a) : (i - pin) / (float)(b - pin);
            h[i] = Mathf.Lerp(straight, h[i], Mathf.SmoothStep(0f, 1f, t));
        }
    }

    /// <summary>Recepción después de un punto fijo alto: la altura de más se va gastando (fuerte al principio, suave al final).</summary>
    private static void LandAfter(float[] h, int e)
    {
        int n = h.Length;
        float offset = h[e] - h[e + 1];
        int probe = Mathf.Min(n - 1, e + 1 + Mathf.RoundToInt(10f / Spacing));
        float grade = probe > e + 1 ? (h[e + 1] - h[probe]) / ((probe - e - 1) * Spacing) : 0f;
        float length = Mathf.Max(12f, 2f * offset / Mathf.Max(0.25f, 0.95f - grade));
        int end = Mathf.Min(n - 1, e + Mathf.RoundToInt(length / Spacing));
        for (int i = e + 1; i <= end; i++)
        {
            float t = (i - e) / (float)Mathf.Max(1, end - e);
            h[i] += offset * (1f - t) * (1f - t);
        }
    }

    /// <summary>
    /// El perfil más parecido al terreno (mínimos cuadrados) que nunca sube más que "maxGrade".
    /// Se resuelve con regresión isotónica (Pool Adjacent Violators) sobre h − maxGrade·distancia,
    /// que debe ir siempre hacia abajo. Las bajadas, incluso las muy empinadas, quedan tal cual.
    /// Con "maxGrade" negativo el tramo baja siempre, al menos ese porcentaje.
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

    /// <summary>
    /// Cruces entre líneas (sin contar donde una sale de otra). Si el par es un puente se marca;
    /// si no, la línea que va después empata la altura de la primera (cruce a nivel, como con el camino).
    /// </summary>
    private static List<Crossing> FindCrossings(List<Line> lines, List<RealMapTrails.Bridge> bridges)
    {
        List<Crossing> found = new List<Crossing>();
        const int stride = 4;            // Se busca cada 3 m y después se afina.
        const float endMargin = 14f;     // Cerca de los extremos las líneas se juntan (cruces de salida), no se cruzan.
        for (int a = 0; a < lines.Count; a++)
        {
            for (int b = a + 1; b < lines.Count; b++)
            {
                Line la = lines[a], lb = lines[b];
                if (la.Sink > 0f || lb.Sink > 0f || (la.Locked && lb.Locked) || (la.IsRoad && lb.IsRoad)) continue;
                Vector2[] pa = la.Points, pb = lb.Points;
                for (int i = 0; i + stride < pa.Length; i += stride)
                {
                    for (int j = 0; j + stride < pb.Length; j += stride)
                    {
                        if (!SegmentsCross(pa[i], pa[i + stride], pb[j], pb[j + stride], out float ta, out float tb)) continue;
                        int ia = Mathf.Clamp(i + Mathf.RoundToInt(ta * stride), 0, pa.Length - 1);
                        int ib = Mathf.Clamp(j + Mathf.RoundToInt(tb * stride), 0, pb.Length - 1);
                        if (NearEnd(pa, ia, endMargin) || NearEnd(pb, ib, endMargin)) continue;
                        // El mismo cruce puede aparecer dos veces en tramos vecinos.
                        if (found.Exists(o => o.A == a && o.B == b && (pa[o.IndexA] - pa[ia]).sqrMagnitude < 100f)) continue;
                        Crossing c = new Crossing { A = a, B = b, IndexA = ia, IndexB = ib };
                        if (bridges != null)
                        {
                            foreach (RealMapTrails.Bridge bridge in bridges)
                            {
                                if (la.IsRoad || lb.IsRoad) continue;
                                bool match = (bridge.Upper == la.Segment && bridge.Lower == lb.Segment) || (bridge.Upper == lb.Segment && bridge.Lower == la.Segment);
                                if (!match) continue;
                                c.Bridge = true;
                                c.Upper = bridge.Upper == la.Segment ? a : b;
                                c.Lower = c.Upper == a ? b : a;
                            }
                        }
                        found.Add(c);
                    }
                }
            }
        }
        return found;
    }

    private static bool NearEnd(Vector2[] points, int i, float margin)
    {
        return (points[i] - points[0]).sqrMagnitude < margin * margin || (points[i] - points[points.Length - 1]).sqrMagnitude < margin * margin;
    }

    private static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out float t, out float u)
    {
        t = u = 0f;
        Vector2 r = p2 - p1, s = q2 - q1;
        float denom = r.x * s.y - r.y * s.x;
        if (Mathf.Abs(denom) < 1e-6f) return false;
        Vector2 qp = q1 - p1;
        t = (qp.x * s.y - qp.y * s.x) / denom;
        u = (qp.x * r.y - qp.y * r.x) / denom;
        return t >= 0f && t < 1f && u >= 0f && u < 1f;
    }

    /// <summary>
    /// Para cada celda: distancia al borde del sendero más cercano y cuál es (línea en los 8 bits altos, punto en los 24 bajos).
    /// Los puntos marcados NoCarve se saltan (bajo el puente manda la ruta de abajo).
    /// </summary>
    private static void Nearest(List<Line> lines, int res, float sizeX, float sizeZ, float reach, bool wantIndex,
        out float[,] edge, out int[,] nearest)
    {
        edge = new float[res, res];
        nearest = wantIndex ? new int[res, res] : null;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++) edge[z, x] = float.MaxValue;
        }
        float cellX = sizeX / (res - 1), cellZ = sizeZ / (res - 1);
        for (int li = 0; li < lines.Count; li++)
        {
            Line line = lines[li];
            float radius = reach + line.HalfWidth;
            int rx = Mathf.CeilToInt(radius / cellX), rz = Mathf.CeilToInt(radius / cellZ);
            // En lo largo de la línea basta con revisar uno de cada pocos puntos cerca del centro de cada celda.
            for (int i = 0; i < line.Points.Length; i++)
            {
                if (line.NoCarve != null && line.NoCarve[i]) continue;
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
                        if (wantIndex) nearest[z, x] = (li << 24) | i;
                    }
                }
            }
        }
    }

    /// <summary>Serrucho de piedras y raíces (−1 a 1): dos escalas de ruido, una de piedras grandes y otra de chicas.</summary>
    private static float RockNoise(float x, float z)
    {
        float a = Mathf.PerlinNoise(x * 0.9f + 11.3f, z * 0.9f + 7.1f) - 0.5f;
        float b = Mathf.PerlinNoise(x * 1.5f + 3.7f, z * 1.5f + 19.9f) - 0.5f;
        return Mathf.Clamp(a * 1.3f + b * 0.9f, -1f, 1f);
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
