using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Diseño de pista": la lista de tramos, el ambiente y la escena que se genera.
/// Se edita en el Inspector y se construye con el botón "Construir escena".
/// Crear uno nuevo: clic derecho en Project → Create → DownhillRace → Diseño de pista.
/// </summary>
[CreateAssetMenu(fileName = "NuevaPista_Diseno", menuName = "DownhillRace/Diseño de pista")]
public class TrackLayout : ScriptableObject
{
    public enum Theme
    {
        BosqueNublado,
        ValleSeco
    }

    [Header("Pista")]
    [Tooltip("Nombre que se muestra en el menú.")]
    [SerializeField] private string displayName = "Nueva pista";

    [Tooltip("Nombre de la escena que se genera (Assets/Scenes/<nombre>.unity). También identifica el récord.")]
    [SerializeField] private string sceneName = "Pista03";

    [Tooltip("Ambiente: texturas, vegetación, luz y niebla.")]
    [SerializeField] private Theme theme = Theme.BosqueNublado;

    [Tooltip("Medio ancho del camino cuando un tramo no dice otro (4.5 = 9 m de ancho).")]
    [SerializeField, Min(1f)] private float defaultHalfWidth = 4.5f;

    [Tooltip("Metros entre checkpoints (donde se reaparece con R).")]
    [SerializeField, Min(20f)] private float checkpointSpacing = 100f;

    [Header("Tramos (de la salida a la meta)")]
    [SerializeField] private List<TrackSection> sections = new List<TrackSection>();

    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public Theme Ambience => theme;
    public float DefaultHalfWidth => defaultHalfWidth;
    public float CheckpointSpacing => checkpointSpacing;
    public IReadOnlyList<TrackSection> Sections => sections;

    /// <summary>Largo total sumando los tramos (la pista real queda un poco más corta al suavizar las curvas).</summary>
    public float TotalLength
    {
        get
        {
            float total = 0f;
            foreach (TrackSection section in sections) total += section.Length;
            return total;
        }
    }

    /// <summary>Medio ancho efectivo de un tramo.</summary>
    public float HalfWidthOf(TrackSection section)
    {
        return section.HalfWidth > 0f ? section.HalfWidth : defaultHalfWidth;
    }

    /// <summary>Rellena el diseño desde código (lo usa el Editor para crear los diseños iniciales).</summary>
    public void Configure(string display, string scene, Theme ambience, float halfWidth, IEnumerable<TrackSection> newSections)
    {
        displayName = display;
        sceneName = scene;
        theme = ambience;
        defaultHalfWidth = halfWidth;
        sections = new List<TrackSection>(newSections);
    }
}
