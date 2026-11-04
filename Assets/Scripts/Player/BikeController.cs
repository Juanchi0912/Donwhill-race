using System;
using UnityEngine;

/// <summary>
/// Física arcade de la bici. La simulación la lleva una esfera invisible
/// (Rigidbody + SphereCollider sin fricción y con la rotación congelada);
/// el rumbo se guarda aparte como un ángulo, así la esfera nunca vuelca
/// y caerse es casi imposible. El modelo visual (BikeVisuals) solo copia este estado.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BikeController : MonoBehaviour
{
    [Header("Velocidad")]
    [Tooltip("Velocidad máxima normal en m/s (22 m/s ≈ 80 km/h).")]
    [SerializeField] private float maxSpeed = 22f;

    [Tooltip("Velocidad máxima agachado en m/s (28 m/s ≈ 100 km/h).")]
    [SerializeField] private float maxSpeedCrouched = 28f;

    [Tooltip("Frenado suave (m/s²) cuando se supera la velocidad máxima; evita cortes bruscos al soltar Shift.")]
    [SerializeField] private float overspeedDeceleration = 6f;

    [Tooltip("Resistencia del aire: desaceleración = coeficiente × velocidad². Define la velocidad tope en cada pendiente.")]
    [SerializeField] private float airDragCoefficient = 0.005f;

    [Tooltip("Multiplicador de la resistencia del aire al ir agachado (menos de 1 = más rápido).")]
    [SerializeField, Range(0.1f, 1f)] private float crouchDragMultiplier = 0.65f;

    [Tooltip("Desaceleración por rodadura en m/s², para que la bici se detenga en lo plano.")]
    [SerializeField] private float rollingResistance = 0.3f;

    [Header("Pedaleo")]
    [Tooltip("Aceleración máxima al pedalear desde parado, en m/s².")]
    [SerializeField] private float pedalAcceleration = 7f;

    [Tooltip("Potencia de las piernas (aceleración × velocidad). Empuja fuerte al arrancar y cada vez menos a más velocidad.")]
    [SerializeField] private float pedalPower = 22f;

    [Tooltip("Velocidad (m/s) a partir de la cual pedalear ya no ayuda (14 m/s ≈ 50 km/h): las piernas no giran más rápido.")]
    [SerializeField] private float pedalTopSpeed = 14f;

    [Tooltip("Metros por segundo antes del tope en los que el pedaleo se va apagando suavemente.")]
    [SerializeField] private float pedalFadeRange = 2.5f;

    [Tooltip("Energía del corredor. Si falta, se pedalea sin cansancio.")]
    [SerializeField] private RiderStamina stamina = null;

    [Tooltip("Multiplicador de la resistencia del aire estando agotado: el corredor se endereza para respirar.")]
    [SerializeField] private float exhaustedDragMultiplier = 1.5f;

    [Header("Ritmo de pedaleo")]
    [Tooltip("Vueltas de biela por segundo al pedalear despacio (arrancando).")]
    [SerializeField] private float minCadence = 1.1f;

    [Tooltip("Vueltas de biela por segundo cerca del tope del pedaleo.")]
    [SerializeField] private float maxCadence = 2.2f;

    [Tooltip("Cuánto varía el empuje dentro de cada pedalada (0 = parejo). Da la sensación de golpes de pedal: " +
             "empuja más con la biela horizontal y casi nada arriba y abajo.")]
    [SerializeField, Range(0f, 0.8f)] private float pedalStrokePulse = 0.35f;

    [Tooltip("Grados por segundo con que las bielas vuelven a quedar niveladas al dejar de pedalear.")]
    [SerializeField] private float crankLevelSpeed = 300f;

    [Header("Freno")]
    [Tooltip("Desaceleración del freno en m/s².")]
    [SerializeField] private float brakeDeceleration = 14f;

    [Header("Pendiente")]
    [Tooltip("Multiplicador de la gravedad a lo largo del suelo. 1 = física real; más de 1 = en bajada " +
             "la bici gana velocidad más rápido aunque no se pedalee (sensación arcade).")]
    [SerializeField, Min(0f)] private float slopeGravityMultiplier = 1.5f;

    [Header("Giro")]
    [Tooltip("Velocidad de giro a baja velocidad, en grados por segundo.")]
    [SerializeField] private float turnRateSlow = 120f;

    [Tooltip("Velocidad de giro a velocidad máxima, en grados por segundo.")]
    [SerializeField] private float turnRateFast = 60f;

    [Tooltip("Multiplicador del giro al ir agachado.")]
    [SerializeField, Range(0.1f, 1f)] private float crouchTurnMultiplier = 0.6f;

    [Tooltip("Fracción del giro disponible con la bici casi detenida (para poder enderezarse).")]
    [SerializeField, Range(0f, 1f)] private float standstillTurnFactor = 0.35f;

    [Header("Agarre")]
    [Tooltip("Rapidez (1/s) con la que se elimina la velocidad lateral. Más alto = la bici va más hacia donde apunta.")]
    [SerializeField] private float lateralGrip = 10f;

    [Tooltip("Aceleración (m/s²) que pega la bici al suelo; ayuda a seguir las bermas y lomas.")]
    [SerializeField] private float groundStickForce = 6f;

    [Tooltip("Desaceleración extra en m/s² al rodar por el pasto, para premiar ir por el camino.")]
    [SerializeField] private float grassDeceleration = 3f;

    [Tooltip("Por debajo de esta velocidad (m/s) el pasto deja de frenar, para no quedarse atascado.")]
    [SerializeField] private float grassMinSpeed = 5f;

    [Header("Salto y aire")]
    [Tooltip("Velocidad vertical del salto en m/s.")]
    [SerializeField] private float jumpSpeed = 4.5f;

    [Tooltip("Gravedad extra en el aire (0.6 = +60 %). Hace que los saltos no se sientan flotantes.")]
    [SerializeField] private float extraAirGravity = 0.6f;

    [Tooltip("Fracción del giro disponible en el aire.")]
    [SerializeField, Range(0f, 1f)] private float airControl = 0.35f;

    [Tooltip("Margen (s) para saltar justo después de dejar el suelo.")]
    [SerializeField] private float coyoteTime = 0.12f;

    [Tooltip("Margen (s) que se recuerda el botón de salto antes de tocar el suelo.")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Suelo")]
    [Tooltip("Distancia extra, por debajo de la esfera, para considerar que está en el suelo.")]
    [SerializeField] private float groundCheckDistance = 0.3f;

    [Tooltip("Capas que cuentan como suelo.")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Tooltip("Rapidez con la que la normal del suelo se suaviza (evita temblores entre triángulos).")]
    [SerializeField] private float normalSmoothing = 15f;

    [Header("Reaparecer")]
    [Tooltip("Si la esfera baja de esta altura, reaparece en el último checkpoint. El constructor de escena la calcula.")]
    [SerializeField] private float fallHeight = -100f;

    [Tooltip("Altura sobre el punto del checkpoint a la que reaparece la bici.")]
    [SerializeField] private float respawnHeightOffset = 0.6f;

    /// <summary>Se dispara al reaparecer (para que la cámara y el modelo salten sin suavizado).</summary>
    public event Action Respawned;

    public Vector3 Velocity => rb.linearVelocity;
    public float Speed => rb.linearVelocity.magnitude;
    public float SpeedKmh => Speed * 3.6f;
    public float ForwardSpeed { get; private set; }
    public bool IsGrounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float SteerInput { get; private set; }
    public bool IsCrouching { get; private set; }
    public float MaxSpeed => maxSpeed;

    /// <summary>Esfuerzo real de pedaleo en este paso (0 a 1): intensidad × potencia disponible.</summary>
    public float PedalEffort { get; private set; }

    /// <summary>Ángulo de las bielas (grados) interpolado entre pasos de física. En 0, el pie izquierdo va adelante.</summary>
    public float InterpolatedCrankAngle => Mathf.LerpAngle(previousCrankAngle, crankAngle, InterpolationFactor);

    /// <summary>Energía del corredor (puede ser null si no se usa cansancio).</summary>
    public RiderStamina Stamina => stamina;

    private bool IsExhausted => stamina != null && stamina.IsExhausted;
    public float Radius => sphere.radius * transform.lossyScale.y;

    /// <summary>Rumbo (yaw en grados) interpolado entre pasos de física, para que el modelo y la cámara no tiemblen.</summary>
    public float InterpolatedHeading
    {
        get
        {
            return Mathf.LerpAngle(previousHeading, heading, InterpolationFactor);
        }
    }

    /// <summary>Dirección horizontal hacia la que apunta la bici.</summary>
    public Vector3 FlatForward => Quaternion.Euler(0f, InterpolatedHeading, 0f) * Vector3.forward;

    private Rigidbody rb;
    private SphereCollider sphere;
    private GameManager gameManager;

    private float heading;
    private float previousHeading;
    private float crankAngle;
    private float previousCrankAngle;

    /// <summary>Fracción del paso de física ya transcurrida en este frame (para interpolar sin tirones).</summary>
    private static float InterpolationFactor =>
        Time.fixedDeltaTime > 0f ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;

    // Input leído en Update y consumido en FixedUpdate.
    private float throttleInput;
    private float brakeInput;
    private float lastJumpPressTime = -10f;
    private float lastGroundedTime = -10f;
    private bool respawnRequested;

    private bool onGrass;
    private float zoneDeceleration; // Suma de las zonas que frenan (agua) en las que está la bici.
    private Collider cachedGroundCollider;
    private TrackGenerator cachedTrack;

    private Vector3 startPosition;
    private float startHeading;
    private Vector3 checkpointPosition;
    private float checkpointHeading;
    private int checkpointIndex = -1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphere = GetComponent<SphereCollider>();
        if (stamina == null) TryGetComponent(out stamina);

        // La rotación física no se usa: el rumbo va en "heading".
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.useGravity = true;

        startPosition = transform.position;
        startHeading = transform.eulerAngles.y;
        heading = previousHeading = startHeading;
        checkpointPosition = startPosition;
        checkpointHeading = startHeading;
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager != null) gameManager.RaceReset += HandleRaceReset;
        else Debug.LogWarning("BikeController: no hay GameManager; la bici se moverá sin cuenta regresiva.", this);
    }

    private void OnDestroy()
    {
        if (gameManager != null) gameManager.RaceReset -= HandleRaceReset;
    }

    private void Update()
    {
        bool canMove = CanMove();

        throttleInput = canMove ? GameInput.Throttle : 0f;
        brakeInput = canMove ? GameInput.Brake : 0f;
        SteerInput = canMove ? GameInput.Steer : 0f;
        // Agotado, el corredor se endereza para respirar: no puede ir agachado.
        IsCrouching = canMove && GameInput.Crouch && !IsExhausted;

        if (canMove && GameInput.JumpPressed) lastJumpPressTime = Time.time;
        if (canMove && GameInput.RespawnPressed) respawnRequested = true;
    }

    private void FixedUpdate()
    {
        previousHeading = heading;
        previousCrankAngle = crankAngle;
        float dt = Time.fixedDeltaTime;

        if (respawnRequested || transform.position.y < fallHeight)
        {
            respawnRequested = false;
            RespawnAtCheckpoint();
            return;
        }

        UpdateGround(dt);

        // Antes del ¡YA! la bici queda quieta en la salida.
        if (gameManager != null && gameManager.State != GameManager.RaceState.Carrera
            && gameManager.State != GameManager.RaceState.Meta)
        {
            rb.linearVelocity = Vector3.zero;
            ForwardSpeed = 0f;
            PedalEffort = 0f;
            return;
        }

        bool racing = CanMove();
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        // --- Energía: solo se pedalea con las ruedas en el suelo; en el aire y sin pedalear se recupera.
        float pedalIntensity = IsGrounded && racing ? throttleInput : 0f;
        float pedalPowerFactor = stamina != null ? stamina.Tick(pedalIntensity, speed < 1f, dt) : 1f;
        PedalEffort = pedalIntensity * pedalPowerFactor;
        UpdateCranks(speed, dt);

        // --- Giro: más lento a alta velocidad, reducido al agacharse y en el aire.
        float speed01 = Mathf.InverseLerp(0f, maxSpeed, speed);
        float turnRate = Mathf.Lerp(turnRateSlow, turnRateFast, speed01);
        if (IsCrouching) turnRate *= crouchTurnMultiplier;
        if (!IsGrounded) turnRate *= airControl;
        turnRate *= Mathf.Lerp(standstillTurnFactor, 1f, Mathf.Clamp01(speed / 4f));
        heading = Mathf.Repeat(heading + SteerInput * turnRate * dt, 360f);

        Vector3 flatForward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
        Vector3 forward = IsGrounded
            ? Vector3.ProjectOnPlane(flatForward, GroundNormal).normalized
            : flatForward;

        float forwardSpeed = Vector3.Dot(velocity, forward);

        if (IsGrounded)
        {
            // --- Agarre: quitar parte de la velocidad lateral para que la bici siga su rumbo.
            Vector3 right = Vector3.Cross(GroundNormal, forward).normalized;
            float lateralSpeed = Vector3.Dot(velocity, right);
            velocity -= right * lateralSpeed * (1f - Mathf.Exp(-lateralGrip * dt));

            // --- Pedaleo con potencia constante: mucho empuje al arrancar y menos a más velocidad.
            // Mientras más se pedalea, más rápido se va (hasta el tope), pero se gasta energía.
            if (PedalEffort > 0f && forwardSpeed < pedalTopSpeed)
            {
                float accel = Mathf.Min(pedalAcceleration, pedalPower / Mathf.Max(forwardSpeed, 1f));
                float fade = pedalFadeRange > 0f ? Mathf.Clamp01((pedalTopSpeed - forwardSpeed) / pedalFadeRange) : 1f;
                // Cada pedalada empuja más con la biela horizontal; el promedio sigue siendo el mismo.
                float stroke = 1f + pedalStrokePulse * (Mathf.Abs(Mathf.Sin(crankAngle * Mathf.Deg2Rad)) * (Mathf.PI * 0.5f) - 1f);
                velocity += forward * accel * fade * stroke * PedalEffort * dt;
            }

            // --- Freno (y freno automático tras la meta, para parar antes de la barrera). Nunca da marcha atrás.
            float brake = racing ? brakeInput : 0.8f;
            if (brake > 0f && forwardSpeed > 0f)
            {
                velocity -= forward * Mathf.Min(forwardSpeed, brakeDeceleration * brake * dt);
            }

            // --- Rodadura y pasto.
            float decel = rollingResistance;
            if (onGrass && forwardSpeed > grassMinSpeed) decel += grassDeceleration;
            decel += Mathf.Max(0f, zoneDeceleration);
            forwardSpeed = Vector3.Dot(velocity, forward);
            if (forwardSpeed > 0f) velocity -= forward * Mathf.Min(forwardSpeed, decel * dt);

            // --- Pendiente: la gravedad ya acelera en bajada; aquí se suma un extra a lo largo del suelo
            // para que bajar se sienta rápido aunque no se pedalee.
            Vector3 slopePull = Vector3.ProjectOnPlane(Physics.gravity, GroundNormal);
            velocity += slopePull * (slopeGravityMultiplier - 1f) * dt;

            // --- Pegarse al suelo, siguiendo la normal (sirve para las bermas).
            velocity -= GroundNormal * groundStickForce * dt;
        }
        else
        {
            // --- Gravedad extra para saltos con peso.
            velocity += Physics.gravity * extraAirGravity * dt;
        }

        // --- Resistencia del aire: limita la velocidad de forma natural en cada pendiente.
        speed = velocity.magnitude;
        if (speed > 0.01f)
        {
            float drag = airDragCoefficient * (IsCrouching ? crouchDragMultiplier : 1f) * (IsExhausted ? exhaustedDragMultiplier : 1f) * speed * speed;
            velocity -= velocity / speed * Mathf.Min(speed, drag * dt);
        }

        // --- Tope de velocidad suave (el de agachado es mayor).
        float limit = IsCrouching ? maxSpeedCrouched : maxSpeed;
        forwardSpeed = Vector3.Dot(velocity, forward);
        if (forwardSpeed > limit)
        {
            float excess = Mathf.Min(forwardSpeed - limit, overspeedDeceleration * dt);
            velocity -= forward * excess;
        }

        // --- Salto, con margen antes y después de tocar el suelo.
        bool jumpBuffered = Time.time - lastJumpPressTime <= jumpBufferTime;
        bool canJump = IsGrounded || Time.time - lastGroundedTime <= coyoteTime;
        if (racing && jumpBuffered && canJump)
        {
            lastJumpPressTime = -10f;
            lastGroundedTime = -10f;
            if (velocity.y < 0f) velocity.y = 0f;
            velocity += Vector3.up * jumpSpeed;
            if (stamina != null) stamina.SpendJump();
            IsGrounded = false;
        }

        rb.linearVelocity = velocity;
        ForwardSpeed = Vector3.Dot(velocity, forward);
    }

    /// <summary>Lo llama un TrackTrigger de tipo Checkpoint. Ignora checkpoints anteriores al actual.</summary>
    public void SetCheckpoint(int index, Vector3 position, float headingDegrees)
    {
        if (index <= checkpointIndex) return;
        checkpointIndex = index;
        checkpointPosition = position;
        checkpointHeading = headingDegrees;
    }

    /// <summary>Lo llaman las SlowZone al entrar (positivo) y salir (negativo).</summary>
    public void AddZoneDeceleration(float amount)
    {
        zoneDeceleration += amount;
    }

    /// <summary>Altura bajo la cual la bici reaparece. La llama el constructor de escena.</summary>
    public void ConfigureFallHeight(float height)
    {
        fallHeight = height;
    }

    public void RespawnAtCheckpoint()
    {
        Teleport(checkpointPosition + Vector3.up * respawnHeightOffset, checkpointHeading);
    }

    private void HandleRaceReset()
    {
        checkpointIndex = -1;
        checkpointPosition = startPosition;
        checkpointHeading = startHeading;
        if (stamina != null) stamina.Refill(); // Cada carrera empieza con energía completa.
        Teleport(startPosition, startHeading);
    }

    private void Teleport(Vector3 position, float headingDegrees)
    {
        rb.linearVelocity = Vector3.zero;
        rb.position = position;
        transform.position = position;
        heading = previousHeading = headingDegrees;
        crankAngle = previousCrankAngle = 0f;
        GroundNormal = Vector3.up;
        IsGrounded = false;
        ForwardSpeed = 0f;
        lastJumpPressTime = -10f;
        Respawned?.Invoke();
    }

    /// <summary>
    /// Gira las bielas al ritmo del pedaleo. Sin pedalear hay rueda libre: las bielas se detienen
    /// y quedan niveladas, con un pie adelante y otro atrás (posición de ataque).
    /// </summary>
    private void UpdateCranks(float speed, float dt)
    {
        if (PedalEffort > 0.01f)
        {
            float cadence = Mathf.Lerp(minCadence, maxCadence, Mathf.InverseLerp(0f, pedalTopSpeed, speed));
            // Con poca fuerza (cansado o agotado) las piernas giran más lento.
            crankAngle += cadence * 360f * Mathf.Lerp(0.35f, 1f, PedalEffort) * dt;
        }
        else
        {
            float target = Mathf.Abs(Mathf.DeltaAngle(crankAngle, 0f)) <= 90f ? 0f : 180f;
            crankAngle = Mathf.MoveTowardsAngle(crankAngle, target, crankLevelSpeed * dt);
        }
        crankAngle = Mathf.Repeat(crankAngle, 360f);
    }

    private bool CanMove()
    {
        return gameManager == null || gameManager.CanRiderMove;
    }

    private void UpdateGround(float dt)
    {
        // Se lanza el rayo según la normal anterior: así la bici "sigue" las paredes de las bermas.
        Vector3 direction = IsGrounded ? -GroundNormal : Vector3.down;
        float distance = Radius + groundCheckDistance;

        if (Physics.Raycast(rb.position, direction, out RaycastHit hit, distance, groundLayers, QueryTriggerInteraction.Ignore)
            || (IsGrounded && Physics.Raycast(rb.position, Vector3.down, out hit, distance, groundLayers, QueryTriggerInteraction.Ignore)))
        {
            IsGrounded = true;
            lastGroundedTime = Time.time;
            GroundNormal = Vector3.Slerp(GroundNormal, hit.normal, 1f - Mathf.Exp(-normalSmoothing * dt)).normalized;
            onGrass = IsGrassHit(hit);
        }
        else
        {
            IsGrounded = false;
            onGrass = false;
            GroundNormal = Vector3.Slerp(GroundNormal, Vector3.up, 1f - Mathf.Exp(-normalSmoothing * 0.3f * dt)).normalized;
        }
    }

    private bool IsGrassHit(RaycastHit hit)
    {
        // Se guarda la referencia para no llamar GetComponent en cada paso de física.
        if (hit.collider != cachedGroundCollider)
        {
            cachedGroundCollider = hit.collider;
            cachedTrack = hit.collider != null ? hit.collider.GetComponent<TrackGenerator>() : null;
        }
        return cachedTrack != null && cachedTrack.GetSurface(hit.triangleIndex) == TrackGenerator.Surface.Pasto;
    }
}
