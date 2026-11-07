using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Forma de la pista de BMX La Concordia: trazado, saltos, peraltes, niveles y colores, sin nada del Editor
/// (así se puede probar fuera de Unity). Como en la pista real: rectas rojas con lomas grandes de todo el ancho
/// y bien separadas, curvas de asfalto negro con peralte, líneas blancas en los bordes, rampa de salida de cemento
/// y cada recta un poco más abajo que la anterior, con un muro de piedra entre recta y recta.
/// </summary>
public class BmxTrackShape
{
    public const float MetersPerPixel = 0.1052f;  // Medido con la longitud que da Google Earth (316 m).
    // La pista real es muy corta para la velocidad del juego: se agranda pareja para que haya espacio entre saltos.
    public const float GameScale = 1.35f;
    public const float Margin = 10f;
    public const float HalfWidth = 4.7f;          // 9,4 m de ancho, como las rectas de la foto.
    private const float Step = 0.25f;
    private const float BermHeight = 2.2f;        // Borde de afuera de las curvas de asfalto.
    private const float TotalDrop = 5f;           // De la salida a la meta la pista baja en terrazas.
    private const float EdgeFalloff = 1.2f;       // De la pista al suelo de al lado.
    private const float LineWidth = 0.22f;        // Líneas blancas de los bordes.

    public enum Kind { StartHill, Rollers, Double, Table }

    public class Feature
    {
        public Kind Kind;
        public float From, To, Height, Period;
    }

    public enum Surface { Grass, Red, Asphalt, White, Concrete }

    public readonly Vector2[] Points;     // Metros en el terreno (x este, y norte).
    public readonly float[] Distance;
    public readonly float[] Curvature;    // Con signo: + gira a la izquierda.
    public readonly List<Feature> Features = new List<Feature>();
    public readonly float SizeX, SizeZ;
    public float Length => Distance[Distance.Length - 1];

    private readonly List<int>[] cells;
    private readonly int gridW, gridH;
    private const float Cell = 2f;

    /// <summary>Trazado marcado sobre la foto aérea (píxeles, norte arriba): sale arriba a la derecha y zigzaguea.</summary>
    private static readonly int[] Pixels =
    {
        646,75, 465,97, 428,101, 371,92, 341,102, 300,102, 261,104, 208,111, 153,125, 100,145, 98,188,
        150,244, 216,244, 262,215, 331,212, 391,212, 455,213, 508,213, 565,216, 597,231, 613,247, 603,293, 589,309,
        551,331, 488,338, 409,341, 343,345, 178,351, 104,376, 118,438, 190,440, 272,445, 343,446, 411,444, 467,436,
        545,430, 634,427, 683,455, 677,495, 593,520, 535,528, 464,521, 410,521, 260,526, 187,530, 107,528, 38,529,
    };

    public BmxTrackShape()
    {
        // Saltos en metros del juego (ya agrandados), con 6–8 m planos entre uno y otro para tomar velocidad.
        // Recta 1: rampa de salida, un doble grande y una loma.
        Add(Kind.StartHill, 0f, 20f, 4.5f, 7f);
        Add(Kind.Double, 30f, 44f, 1.5f);
        Add(Kind.Rollers, 50f, 58f, 1.2f, 8f);
        // Recta 2: mesa y doble.
        Add(Kind.Table, 110f, 124f, 1.4f);
        Add(Kind.Double, 131f, 145f, 1.6f);
        // Recta 3: tres lomas seguidas (ritmo) y un doble.
        Add(Kind.Rollers, 186f, 211f, 1.1f, 8.3f);
        Add(Kind.Double, 220f, 234f, 1.5f);
        // Recta 4: doble, mesa y una loma.
        Add(Kind.Double, 270f, 284f, 1.6f);
        Add(Kind.Table, 291f, 305f, 1.4f);
        Add(Kind.Rollers, 312f, 320f, 1.1f, 8f);
        // Recta 5: tres lomas, un doble y el sprint a la meta.
        Add(Kind.Rollers, 358f, 383f, 1f, 8.3f);
        Add(Kind.Double, 392f, 406f, 1.5f);

        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        for (int i = 0; i < Pixels.Length; i += 2)
        {
            minX = Mathf.Min(minX, Pixels[i]); maxX = Mathf.Max(maxX, Pixels[i]);
            minY = Mathf.Min(minY, Pixels[i + 1]); maxY = Mathf.Max(maxY, Pixels[i + 1]);
        }
        float k = MetersPerPixel * GameScale;
        SizeX = (maxX - minX) * k + Margin * 2f;
        SizeZ = (maxY - minY) * k + Margin * 2f;
        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < Pixels.Length; i += 2) points.Add(new Vector2((Pixels[i] - minX) * k + Margin, (maxY - Pixels[i + 1]) * k + Margin));
        for (int pass = 0; pass < 3; pass++) points = Chaikin(points);
        Points = TrailTerrainCarver.Resample(points, Step);
        int n = Points.Length;
        Distance = new float[n];
        for (int i = 1; i < n; i++) Distance[i] = Distance[i - 1] + Vector2.Distance(Points[i - 1], Points[i]);
        Curvature = SmoothedCurvature(Points);

        gridW = Mathf.CeilToInt(SizeX / Cell) + 1;
        gridH = Mathf.CeilToInt(SizeZ / Cell) + 1;
        cells = new List<int>[gridW * gridH];
        for (int i = 0; i < n; i++)
        {
            int c = Mathf.Clamp((int)(Points[i].y / Cell), 0, gridH - 1) * gridW + Mathf.Clamp((int)(Points[i].x / Cell), 0, gridW - 1);
            if (cells[c] == null) cells[c] = new List<int>();
            cells[c].Add(i);
        }
    }

    private void Add(Kind kind, float from, float to, float height, float period = 0f)
    {
        Features.Add(new Feature { Kind = kind, From = from, To = to, Height = height, Period = period });
    }

    // =====================================================================
    // Consultas
    // =====================================================================

    /// <summary>Altura del terreno en un punto (m, sobre el punto más bajo de la meta).</summary>
    public float HeightAt(Vector2 p)
    {
        int i = Nearest(p, out float lateral);
        float baseLevel = Base(i);
        float edge = Mathf.Abs(lateral) - HalfWidth;
        float surface = baseLevel + FeatureHeight(Distance[i]) + Bank(i, Mathf.Clamp(lateral, -HalfWidth, HalfWidth));
        if (edge <= 0f) return surface;
        // Fuera de la pista: el suelo queda al nivel de su recta (terraza); entre una recta y la de abajo,
        // el cambio de nivel es el muro de piedra.
        return Mathf.Lerp(surface, baseLevel, Mathf.SmoothStep(0f, 1f, edge / EdgeFalloff));
    }

    /// <summary>De qué es el suelo en un punto: pasto, rojo (rectas y saltos), asfalto (curvas), línea blanca o cemento (salida).</summary>
    public Surface SurfaceAt(Vector2 p, out float trackAmount)
    {
        int i = Nearest(p, out float lateral);
        float edge = Mathf.Abs(lateral) - HalfWidth;
        trackAmount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.25f, edge));
        if (trackAmount <= 0f) return Surface.Grass;
        if (Distance[i] < StartRampEnd) return Surface.Concrete;
        // Línea blanca pegada a cada borde.
        if (edge > -LineWidth - 0.05f && edge < 0.05f) return Surface.White;
        // La recta de llegada es roja hasta el final (el extremo no es curva).
        if (Distance[i] > Length - 15f) return Surface.Red;
        return BermWeight(Curvature[i]) > 0.35f ? Surface.Asphalt : Surface.Red;
    }

    /// <summary>Índice del punto de la línea central en "distance" metros.</summary>
    public int IndexAt(float distance)
    {
        for (int i = 0; i < Distance.Length; i++)
        {
            if (Distance[i] >= distance) return i;
        }
        return Distance.Length - 1;
    }

    private float StartRampEnd
    {
        get
        {
            foreach (Feature f in Features) if (f.Kind == Kind.StartHill) return f.To + 1f;
            return 0f;
        }
    }

    // =====================================================================
    // Forma
    // =====================================================================

    /// <summary>Nivel de la pista: baja pareja de la salida a la meta (las rectas quedan en terrazas).</summary>
    private float Base(int i)
    {
        return TotalDrop * (1f - Distance[i] / Length);
    }

    private float FeatureHeight(float s)
    {
        float h = 0f;
        foreach (Feature f in Features)
        {
            if (s < f.From || s > f.To) continue;
            float t = s - f.From, length = f.To - f.From, u = t / length, v;
            switch (f.Kind)
            {
                case Kind.StartHill:
                    // Plataforma de salida y bajada recta y empinada que se redondea abajo.
                    v = t <= f.Period ? f.Height : f.Height * (1f - Mathf.SmoothStep(0f, 1f, (t - f.Period) / (length - f.Period)));
                    break;
                case Kind.Rollers:
                    // Lomas redondas, una tras otra (cada una de largo "Period").
                    v = f.Height * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * Mathf.Min(t, length) / f.Period));
                    break;
                case Kind.Table:
                    if (u < 0.3f) v = f.Height * Mathf.SmoothStep(0f, 1f, u / 0.3f);
                    else if (u < 0.7f) v = f.Height;
                    else v = f.Height * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));
                    break;
                default: // Doble: despegue, hueco y aterrizaje, cada loma redondeada.
                    if (u < 0.4f) v = f.Height * Mathf.Sin(Mathf.PI * Mathf.Pow(u / 0.4f, 0.8f) * 0.5f) * (u < 0.32f ? 1f : 1f - (u - 0.32f) / 0.08f * 0.8f);
                    else if (u < 0.6f) v = f.Height * 0.2f;
                    else if (u < 0.68f) v = f.Height * Mathf.Lerp(0.2f, 1f, Mathf.SmoothStep(0f, 1f, (u - 0.6f) / 0.08f));
                    else v = f.Height * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.68f) / 0.32f));
                    break;
            }
            h = Mathf.Max(h, v);
        }
        return h;
    }

    private float Bank(int i, float lateral)
    {
        float weight = BermWeight(Curvature[i]);
        if (weight <= 0f) return 0f;
        // Girando a la izquierda (+), el lado de afuera es la derecha (lateral +).
        float outside = Curvature[i] > 0f ? lateral : -lateral;
        return BermHeight * weight * Mathf.SmoothStep(0f, 1f, (outside + HalfWidth * 0.5f) / (HalfWidth * 1.5f));
    }

    private static float BermWeight(float curvature)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f / 45f, 1f / 14f, Mathf.Abs(curvature)));
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    /// <summary>Punto de la línea central más cercano y distancia lateral con signo (+ = derecha).</summary>
    private int Nearest(Vector2 p, out float lateral)
    {
        int best = 0;
        float bestD = float.MaxValue;
        int cx = (int)(p.x / Cell), cz = (int)(p.y / Cell);
        for (int r = 0; r < 40 && bestD == float.MaxValue; r += 4)
        {
            for (int z = Mathf.Max(0, cz - r - 4); z <= Mathf.Min(gridH - 1, cz + r + 4); z++)
            {
                for (int x = Mathf.Max(0, cx - r - 4); x <= Mathf.Min(gridW - 1, cx + r + 4); x++)
                {
                    List<int> bucket = cells[z * gridW + x];
                    if (bucket == null) continue;
                    foreach (int i in bucket)
                    {
                        float d = (Points[i] - p).sqrMagnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                }
            }
        }
        Vector2 a = Points[Mathf.Max(0, best - 1)], b = Points[Mathf.Min(Points.Length - 1, best + 1)];
        Vector2 tangent = (b - a).normalized;
        lateral = Vector2.Dot(p - Points[best], new Vector2(tangent.y, -tangent.x));
        // Antes de la salida o después de la meta, la distancia "lateral" es la distancia real al extremo.
        if (best == 0 || best == Points.Length - 1)
        {
            float along = Vector2.Dot(p - Points[best], tangent) * (best == 0 ? -1f : 1f);
            if (along > 0f) lateral = Mathf.Sign(lateral == 0f ? 1f : lateral) * Mathf.Sqrt(lateral * lateral + along * along);
        }
        return best;
    }

    private static float[] SmoothedCurvature(Vector2[] points)
    {
        int n = points.Length;
        float[] raw = new float[n];
        const int k = 8;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = points[Mathf.Max(0, i - k)], b = points[i], c = points[Mathf.Min(n - 1, i + k)];
            Vector2 v1 = b - a, v2 = c - b;
            float length = (v1.magnitude + v2.magnitude) * 0.5f;
            // En los extremos (salida y meta) no hay curva: ahí los vecinos se cortan y darían una falsa.
            if (length < 0.01f || i < k || i > n - 1 - k) continue;
            raw[i] = Mathf.Atan2(v1.x * v2.y - v1.y * v2.x, Vector2.Dot(v1, v2)) / length;
        }
        float[] smooth = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            int count = 0;
            for (int j = Mathf.Max(0, i - 12); j <= Mathf.Min(n - 1, i + 12); j++) { sum += raw[j]; count++; }
            smooth[i] = sum / count;
        }
        return smooth;
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
}
