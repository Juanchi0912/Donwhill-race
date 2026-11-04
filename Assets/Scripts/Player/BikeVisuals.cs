using UnityEngine;

/// <summary>
/// Hace que el modelo visual siga a la esfera física de BikeController:
/// se alinea con el suelo, se inclina en las curvas, gira las ruedas y el manubrio
/// y agacha al corredor. No tiene física propia; va en un objeto aparte para que
/// la rotación congelada de la esfera no le afecte.
/// </summary>
public class BikeVisuals : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("La esfera física que se sigue.")]
    [SerializeField] private BikeController controller = null;

    [Tooltip("Pivote que se inclina en las curvas (contiene cuadro, ruedas, dirección y corredor).")]
    [SerializeField] private Transform inclinacion = null;

    [Tooltip("Pivote del eje de la rueda trasera.")]
    [SerializeField] private Transform ruedaTrasera = null;

    [Tooltip("Pivote del eje de la rueda delantera (hijo de Direccion).")]
    [SerializeField] private Transform ruedaDelantera = null;

    [Tooltip("Pivote de la dirección: horquilla, manubrio y rueda delantera.")]
    [SerializeField] private Transform direccion = null;

    [Tooltip("Grupo de las piernas del corredor; se encoge al agacharse.")]
    [SerializeField] private Transform piernas = null;

    [Tooltip("Pivote del torso (cadera): baja y se inclina hacia delante al agacharse.")]
    [SerializeField] private Transform torso = null;

    [Header("Ruedas")]
    [Tooltip("Radio visual de las ruedas en metros (0.35 = 0.7 m de diámetro).")]
    [SerializeField] private float wheelRadius = 0.35f;

    [Header("Inclinación")]
    [Tooltip("Inclinación máxima en curvas, en grados.")]
    [SerializeField] private float maxLeanAngle = 28f;

    [Tooltip("Velocidad (m/s) a la que ya se alcanza la inclinación completa.")]
    [SerializeField] private float fullLeanSpeed = 12f;

    [Tooltip("Rapidez del cambio de inclinación.")]
    [SerializeField] private float leanSmoothing = 6f;

    [Header("Dirección")]
    [Tooltip("Giro máximo del manubrio a baja velocidad, en grados.")]
    [SerializeField] private float maxSteerAngle = 25f;

    [Tooltip("Giro máximo del manubrio a velocidad máxima, en grados (a alta velocidad casi no se gira).")]
    [SerializeField] private float minSteerAngle = 6f;

    [Tooltip("Rapidez del movimiento del manubrio.")]
    [SerializeField] private float steerSmoothing = 10f;

    [Header("Alineación con el suelo")]
    [Tooltip("Rapidez con la que el modelo copia la pendiente del suelo.")]
    [SerializeField] private float alignSmoothing = 12f;

    [Header("Agacharse")]
    [Tooltip("Cuánto se encogen las piernas al agacharse (0.85 = 85 % de su altura).")]
    [SerializeField, Range(0.5f, 1f)] private float crouchLegScale = 0.85f;

    [Tooltip("Cuánto baja el torso al agacharse, en metros.")]
    [SerializeField] private float crouchTorsoDrop = 0.1f;

    [Tooltip("Inclinación del torso hacia delante al agacharse, en grados.")]
    [SerializeField] private float crouchTorsoPitch = 15f;

    [Tooltip("Rapidez de la transición al agacharse.")]
    [SerializeField] private float crouchSmoothing = 8f;

    private Quaternion currentRotation = Quaternion.identity;
    private float currentLean;
    private float currentSteer;
    private float currentCrouch;
    private float rearSpin;
    private float frontSpin;

    private Vector3 torsoBasePosition;
    private Quaternion torsoBaseRotation;
    private Vector3 legsBaseScale = Vector3.one;

    private void Awake()
    {
        if (torso != null)
        {
            torsoBasePosition = torso.localPosition;
            torsoBaseRotation = torso.localRotation;
        }
        if (piernas != null) legsBaseScale = piernas.localScale;
    }

    private void OnEnable()
    {
        if (controller != null) controller.Respawned += SnapToController;
    }

    private void OnDisable()
    {
        if (controller != null) controller.Respawned -= SnapToController;
    }

    private void Start()
    {
        SnapToController();
    }

    private void LateUpdate()
    {
        if (controller == null) return;

        float dt = Time.deltaTime;
        UpdateRootTransform(1f - Mathf.Exp(-alignSmoothing * dt));

        float speed = controller.Speed;
        float speed01 = Mathf.InverseLerp(0f, controller.MaxSpeed, speed);

        // Inclinación: hacia el lado de la curva (giro a la derecha = ángulo Z negativo).
        float targetLean = -controller.SteerInput * maxLeanAngle * Mathf.Clamp01(speed / fullLeanSpeed);
        currentLean = Mathf.Lerp(currentLean, targetLean, 1f - Mathf.Exp(-leanSmoothing * dt));
        if (inclinacion != null) inclinacion.localRotation = Quaternion.Euler(0f, 0f, currentLean);

        // Manubrio: gira mucho a baja velocidad y poco a alta, como una bici real.
        float steerRange = Mathf.Lerp(maxSteerAngle, minSteerAngle, speed01);
        currentSteer = Mathf.Lerp(currentSteer, controller.SteerInput * steerRange, 1f - Mathf.Exp(-steerSmoothing * dt));
        if (direccion != null) direccion.localRotation = Quaternion.Euler(0f, currentSteer, 0f);

        // Ruedas: el ángulo avanzado es distancia / radio.
        float spinDegrees = controller.ForwardSpeed / Mathf.Max(0.05f, wheelRadius) * Mathf.Rad2Deg * dt;
        rearSpin = Mathf.Repeat(rearSpin + spinDegrees, 360f);
        frontSpin = Mathf.Repeat(frontSpin + spinDegrees, 360f);
        if (ruedaTrasera != null) ruedaTrasera.localRotation = Quaternion.Euler(rearSpin, 0f, 0f);
        if (ruedaDelantera != null) ruedaDelantera.localRotation = Quaternion.Euler(frontSpin, 0f, 0f);

        // Agacharse.
        float targetCrouch = controller.IsCrouching ? 1f : 0f;
        currentCrouch = Mathf.Lerp(currentCrouch, targetCrouch, 1f - Mathf.Exp(-crouchSmoothing * dt));
        if (piernas != null)
        {
            Vector3 scale = legsBaseScale;
            scale.y *= Mathf.Lerp(1f, crouchLegScale, currentCrouch);
            piernas.localScale = scale;
        }
        if (torso != null)
        {
            torso.localPosition = torsoBasePosition + Vector3.down * crouchTorsoDrop * currentCrouch;
            torso.localRotation = torsoBaseRotation * Quaternion.Euler(crouchTorsoPitch * currentCrouch, 0f, 0f);
        }
    }

    /// <summary>Coloca el modelo justo en la esfera, sin suavizado (al empezar o reaparecer).</summary>
    private void SnapToController()
    {
        if (controller == null) return;
        currentLean = 0f;
        currentSteer = 0f;
        UpdateRootTransform(1f);
    }

    private void UpdateRootTransform(float blend)
    {
        // El origen del modelo es el punto de contacto con el suelo.
        Vector3 up = controller.IsGrounded ? controller.GroundNormal : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(controller.FlatForward, up);
        if (forward.sqrMagnitude < 0.0001f) forward = controller.FlatForward;

        Quaternion target = Quaternion.LookRotation(forward.normalized, up);
        currentRotation = blend >= 1f ? target : Quaternion.Slerp(currentRotation, target, blend);

        transform.SetPositionAndRotation(
            controller.transform.position - currentRotation * Vector3.up * controller.Radius,
            currentRotation);
    }
}
