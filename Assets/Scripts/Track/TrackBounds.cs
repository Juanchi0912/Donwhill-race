using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Límite de una pista de mapa real (el borde que se dibujó sobre el mapa). Si la bici sale de él,
/// aparece "¡FUERA DE PISTA!" con una cuenta y, si no vuelve a tiempo, reaparece en el último checkpoint.
/// Deja salirse un poco a buscar líneas propias, pero no irse por el cerro.
/// Lo crea la herramienta DownhillRace → Crear mapa desde heightmap.
/// </summary>
public class TrackBounds : MonoBehaviour
{
    [SerializeField] private BikeController bike = null;
    [SerializeField] private Text warning = null;

    [Tooltip("Segundos fuera del límite antes de reaparecer.")]
    [SerializeField, Min(0.5f)] private float graceSeconds = 3f;

    [Tooltip("Borde en el plano (x, z del mundo).")]
    [SerializeField] private Vector2[] polygon = new Vector2[0];

    private float outsideTime;
    private int shownSeconds = -1;

    /// <summary>Lo usa la herramienta del Editor al crear el mapa.</summary>
    public void Configure(BikeController rider, Text warningText, Vector2[] worldPolygon)
    {
        bike = rider;
        warning = warningText;
        polygon = worldPolygon;
    }

    private void Awake()
    {
        ShowWarning(false);
    }

    private void Start()
    {
        // Si la salida misma queda fuera del límite, reaparecer no serviría (se repetiría sin parar): se apaga.
        if (bike != null && polygon.Length >= 3 && !Contains(new Vector2(bike.transform.position.x, bike.transform.position.z)))
        {
            Debug.LogWarning("TrackBounds: la salida queda fuera del límite de pista; el límite se desactiva.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (bike == null || polygon.Length < 3) return;
        bool racing = GameManager.Instance == null || GameManager.Instance.CanRiderMove;
        Vector3 p = bike.transform.position;
        if (!racing || Contains(new Vector2(p.x, p.z)))
        {
            outsideTime = 0f;
            ShowWarning(false);
            return;
        }

        outsideTime += Time.deltaTime;
        if (outsideTime >= graceSeconds)
        {
            outsideTime = 0f;
            ShowWarning(false);
            bike.RespawnAtCheckpoint();
            return;
        }
        int seconds = Mathf.CeilToInt(graceSeconds - outsideTime);
        // El texto solo se cambia cuando cambia el número, para no generar basura cada frame.
        if (warning != null && seconds != shownSeconds)
        {
            warning.text = "¡FUERA DE PISTA!  " + seconds;
            shownSeconds = seconds;
        }
        ShowWarning(true);
    }

    private void ShowWarning(bool visible)
    {
        if (warning != null && warning.gameObject.activeSelf != visible) warning.gameObject.SetActive(visible);
        if (!visible) shownSeconds = -1;
    }

    /// <summary>Punto dentro del polígono (regla del rayo: cuenta cuántos bordes cruza).</summary>
    private bool Contains(Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private void OnDrawGizmos()
    {
        if (polygon == null || polygon.Length < 2) return;
        // Se dibuja a la altura del objeto (la herramienta lo pone a la altura media del terreno).
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        float y = transform.position.y;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Length];
            Gizmos.DrawLine(new Vector3(a.x, y, a.y), new Vector3(b.x, y, b.y));
        }
    }
}
