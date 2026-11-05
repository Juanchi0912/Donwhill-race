using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el flujo de la partida: Inicio → Cuenta regresiva → Carrera → Meta.
/// Al llegar desde el menú, la cuenta regresiva empieza sola; desde la meta, Esc vuelve al menú.
/// Lleva el cronómetro y guarda el mejor tiempo. Los demás sistemas (bici, HUD, menú)
/// escuchan sus eventos en lugar de llamarse entre sí, para no acoplarlos.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum RaceState
    {
        Inicio,
        CuentaRegresiva,
        Carrera,
        Meta
    }

    // Clave por pista para que en el futuro cada pista tenga su propio récord.
    private const string BestTimeKeyPrefix = "DownhillRace_MejorTiempo_";

    public static GameManager Instance { get; private set; }

    [Header("Pista")]
    [Tooltip("Identificador de la pista; se usa para guardar su mejor tiempo.")]
    [SerializeField] private string trackId = "Pista01";

    [Tooltip("Empieza la cuenta regresiva apenas carga la escena (el menú ya quedó atrás). " +
             "Si se desactiva, espera a que se presione Enter.")]
    [SerializeField] private bool startOnLoad = true;

    [Header("Cuenta regresiva")]
    [Tooltip("Segundos de la cuenta regresiva antes del ¡YA! (3 → 3, 2, 1).")]
    [SerializeField, Min(1)] private int countdownSeconds = 3;

    [Tooltip("Segundos que el texto ¡YA! sigue en pantalla después de arrancar.")]
    [SerializeField, Min(0f)] private float goTextDuration = 1f;

    /// <summary>Se dispara cada vez que cambia el estado (nuevo estado).</summary>
    public event Action<RaceState> StateChanged;

    /// <summary>Se dispara al preparar una carrera nueva: la bici debe volver a la salida.</summary>
    public event Action RaceReset;

    /// <summary>Se dispara al cruzar la meta (tiempo final, ¿es récord?).</summary>
    public event Action<float, bool> RaceFinished;

    public RaceState State { get; private set; } = RaceState.Inicio;

    /// <summary>Tiempo de carrera actual en segundos.</summary>
    public float RaceTime { get; private set; }

    /// <summary>Mejor tiempo guardado, o 0 si todavía no hay ninguno.</summary>
    public float BestTime { get; private set; }

    public bool HasBestTime => BestTime > 0f;

    /// <summary>True si la última carrera terminada batió el récord.</summary>
    public bool IsNewRecord { get; private set; }

    /// <summary>La bici solo puede moverse durante la carrera (nunca antes del ¡YA!).</summary>
    public bool CanRiderMove => State == RaceState.Carrera;

    /// <summary>Texto grande de la cuenta: "3", "2", "1", "¡YA!" o vacío.</summary>
    public string CountdownLabel
    {
        get
        {
            if (State == RaceState.CuentaRegresiva)
            {
                // Ceil para que se vea "3" durante el primer segundo completo.
                return Mathf.CeilToInt(countdownRemaining).ToString();
            }
            if (State == RaceState.Carrera && RaceTime < goTextDuration)
            {
                return "¡YA!";
            }
            return string.Empty;
        }
    }

    private float countdownRemaining;
    private bool autoStartPending;

    private string BestTimeKey => BestTimeKeyPrefix + trackId;

    private void Awake()
    {
        // Solo debe haber un GameManager por escena.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Hay más de un GameManager en la escena; se elimina el duplicado.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
        autoStartPending = startOnLoad;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Se arranca en el primer Update y no en Awake/Start: así la bici, la cámara y el HUD
        // ya se suscribieron a los eventos, sin depender del orden de ejecución de los scripts.
        if (autoStartPending)
        {
            autoStartPending = false;
            StartRace();
            return;
        }

        switch (State)
        {
            case RaceState.Inicio:
                if (GameInput.ConfirmPressed) StartRace();
                break;

            case RaceState.CuentaRegresiva:
                if (GameInput.RestartPressed)
                {
                    StartRace();
                    break;
                }
                countdownRemaining -= Time.deltaTime;
                if (countdownRemaining <= 0f) BeginRacing();
                break;

            case RaceState.Carrera:
                RaceTime += Time.deltaTime;
                // Esc / Select reinicia la bajada desde la salida, sin pasar por el menú.
                if (GameInput.RestartPressed) StartRace();
                break;

            case RaceState.Meta:
                // Un momento de espera: el mismo botón A con que se saltó al cruzar la meta no reinicia sin querer.
                if (Time.time - finishedAt < FinishInputDelay) break;
                if (GameInput.ConfirmPressed) StartRace();
                else if (GameInput.BackPressed) ReturnToMenu();
                break;
        }
    }

    /// <summary>Prepara una carrera nueva: devuelve la bici a la salida y empieza la cuenta regresiva.</summary>
    public void StartRace()
    {
        RaceTime = 0f;
        IsNewRecord = false;
        countdownRemaining = countdownSeconds;
        RaceReset?.Invoke();
        SetState(RaceState.CuentaRegresiva);
    }

    /// <summary>Lo llama el trigger de meta. Ignora llamadas fuera de la carrera.</summary>
    public void FinishRace()
    {
        if (State != RaceState.Carrera) return;

        IsNewRecord = !HasBestTime || RaceTime < BestTime;
        if (IsNewRecord)
        {
            BestTime = RaceTime;
            PlayerPrefs.SetFloat(BestTimeKey, BestTime);
            PlayerPrefs.Save();
        }

        finishedAt = Time.time;
        SetState(RaceState.Meta);
        RaceFinished?.Invoke(RaceTime, IsNewRecord);
    }

    /// <summary>Vuelve al menú principal (sin la presentación). Si la escena Menu no existe, reinicia la carrera.</summary>
    public void ReturnToMenu()
    {
        if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene))
        {
            GameSession.SkipIntro = true;
            SceneManager.LoadScene(GameSession.MenuScene);
        }
        else
        {
            Debug.LogWarning("GameManager: la escena '" + GameSession.MenuScene + "' no está en Build Settings; se reinicia la carrera. " +
                             "Créala con DownhillRace → Crear escena del menú.");
            StartRace();
        }
    }

    /// <summary>Vuelve al estado Inicio con la bici en la salida (espera Enter para correr).</summary>
    public void ReturnToTitle()
    {
        RaceTime = 0f;
        IsNewRecord = false;
        RaceReset?.Invoke();
        SetState(RaceState.Inicio);
    }

    /// <summary>Borra el récord guardado (útil para pruebas desde el menú contextual).</summary>
    [ContextMenu("Borrar mejor tiempo")]
    public void ClearBestTime()
    {
        PlayerPrefs.DeleteKey(BestTimeKey);
        BestTime = 0f;
    }

    /// <summary>Formatea segundos como m:ss.cc (por ejemplo, 1:05.42).</summary>
    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int totalCentiseconds = Mathf.FloorToInt(seconds * 100f);
        int minutes = totalCentiseconds / 6000;
        int secs = (totalCentiseconds / 100) % 60;
        int centis = totalCentiseconds % 100;
        return $"{minutes}:{secs:00}.{centis:00}";
    }

    private void BeginRacing()
    {
        RaceTime = 0f;
        SetState(RaceState.Carrera);
    }

    private const float FinishInputDelay = 1f;
    private float finishedAt;

    private void SetState(RaceState newState)
    {
        State = newState;
        StateChanged?.Invoke(newState);
    }
}
