using UnityEngine;

/// <summary>
/// Lanza barro y polvo desde la rueda trasera según la velocidad, y una nube de polvo al aterrizar.
/// Solo cambia la cantidad de partículas: los sistemas ya vienen configurados desde el constructor de escena.
/// </summary>
public class WheelSpray : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Bici de la que se leen la velocidad y si toca el suelo.")]
    [SerializeField] private BikeController bike = null;

    [Tooltip("Partículas de barro (pequeñas y pesadas).")]
    [SerializeField] private ParticleSystem mud = null;

    [Tooltip("Partículas de polvo (grandes y suaves).")]
    [SerializeField] private ParticleSystem dust = null;

    [Tooltip("Salpicaduras de agua al cruzar el río (opcional).")]
    [SerializeField] private ParticleSystem splash = null;

    [Tooltip("Gotas por segundo en el agua a velocidad máxima.")]
    [SerializeField] private float maxSplashRate = 120f;

    [Header("Cantidad")]
    [Tooltip("Velocidad (m/s) a partir de la cual la rueda empieza a levantar tierra.")]
    [SerializeField] private float minSpeed = 3f;

    [Tooltip("Velocidad (m/s) a la que se alcanza la cantidad máxima.")]
    [SerializeField] private float fullSpeed = 22f;

    [Tooltip("Partículas de barro por segundo a velocidad máxima.")]
    [SerializeField] private float maxMudRate = 70f;

    [Tooltip("Partículas de polvo por segundo a velocidad máxima.")]
    [SerializeField] private float maxDustRate = 16f;

    [Header("Aterrizaje")]
    [Tooltip("Partículas de polvo que salen al aterrizar de un salto.")]
    [SerializeField] private int landingBurst = 25;

    [Tooltip("Tiempo mínimo en el aire (s) para que el aterrizaje levante polvo.")]
    [SerializeField] private float minAirTime = 0.35f;

    private float airTime;
    private bool wasGrounded = true;

    private void Update()
    {
        if (bike == null) return;

        bool grounded = bike.IsGrounded;
        bool inWater = bike.InSlowZone;
        float amount = grounded ? Mathf.InverseLerp(minSpeed, fullSpeed, bike.Speed) : 0f;
        // En el agua no hay polvo: salen gotas, incluso a poca velocidad.
        SetRate(mud, inWater ? 0f : amount * maxMudRate);
        SetRate(dust, inWater ? 0f : amount * maxDustRate);
        SetRate(splash, inWater ? Mathf.Max(0.25f, amount) * maxSplashRate : 0f);

        if (!grounded)
        {
            airTime += Time.deltaTime;
        }
        else
        {
            if (!wasGrounded && airTime >= minAirTime && dust != null) dust.Emit(landingBurst);
            airTime = 0f;
        }
        wasGrounded = grounded;
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        if (system == null) return;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
    }
}
