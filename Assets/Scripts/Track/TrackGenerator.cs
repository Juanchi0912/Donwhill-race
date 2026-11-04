using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera la malla de la pista a partir de puntos de control unidos con una curva Catmull-Rom.
/// Perfil transversal: pasto | berma | camino de tierra | berma | pasto.
/// En las curvas el camino se peralta y la berma exterior sube, para que ayude a girar.
/// La misma malla sirve de MeshCollider; tiene 3 submallas (tierra, bermas, pasto).
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TrackGenerator : MonoBehaviour
{
    public enum Surface
    {
        Tierra,
        Berma,
        Pasto
    }

    // Puntos del perfil por lado: borde del camino, berma (interior, cima, cima exterior, pie) y fin del pasto.
    private const int ProfilePointsPerSide = 5;
    // Tiras del perfil de izquierda a derecha: pasto, 3 de berma, 4 de camino, 3 de berma, pasto.
    private const int StripCount = 12;

    [Header("Recorrido")]
    [Tooltip("Puntos de control del centro del camino, en orden de bajada.")]
    [SerializeField] private List<Vector3> controlPoints = new List<Vector3>();

    [Tooltip("Medio ancho del camino en cada punto de control (opcional). Si falta, se usa el valor por defecto.")]
    [SerializeField] private List<float> controlHalfWidths = new List<float>();

    [Tooltip("Distancia entre cortes transversales de la malla, en metros.")]
    [SerializeField, Min(0.25f)] private float sampleSpacing = 1f;

    [Header("Camino")]
    [Tooltip("Medio ancho del camino de tierra en metros (4.5 = 9 m de ancho).")]
    [SerializeField] private float defaultHalfWidth = 4.5f;

    [Tooltip("Peralte máximo del camino en las curvas, en grados.")]
    [SerializeField] private float maxBankAngle = 10f;

    [Tooltip("Grados de peralte por cada grado/metro de curvatura.")]
    [SerializeField] private float bankPerCurvature = 4f;

    [Header("Bermas")]
    [Tooltip("Altura de las bermas en las rectas, en metros.")]
    [SerializeField] private float bermBaseHeight = 0.5f;

    [Tooltip("Altura extra máxima de la berma exterior en las curvas, en metros.")]
    [SerializeField] private float bermMaxExtraHeight = 1.6f;

    [Tooltip("Metros de altura extra por cada grado/metro de curvatura.")]
    [SerializeField] private float bermPerCurvature = 0.8f;

    [Tooltip("Ancho de la cara interior de la berma por cada metro de altura (más ancho = pared más tendida).")]
    [SerializeField] private float bermSlopeWidthPerMeter = 1.2f;

    [Tooltip("Ancho de la cima plana de la berma.")]
    [SerializeField] private float bermTopWidth = 0.6f;

    [Tooltip("Ancho de la cara trasera de la berma, hacia el pasto.")]
    [SerializeField] private float bermBackWidth = 1.5f;

    [Header("Pasto")]
    [Tooltip("Ancho de la franja de pasto a cada lado, en metros.")]
    [SerializeField] private float grassWidth = 14f;

    [Tooltip("Cuánto sube el pasto hacia afuera (forma de valle que devuelve la bici al camino).")]
    [SerializeField] private float grassRise = 0.6f;

    [Header("Suavizado")]
    [Tooltip("Metros a cada lado usados para suavizar la curvatura (peralte y bermas).")]
    [SerializeField] private float curvatureSmoothingDistance = 10f;

    // Cantidad de triángulos de cada submalla; sirve para saber la superficie de un impacto.
    [SerializeField, HideInInspector] private int dirtTriangleCount;
    [SerializeField, HideInInspector] private int bermTriangleCount;

    // Datos de los cortes; solo existen después de Generate (se usan al construir la escena).
    private Vector3[] positions;
    private Vector3[] forwards;
    private Vector3[] rights;
    private float[] distances;
    private float[] halfWidths;
    private float[] profileOffsets; // [muestra, lado, punto]
    private float[] profileHeights;

    public int SampleCount => positions != null ? positions.Length : 0;
    public float SampleSpacing => sampleSpacing;
    public float TotalLength => SampleCount > 0 ? distances[SampleCount - 1] : 0f;

    // Posiciones y direcciones en coordenadas de mundo.
    public Vector3 GetPosition(int i) => transform.TransformPoint(positions[ClampIndex(i)]);
    public Vector3 GetForward(int i) => transform.TransformDirection(forwards[ClampIndex(i)]);
    public Vector3 GetRight(int i) => transform.TransformDirection(rights[ClampIndex(i)]);
    public float GetDistance(int i) => distances[ClampIndex(i)];
    public float GetHalfWidth(int i) => halfWidths[ClampIndex(i)];

    /// <summary>Índice de la muestra más cercana a una distancia (m) desde la salida.</summary>
    public int GetIndexAtDistance(float distance)
    {
        return ClampIndex(Mathf.RoundToInt(distance / sampleSpacing));
    }

    /// <summary>Distancia lateral (m) hasta el pie exterior de la berma, a la derecha (side = 1) o izquierda (-1).</summary>
    public float GetBermFootOffset(int i, int side) => GetProfileOffset(ClampIndex(i), side, 3);

    /// <summary>Distancia lateral (m) hasta el final del pasto en ese lado.</summary>
    public float GetOuterOffset(int i, int side) => GetProfileOffset(ClampIndex(i), side, 4);

    /// <summary>Punto de la superficie de la pista a una distancia lateral (positivo = derecha).</summary>
    public Vector3 GetSurfacePoint(int i, float lateral)
    {
        i = ClampIndex(i);
        int side = lateral >= 0f ? 1 : -1;
        float a = Mathf.Abs(lateral);
        float height;

        float edge = GetProfileOffset(i, side, 0);
        if (a <= edge)
        {
            // En el camino la altura sale del peralte, que es lineal desde el centro.
            height = GetProfileHeight(i, side, 0) * (a / Mathf.Max(0.001f, edge));
        }
        else
        {
            height = GetProfileHeight(i, side, ProfilePointsPerSide - 1);
            for (int k = 1; k < ProfilePointsPerSide; k++)
            {
                float a0 = GetProfileOffset(i, side, k - 1);
                float a1 = GetProfileOffset(i, side, k);
                if (a <= a1)
                {
                    float t = Mathf.InverseLerp(a0, a1, a);
                    height = Mathf.Lerp(GetProfileHeight(i, side, k - 1), GetProfileHeight(i, side, k), t);
                    break;
                }
            }
        }

        return transform.TransformPoint(positions[i] + rights[i] * lateral + Vector3.up * height);
    }

    /// <summary>Superficie de un triángulo de la malla (según RaycastHit.triangleIndex).</summary>
    public Surface GetSurface(int triangleIndex)
    {
        if (triangleIndex < 0) return Surface.Tierra;
        if (triangleIndex < dirtTriangleCount) return Surface.Tierra;
        if (triangleIndex < dirtTriangleCount + bermTriangleCount) return Surface.Berma;
        return Surface.Pasto;
    }

    /// <summary>Define el recorrido. Los anchos son opcionales (uno por punto).</summary>
    public void SetLayout(List<Vector3> points, List<float> pointHalfWidths)
    {
        controlPoints = new List<Vector3>(points);
        controlHalfWidths = pointHalfWidths != null ? new List<float>(pointHalfWidths) : new List<float>();
    }

    /// <summary>Construye la malla, la asigna al MeshFilter y al MeshCollider, y la devuelve.</summary>
    public Mesh Generate()
    {
        if (controlPoints == null || controlPoints.Count < 2)
        {
            Debug.LogError("TrackGenerator: hacen falta al menos 2 puntos de control.", this);
            return null;
        }

        SampleCenterLine();
        BuildProfiles();
        Mesh mesh = BuildMesh();

        GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null; // Forzar a que el collider se reconstruya.
        meshCollider.sharedMesh = mesh;
        return mesh;
    }

    // ---------------------------------------------------------------------
    // Línea central
    // ---------------------------------------------------------------------

    private void SampleCenterLine()
    {
        // 1) Curva densa para medir la longitud real.
        const int subdivisions = 24;
        List<Vector3> dense = new List<Vector3>();
        List<float> denseWidth = new List<float>();
        List<float> denseLength = new List<float>();
        float length = 0f;

        int count = controlPoints.Count;
        for (int k = 0; k < count - 1; k++)
        {
            Vector3 p0 = controlPoints[Mathf.Max(k - 1, 0)];
            Vector3 p1 = controlPoints[k];
            Vector3 p2 = controlPoints[k + 1];
            Vector3 p3 = controlPoints[Mathf.Min(k + 2, count - 1)];
            float w1 = GetControlHalfWidth(k);
            float w2 = GetControlHalfWidth(k + 1);

            int steps = k == count - 2 ? subdivisions + 1 : subdivisions;
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)subdivisions;
                Vector3 p = CatmullRom(p0, p1, p2, p3, t);
                if (dense.Count > 0) length += Vector3.Distance(dense[dense.Count - 1], p);
                dense.Add(p);
                denseWidth.Add(Mathf.Lerp(w1, w2, t));
                denseLength.Add(length);
            }
        }

        // 2) Remuestreo a distancia fija, para que los cortes queden parejos.
        int sampleCount = Mathf.Max(2, Mathf.FloorToInt(length / sampleSpacing) + 1);
        positions = new Vector3[sampleCount];
        forwards = new Vector3[sampleCount];
        rights = new Vector3[sampleCount];
        distances = new float[sampleCount];
        halfWidths = new float[sampleCount];

        int segment = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float d = Mathf.Min(i * sampleSpacing, length);
            while (segment < dense.Count - 2 && denseLength[segment + 1] < d) segment++;
            float span = denseLength[segment + 1] - denseLength[segment];
            float t = span > 0f ? (d - denseLength[segment]) / span : 0f;
            positions[i] = Vector3.Lerp(dense[segment], dense[segment + 1], t);
            halfWidths[i] = Mathf.Lerp(denseWidth[segment], denseWidth[segment + 1], t);
            distances[i] = d;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 f = positions[Mathf.Min(i + 1, sampleCount - 1)] - positions[Mathf.Max(i - 1, 0)];
            forwards[i] = f.normalized;
            Vector3 flat = new Vector3(f.x, 0f, f.z);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            rights[i] = Vector3.Cross(Vector3.up, flat.normalized).normalized;
        }
    }

    private float GetControlHalfWidth(int index)
    {
        if (controlHalfWidths != null && index < controlHalfWidths.Count && controlHalfWidths[index] > 0f)
        {
            return controlHalfWidths[index];
        }
        return defaultHalfWidth;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1
                       + (-p0 + p2) * t
                       + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                       + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    // ---------------------------------------------------------------------
    // Perfil transversal
    // ---------------------------------------------------------------------

    private void BuildProfiles()
    {
        int n = positions.Length;

        // Curvatura con signo en grados por metro (positivo = curva a la derecha).
        float[] rawCurvature = new float[n];
        for (int i = 0; i < n; i++)
        {
            int a = Mathf.Max(i - 1, 0);
            int b = Mathf.Min(i + 1, n - 1);
            float yawA = Mathf.Atan2(forwards[a].x, forwards[a].z) * Mathf.Rad2Deg;
            float yawB = Mathf.Atan2(forwards[b].x, forwards[b].z) * Mathf.Rad2Deg;
            float span = distances[b] - distances[a];
            rawCurvature[i] = span > 0f ? Mathf.DeltaAngle(yawA, yawB) / span : 0f;
        }

        int window = Mathf.Max(1, Mathf.RoundToInt(curvatureSmoothingDistance / sampleSpacing));
        float[] curvature = new float[n];
        float[] peakCurvature = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0f;
            float peak = 0f;
            int samples = 0;
            for (int j = Mathf.Max(0, i - window); j <= Mathf.Min(n - 1, i + window); j++)
            {
                sum += rawCurvature[j];
                peak = Mathf.Max(peak, Mathf.Abs(rawCurvature[j]));
                samples++;
            }
            curvature[i] = sum / samples;
            peakCurvature[i] = peak;
        }

        // Límite lateral de cada lado: no pasar del centro de giro ni invadir otro tramo de la pista.
        float fullExtent = defaultHalfWidth + 20f + grassWidth;
        float[] extentLeft = new float[n];
        float[] extentRight = new float[n];
        for (int i = 0; i < n; i++)
        {
            float left = fullExtent;
            float right = fullExtent;

            if (peakCurvature[i] > 0.01f)
            {
                float radius = Mathf.Rad2Deg / peakCurvature[i];
                if (curvature[i] > 0f) right = Mathf.Min(right, radius * 0.85f);
                else left = Mathf.Min(left, radius * 0.85f);
            }

            for (int j = 0; j < n; j++)
            {
                if (Mathf.Abs(distances[i] - distances[j]) < 40f) continue;
                Vector3 d = positions[j] - positions[i];
                d.y = 0f;
                if (Mathf.Abs(Vector3.Dot(d, forwards[i])) > 30f) continue;
                float lateral = Vector3.Dot(d, rights[i]);
                // Cada tramo ocupa la mitad del espacio que los separa.
                if (lateral > 0f) right = Mathf.Min(right, lateral * 0.5f - 0.5f);
                else left = Mathf.Min(left, -lateral * 0.5f - 0.5f);
            }

            extentLeft[i] = left;
            extentRight[i] = right;
        }
        ErodeMinimum(extentLeft, 8);
        ErodeMinimum(extentRight, 8);

        profileOffsets = new float[n * 2 * ProfilePointsPerSide];
        profileHeights = new float[n * 2 * ProfilePointsPerSide];

        for (int i = 0; i < n; i++)
        {
            float bank = Mathf.Clamp(curvature[i] * bankPerCurvature, -maxBankAngle, maxBankAngle);
            float bankSlope = Mathf.Tan(bank * Mathf.Deg2Rad);
            float hw = halfWidths[i];

            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                int side = sideIndex == 0 ? -1 : 1;

                // La berma alta va en el lado exterior de la curva (izquierda si gira a la derecha).
                float outward = Mathf.Max(0f, -side * curvature[i]);
                float bermHeight = bermBaseHeight + Mathf.Min(bermMaxExtraHeight, outward * bermPerCurvature);

                // Peralte: el lado exterior queda más alto.
                float edgeHeight = -side * hw * bankSlope;
                float extent = side < 0 ? extentLeft[i] : extentRight[i];

                float a0 = hw;
                float a1 = a0 + 0.4f + bermHeight * bermSlopeWidthPerMeter;
                float a2 = a1 + bermTopWidth;
                float a3 = a2 + bermBackWidth;
                float a4 = Mathf.Max(a3 + 0.5f, Mathf.Min(a3 + grassWidth, extent));

                SetProfile(i, side, 0, a0, edgeHeight);
                SetProfile(i, side, 1, a1, edgeHeight + bermHeight);
                SetProfile(i, side, 2, a2, edgeHeight + bermHeight);
                SetProfile(i, side, 3, a3, edgeHeight + 0.1f);
                SetProfile(i, side, 4, a4, edgeHeight + 0.1f + grassRise * Mathf.InverseLerp(a3, a3 + grassWidth, a4));
            }
        }
    }

    private static void ErodeMinimum(float[] values, int radius)
    {
        float[] copy = (float[])values.Clone();
        for (int i = 0; i < values.Length; i++)
        {
            float min = copy[i];
            for (int j = Mathf.Max(0, i - radius); j <= Mathf.Min(values.Length - 1, i + radius); j++)
            {
                min = Mathf.Min(min, copy[j]);
            }
            values[i] = min;
        }
    }

    private int ProfileIndex(int sample, int side, int point)
    {
        int sideIndex = side < 0 ? 0 : 1;
        return (sample * 2 + sideIndex) * ProfilePointsPerSide + point;
    }

    private void SetProfile(int sample, int side, int point, float offset, float height)
    {
        int index = ProfileIndex(sample, side, point);
        profileOffsets[index] = offset;
        profileHeights[index] = height;
    }

    private float GetProfileOffset(int sample, int side, int point) => profileOffsets[ProfileIndex(sample, side, point)];
    private float GetProfileHeight(int sample, int side, int point) => profileHeights[ProfileIndex(sample, side, point)];

    // ---------------------------------------------------------------------
    // Malla
    // ---------------------------------------------------------------------

    private Mesh BuildMesh()
    {
        int n = positions.Length;
        int vertsPerSection = StripCount * 2;
        Vector3[] vertices = new Vector3[n * vertsPerSection];
        Vector2[] uvs = new Vector2[vertices.Length];

        // Puntos del corte de izquierda a derecha (lateral con signo, altura).
        float[] lateral = new float[StripCount + 1];
        float[] height = new float[StripCount + 1];

        for (int i = 0; i < n; i++)
        {
            int p = 0;
            for (int k = ProfilePointsPerSide - 1; k >= 0; k--)
            {
                lateral[p] = -GetProfileOffset(i, -1, k);
                height[p] = GetProfileHeight(i, -1, k);
                p++;
            }
            // Puntos intermedios del camino (para que el peralte quede bien repartido).
            float hw = halfWidths[i];
            float leftEdge = GetProfileHeight(i, -1, 0);
            float rightEdge = GetProfileHeight(i, 1, 0);
            lateral[p] = -hw * 0.5f; height[p] = leftEdge * 0.5f; p++;
            lateral[p] = 0f; height[p] = 0f; p++;
            lateral[p] = hw * 0.5f; height[p] = rightEdge * 0.5f; p++;
            for (int k = 0; k < ProfilePointsPerSide; k++)
            {
                lateral[p] = GetProfileOffset(i, 1, k);
                height[p] = GetProfileHeight(i, 1, k);
                p++;
            }

            // Cada tira tiene sus propios vértices: así los bordes entre superficies quedan marcados.
            for (int s = 0; s < StripCount; s++)
            {
                int v = i * vertsPerSection + s * 2;
                vertices[v] = positions[i] + rights[i] * lateral[s] + Vector3.up * height[s];
                vertices[v + 1] = positions[i] + rights[i] * lateral[s + 1] + Vector3.up * height[s + 1];
                uvs[v] = new Vector2(lateral[s] * 0.25f, distances[i] * 0.25f);
                uvs[v + 1] = new Vector2(lateral[s + 1] * 0.25f, distances[i] * 0.25f);
            }
        }

        List<int> dirt = new List<int>();
        List<int> berm = new List<int>();
        List<int> grass = new List<int>();

        for (int i = 0; i < n - 1; i++)
        {
            for (int s = 0; s < StripCount; s++)
            {
                List<int> target = s == 0 || s == StripCount - 1 ? grass : (s >= 4 && s <= 7 ? dirt : berm);
                int a0 = i * vertsPerSection + s * 2;
                int b0 = a0 + 1;
                int a1 = a0 + vertsPerSection;
                int b1 = a1 + 1;
                // Orden horario visto desde arriba: la cara mira hacia arriba.
                target.Add(a0); target.Add(a1); target.Add(b0);
                target.Add(b0); target.Add(a1); target.Add(b1);
            }
        }

        dirtTriangleCount = dirt.Count / 3;
        bermTriangleCount = berm.Count / 3;

        Mesh mesh = new Mesh { name = "PistaMalla" };
        if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 3;
        mesh.SetTriangles(dirt, 0);
        mesh.SetTriangles(berm, 1);
        mesh.SetTriangles(grass, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents(); // Necesario para los mapas normales de tierra y pasto.
        mesh.RecalculateBounds();
        return mesh;
    }

    private int ClampIndex(int i)
    {
        return Mathf.Clamp(i, 0, Mathf.Max(0, SampleCount - 1));
    }
}
