using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Plants = TrackSection.Vegetation;
using Ramp = TrackSection.Ramp;

/// <summary>
/// Diseños de pista iniciales. Cada uno se guarda como asset en Assets/Settings/Pistas la primera vez;
/// desde ahí se edita en el Inspector y esos cambios se respetan (no se vuelven a sobrescribir).
/// </summary>
public static class TrackLayoutLibrary
{
    public const string Folder = "Assets/Settings/Pistas";

    // Sube este número cuando cambie el diseño original: los assets viejos ofrecen actualizarse.
    private const int Pista01Version = 1;

    public static TrackLayout GetPista01()
    {
        return GetOrCreate(Folder + "/Pista01_Diseno.asset", Pista01Version, layout =>
            layout.Configure("Pista 1 · Bosque nublado", "Pista01", TrackLayout.Theme.BosqueNublado, 4.5f, Pista01Sections()));
    }

    private static TrackLayout GetOrCreate(string path, int version, Action<TrackLayout> fill)
    {
        TrackLayout layout = AssetDatabase.LoadAssetAtPath<TrackLayout>(path);
        if (layout != null)
        {
            if (layout.LibraryVersion < version)
            {
                // El asset viene de un diseño más viejo: se pregunta antes de pisar los cambios del usuario.
                bool update = EditorUtility.DisplayDialog("Diseño de pista actualizado",
                    "Hay una versión nueva del diseño «" + layout.DisplayName + "». " +
                    "¿Actualizar " + path + "? Se perderán los cambios que le hiciste en el Inspector.",
                    "Actualizar", "Mantener el mío");
                if (update) fill(layout);
                layout.SetLibraryVersion(version);
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
            }
            return layout;
        }

        VisualAssetFactory.EnsureFolder(Folder);
        layout = ScriptableObject.CreateInstance<TrackLayout>();
        fill(layout);
        layout.SetLibraryVersion(version);
        AssetDatabase.CreateAsset(layout, path);
        AssetDatabase.SaveAssets();
        Debug.Log("DownhillRace: diseño de pista creado en " + path + ". Puedes editarlo en el Inspector.");
        return layout;
    }

    /// <summary>La Pista 1 original (≈ 548 m): curvas en S, dos rampas de madera, bosque y zona de rocas.</summary>
    private static List<TrackSection> Pista01Sections()
    {
        return new List<TrackSection>
        {
            new TrackSection("Salida", 30f, 0f, 2f),
            new TrackSection("Recta rápida", 70f, 0f, 9f),
            new TrackSection("Transición", 12f, 0f, 5f),
            new TrackSection("Curva S 1", 38f, 70f, 4.5f),
            new TrackSection("Entre curvas", 10f, 0f, 4.5f),
            new TrackSection("Curva S 2", 40f, -80f, 4.5f),
            new TrackSection("Antes del salto", 35f, 0f, 5f).WithRamp(Ramp.Pequena, 22f / 35f),
            new TrackSection("Caída del salto", 25f, 0f, 9f),
            new TrackSection("Bosque A", 25f, 20f, 5f).WithVegetation(Plants.Densa),
            new TrackSection("Bosque B", 25f, -25f, 5f).WithVegetation(Plants.Densa),
            new TrackSection("Bosque C", 25f, 15f, 5f).WithVegetation(Plants.Densa),
            new TrackSection("Rocas", 50f, -10f, 4.5f, 3.5f).WithRocks(),
            new TrackSection("Antes de la cerrada", 15f, 0f, 4f),
            new TrackSection("Curva cerrada", 28f, 135f, 3f),
            new TrackSection("Aproximación salto final", 30f, 0f, 5f).WithRamp(Ramp.Grande, 20f / 30f),
            new TrackSection("Caída salto final", 30f, 0f, 10f),
            new TrackSection("Sprint", 60f, 0f, 2f),
        };
    }
}
