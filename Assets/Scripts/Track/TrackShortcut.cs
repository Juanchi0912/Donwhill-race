using System;
using UnityEngine;

/// <summary>
/// Atajo oculto: un sendero angosto que sale de un tramo de la pista y vuelve a entrar más abajo,
/// cortando una horquilla o una curva. Es más corto pero más empinado, y su entrada queda
/// medio tapada por la vegetación, así no es obvio.
/// </summary>
[Serializable]
public class TrackShortcut
{
    [Tooltip("Nombre del atajo (solo para orientarse).")]
    [SerializeField] private string name = "Atajo";

    [Tooltip("Nombre exacto del tramo donde empieza el atajo.")]
    [SerializeField] private string fromSection = "";

    [Tooltip("Dónde sale, como fracción de ese tramo (0 = inicio, 1 = final).")]
    [SerializeField, Range(0f, 1f)] private float fromPosition = 0.7f;

    [Tooltip("Nombre exacto del tramo donde el atajo vuelve a la pista (más abajo).")]
    [SerializeField] private string toSection = "";

    [Tooltip("Dónde vuelve, como fracción de ese tramo.")]
    [SerializeField, Range(0f, 1f)] private float toPosition = 0.3f;

    [Tooltip("Medio ancho del sendero del atajo (1.1 = muy angosto).")]
    [SerializeField, Min(0.6f)] private float halfWidth = 1.1f;

    [Tooltip("Cuánto se curva el atajo hacia un lado, en metros (0 = recto).")]
    [SerializeField] private float bend = 2f;

    [Tooltip("Raíces cruzando el atajo.")]
    [SerializeField] private bool roots = true;

    public string Name => name;
    public string FromSection => fromSection;
    public float FromPosition => fromPosition;
    public string ToSection => toSection;
    public float ToPosition => toPosition;
    public float HalfWidth => halfWidth;
    public float Bend => bend;
    public bool Roots => roots;

    public TrackShortcut()
    {
    }

    public TrackShortcut(string name, string fromSection, float fromPosition, string toSection, float toPosition, float bend = 2f)
    {
        this.name = name;
        this.fromSection = fromSection;
        this.fromPosition = fromPosition;
        this.toSection = toSection;
        this.toPosition = toPosition;
        this.bend = bend;
    }
}
