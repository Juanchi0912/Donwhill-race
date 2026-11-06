using UnityEngine;

/// <summary>
/// Datos que viajan entre escenas (menú → carrera → menú): el modo elegido y
/// si al volver al menú hay que saltarse la presentación.
/// </summary>
public static class GameSession
{
    public const string MenuScene = "Menu";
    public const string RaceScene = "Pista01";
    // La Paz es un mapa real (heightmap de Yunguilla), creado con DownhillRace → Crear mapa desde heightmap.
    public const string LaPazScene = "LaPaz";
    // El Turi Bike Park vive en la escena del mapa real (la misma donde se diseñan rampas y puentes).
    public const string TuriScene = "MapaReal";

    /// <summary>Escena de la pista elegida en el menú.</summary>
    public static string SelectedTrackScene { get; set; } = RaceScene;

    /// <summary>
    /// Energía para pedalear (se gasta y hay que recuperarla). Apagada por ahora mientras se prueban
    /// las pistas largas, donde se hacía lenta. Para volver a activarla, cambia a true.
    /// </summary>
    public static readonly bool EnergyEnabled = false;

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
        SelectedTrackScene = RaceScene;
        SkipIntro = false;
    }
}
