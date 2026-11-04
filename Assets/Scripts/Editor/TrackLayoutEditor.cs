using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector del "Diseño de pista": muestra un resumen y el botón "Construir escena",
/// para editar los tramos y ver el resultado sin tocar código.
/// </summary>
[CustomEditor(typeof(TrackLayout))]
public class TrackLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TrackLayout layout = (TrackLayout)target;

        EditorGUILayout.HelpBox(
            layout.Sections.Count + " tramos · " + layout.TotalLength.ToString("0") + " m en total.\n" +
            "Edita los tramos (largo, giro, pendiente, ancho y qué hay en cada uno) y presiona Construir escena. " +
            "Giro: + derecha, − izquierda. Una horquilla es ±150° en unos 22 m.",
            MessageType.Info);

        if (GUILayout.Button("Construir escena  (" + layout.SceneName + ")", GUILayout.Height(32)))
        {
            AssetDatabase.SaveAssets();
            // Se construye después de dibujar el Inspector: cambiar de escena en medio del dibujo da errores.
            EditorApplication.delayCall += () => PrototypeSceneBuilder.BuildScene(layout);
        }

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
