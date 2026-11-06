using UnityEngine;

/// <summary>
/// Una ruta de un mapa real (por ejemplo «El Bosque» del Turi Bike Park): la línea central del sendero,
/// de la salida a la meta, sobre el terreno. Se dibuja en la vista de escena para ubicarse al poner rampas
/// y puentes, y más adelante servirá para checkpoints, reaparecer y rivales.
/// La crea la herramienta DownhillRace → Crear mapa desde heightmap.
/// </summary>
public class TrailPath : MonoBehaviour
{
    [SerializeField] private string routeName = "Ruta";
    [SerializeField] private Color color = Color.yellow;
    [Tooltip("Ancho aproximado del sendero (m).")]
    [SerializeField] private float width = 6f;
    [SerializeField] private Vector3[] points = new Vector3[0];

    public string RouteName => routeName;
    public Color Color => color;
    public float Width => width;
    public int PointCount => points.Length;

    /// <summary>Largo total de la ruta en metros.</summary>
    public float Length
    {
        get
        {
            float length = 0f;
            for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }
    }

    public Vector3 GetPoint(int index)
    {
        return points[Mathf.Clamp(index, 0, points.Length - 1)];
    }

    /// <summary>Lo usa la herramienta del Editor al crear el mapa.</summary>
    public void Configure(string name, Color lineColor, float trailWidth, Vector3[] worldPoints)
    {
        routeName = name;
        color = lineColor;
        width = trailWidth;
        points = worldPoints;
    }

    private void OnDrawGizmos()
    {
        if (points == null || points.Length < 2) return;
        // Un poco sobre el suelo para que no la tape el terreno.
        Vector3 lift = Vector3.up * 0.6f;
        Gizmos.color = color;
        for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1] + lift, points[i] + lift);
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.Label(points[points.Length / 2] + Vector3.up * 3f, routeName);
#endif
    }
}
