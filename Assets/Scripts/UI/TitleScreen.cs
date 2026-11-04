using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pantalla de inicio: título, modo de juego, texto parpadeante y controles.
/// Solo muestra y oculta la UI según el estado; el Enter lo atiende GameManager
/// y la vuelta de cámara alrededor del corredor la hace CameraFollow.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Raíz de la pantalla de inicio; se oculta al empezar la carrera.")]
    [SerializeField] private GameObject root = null;

    [Tooltip("Texto \"Presiona ENTER para comenzar\".")]
    [SerializeField] private Text pressStartText = null;

    [Tooltip("Texto con el mejor tiempo (opcional).")]
    [SerializeField] private Text bestTimeText = null;

    [Header("Parpadeo")]
    [Tooltip("Parpadeos por segundo del texto de inicio.")]
    [SerializeField] private float blinkSpeed = 1.2f;

    [Tooltip("Opacidad mínima durante el parpadeo.")]
    [SerializeField, Range(0f, 1f)] private float blinkMinAlpha = 0.15f;

    private GameManager gameManager;

    private void Start()
    {
        gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.StateChanged += HandleStateChanged;
            HandleStateChanged(gameManager.State);
        }
    }

    private void OnDestroy()
    {
        if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        if (pressStartText == null || root == null || !root.activeSelf) return;

        // Se usa unscaledTime para que el parpadeo siga aunque el juego se pause en el futuro.
        float wave = 0.5f + 0.5f * Mathf.Cos(Time.unscaledTime * blinkSpeed * Mathf.PI * 2f);
        Color color = pressStartText.color;
        color.a = Mathf.Lerp(blinkMinAlpha, 1f, wave);
        pressStartText.color = color;
    }

    private void HandleStateChanged(GameManager.RaceState state)
    {
        bool visible = state == GameManager.RaceState.Inicio;
        if (root != null) root.SetActive(visible);

        if (visible && bestTimeText != null)
        {
            bestTimeText.text = gameManager.HasBestTime
                ? "Mejor tiempo: " + GameManager.FormatTime(gameManager.BestTime)
                : string.Empty;
        }
    }
}
