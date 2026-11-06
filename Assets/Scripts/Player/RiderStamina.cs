using System;
using UnityEngine;

/// <summary>
/// Energía del corredor. Pedalear la gasta y dejar de pedalear la recupera.
/// Con poca energía las piernas empujan menos; si llega a cero, el corredor queda agotado:
/// solo pedalea lento y sin fuerza, y no puede agacharse, hasta recuperar parte de la energía.
/// BikeController la actualiza en cada paso de física (Tick).
/// </summary>
public class RiderStamina : MonoBehaviour
{
    [Header("Energía")]
    [Tooltip("Energía máxima del corredor.")]
    [SerializeField, Min(1f)] private float maxEnergy = 100f;

    [Header("Gasto")]
    [Tooltip("Energía que se gasta por segundo pedaleando a fondo (100 / 11 ≈ 9 s de pedaleo seguido).")]
    [SerializeField] private float drainPerSecond = 11f;

    [Tooltip("Energía que cuesta cada salto con Espacio.")]
    [SerializeField] private float jumpCost = 5f;

    [Header("Recuperación")]
    [Tooltip("Energía que se recupera por segundo sin pedalear.")]
    [SerializeField] private float regenPerSecond = 10f;

    [Tooltip("Segundos sin pedalear antes de empezar a recuperar (evita recuperar entre pedaleo y pedaleo).")]
    [SerializeField] private float regenDelay = 0.8f;

    [Tooltip("Multiplicador de la recuperación con la bici detenida.")]
    [SerializeField] private float restingRegenMultiplier = 1.6f;

    [Header("Cansancio")]
    [Tooltip("Por debajo de esta fracción de energía la potencia del pedaleo empieza a bajar.")]
    [SerializeField, Range(0f, 1f)] private float tiredThreshold = 0.3f;

    [Tooltip("Potencia que queda (fracción) justo antes de agotarse.")]
    [SerializeField, Range(0f, 1f)] private float tiredPower = 0.35f;

    [Tooltip("Potencia que queda estando agotado: un pedaleo lento, sin fuerza.")]
    [SerializeField, Range(0f, 1f)] private float exhaustedPower = 0.15f;

    [Tooltip("Fracción de energía que hay que recuperar para dejar de estar agotado.")]
    [SerializeField, Range(0f, 1f)] private float recoverThreshold = 0.35f;

    [Tooltip("Multiplicador de la recuperación si se sigue intentando pedalear estando agotado.")]
    [SerializeField, Range(0f, 1f)] private float exhaustedPedalingRegen = 0.5f;

    /// <summary>Se dispara al quedar agotado.</summary>
    public event Action Exhausted;

    /// <summary>Se dispara al recuperarse del agotamiento.</summary>
    public event Action Recovered;

    public float Energy { get; private set; }
    public float MaxEnergy => maxEnergy;
    public float Energy01 => Energy / maxEnergy;
    public bool IsExhausted { get; private set; }
    public bool IsPedaling { get; private set; }

    /// <summary>Fracción de la potencia de las piernas disponible ahora (0 a 1).</summary>
    public float PowerFactor { get; private set; } = 1f;

    private float timeSincePedal;

    private void Awake()
    {
        Refill();
    }

    /// <summary>
    /// Avanza la energía un paso. pedalIntensity va de 0 a 1 (la tecla o el gatillo).
    /// Devuelve la fracción de potencia con la que se puede pedalear en este paso.
    /// </summary>
    public float Tick(float pedalIntensity, bool resting, float deltaTime)
    {
        IsPedaling = pedalIntensity > 0.01f;
        if (!GameSession.EnergyEnabled)
        {
            // Energía apagada (pruebas): siempre llena y con toda la potencia.
            Energy = maxEnergy;
            IsExhausted = false;
            PowerFactor = 1f;
            return PowerFactor;
        }

        if (IsPedaling && !IsExhausted)
        {
            Energy -= drainPerSecond * pedalIntensity * deltaTime;
            timeSincePedal = 0f;
        }
        else
        {
            timeSincePedal += deltaTime;
            // Agotado se recupera sin esperar, pero más lento si se insiste en pedalear.
            if (timeSincePedal >= regenDelay || IsExhausted)
            {
                float rate = regenPerSecond * (resting ? restingRegenMultiplier : 1f);
                if (IsExhausted && IsPedaling) rate *= exhaustedPedalingRegen;
                Energy += rate * deltaTime;
            }
        }

        Energy = Mathf.Clamp(Energy, 0f, maxEnergy);
        UpdateExhaustion();

        PowerFactor = IsExhausted
            ? exhaustedPower
            : Mathf.Lerp(tiredPower, 1f, tiredThreshold > 0f ? Mathf.Clamp01(Energy01 / tiredThreshold) : 1f);
        return PowerFactor;
    }

    /// <summary>Descuenta el costo de un salto (el salto siempre se permite).</summary>
    public void SpendJump()
    {
        if (jumpCost <= 0f || !GameSession.EnergyEnabled) return;
        Energy = Mathf.Max(0f, Energy - jumpCost);
        timeSincePedal = 0f;
        UpdateExhaustion();
    }

    /// <summary>Energía al máximo (al empezar una carrera nueva).</summary>
    public void Refill()
    {
        Energy = maxEnergy;
        IsExhausted = false;
        IsPedaling = false;
        PowerFactor = 1f;
        timeSincePedal = regenDelay;
    }

    private void UpdateExhaustion()
    {
        if (!IsExhausted && Energy <= 0f)
        {
            IsExhausted = true;
            Exhausted?.Invoke();
        }
        else if (IsExhausted && Energy01 >= recoverThreshold)
        {
            IsExhausted = false;
            Recovered?.Invoke();
        }
    }
}
