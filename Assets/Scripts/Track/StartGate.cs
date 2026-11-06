using UnityEngine;

/// <summary>
/// Portón de salida de BMX: se queda parado durante la cuenta regresiva y cae hacia adelante con el ¡YA!.
/// Al reiniciar la carrera vuelve a levantarse. Va en el objeto del portón (el pivote es su borde de abajo).
/// </summary>
public class StartGate : MonoBehaviour
{
    [Tooltip("Segundos que tarda en caer.")]
    [SerializeField, Min(0.05f)] private float dropTime = 0.3f;

    private float progress;
    private bool dropping;
    private GameManager subscribed;

    private void Start()
    {
        subscribed = GameManager.Instance;
        if (subscribed != null) subscribed.StateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (subscribed != null) subscribed.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameManager.RaceState state)
    {
        if (state == GameManager.RaceState.Carrera)
        {
            dropping = true;
        }
        else if (state == GameManager.RaceState.CuentaRegresiva || state == GameManager.RaceState.Inicio)
        {
            dropping = false;
            progress = 0f;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        if (!dropping || progress >= 1f) return;
        progress = Mathf.Min(1f, progress + Time.deltaTime / dropTime);
        // Cae acelerando, como un portón que se suelta.
        transform.localRotation = Quaternion.Euler(90f * progress * progress, 0f, 0f);
    }
}
