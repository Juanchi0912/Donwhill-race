using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Navegación del menú: Presentación → Menú principal (Jugar, Configuraciones) → Modos de juego → Pistas → Carga.
/// Solo un panel se ve a la vez. Funciona con mouse, teclado y mando (Esc / Select vuelve atrás).
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private enum MenuScreen
    {
        Presentacion,
        Principal,
        Configuraciones,
        Modos,
        Pistas,
        Cargando
    }

    [Header("Pantallas")]
    [Tooltip("Presentación con el nombre del juego.")]
    [SerializeField] private GameObject introPanel = null;

    [Tooltip("Grupo que se desvanece en la presentación (el nombre del juego).")]
    [SerializeField] private CanvasGroup introFade = null;

    [Tooltip("Menú principal: Jugar y Configuraciones.")]
    [SerializeField] private GameObject mainPanel = null;

    [Tooltip("Pantalla de configuraciones (por ahora vacía).")]
    [SerializeField] private GameObject settingsPanel = null;

    [Tooltip("Pantalla de modos de juego.")]
    [SerializeField] private GameObject modesPanel = null;

    [Tooltip("Pantalla para elegir la pista de la carrera.")]
    [SerializeField] private GameObject tracksPanel = null;

    [Tooltip("Pantalla de carga que abre la carrera.")]
    [SerializeField] private LoadingScreen loadingScreen = null;

    [Header("Botones del menú principal")]
    [SerializeField] private Button playButton = null;
    [SerializeField] private Button settingsButton = null;

    [Header("Botones de configuraciones")]
    [SerializeField] private Button settingsBackButton = null;

    [Header("Botones de modos de juego")]
    [SerializeField] private Button arcadeButton = null;
    [SerializeField] private Button avalanchaButton = null;
    [SerializeField] private Button carreraButton = null;
    [SerializeField] private Button freerideButton = null;
    [SerializeField] private Button bmxButton = null;
    [SerializeField] private Button modesBackButton = null;

    [Header("Botones de pistas")]
    [SerializeField] private Button track1Button = null;
    [SerializeField] private Button track2Button = null;
    [SerializeField] private Button track3Button = null;
    [SerializeField] private Button tracksBackButton = null;

    [Tooltip("Texto que avisa \"Próximamente\" al elegir un modo que aún no existe.")]
    [SerializeField] private Text modesMessage = null;

    [Tooltip("Segundos que se ve el aviso de \"Próximamente\".")]
    [SerializeField] private float messageDuration = 2f;

    [Header("Presentación")]
    [Tooltip("Segundos que tarda en aparecer el nombre del juego.")]
    [SerializeField] private float introFadeIn = 0.8f;

    [Tooltip("Segundos que el nombre queda en pantalla.")]
    [SerializeField] private float introHold = 1.6f;

    [Tooltip("Segundos que tarda en desaparecer.")]
    [SerializeField] private float introFadeOut = 0.6f;

    private MenuScreen current;
    private float introTimer;
    private float messageTimer;
    private GameObject pendingSelection;
    private Button lastModeButton;

    private void Awake()
    {
        // Los botones se conectan por código: así no hay eventos ocultos en la escena.
        AddListener(playButton, () => Show(MenuScreen.Modos));
        AddListener(settingsButton, () => Show(MenuScreen.Configuraciones));
        AddListener(settingsBackButton, () => Show(MenuScreen.Principal));
        AddListener(modesBackButton, () => Show(MenuScreen.Principal));
        AddListener(arcadeButton, () => ChooseMode(GameMode.Arcade, arcadeButton));
        AddListener(avalanchaButton, () => ChooseMode(GameMode.Avalancha, avalanchaButton));
        AddListener(carreraButton, () => ChooseMode(GameMode.Carrera, carreraButton));
        AddListener(freerideButton, () => ChooseMode(GameMode.Freeride, freerideButton));
        AddListener(bmxButton, () => ChooseMode(GameMode.BMX, bmxButton));
        AddListener(track1Button, () => StartTrack(GameSession.RaceScene));
        AddListener(track2Button, () => StartTrack(GameSession.LaPazScene));
        AddListener(track3Button, () => StartTrack(GameSession.TuriScene));
        AddListener(tracksBackButton, () => Show(MenuScreen.Modos));
    }

    private void Start()
    {
        if (GameSession.SkipIntro)
        {
            // Al volver de una carrera se va directo al menú, sin repetir la presentación.
            GameSession.SkipIntro = false;
            Show(MenuScreen.Principal);
        }
        else
        {
            Show(MenuScreen.Presentacion);
        }
    }

    private void Update()
    {
        switch (current)
        {
            case MenuScreen.Presentacion:
                UpdateIntro();
                break;

            case MenuScreen.Configuraciones:
            case MenuScreen.Modos:
                if (GameInput.BackPressed) Show(MenuScreen.Principal);
                break;

            case MenuScreen.Pistas:
                if (GameInput.BackPressed) Show(MenuScreen.Modos);
                break;
        }

        UpdateMessage();
        RecoverSelection();
    }

    private void LateUpdate()
    {
        // La selección se aplica al final del frame: así el mismo Enter que cerró una pantalla
        // no "presiona" también el primer botón de la siguiente.
        if (pendingSelection != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(pendingSelection);
            pendingSelection = null;
        }
    }

    private void UpdateIntro()
    {
        introTimer += Time.unscaledDeltaTime;
        float total = introFadeIn + introHold + introFadeOut;

        if (introFade != null)
        {
            float alpha;
            if (introTimer < introFadeIn) alpha = introTimer / Mathf.Max(0.01f, introFadeIn);
            else if (introTimer < introFadeIn + introHold) alpha = 1f;
            else alpha = 1f - (introTimer - introFadeIn - introHold) / Mathf.Max(0.01f, introFadeOut);
            introFade.alpha = Mathf.Clamp01(alpha);
        }

        // Se puede saltar con cualquier botón, pero no en el primer instante (evita saltarla sin querer).
        bool skip = introTimer > 0.2f && GameInput.AnySkipPressed;
        if (skip || introTimer >= total) Show(MenuScreen.Principal);
    }

    private void ChooseMode(GameMode mode, Button source)
    {
        lastModeButton = source;
        if (!GameSession.IsAvailable(mode))
        {
            ShowMessage(GameSession.DisplayName(mode) + ": próximamente");
            return;
        }

        GameSession.SelectedMode = mode;
        // BMX tiene una sola pista por ahora: va directo a ella.
        if (mode == GameMode.BMX)
        {
            StartTrack(GameSession.BmxScene);
            return;
        }
        // Sin pantalla de pistas (escena del menú antigua), se corre directo la Pista 1.
        if (tracksPanel != null) Show(MenuScreen.Pistas);
        else StartTrack(GameSession.RaceScene);
    }

    private void StartTrack(string scene)
    {
        GameSession.SelectedTrackScene = scene;
        Show(MenuScreen.Cargando);
        if (loadingScreen != null) loadingScreen.Load(scene);
        else Debug.LogError("MainMenuController: falta la referencia a LoadingScreen.", this);
    }

    private void Show(MenuScreen screen)
    {
        current = screen;
        SetActive(introPanel, screen == MenuScreen.Presentacion);
        SetActive(mainPanel, screen == MenuScreen.Principal);
        SetActive(settingsPanel, screen == MenuScreen.Configuraciones);
        SetActive(modesPanel, screen == MenuScreen.Modos);
        SetActive(tracksPanel, screen == MenuScreen.Pistas);

        if (screen == MenuScreen.Presentacion)
        {
            introTimer = 0f;
            if (introFade != null) introFade.alpha = 0f;
        }
        if (screen != MenuScreen.Modos) HideMessage();

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Button first = DefaultButton(screen);
        pendingSelection = first != null ? first.gameObject : null;
    }

    private Button DefaultButton(MenuScreen screen)
    {
        switch (screen)
        {
            case MenuScreen.Principal: return playButton;
            case MenuScreen.Configuraciones: return settingsBackButton;
            case MenuScreen.Modos: return carreraButton;
            case MenuScreen.Pistas: return track1Button;
            default: return null;
        }
    }

    /// <summary>
    /// Si el mouse dejó todo sin seleccionar (clic en el fondo), el teclado o el mando
    /// vuelven a seleccionar un botón en cuanto se usan las flechas o el stick.
    /// </summary>
    private void RecoverSelection()
    {
        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null || pendingSelection != null) return;
        bool navigating = Mathf.Abs(GameInput.Steer) > 0.5f || GameInput.Throttle > 0.5f || GameInput.Brake > 0.5f;
        if (!navigating) return;

        Button fallback = current == MenuScreen.Modos && lastModeButton != null ? lastModeButton : DefaultButton(current);
        if (fallback != null) pendingSelection = fallback.gameObject;
    }

    private void ShowMessage(string text)
    {
        if (modesMessage == null) return;
        modesMessage.text = text;
        modesMessage.gameObject.SetActive(true);
        messageTimer = messageDuration;
    }

    private void HideMessage()
    {
        messageTimer = 0f;
        if (modesMessage != null) modesMessage.gameObject.SetActive(false);
    }

    private void UpdateMessage()
    {
        if (messageTimer <= 0f) return;
        messageTimer -= Time.unscaledDeltaTime;
        if (messageTimer <= 0f) HideMessage();
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }
}
