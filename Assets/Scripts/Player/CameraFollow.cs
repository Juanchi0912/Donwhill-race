using UnityEngine;

/// <summary>
/// Cámara en tercera persona detrás del corredor, con movimiento suave y campo de visión
/// que se abre con la velocidad. En la pantalla de inicio gira despacio alrededor del corredor.
/// Usa un linecast para no atravesar el suelo ni las bermas.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("La bici que se sigue.")]
    [SerializeField] private BikeController target = null;

    [Header("Posición")]
    [Tooltip("Metros detrás del corredor.")]
    [SerializeField] private float distance = 5f;

    [Tooltip("Metros por encima del corredor.")]
    [SerializeField] private float height = 2.1f;

    [Tooltip("Altura del punto al que mira la cámara, sobre la bici.")]
    [SerializeField] private float lookHeight = 1.2f;

    [Tooltip("Metros por delante de la bici a los que mira la cámara (ayuda a ver las curvas).")]
    [SerializeField] private float lookAhead = 3f;

    [Tooltip("Rapidez con la que la cámara alcanza su posición. Más bajo = más suave pero más retrasada.")]
    [SerializeField] private float positionSmoothing = 7f;

    [Tooltip("Rapidez con la que la cámara gira detrás del rumbo de la bici. Más bajo = marea menos en las curvas.")]
    [SerializeField] private float headingSmoothing = 5f;

    [Tooltip("Rapidez con la que la cámara cambia hacia dónde mira.")]
    [SerializeField] private float rotationSmoothing = 10f;

    [Header("Campo de visión")]
    [Tooltip("Campo de visión con la bici detenida.")]
    [SerializeField] private float minFieldOfView = 60f;

    [Tooltip("Campo de visión a velocidad máxima; da sensación de velocidad.")]
    [SerializeField] private float maxFieldOfView = 76f;

    [Tooltip("Velocidad (m/s) a la que se alcanza el campo de visión máximo.")]
    [SerializeField] private float fieldOfViewMaxSpeed = 28f;

    [Tooltip("Rapidez del cambio de campo de visión.")]
    [SerializeField] private float fieldOfViewSmoothing = 3f;

    [Header("Colisión")]
    [Tooltip("Capas que la cámara no debe atravesar.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Tooltip("Separación mínima entre la cámara y el suelo u obstáculos.")]
    [SerializeField] private float collisionPadding = 0.3f;

    [Header("Pantalla de inicio")]
    [Tooltip("Radio de la vuelta alrededor del corredor en la pantalla de inicio.")]
    [SerializeField] private float orbitRadius = 6f;

    [Tooltip("Altura de la cámara durante la vuelta.")]
    [SerializeField] private float orbitHeight = 2.2f;

    [Tooltip("Grados por segundo de la vuelta.")]
    [SerializeField] private float orbitSpeed = 12f;

    private Camera cam;
    private GameManager gameManager;
    private float smoothedHeading;
    private float orbitAngle;
    private bool wasOrbiting;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        if (target != null) target.Respawned += SnapBehindTarget;
    }

    private void OnDisable()
    {
        if (target != null) target.Respawned -= SnapBehindTarget;
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        if (target != null)
        {
            smoothedHeading = target.InterpolatedHeading;
            orbitAngle = smoothedHeading + 150f; // Empieza viendo al corredor de frente y de lado.
        }
        cam.fieldOfView = minFieldOfView;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;
        Vector3 pivot = target.transform.position + Vector3.up * lookHeight;
        bool orbiting = gameManager != null && gameManager.State == GameManager.RaceState.Inicio;

        Vector3 desiredPosition;
        Vector3 lookPoint;

        if (orbiting)
        {
            orbitAngle += orbitSpeed * dt;
            Vector3 offset = Quaternion.Euler(0f, orbitAngle, 0f) * Vector3.forward * orbitRadius;
            desiredPosition = target.transform.position + offset + Vector3.up * orbitHeight;
            lookPoint = pivot;
            smoothedHeading = target.InterpolatedHeading;
        }
        else
        {
            // Se suaviza el rumbo por separado: la cámara gira detrás de la bici sin dar latigazos.
            smoothedHeading = Mathf.LerpAngle(smoothedHeading, target.InterpolatedHeading, 1f - Mathf.Exp(-headingSmoothing * dt));
            Vector3 back = Quaternion.Euler(0f, smoothedHeading, 0f) * Vector3.back;
            desiredPosition = target.transform.position + back * distance + Vector3.up * height;
            lookPoint = pivot - back * lookAhead;
        }

        desiredPosition = ResolveCollision(pivot, desiredPosition);

        // Al pasar del menú a la carrera, la transición la hace el propio suavizado.
        float positionBlend = 1f - Mathf.Exp(-positionSmoothing * dt);
        if (orbiting && !wasOrbiting) positionBlend = 1f;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmoothing * dt));

        // Campo de visión según la velocidad.
        float speed01 = orbiting ? 0f : Mathf.Clamp01(target.Speed / fieldOfViewMaxSpeed);
        float targetFov = Mathf.Lerp(minFieldOfView, maxFieldOfView, speed01);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fieldOfViewSmoothing * dt));

        wasOrbiting = orbiting;
    }

    /// <summary>Si hay suelo u obstáculo entre la bici y la cámara, la acerca.</summary>
    private Vector3 ResolveCollision(Vector3 from, Vector3 to)
    {
        if (Physics.Linecast(from, to, out RaycastHit hit, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            to = hit.point + hit.normal * collisionPadding;
        }

        // Comprobación extra hacia abajo, por si la cámara queda justo dentro de una loma.
        Vector3 above = to + Vector3.up * 3f;
        if (Physics.Raycast(above, Vector3.down, out RaycastHit ground, 3f + collisionPadding, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            to.y = Mathf.Max(to.y, ground.point.y + collisionPadding);
        }
        return to;
    }

    /// <summary>Al reaparecer, la cámara se coloca directo detrás de la bici.</summary>
    private void SnapBehindTarget()
    {
        if (target == null) return;
        smoothedHeading = target.InterpolatedHeading;
        Vector3 back = Quaternion.Euler(0f, smoothedHeading, 0f) * Vector3.back;
        Vector3 pivot = target.transform.position + Vector3.up * lookHeight;
        transform.position = ResolveCollision(pivot, target.transform.position + back * distance + Vector3.up * height);
        transform.rotation = Quaternion.LookRotation(pivot - back * lookAhead - transform.position, Vector3.up);
    }
}
