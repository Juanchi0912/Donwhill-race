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

    [Header("Atajos ocultos")]
    [Tooltip("Senderos angostos que cortan una curva: salen de un tramo y vuelven más abajo.")]
    [SerializeField] private List<TrackShortcut> shortcuts = new List<TrackShortcut>();

    [Header("Montañas a los lados")]
    [Tooltip("Crea un terreno con paredes de montaña a ambos lados de la pista, para que no se vea vacío.")]
    [SerializeField] private bool mountainWalls = true;

    [Tooltip("Altura máxima de las paredes sobre la pista, en metros.")]
    [SerializeField, Min(5f)] private float wallHeight = 70f;

    [Tooltip("Qué tan empinadas suben las paredes (metros de altura por metro de distancia).")]
    [SerializeField, Range(0.2f, 2f)] private float wallSteepness = 0.8f;

    // Versión del diseño original del que salió este asset: sirve para ofrecer actualizarlo cuando cambia.
    [SerializeField, HideInInspector] private int libraryVersion;

    public string DisplayName => displayName;
    public string SceneName => sceneName;
    public Theme Ambience => theme;
    public float DefaultHalfWidth => defaultHalfWidth;
    public float CheckpointSpacing => checkpointSpacing;
    public IReadOnlyList<TrackSection> Sections => sections;
    public IReadOnlyList<TrackShortcut> Shortcuts => shortcuts;
    public bool MountainWalls => mountainWalls;
    public float WallHeight => wallHeight;
    public float WallSteepness => wallSteepness;
    public int LibraryVersion => libraryVersion;

    /// <summary>Marca de qué versión del diseño original viene este asset.</summary>
    public void SetLibraryVersion(int version)
    {
        libraryVersion = version;
    }

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
    public void Configure(string display, string scene, Theme ambience, float halfWidth, IEnumerable<TrackSection> newSections,
        IEnumerable<TrackShortcut> newShortcuts = null)
    {
        displayName = display;
        sceneName = scene;
        theme = ambience;
        defaultHalfWidth = halfWidth;
        sections = new List<TrackSection>(newSections);
        shortcuts = newShortcuts != null ? new List<TrackShortcut>(newShortcuts) : new List<TrackShortcut>();
    }
}
