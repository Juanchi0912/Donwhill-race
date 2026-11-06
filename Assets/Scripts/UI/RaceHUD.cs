using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de la carrera: cronómetro, velocidad, mejor tiempo, cuenta regresiva y pantalla de meta.
/// Los textos solo se reescriben cuando cambia su valor, para no generar basura en cada frame.
/// </summary>
public class RaceHUD : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Bici de la que se lee la velocidad.")]
    [SerializeField] private BikeController bike = null;

    [Tooltip("Raíz del HUD de carrera; se oculta en la pantalla de inicio.")]
    [SerializeField] private GameObject hudRoot = null;

    [Tooltip("Cronómetro, arriba al centro.")]
    [SerializeField] private Text timeText = null;

    [Tooltip("Mejor tiempo, en pequeño bajo el cronómetro.")]
    [SerializeField] private Text bestTimeText = null;

    [Tooltip("Velocidad en km/h, abajo a la derecha.")]
    [SerializeField] private Text speedText = null;

    [Tooltip("Cuenta regresiva grande (3, 2, 1, ¡YA!).")]
    [SerializeField] private Text countdownText = null;

    [Tooltip("Panel con los controles; se ve solo durante la cuenta regresiva (opcional).")]
    [SerializeField] private GameObject controlsPanel = null;

    [Header("Energía")]
    [Tooltip("Relleno de la barra de energía: se estira de izquierda a derecha.")]
    [SerializeField] private RectTransform energyFill = null;

    [Tooltip("Imagen del relleno (cambia de color según la energía).")]
    [SerializeField] private Image energyFillImage = null;

    [Tooltip("Texto sobre la barra: \"ENERGÍA\" o \"AGOTADO\".")]
    [SerializeField] private Text energyLabel = null;

    [Tooltip("Color con mucha energía.")]
    [SerializeField] private Color energyHighColor = new Color(0.35f, 0.85f, 0.35f);

    [Tooltip("Color con energía media.")]
    [SerializeField] private Color energyMidColor = new Color(1f, 0.8f, 0.2f);

    [Tooltip("Color con poca energía o agotado.")]
    [SerializeField] private Color energyLowColor = new Color(0.95f, 0.25f, 0.15f);

    [Header("Pantalla de meta")]
    [Tooltip("Raíz de la pantalla de meta.")]
    [SerializeField] private GameObject finishRoot = null;

    [Tooltip("Tiempo final.")]
    [SerializeField] private Text finishTimeText = null;

    [Tooltip("Mejor tiempo en la pantalla de meta.")]
    [SerializeField] private Text finishBestText = null;

    [Tooltip("Texto \"¡Nuevo récord!\".")]
    [SerializeField] private Text newRecordText = null;

    [Header("Animación")]
    [Tooltip("Cuánto crece el número de la cuenta regresiva al aparecer.")]
    [SerializeField] private float countdownPunchScale = 1.6f;

    [Tooltip("Parpadeos por segundo del texto de récord.")]
    [SerializeField] private float recordBlinkSpeed = 2f;

    private GameManager gameManager;
    private int lastCentiseconds = -1;
    private int lastSpeed = -1;
    private string lastCountdown;
    private float countdownChangeTime;
    private RiderStamina stamina;
    private bool lastExhausted;

    private void Start()
    {
        gameManager = GameManager.Instance;
        if (bike != null) stamina = bike.Stamina;
        if (gameManager == null)
        {
            Debug.LogWarning("RaceHUD: no hay GameManager en la escena.", this);
            return;
        }
        gameManager.StateChanged += HandleStateChanged;
        gameManager.RaceFinished += HandleRaceFinished;
        HandleStateChanged(gameManager.State);
    }

    private void OnDestroy()
    {
        if (gameManager == null) return;
        gameManager.StateChanged -= HandleStateChanged;
        gameManager.RaceFinished -= HandleRaceFinished;
    }

    private void Update()
    {
        if (gameManager == null || hudRoot == null || !hudRoot.activeSelf) return;

        // Cronómetro.
        int centiseconds = Mathf.FloorToInt(gameManager.RaceTime * 100f);
        if (centiseconds != lastCentiseconds && timeText != null)
        {
            lastCentiseconds = centiseconds;
            timeText.text = GameManager.FormatTime(gameManager.RaceTime);
        }

        // Velocidad.
        if (bike != null && speedText != null)
        {
            int kmh = Mathf.RoundToInt(bike.SpeedKmh);
            if (kmh != lastSpeed)
            {
                lastSpeed = kmh;
                speedText.text = kmh.ToString();
            }
        }

        // Cuenta regresiva con un pequeño "golpe" de escala en cada número.
        if (countdownText != null)
        {
            string label = gameManager.CountdownLabel;
            if (label != lastCountdown)
            {
                lastCountdown = label;
                countdownText.text = label;
                countdownChangeTime = Time.unscaledTime;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - countdownChangeTime) / 0.25f);
            float scale = Mathf.Lerp(countdownPunchScale, 1f, 1f - (1f - t) * (1f - t));
            countdownText.rectTransform.localScale = Vector3.one * scale;
        }

        UpdateEnergy();

        // Récord parpadeando.
        if (newRecordText != null && newRecordText.gameObject.activeSelf)
        {
            Color color = newRecordText.color;
            color.a = 0.55f + 0.45f * Mathf.Cos(Time.unscaledTime * recordBlinkSpeed * Mathf.PI * 2f);
            newRecordText.color = color;
        }
    }

    private const string EnergyOffText = "ENERGÍA (APAGADA)";

    private void UpdateEnergy()
    {
        if (stamina == null) return;
        if (!GameSession.EnergyEnabled)
        {
            // Energía apagada mientras se prueban las pistas: la barra queda llena y lo dice.
            if (energyLabel != null && energyLabel.text != EnergyOffText) energyLabel.text = EnergyOffText;
            if (energyFill != null) energyFill.anchorMax = Vector2.one;
            return;
        }
        float energy = stamina.Energy01;
        if (energyFill != null) energyFill.anchorMax = new Vector2(energy, 1f);

        bool exhausted = stamina.IsExhausted;
        if (energyFillImage != null)
        {
            Color color = energy > 0.5f
                ? Color.Lerp(energyMidColor, energyHighColor, (energy - 0.5f) * 2f)
                : Color.Lerp(energyLowColor, energyMidColor, energy * 2f);
            if (exhausted)
            {
                // Parpadeo rojo para que se note sin leer el texto.
                float pulse = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * 6f * Mathf.PI);
                color = Color.Lerp(energyLowColor, Color.white, pulse * 0.4f);
            }
            energyFillImage.color = color;
        }

        // El texto solo se cambia cuando cambia el estado, para no generar basura cada frame.
        if (energyLabel != null && exhausted != lastExhausted)
        {
            energyLabel.text = exhausted ? "AGOTADO" : "ENERGÍA";
            energyLabel.color = exhausted ? energyLowColor : new Color(1f, 1f, 1f, 0.8f);
        }
        lastExhausted = exhausted;
    }

    private void HandleStateChanged(GameManager.RaceState state)
    {
        bool showHud = state != GameManager.RaceState.Inicio;
        if (hudRoot != null) hudRoot.SetActive(showHud);
        if (finishRoot != null) finishRoot.SetActive(state == GameManager.RaceState.Meta);
        if (controlsPanel != null) controlsPanel.SetActive(state == GameManager.RaceState.CuentaRegresiva);

        if (state == GameManager.RaceState.CuentaRegresiva)
        {
            lastCentiseconds = -1;
            lastCountdown = null;
        }
        RefreshBestTime();
    }

    private void HandleRaceFinished(float time, bool isNewRecord)
    {
        if (finishTimeText != null) finishTimeText.text = "Tiempo: " + GameManager.FormatTime(time);
        if (finishBestText != null) finishBestText.text = "Mejor: " + GameManager.FormatTime(gameManager.BestTime);
        if (newRecordText != null) newRecordText.gameObject.SetActive(isNewRecord);
        RefreshBestTime();
    }

    private void RefreshBestTime()
    {
        if (bestTimeText == null || gameManager == null) return;
        bestTimeText.text = gameManager.HasBestTime
            ? "Mejor  " + GameManager.FormatTime(gameManager.BestTime)
            : "Mejor  --:--.--";
    }
}
