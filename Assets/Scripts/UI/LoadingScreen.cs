using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pantalla de carga: carga una escena en segundo plano y muestra el progreso con una barra.
/// Se queda un tiempo mínimo en pantalla para que no "parpadee" cuando la carga es muy rápida.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Raíz de la pantalla de carga (se oculta hasta que empieza una carga).")]
    [SerializeField] private GameObject root = null;

    [Tooltip("Relleno de la barra: se estira de izquierda a derecha según el progreso.")]
    [SerializeField] private RectTransform barFill = null;

    [Tooltip("Texto con el porcentaje (opcional).")]
    [SerializeField] private Text percentText = null;

    [Tooltip("Texto donde se muestra un consejo al azar (opcional).")]
    [SerializeField] private Text tipText = null;

    [Header("Ajustes")]
    [Tooltip("Segundos mínimos que se ve la pantalla de carga.")]
    [SerializeField, Min(0f)] private float minimumDuration = 1.2f;

    [Tooltip("Consejos que aparecen mientras carga.")]
    [SerializeField] private string[] tips =
    {
        "Agáchate con Shift en las rectas para llegar a 100 km/h.",
        "Las bermas de las curvas te ayudan a girar sin frenar tanto.",
        "Si te caes, presiona R para volver al último checkpoint.",
        "En la curva cerrada conviene frenar antes de entrar.",
        "Pedalear solo ayuda hasta unos 40 km/h: en bajada manda la pendiente.",
    };

    public bool IsLoading { get; private set; }

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    /// <summary>Muestra la pantalla y carga la escena. Ignora la llamada si ya está cargando.</summary>
    public void Load(string sceneName)
    {
        if (IsLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("LoadingScreen: la escena '" + sceneName + "' no está en Build Settings. " +
                           "Créala con el menú DownhillRace.", this);
            return;
        }
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        IsLoading = true;
        if (root != null) root.SetActive(true);
        if (tipText != null && tips != null && tips.Length > 0) tipText.text = tips[Random.Range(0, tips.Length)];
        SetProgress(0f);

        // Se deja un frame para que la pantalla se dibuje antes de empezar a cargar.
        yield return null;

        float start = Time.unscaledTime;
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        float shown = 0f;
        while (true)
        {
            // Unity llega a 0.9 cuando terminó de cargar y solo falta activar la escena.
            float loaded = Mathf.Clamp01(operation.progress / 0.9f);
            float timeLimit = minimumDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - start) / minimumDuration) : 1f;
            shown = Mathf.MoveTowards(shown, Mathf.Min(loaded, timeLimit), Time.unscaledDeltaTime * 2.5f);
            SetProgress(shown);
            if (shown >= 1f && operation.progress >= 0.9f) break;
            yield return null;
        }

        operation.allowSceneActivation = true;
    }

    private void SetProgress(float progress)
    {
        if (barFill != null) barFill.anchorMax = new Vector2(progress, 1f);
        if (percentText != null) percentText.text = Mathf.RoundToInt(progress * 100f) + "%";
    }
}
