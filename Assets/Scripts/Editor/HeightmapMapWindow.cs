using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana para crear un mapa a escala real desde un heightmap y luego ayudar a decorarlo
/// (quitar árboles de los senderos pintados, repartir árboles otra vez).
/// Menú: DownhillRace → Crear mapa desde heightmap.
/// </summary>
public class HeightmapMapWindow : EditorWindow
{
    private const string PrefsKey = "DownhillRace.MapaDesdeHeightmap";
    private static readonly int[] Resolutions = { 513, 1025, 2049 };
    private static readonly string[] ResolutionNames = { "513 (rápido)", "1025 (liviano)", "2049 (recomendado)" };

    [SerializeField] private HeightmapMapBuilder.Settings settings = new HeightmapMapBuilder.Settings();
    [SerializeField] private float trailMargin = 1.5f;
    private Vector2 scroll;

    [MenuItem("DownhillRace/Crear mapa desde heightmap", priority = 20)]
    public static void Open()
    {
        GetWindow<HeightmapMapWindow>("Mapa desde heightmap").minSize = new Vector2(380f, 520f);
    }

    private void OnEnable()
    {
        string json = EditorPrefs.GetString(PrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(json)) JsonUtility.FromJsonOverwrite(json, settings);
    }

    private void OnDisable()
    {
        SaveSettings();
    }

    private void SaveSettings()
    {
        EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(settings));
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Crea un terreno a escala real desde un heightmap (imagen en gris: claro = alto). " +
                                "Queda en una escena aparte: no se agrega al juego ni al menú.", MessageType.Info);

        // La ventana recuerda los valores de la última vez: este botón vuelve a los del Turi Bike Park.
        if (GUILayout.Button("Usar valores recomendados (Turi Bike Park)"))
        {
            settings = new HeightmapMapBuilder.Settings();
            SaveSettings();
            GUI.FocusControl(null);
        }

        EditorGUI.BeginChangeCheck();
        DrawHeightmapField();
        settings.MapName = EditorGUILayout.TextField(new GUIContent("Nombre del mapa", "Nombre de la escena y del terreno."), settings.MapName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Medidas reales de la imagen completa", EditorStyles.boldLabel);
        settings.Width = Mathf.Max(10f, EditorGUILayout.FloatField(new GUIContent("Ancho (oeste → este) m", "Lo que mide de lado a lado el área exportada."), settings.Width));
        settings.Length = Mathf.Max(10f, EditorGUILayout.FloatField(new GUIContent("Largo (sur → norte) m", "Lo que mide de abajo a arriba el área exportada."), settings.Length));
        settings.HeightDifference = Mathf.Max(1f, EditorGUILayout.FloatField(new GUIContent("Desnivel total m", "Del punto más bajo al más alto de la imagen."), settings.HeightDifference));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Relieve para el juego", EditorStyles.boldLabel);
        settings.ReliefExaggeration = EditorGUILayout.Slider(new GUIContent("Exagerar desnivel ×", "Multiplica la altura. Tal cual es la realidad, la bajada se siente suave en el juego."), settings.ReliefExaggeration, 1f, 2.5f);
        settings.ReliefAccent = EditorGUILayout.Slider(new GUIContent("Acentuar quebradas", "Hace más hondas las quebradas y más marcadas las lomas (0 = como el mapa)."), settings.ReliefAccent, 0f, 1f);
        EditorGUILayout.LabelField("Desnivel en el juego", "≈ " + (settings.HeightDifference * settings.ReliefExaggeration).ToString("0") + " m (más lo que sume el acento)");
        settings.AddTrails = EditorGUILayout.Toggle(new GUIContent("Senderos Turi Bike Park", "Talla y pinta las 4 rutas del Turi Bike Park. Solo calzan con el heightmap de Turi – Ictocruz."), settings.AddTrails);
        using (new EditorGUI.DisabledScope(!settings.AddTrails))
        {
            settings.Playable = EditorGUILayout.Toggle(new GUIContent("Jugable (menú Carrera)", "Pone la bici en la salida, cronómetro, checkpoints y meta, y agrega la escena al menú: Carrera → Turi Bike Park."), settings.Playable);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recorte (qué parte de la imagen usar)", EditorStyles.boldLabel);
        MinMax("Oeste → Este", ref settings.CropX);
        MinMax("Sur → Norte", ref settings.CropZ);
        EditorGUILayout.LabelField("El mapa medirá", settings.CroppedWidth.ToString("0") + " × " + settings.CroppedLength.ToString("0") + " m");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Detalle", EditorStyles.boldLabel);
        int current = System.Array.IndexOf(Resolutions, settings.Resolution);
        settings.Resolution = Resolutions[EditorGUILayout.Popup("Resolución", current < 0 ? 2 : current, ResolutionNames)];
        settings.SmoothMeters = EditorGUILayout.Slider(new GUIContent("Suavizado (m)", "Borra los escalones de la imagen. Más alto = más liso pero menos detalle."), settings.SmoothMeters, 0f, 20f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vegetación", EditorStyles.boldLabel);
        settings.Ambience = (TrackLayout.Theme)EditorGUILayout.EnumPopup(new GUIContent("Ambiente", "Bosque nublado: pinos y pasto verde. Valle seco: árboles bajos y pasto seco."), settings.Ambience);
        settings.TreesPerHectare = EditorGUILayout.Slider(new GUIContent("Árboles por hectárea", "Una hectárea = 100 × 100 m. Muchos árboles en un mapa grande pueden bajar los FPS."), settings.TreesPerHectare, 0f, 120f);
        settings.Seed = EditorGUILayout.IntField(new GUIContent("Semilla", "Cambia el número para otra distribución de árboles."), settings.Seed);
        if (EditorGUI.EndChangeCheck()) SaveSettings();

        EditorGUILayout.Space();
        if (GUILayout.Button("Crear mapa", GUILayout.Height(34f)))
        {
            SaveSettings();
            HeightmapMapBuilder.Build(settings);
            GUIUtility.ExitGUI();
        }

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Con el mapa abierto", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Para senderos nuevos o retoques: pinta con la capa «…_CapaTierra» (Terrain → Paint Texture) y luego quita los árboles que quedaron encima.", MessageType.None);
        trailMargin = EditorGUILayout.Slider(new GUIContent("Margen del sendero (m)", "Distancia libre de árboles a cada lado de lo pintado."), trailMargin, 0f, 6f);
        using (new EditorGUI.DisabledScope(FindTerrain() == null))
        {
            if (GUILayout.Button("Quitar árboles de los senderos")) HeightmapMapBuilder.ClearTreesOnTrails(FindTerrain(), trailMargin);
            if (GUILayout.Button("Repartir árboles otra vez (reemplaza los actuales)"))
            {
                if (EditorUtility.DisplayDialog("Repartir árboles", "Se borran todos los árboles del mapa (también los pintados a mano) y se reparten de nuevo, sin tocar los senderos.", "Repartir", "Cancelar"))
                {
                    HeightmapMapBuilder.RescatterTrees(FindTerrain(), settings);
                }
            }
        }
        if (FindTerrain() == null) EditorGUILayout.HelpBox("Abre la escena del mapa para usar estos botones.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawHeightmapField()
    {
        Texture2D current = AssetDatabase.LoadAssetAtPath<Texture2D>(settings.HeightmapPath);
        Texture2D picked = (Texture2D)EditorGUILayout.ObjectField(new GUIContent("Heightmap (PNG)", "Ponlo dentro de Assets/Mapas."), current, typeof(Texture2D), false);
        if (picked != current && picked != null)
        {
            settings.HeightmapPath = AssetDatabase.GetAssetPath(picked);
            // Algunas páginas ponen el desnivel en el nombre (…_331m.png): se usa si está.
            Match match = Regex.Match(settings.HeightmapPath, @"_(\d+(?:\.\d+)?)m\.png$");
            if (match.Success) settings.HeightDifference = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        if (picked == null)
        {
            EditorGUILayout.HelpBox("Arrastra aquí el PNG del heightmap (copiado dentro de Assets/Mapas).", MessageType.Warning);
        }
        else if (!settings.HeightmapPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
        {
            EditorGUILayout.HelpBox("El heightmap debe ser un archivo PNG.", MessageType.Error);
        }
        else if (PngHeightmapReader.TryReadInfo(settings.HeightmapPath, out int w, out int h, out int bits))
        {
            string text = w + " × " + h + " px, " + bits + " bits.";
            if (bits == 8 && settings.SmoothMeters < 4f)
            {
                EditorGUILayout.HelpBox(text + " Con 8 bits sube el suavizado a 5 m o más, o el terreno queda con escalones.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(text, MessageType.None);
            }
        }
    }

    private static void MinMax(string label, ref Vector2 range)
    {
        float min = range.x, max = range.y;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.MinMaxSlider(label, ref min, ref max, 0f, 1f);
        EditorGUILayout.LabelField(Mathf.RoundToInt(min * 100f) + "–" + Mathf.RoundToInt(max * 100f) + " %", GUILayout.Width(70f));
        EditorGUILayout.EndHorizontal();
        // Al menos un 5 % para no quedar con un terreno sin ancho.
        if (max - min < 0.05f)
        {
            if (min + 0.05f <= 1f) max = min + 0.05f;
            else min = max - 0.05f;
        }
        range = new Vector2(min, max);
    }

    private static Terrain FindTerrain()
    {
        if (Selection.activeGameObject != null)
        {
            Terrain selected = Selection.activeGameObject.GetComponent<Terrain>();
            if (selected != null) return selected;
        }
        return Object.FindFirstObjectByType<Terrain>();
    }
}
