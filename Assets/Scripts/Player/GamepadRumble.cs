using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Vibración del mando: aterrizajes (más fuerte cuanto más tiempo en el aire), entrar al río,
/// quedarse sin energía, reaparecer y un temblor suave del terreno según la velocidad.
/// Solo vibra si se está jugando con mando.
/// </summary>
public class GamepadRumble : MonoBehaviour
{
    [SerializeField] private BikeController bike = null;
    [SerializeField] private RiderStamina stamina = null;

    [Tooltip("Fuerza general de la vibración (0 = apagada).")]
    [SerializeField, Range(0f, 1.5f)] private float strength = 1f;

    [Tooltip("Tiempo mínimo en el aire (s) para que el aterrizaje vibre.")]
    [SerializeField] private float minAirTime = 0.35f;

    [Tooltip("Temblor del terreno a velocidad máxima (0 = nada). Da sensación de tierra bajo las ruedas.")]
    [SerializeField, Range(0f, 0.3f)] private float terrainFeel = 0.06f;

    private float pulseLow, pulseHigh, pulseEnd;
    private float airTime;
    private bool wasGrounded = true;
    private bool wasInWater;
#if ENABLE_INPUT_SYSTEM
    private float sentLow = -1f, sentHigh = -1f;
    private Gamepad activePad;
#endif

    private void Awake()
    {
        if (bike == null) bike = GetComponent<BikeController>();
        if (stamina == null) stamina = GetComponent<RiderStamina>();
    }

    private void OnEnable()
    {
        if (bike != null) bike.Respawned += OnRespawned;
        if (stamina != null) stamina.Exhausted += OnExhausted;
    }

    private void OnDisable()
    {
        if (bike != null) bike.Respawned -= OnRespawned;
        if (stamina != null) stamina.Exhausted -= OnExhausted;
        Stop();
    }

    // Si el juego pierde el foco, el mando no debe quedarse vibrando.
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) Stop();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Stop();
    }

    private void Update()
    {
        if (bike == null) return;
        float dt = Time.deltaTime;

        // Aterrizaje: el golpe crece con el tiempo en el aire.
        if (!bike.IsGrounded)
        {
            airTime += dt;
        }
        else
        {
            if (!wasGrounded && airTime >= minAirTime)
            {
                float k = Mathf.Clamp01((airTime - minAirTime) / 1.2f);
                Pulse(0.25f + 0.55f * k, 0.1f + 0.45f * k, 0.12f + 0.18f * k);
            }
            airTime = 0f;
        }
        wasGrounded = bike.IsGrounded;

        // Entrar al río: un golpe de agua.
        if (bike.InSlowZone && !wasInWater) Pulse(0.55f, 0.25f, 0.3f);
        wasInWater = bike.InSlowZone;

        float low = 0f, high = 0f;
        if (Time.time < pulseEnd)
        {
            low = pulseLow;
            high = pulseHigh;
        }
        if (bike.IsGrounded)
        {
            float speed01 = Mathf.Clamp01(bike.Speed / Mathf.Max(1f, bike.MaxSpeed));
            low = Mathf.Max(low, terrainFeel * speed01);
            // En el agua se siente la corriente contra las ruedas.
            if (bike.InSlowZone && bike.Speed > 0.5f) low = Mathf.Max(low, 0.08f + 0.1f * speed01);
        }
        Apply(low * strength, high * strength);
    }

    private void OnRespawned()
    {
        Pulse(0.45f, 0.45f, 0.18f);
    }

    private void OnExhausted()
    {
        Pulse(0.3f, 0f, 0.45f);
    }

    /// <summary>Golpe corto. Si ya hay uno más fuerte en curso, se respeta.</summary>
    private void Pulse(float low, float high, float duration)
    {
        if (Time.time < pulseEnd && pulseLow + pulseHigh > low + high) return;
        pulseLow = low;
        pulseHigh = high;
        pulseEnd = Time.time + duration;
    }

    private void Apply(float low, float high)
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad pad = GameInput.UsingGamepad ? Gamepad.current : null;
        if (pad != activePad)
        {
            Stop();
            activePad = pad;
        }
        if (activePad == null) return;
        low = Mathf.Clamp01(low);
        high = Mathf.Clamp01(high);
        // Solo se manda al mando cuando cambia: evita saturar la conexión (sobre todo por Bluetooth).
        if (Mathf.Abs(low - sentLow) < 0.01f && Mathf.Abs(high - sentHigh) < 0.01f) return;
        activePad.SetMotorSpeeds(low, high);
        sentLow = low;
        sentHigh = high;
#endif
    }

    private void Stop()
    {
#if ENABLE_INPUT_SYSTEM
        if (activePad != null) activePad.SetMotorSpeeds(0f, 0f);
        activePad = null;
        sentLow = -1f;
        sentHigh = -1f;
#endif
    }
}
