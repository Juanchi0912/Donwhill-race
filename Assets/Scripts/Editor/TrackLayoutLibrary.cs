using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Jump = TrackSection.NaturalJump;
using Plants = TrackSection.Vegetation;
using Ramp = TrackSection.Ramp;

/// <summary>
/// Diseños de pista iniciales. Cada uno se guarda como asset en Assets/Settings/Pistas la primera vez;
/// desde ahí se edita en el Inspector y esos cambios se respetan (no se vuelven a sobrescribir).
/// </summary>
public static class TrackLayoutLibrary
{
    public const string Folder = "Assets/Settings/Pistas";
    private const float Narrow = 1.8f;  // Sendero angosto (líneas delgadas del mapa).

    public static TrackLayout GetPista01()
    {
        return GetOrCreate(Folder + "/Pista01_Diseno.asset", layout =>
            layout.Configure("Pista 1 · Bosque nublado", "Pista01", TrackLayout.Theme.BosqueNublado, 4.5f, Pista01Sections()));
    }

    public static TrackLayout GetLaPazYunguilla()
    {
        return GetOrCreate(Folder + "/LaPazYunguilla_Diseno.asset", layout =>
            layout.Configure("La Paz — Yunguilla", "Pista02", TrackLayout.Theme.ValleSeco, 2.5f, LaPazSections()));
    }

    private static TrackLayout GetOrCreate(string path, Action<TrackLayout> fill)
    {
        TrackLayout layout = AssetDatabase.LoadAssetAtPath<TrackLayout>(path);
        if (layout != null) return layout;

        VisualAssetFactory.EnsureFolder(Folder);
        layout = ScriptableObject.CreateInstance<TrackLayout>();
        fill(layout);
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

    /// <summary>
    /// La Paz — Yunguilla (Azuay), basada en el video de la carrera (≈ 1.29 km, 146 m de desnivel, ≈ 1:30 de bajada).
    /// Parte 1: salida en el pueblo y carretera de tierra, ladera abierta con un salto de madera, cresta con bermas,
    /// bajada empinada, el matorral angosto y el pedregal. Parte 2: cresta rápida con el salto grande, caída de la
    /// cresta, zigzag de horquillas cerradas, llegada al pueblo con el salto de concreto, sendero verde,
    /// carretera entre cañaverales, el río y la meta. Exagerada: horquillas más cerradas, bajadas más empinadas
    /// y piedras dentro del camino.
    /// </summary>
    private static List<TrackSection> LaPazSections()
    {
        const float Road = 3.6f, Mid = 2.5f, Hairpin = 2.4f;
        List<TrackSection> s = new List<TrackSection>();

        // --- Parte 1
        s.Add(new TrackSection("Salida en el pueblo", 30f, 0f, 3f, Road).AsRoad().WithBuildings().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Carretera 1", 30f, 20f, 6f, Road).AsRoad().WithBuildings().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Carretera 2", 30f, -35f, 8f, Road).AsRoad().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Carretera 3", 30f, 15f, 8f, Road).AsRoad().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Entrada al sendero", 20f, 40f, 5f, 3f).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Ladera abierta 1", 35f, 30f, 9f, Mid).WithVegetation(Plants.Escasa).WithRamp(Ramp.Pequena, 0.55f));
        s.Add(new TrackSection("Ladera abierta 2", 35f, -45f, 10f, Mid).WithVegetation(Plants.Escasa).WithJump(Jump.PiedraDerecha, 0.5f));
        s.Add(new TrackSection("Curva con berma", 30f, -100f, 7f, Mid).WithVegetation(Plants.Escasa));
        AddWiggles(s, "Cresta", 60f, new[] { -10f, 25f, -15f }, 8f, Plants.Normal, 2f);
        s[s.Count - 2].WithRoots();
        s[s.Count - 1].WithJump(Jump.PiedraIzquierda, 0.5f);
        s.Add(new TrackSection("Horquilla de tierra", 18f, 150f, 7f, Hairpin));
        s.Add(new TrackSection("Bajada empinada", 35f, -75f, 18f, 2.2f).WithRoots().WithJump(Jump.Raiz, 0.4f));
        s.Add(new TrackSection("Curva al matorral", 22f, 75f, 6f, Hairpin).WithVegetation(Plants.Densa));
        AddWiggles(s, "Matorral", 100f, new[] { -30f, 35f, -25f, 40f, -35f, 30f, -15f }, 6f, Plants.Densa, Narrow);
        s[s.Count - 6].WithRoots();
        s[s.Count - 4].WithRoots();
        s[s.Count - 3].WithJump(Jump.Raiz, 0.5f);
        s[s.Count - 2].WithRoots();
        s.Add(new TrackSection("Horquilla del matorral", 18f, -150f, 6f, Hairpin).WithVegetation(Plants.Densa));
        AddWiggles(s, "Pedregal", 70f, new[] { 25f, -30f, 20f, -25f, 10f }, 8f, Plants.Normal, Narrow);
        for (int i = s.Count - 5; i < s.Count; i++) s[i].WithRockGarden().WithRocks();
        s[s.Count - 3].WithJump(Jump.PiedraIzquierda, 0.5f);
        s.Add(new TrackSection("Horquilla del pedregal", 20f, 150f, 6f, Hairpin));

        // --- Parte 2
        AddWiggles(s, "Cresta rápida", 100f, new[] { 10f, -15f, 5f }, 9f, Plants.Escasa, Mid);
        s[s.Count - 2].WithRamp(Ramp.Grande, 0.45f);
        s[s.Count - 1].WithJump(Jump.PiedraDerecha, 0.5f);
        s.Add(new TrackSection("Curva al vacío", 28f, -75f, 7f, Mid).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Caída de la cresta", 40f, 0f, 18f, Hairpin).WithRocks().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Zigzag 1", 32f, -75f, 9f, 2f).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Horquilla Z1", 18f, 150f, 7f, Hairpin).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Zigzag 2", 32f, 0f, 9f, 2f).WithRoots().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Horquilla Z2", 18f, -150f, 7f, Hairpin).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Zigzag 3", 32f, 0f, 6f, 2f).WithJump(Jump.PiedraIzquierda, 0.4f).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Pequeña subida", 16f, 0f, -4f, 2f).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Horquilla Z3", 18f, 150f, 7f, Hairpin).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Llegada al pueblo", 40f, -75f, 4f, 3f).WithBuildings().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Calle del pueblo", 50f, 0f, 5f, Road).AsRoad().WithBuildings().WithVegetation(Plants.Escasa)
            .WithRamp(Ramp.Concreto, 0.55f));
        AddWiggles(s, "Sendero verde", 50f, new[] { -25f, 30f, -5f }, 5f, Plants.Verde, Narrow);
        s[s.Count - 2].WithRoots();
        s.Add(new TrackSection("Carretera entre cañaverales 1", 35f, 15f, 3f, 3f).AsRoad().WithVegetation(Plants.Canaveral));
        s.Add(new TrackSection("Carretera entre cañaverales 2", 35f, -25f, 3f, 3f).AsRoad().WithVegetation(Plants.Canaveral));
        s.Add(new TrackSection("Carretera entre cañaverales 3", 30f, 10f, 2f, 3f).AsRoad().WithVegetation(Plants.Canaveral));
        s.Add(new TrackSection("Bajada al río", 20f, 0f, 4f, 3f).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Río", 22f, 0f, 1f, 3.4f).WithWater().WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Salida del río", 12f, 0f, -3f, 3f).WithVegetation(Plants.Escasa));
        s.Add(new TrackSection("Meta", 55f, 0f, 1.5f, 3f).WithVegetation(Plants.Canaveral));
        return s;
    }

    /// <summary>Tramo "trabado": varias curvitas seguidas (las del mapa con muchas vueltas).</summary>
    private static void AddWiggles(List<TrackSection> list, string name, float total, float[] turns, float slope, Plants plants, float halfWidth)
    {
        for (int i = 0; i < turns.Length; i++)
        {
            list.Add(new TrackSection(name + " " + (i + 1), total / turns.Length, turns[i], slope, halfWidth).WithVegetation(plants));
        }
    }

    [MenuItem("DownhillRace/Restaurar diseño original de La Paz — Yunguilla", priority = 20)]
    private static void ResetLaPaz()
    {
        string path = Folder + "/LaPazYunguilla_Diseno.asset";
        if (!EditorUtility.DisplayDialog("Restaurar diseño", "Se borrarán tus cambios en " + path + " y se volverá al diseño original.", "Restaurar", "Cancelar")) return;
        AssetDatabase.DeleteAsset(path);
        Selection.activeObject = GetLaPazYunguilla();
    }
}
