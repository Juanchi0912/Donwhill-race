using UnityEngine;

/// <summary>
/// Datos que viajan entre escenas (menú → carrera → menú): el modo elegido y
/// si al volver al menú hay que saltarse la presentación.
/// </summary>
public static class GameSession
{
    public const string MenuScene = "Menu";
    public const string RaceScene = "Pista01";

    /// <summary>Modo elegido en el menú.</summary>
    public static GameMode SelectedMode { get; set; } = GameMode.Carrera;

    /// <summary>True al volver desde una carrera: el menú abre directo, sin la presentación.</summary>
    public static bool SkipIntro { get; set; }

    /// <summary>Por ahora solo Carrera se puede jugar.</summary>
    public static bool IsAvailable(GameMode mode)
    {
        return mode == GameMode.Carrera;
    }

    /// <summary>Nombre para mostrar en pantalla.</summary>
    public static string DisplayName(GameMode mode)
    {
        switch (mode)
        {
            case GameMode.Arcade: return "Arcade";
            case GameMode.Avalancha: return "Avalancha";
            case GameMode.Carrera: return "Carrera";
            default: return "Freeride";
        }
    }

    // Los valores estáticos sobreviven entre sesiones de Play si se desactiva la recarga de dominio;
    // se reinician aquí para que cada Play empiece igual.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        SelectedMode = GameMode.Carrera;
        SkipIntro = false;
    }
}
