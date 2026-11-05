using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using PT = ProceduralTextures;

/// <summary>
/// Construye por código la escena del menú (presentación, menú principal, configuraciones,
/// modos de juego y pantalla de carga) y la guarda en Assets/Scenes/Menu.unity, primera en Build Settings.
/// Menú: DownhillRace → Crear escena del menú.
/// </summary>
public static class MenuSceneBuilder
{
    private const float ModeCardSpacing = 420f;

    [MenuItem("DownhillRace/Crear escena del menú")]
    public static void CreateMenuScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string path = BuildSceneList.MenuScenePath;
        if (File.Exists(path) && !EditorUtility.DisplayDialog(
                "Crear escena del menú",
                "Ya existe " + path + ".\n¿Quieres reemplazarla por una escena nueva?",
                "Reemplazar", "Cancelar"))
        {
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildCamera();
        BuildEventSystem();
        BuildMenu();

        VisualAssetFactory.EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, path);
        BuildSceneList.Register(path);
        AssetDatabase.SaveAssets();
        Debug.Log("DownhillRace: escena del menú creada en " + path + ". Presiona Play para probarla.");
    }

    private static void BuildCamera()
    {
        // La UI se dibuja encima; la cámara solo pinta un color de fondo, sin 3D ni post-procesado.
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.035f, 0.045f);
        cam.cullingMask = 0;
        go.AddComponent<AudioListener>();
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;
    }

    private static void BuildEventSystem()
    {
        // El EventSystem permite usar los botones con mouse, teclado (flechas + Enter) y mando.
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void BuildMenu()
    {
        Font font = UiBuilder.DefaultFont;
        Texture2D background = VisualAssetFactory.SaveTexture(
            PT.MenuBackground(512, 512, new PT.Rgb(0.03f, 0.035f, 0.05f), new PT.Rgb(0.14f, 0.17f, 0.22f)),
            "FondoMenu", VisualAssetFactory.TextureKind.Interface, 512);

        Canvas canvas = UiBuilder.Canvas("UI", true);
        Transform root = canvas.transform;

        // --- Presentación: solo el nombre del juego, que aparece y se desvanece.
        RectTransform intro = UiBuilder.Stretch(UiBuilder.UIObject("PantallaPresentacion", root));
        UiBuilder.Background("Fondo", intro, null, Color.black);
        RectTransform introContent = UiBuilder.Stretch(UiBuilder.UIObject("Contenido", intro));
        CanvasGroup introFade = introContent.gameObject.AddComponent<CanvasGroup>();
        UiBuilder.Label("Titulo", introContent, font, "DOWNHILL RACE", 170, new Vector2(0.5f, 0.5f), Vector2.zero,
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(1800f, 260f));

        // --- Menú principal.
        RectTransform main = UiBuilder.Stretch(UiBuilder.UIObject("PantallaPrincipal", root));
        UiBuilder.Background("Fondo", main, background, Color.white);
        UiBuilder.Label("Titulo", main, font, "DOWNHILL RACE", 120, new Vector2(0.5f, 1f), new Vector2(0f, -260f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Button play = UiBuilder.Button("Jugar", main, font, "JUGAR", 46, new Vector2(0f, 20f), new Vector2(560f, 96f));
        Button settings = UiBuilder.Button("Configuraciones", main, font, "CONFIGURACIONES", 46, new Vector2(0f, -100f), new Vector2(560f, 96f));
        Footer(main, font, "↑ ↓  elegir     ·     Enter  aceptar");

        // --- Configuraciones (vacía por ahora).
        RectTransform config = UiBuilder.Stretch(UiBuilder.UIObject("PantallaConfiguraciones", root));
        UiBuilder.Background("Fondo", config, background, Color.white);
        UiBuilder.Label("Titulo", config, font, "CONFIGURACIONES", 90, new Vector2(0.5f, 1f), new Vector2(0f, -200f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        UiBuilder.Label("Proximamente", config, font, "Próximamente", 46, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
            new Color(1f, 1f, 1f, 0.7f), FontStyle.Italic, TextAnchor.MiddleCenter, false);
        Button configBack = UiBuilder.Button("Volver", config, font, "VOLVER", 40, new Vector2(0f, -230f), new Vector2(360f, 84f));
        Footer(config, font, "Enter  aceptar     ·     Esc  volver");

        // --- Modos de juego: cuatro tarjetas en fila; solo Carrera está disponible.
        RectTransform modes = UiBuilder.Stretch(UiBuilder.UIObject("PantallaModos", root));
        UiBuilder.Background("Fondo", modes, background, Color.white);
        UiBuilder.Label("Titulo", modes, font, "MODOS DE JUEGO", 90, new Vector2(0.5f, 1f), new Vector2(0f, -180f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Button arcade = ModeCard(modes, font, GameMode.Arcade, "Próximamente", 0);
        Button avalancha = ModeCard(modes, font, GameMode.Avalancha, "Próximamente", 1);
        Button carrera = ModeCard(modes, font, GameMode.Carrera, "Contrarreloj · 2 pistas", 2);
        Button freeride = ModeCard(modes, font, GameMode.Freeride, "Próximamente", 3);
        Text message = UiBuilder.Label("Aviso", modes, font, "Próximamente", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -190f),
            UiBuilder.Gold, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        message.gameObject.SetActive(false);
        Button modesBack = UiBuilder.Button("Volver", modes, font, "VOLVER", 40, new Vector2(0f, -320f), new Vector2(360f, 84f));
        Footer(modes, font, "← →  elegir     ·     Enter  aceptar     ·     Esc  volver");

        // --- Pistas de la carrera.
        RectTransform tracks = UiBuilder.Stretch(UiBuilder.UIObject("PantallaPistas", root));
        UiBuilder.Background("Fondo", tracks, background, Color.white);
        UiBuilder.Label("Titulo", tracks, font, "ELIGE LA PISTA", 90, new Vector2(0.5f, 1f), new Vector2(0f, -180f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Button track1 = TrackCard(tracks, font, "PISTA 1", "Bosque nublado  ·  0,55 km", new Vector2(-300f, 20f));
        // La segunda pista se presenta por su nombre ("La Paz"), nunca como "Pista 2".
        Button track2 = TrackCard(tracks, font, "LA PAZ", "Yunguilla, Azuay  ·  1,3 km", new Vector2(300f, 20f));
        Button tracksBack = UiBuilder.Button("Volver", tracks, font, "VOLVER", 40, new Vector2(0f, -320f), new Vector2(360f, 84f));
        Footer(tracks, font, "← →  elegir     ·     Enter  aceptar     ·     Esc  volver");

        // --- Pantalla de carga.
        RectTransform loading = UiBuilder.Stretch(UiBuilder.UIObject("PantallaCarga", root));
        UiBuilder.Background("Fondo", loading, background, Color.white);
        UiBuilder.Label("Titulo", loading, font, "CARGANDO", 80, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        RectTransform bar = UiBuilder.Panel("Barra", loading, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
            new Vector2(1000f, 26f), new Color(1f, 1f, 1f, 0.12f));
        RectTransform fill = UiBuilder.Panel("Relleno", bar, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, UiBuilder.Accent);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        Text percent = UiBuilder.Label("Porcentaje", loading, font, "0%", 34, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
            new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold, TextAnchor.MiddleCenter, false);
        Text tip = UiBuilder.Label("Consejo", loading, font, string.Empty, 30, new Vector2(0.5f, 0f), new Vector2(0f, 140f),
            new Color(1f, 1f, 1f, 0.7f), FontStyle.Italic, TextAnchor.MiddleCenter, false);

        // --- Componentes y referencias.
        LoadingScreen loadingScreen = canvas.gameObject.AddComponent<LoadingScreen>();
        SetReference(loadingScreen, "root", loading.gameObject);
        SetReference(loadingScreen, "barFill", fill);
        SetReference(loadingScreen, "percentText", percent);
        SetReference(loadingScreen, "tipText", tip);

        MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();
        SetReference(controller, "introPanel", intro.gameObject);
        SetReference(controller, "introFade", introFade);
        SetReference(controller, "mainPanel", main.gameObject);
        SetReference(controller, "settingsPanel", config.gameObject);
        SetReference(controller, "modesPanel", modes.gameObject);
        SetReference(controller, "loadingScreen", loadingScreen);
        SetReference(controller, "playButton", play);
        SetReference(controller, "settingsButton", settings);
        SetReference(controller, "settingsBackButton", configBack);
        SetReference(controller, "arcadeButton", arcade);
        SetReference(controller, "avalanchaButton", avalancha);
        SetReference(controller, "carreraButton", carrera);
        SetReference(controller, "freerideButton", freeride);
        SetReference(controller, "modesBackButton", modesBack);
        SetReference(controller, "modesMessage", message);
        SetReference(controller, "tracksPanel", tracks.gameObject);
        SetReference(controller, "track1Button", track1);
        SetReference(controller, "track2Button", track2);
        SetReference(controller, "tracksBackButton", tracksBack);

        // Estado inicial en el Editor: solo la presentación visible (el controlador lo maneja en Play).
        main.gameObject.SetActive(false);
        config.gameObject.SetActive(false);
        modes.gameObject.SetActive(false);
        tracks.gameObject.SetActive(false);
        loading.gameObject.SetActive(false);
    }

    /// <summary>Tarjeta de un modo de juego: nombre grande y una línea debajo.</summary>
    private static Button ModeCard(Transform parent, Font font, GameMode mode, string subtitle, int index)
    {
        float x = (index - 1.5f) * ModeCardSpacing;
        Button card = UiBuilder.Button("Modo" + mode, parent, font, string.Empty, 50, new Vector2(x, 20f), new Vector2(380f, 260f));
        bool available = GameSession.IsAvailable(mode);
        UiBuilder.Label("Nombre", card.transform, font, GameSession.DisplayName(mode).ToUpperInvariant(), 50, new Vector2(0.5f, 0.5f), new Vector2(0f, 35f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, false, new Vector2(360f, 80f));
        UiBuilder.Label("Detalle", card.transform, font, subtitle, 26, new Vector2(0.5f, 0.5f), new Vector2(0f, -45f),
            available ? UiBuilder.Gold : new Color(1f, 1f, 1f, 0.55f), FontStyle.Italic, TextAnchor.MiddleCenter, false, new Vector2(360f, 50f));
        return card;
    }

    /// <summary>Tarjeta de una pista: nombre y una línea con el ambiente y el largo.</summary>
    private static Button TrackCard(Transform parent, Font font, string title, string subtitle, Vector2 position)
    {
        Button card = UiBuilder.Button("Pista " + title, parent, font, string.Empty, 46, position, new Vector2(560f, 260f));
        UiBuilder.Label("Nombre", card.transform, font, title, 46, new Vector2(0.5f, 0.5f), new Vector2(0f, 35f),
            UiBuilder.White, FontStyle.Bold, TextAnchor.MiddleCenter, false, new Vector2(540f, 80f));
        UiBuilder.Label("Detalle", card.transform, font, subtitle, 28, new Vector2(0.5f, 0.5f), new Vector2(0f, -45f),
            UiBuilder.Gold, FontStyle.Italic, TextAnchor.MiddleCenter, false, new Vector2(540f, 50f));
        return card;
    }

    private static void Footer(Transform parent, Font font, string text)
    {
        UiBuilder.Label("Ayuda", parent, font, text, 26, new Vector2(0.5f, 0f), new Vector2(0f, 60f),
            new Color(1f, 1f, 1f, 0.6f), FontStyle.Normal, TextAnchor.MiddleCenter, false);
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError("MenuSceneBuilder: " + target.GetType().Name + " no tiene el campo '" + field + "'.");
            return;
        }
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
