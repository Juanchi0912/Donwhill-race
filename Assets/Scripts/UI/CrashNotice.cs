using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra "¡CAÍDA!" mientras el corredor está en el suelo después de chocar fuerte.
/// Va en el objeto UI de la escena; lo crea la herramienta de mapas en las pistas con caídas.
/// </summary>
public class CrashNotice : MonoBehaviour
{
    [SerializeField] private BikeController bike = null;
    [SerializeField] private Text label = null;

    /// <summary>Lo usa la herramienta del Editor al crear el mapa.</summary>
    public void Configure(BikeController rider, Text text)
    {
        bike = rider;
        label = text;
    }

    private void Awake()
    {
        if (label != null) label.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (bike == null || label == null) return;
        bool show = bike.IsCrashed;
        if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
    }
}
