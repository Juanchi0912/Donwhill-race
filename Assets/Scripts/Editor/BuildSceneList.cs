using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// Mantiene el orden de las escenas en Build Settings: el menú primero (el juego arranca ahí)
/// y luego la pista. Las demás escenas que ya existían quedan después, sin borrarse.
/// </summary>
public static class BuildSceneList
{
    public const string MenuScenePath = "Assets/Scenes/Menu.unity";
    public const string RaceScenePath = "Assets/Scenes/Pista01.unity";

    private static readonly string[] PreferredOrder = { MenuScenePath, RaceScenePath };

    /// <summary>Agrega la escena (si falta) y reordena la lista.</summary>
    public static void Register(string path)
    {
        List<EditorBuildSettingsScene> current = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!current.Exists(s => s.path == path)) current.Add(new EditorBuildSettingsScene(path, true));

        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>();
        foreach (string preferred in PreferredOrder)
        {
            EditorBuildSettingsScene found = current.Find(s => s.path == preferred);
            if (found != null) ordered.Add(new EditorBuildSettingsScene(found.path, true));
        }
        foreach (EditorBuildSettingsScene scene in current)
        {
            if (System.Array.IndexOf(PreferredOrder, scene.path) < 0) ordered.Add(scene);
        }
        EditorBuildSettings.scenes = ordered.ToArray();
    }
}
