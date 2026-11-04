using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Construye por código la escena completa del prototipo (pista, bici, Corredor 1, cámara,
/// UI y luces) y la guarda en Assets/Scenes/Pista01.unity. Así nadie tiene que armar
/// la escena a mano ni editar archivos .unity.
/// Menú: DownhillRace → Crear escena del prototipo.
/// </summary>
public static class PrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Pista01.unity";
    private const string MaterialsFolder = "Assets/Art/Materials/Prototipo";
    private const string ModelsFolder = "Assets/Art/Models/Prototipo";

    // Colores originales del prototipo.
    private static readonly Color SkyColor = new Color(0.62f, 0.76f, 0.90f);
    private static readonly Color FogColor = new Color(0.70f, 0.80f, 0.88f);

    private const float StartDistance = 6f;       // Dónde arranca la bici (m desde el inicio de la pista).
    private const float StartArchDistance = 10f;
    private const float FinishBeforeEnd = 28f;    // La meta queda a 28 m del final, para frenar antes de la barrera.
    private const float CheckpointSpacing = 100f;
    private const float RampAngle = 14f;

    /// <summary>Un tramo del recorrido, descrito como lo recorrería una "tortuga".</summary>
    private struct Tramo
    {
        public string Nombre;
        public float Largo;      // metros
        public float Giro;       // grados totales (positivo = derecha)
        public float Pendiente;  // grados de bajada
        public float MedioAncho; // medio ancho del camino

        public Tramo(string nombre, float largo, float giro, float pendiente, float medioAncho = 4.5f)
        {
            Nombre = nombre;
            Largo = largo;
            Giro = giro;
            Pendiente = pendiente;
            MedioAncho = medioAncho;
        }
    }

    // Recorrido de unos 548 m y 50 m de desnivel, siempre bajando, según la tabla de tramos del plan.
    // Pendientes elegidas para que una bajada buena dure unos 45-60 s.
    private static readonly Tramo[] Tramos =
    {
        new Tramo("Salida", 30f, 0f, 2f),
        new Tramo("Recta rápida", 70f, 0f, 9f),
        new Tramo("Transición", 12f, 0f, 5f),
        new Tramo("Curva S 1", 38f, 70f, 4.5f),
        new Tramo("Entre curvas", 10f, 0f, 4.5f),
        new Tramo("Curva S 2", 40f, -80f, 4.5f),
        new Tramo("Antes del salto", 35f, 0f, 5f),
        new Tramo("Caída del salto", 25f, 0f, 9f),
        new Tramo("Bosque A", 25f, 20f, 5f),
        new Tramo("Bosque B", 25f, -25f, 5f),
        new Tramo("Bosque C", 25f, 15f, 5f),
        new Tramo("Rocas", 50f, -10f, 4.5f, 3.5f),
        new Tramo("Antes de la cerrada", 15f, 0f, 4f),
        new Tramo("Curva cerrada", 28f, 135f, 3f),
        new Tramo("Aproximación salto final", 30f, 0f, 5f),
        new Tramo("Caída salto final", 30f, 0f, 10f),
        new Tramo("Sprint", 60f, 0f, 2f),
    };

    /// <summary>Materiales compartidos por toda la escena.</summary>
    private class Palette
    {
        public Material Tierra, Berma, Pasto;
        public Material Tronco, Hojas, HojasClaras, Roca, Rampa;
        public Material Blanco, Rojo, Negro, AzulSalida;
        public Material Cuadro, Llanta, Aro, Metal, Jersey, Casco, Piel, RopaOscura;
        public PhysicsMaterial SinFriccion;
        public Mesh Cono;
    }

    [MenuItem("DownhillRace/Crear escena del prototipo")]
    public static void CreatePrototypeScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog(
                "Crear escena del prototipo",
                "Ya existe " + ScenePath + ".\n¿Quieres reemplazarla por una escena nueva?",
                "Reemplazar", "Cancelar"))
        {
            return;
        }

        try
        {
            Progress("Preparando materiales", 0.05f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Palette palette = CreatePalette();

            new GameObject("GameManager").AddComponent<GameManager>();

            Progress("Generando la pista", 0.2f);
            List<float> tramoStarts;
            TrackGenerator track = BuildTrack(palette, out tramoStarts);

            Progress("Armando la bici y el Corredor 1", 0.45f);
            BikeController bike = BuildBikeAndRider(palette, track);

            Progress("Colocando checkpoints, rampas, rocas y árboles", 0.6f);
            PopulateTrack(track, tramoStarts, palette);
            ConfigureFallHeight(bike, track);

            Progress("Creando cámara, luces y UI", 0.85f);
            BuildCamera(bike);
            BuildLighting();
            BuildUI(bike);
            ApplyPerformanceSettings();

            Progress("Guardando la escena", 0.95f);
            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneFirstInBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = bike.gameObject;
            Debug.Log("DownhillRace: escena creada en " + ScenePath + ". Presiona Play para probarla.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // =====================================================================
    // Fase 3: bici y Corredor 1
    // =====================================================================

    /// <summary>
    /// Crea la esfera física y el modelo visual con primitivas, con la jerarquía
    /// Inclinacion → (Cuadro, RuedaTrasera, Direccion → RuedaDelantera, Corredor).
    /// </summary>
    private static BikeController BuildBikeAndRider(Palette p, TrackGenerator track)
    {
        int startIndex = track.GetIndexAtDistance(StartDistance);
        Vector3 startPoint = track.GetSurfacePoint(startIndex, 0f);
        Vector3 flat = Flat(track.GetForward(startIndex));
        Quaternion startRotation = Quaternion.LookRotation(flat, Vector3.up);

        // --- Esfera física invisible.
        GameObject physics = new GameObject("Bici");
        physics.transform.SetPositionAndRotation(startPoint + Vector3.up * 0.55f, startRotation);
        SphereCollider sphere = physics.AddComponent<SphereCollider>();
        sphere.radius = 0.5f;
        sphere.sharedMaterial = p.SinFriccion;
        Rigidbody rb = physics.AddComponent<Rigidbody>();
        rb.mass = 80f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.freezeRotation = true;
        BikeController controller = physics.AddComponent<BikeController>();

        // --- Modelo visual. Se arma en el origen con coordenadas locales de la bici
        // (x = derecha, y = arriba, z = adelante; el origen es el punto de contacto con el suelo).
        GameObject visual = new GameObject("BiciVisual");
        Transform root = visual.transform;
        Transform inclinacion = Pivot("Inclinacion", root, Vector3.zero);

        // Cuadro.
        Transform cuadro = Pivot("Cuadro", inclinacion, Vector3.zero);
        Vector3 rearAxle = new Vector3(0f, 0.35f, -0.575f);
        Vector3 frontAxle = new Vector3(0f, 0.35f, 0.575f);
        Vector3 bottomBracket = new Vector3(0f, 0.30f, -0.05f);
        Vector3 seatTop = new Vector3(0f, 0.84f, -0.27f);
        Vector3 headTop = new Vector3(0f, 0.92f, 0.40f);
        Vector3 headBottom = new Vector3(0f, 0.74f, 0.45f);

        Tube("TuboAsiento", cuadro, bottomBracket, seatTop, 0.05f, p.Cuadro);
        Tube("TuboSuperior", cuadro, new Vector3(0f, 0.80f, -0.24f), headTop, 0.05f, p.Cuadro);
        Tube("TuboInferior", cuadro, bottomBracket, headBottom, 0.06f, p.Cuadro);
        Tube("TuboDireccion", cuadro, headBottom, headTop, 0.07f, p.Cuadro);
        foreach (float x in new[] { -0.06f, 0.06f })
        {
            Tube("Vaina", cuadro, new Vector3(x, 0.30f, -0.05f), new Vector3(x, 0.35f, -0.575f), 0.03f, p.Cuadro);
            Tube("Tirante", cuadro, new Vector3(x * 0.6f, 0.78f, -0.25f), new Vector3(x, 0.35f, -0.575f), 0.025f, p.Cuadro);
        }
        Tube("Tija", cuadro, seatTop, new Vector3(0f, 0.88f, -0.29f), 0.03f, p.Metal);
        Part(PrimitiveType.Cube, "Asiento", cuadro, new Vector3(0f, 0.90f, -0.30f), new Vector3(0.13f, 0.05f, 0.26f), Quaternion.identity, p.Negro);
        Part(PrimitiveType.Cylinder, "Eje", cuadro, bottomBracket, new Vector3(0.08f, 0.06f, 0.08f), Quaternion.Euler(0f, 0f, 90f), p.Metal);
        Vector3 pedalFront = new Vector3(-0.13f, 0.30f, 0.12f);
        Vector3 pedalBack = new Vector3(0.13f, 0.30f, -0.22f);
        Tube("Biela", cuadro, new Vector3(-0.09f, 0.30f, -0.05f), pedalFront, 0.025f, p.Metal);
        Tube("Biela", cuadro, new Vector3(0.09f, 0.30f, -0.05f), pedalBack, 0.025f, p.Metal);
        Part(PrimitiveType.Cube, "Pedal", cuadro, pedalFront, new Vector3(0.1f, 0.02f, 0.09f), Quaternion.identity, p.Negro);
        Part(PrimitiveType.Cube, "Pedal", cuadro, pedalBack, new Vector3(0.1f, 0.02f, 0.09f), Quaternion.identity, p.Negro);

        // Ruedas y dirección.
        Transform ruedaTrasera = Wheel("RuedaTrasera", inclinacion, rearAxle, p);
        Transform direccion = Pivot("Direccion", inclinacion, new Vector3(0f, 0f, 0.5f));
        foreach (float x in new[] { -0.055f, 0.055f })
        {
            Tube("Horquilla", direccion, new Vector3(x, 0.74f, 0.45f), new Vector3(x, 0.35f, 0.575f), 0.035f, p.Metal);
        }
        Part(PrimitiveType.Cube, "Corona", direccion, new Vector3(0f, 0.74f, 0.45f), new Vector3(0.15f, 0.04f, 0.06f), Quaternion.identity, p.Metal);
        Tube("Potencia", direccion, headTop, new Vector3(0f, 0.98f, 0.47f), 0.04f, p.RopaOscura);
        Tube("Manubrio", direccion, new Vector3(-0.39f, 0.99f, 0.47f), new Vector3(0.39f, 0.99f, 0.47f), 0.03f, p.RopaOscura);
        Tube("Puno", direccion, new Vector3(-0.39f, 0.99f, 0.47f), new Vector3(-0.29f, 0.99f, 0.47f), 0.04f, p.Negro);
        Tube("Puno", direccion, new Vector3(0.29f, 0.99f, 0.47f), new Vector3(0.39f, 0.99f, 0.47f), 0.04f, p.Negro);
        Transform ruedaDelantera = Wheel("RuedaDelantera", direccion, frontAxle, p);

        // --- Corredor 1, de pie sobre los pedales en posición de ataque.
        Transform corredor = Pivot("Corredor", inclinacion, Vector3.zero);
        Transform piernas = Pivot("Piernas", corredor, new Vector3(0f, 0.30f, 0f));
        BuildLeg(piernas, -1f, new Vector3(-0.10f, 0.98f, -0.22f), new Vector3(-0.16f, 0.66f, 0.08f), new Vector3(-0.13f, 0.36f, 0.12f), p);
        BuildLeg(piernas, 1f, new Vector3(0.10f, 0.98f, -0.22f), new Vector3(0.16f, 0.64f, -0.04f), new Vector3(0.13f, 0.36f, -0.22f), p);

        Transform torso = Pivot("Torso", corredor, new Vector3(0f, 0.98f, -0.20f));
        Part(PrimitiveType.Cube, "Cadera", torso, new Vector3(0f, 0.98f, -0.21f), new Vector3(0.30f, 0.16f, 0.20f), Quaternion.identity, p.RopaOscura);
        Box("Pecho", torso, new Vector3(0f, 1.0f, -0.18f), new Vector3(0f, 1.42f, 0.08f), new Vector2(0.34f, 0.20f), p.Jersey);
        Tube("Cuello", torso, new Vector3(0f, 1.40f, 0.07f), new Vector3(0f, 1.48f, 0.12f), 0.08f, p.Piel);
        Part(PrimitiveType.Sphere, "Cabeza", torso, new Vector3(0f, 1.55f, 0.16f), new Vector3(0.19f, 0.21f, 0.21f), Quaternion.identity, p.Piel);
        Part(PrimitiveType.Sphere, "Casco", torso, new Vector3(0f, 1.60f, 0.13f), new Vector3(0.26f, 0.22f, 0.32f), Quaternion.identity, p.Casco);
        Part(PrimitiveType.Cube, "Visera", torso, new Vector3(0f, 1.62f, 0.30f), new Vector3(0.22f, 0.03f, 0.10f), Quaternion.Euler(-10f, 0f, 0f), p.Casco);
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 shoulder = new Vector3(0.18f * side, 1.38f, 0.04f);
            Vector3 elbow = new Vector3(0.30f * side, 1.17f, 0.22f);
            Vector3 hand = new Vector3(0.33f * side, 1.0f, 0.45f);
            Tube("Brazo", torso, shoulder, elbow, 0.10f, p.Jersey);
            Tube("Antebrazo", torso, elbow, hand, 0.085f, p.Jersey);
            Part(PrimitiveType.Sphere, "Guante", torso, hand, Vector3.one * 0.09f, Quaternion.identity, p.Negro);
        }

        BikeVisuals visuals = visual.AddComponent<BikeVisuals>();
        SetReference(visuals, "controller", controller);
        SetReference(visuals, "inclinacion", inclinacion);
        SetReference(visuals, "ruedaTrasera", ruedaTrasera);
        SetReference(visuals, "ruedaDelantera", ruedaDelantera);
        SetReference(visuals, "direccion", direccion);
        SetReference(visuals, "piernas", piernas);
        SetReference(visuals, "torso", torso);

        // Recién armado, el modelo se lleva a la salida.
        root.SetPositionAndRotation(startPoint, startRotation);
        return controller;
    }

    private static void BuildLeg(Transform parent, float side, Vector3 hip, Vector3 knee, Vector3 ankle, Palette p)
    {
        Tube("Muslo", parent, hip, knee, 0.13f, p.RopaOscura);
        Tube("Pierna", parent, knee, ankle, 0.10f, p.RopaOscura);
        Part(PrimitiveType.Cube, "Zapatilla", parent, ankle + new Vector3(0f, -0.03f, 0.02f), new Vector3(0.10f, 0.08f, 0.22f), Quaternion.identity, p.Negro);
    }

    /// <summary>Rueda: pivote en el eje (lo gira BikeVisuals) con llanta, aro, buje y rayos.</summary>
    private static Transform Wheel(string name, Transform parent, Vector3 axle, Palette p)
    {
        Transform pivot = Pivot(name, parent, axle);
        Quaternion sideways = Quaternion.Euler(0f, 0f, 90f); // El cilindro queda con su eje en X.
        Part(PrimitiveType.Cylinder, "Llanta", pivot, axle, new Vector3(0.70f, 0.03f, 0.70f), sideways, p.Llanta);
        Part(PrimitiveType.Cylinder, "Aro", pivot, axle, new Vector3(0.52f, 0.035f, 0.52f), sideways, p.Aro);
        Part(PrimitiveType.Cylinder, "Buje", pivot, axle, new Vector3(0.08f, 0.06f, 0.08f), sideways, p.RopaOscura);
        // Rayos oscuros en cruz: hacen visible el giro de la rueda.
        Part(PrimitiveType.Cube, "Rayos", pivot, axle, new Vector3(0.075f, 0.48f, 0.025f), Quaternion.identity, p.RopaOscura);
        Part(PrimitiveType.Cube, "Rayos", pivot, axle, new Vector3(0.075f, 0.025f, 0.48f), Quaternion.identity, p.RopaOscura);
        return pivot;
    }

    // =====================================================================
    // Fase 4: pista
    // =====================================================================

    private static TrackGenerator BuildTrack(Palette p, out List<float> tramoStarts)
    {
        // Se recorren los tramos como una "tortuga": cada 5 m se avanza, se gira y se baja.
        List<Vector3> points = new List<Vector3>();
        List<float> widths = new List<float>();
        tramoStarts = new List<float>();

        Vector3 position = Vector3.zero;
        float heading = 0f;
        float distance = 0f;
        points.Add(position);
        widths.Add(Tramos[0].MedioAncho);

        foreach (Tramo tramo in Tramos)
        {
            tramoStarts.Add(distance);
            int steps = Mathf.Max(1, Mathf.CeilToInt(tramo.Largo / 5f));
            float stepLength = tramo.Largo / steps;
            float stepTurn = tramo.Giro / steps;
            for (int s = 0; s < steps; s++)
            {
                heading += stepTurn * 0.5f;
                Vector3 flatDir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                float slope = tramo.Pendiente * Mathf.Deg2Rad;
                position += flatDir * stepLength * Mathf.Cos(slope) + Vector3.down * stepLength * Mathf.Sin(slope);
                heading += stepTurn * 0.5f;
                distance += stepLength;
                points.Add(position);
                widths.Add(tramo.MedioAncho);
            }
        }

        GameObject go = new GameObject("Pista");
        go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();
        TrackGenerator track = go.AddComponent<TrackGenerator>();
        track.SetLayout(points, widths);

        Mesh generated = track.Generate();
        Mesh saved = SaveMeshAsset(generated, ModelsFolder + "/Pista01.asset");
        go.GetComponent<MeshFilter>().sharedMesh = saved;
        MeshCollider meshCollider = go.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = saved;

        renderer.sharedMaterials = new[] { p.Tierra, p.Berma, p.Pasto };
        MarkStatic(go);
        return track;
    }

    private static void PopulateTrack(TrackGenerator track, List<float> tramoStarts, Palette p)
    {
        System.Random rng = new System.Random(20260101); // Semilla fija: la pista sale igual cada vez.
        float length = track.TotalLength;
        float finishDistance = length - FinishBeforeEnd;

        // --- Checkpoints cada 100 m y meta.
        Transform checkpoints = new GameObject("Checkpoints").transform;
        int index = 1;
        for (float d = CheckpointSpacing; d < finishDistance - 10f; d += CheckpointSpacing)
        {
            CreateTrigger(track, d, TrackTrigger.TriggerType.Checkpoint, index, "Checkpoint " + index, checkpoints);
            index++;
        }
        CreateTrigger(track, finishDistance, TrackTrigger.TriggerType.Meta, 0, "Meta", checkpoints);

        // --- Arcos de salida y meta (solo decorado, sin collider).
        Transform decorado = new GameObject("Decorado").transform;
        CreateArch(track, StartArchDistance, "ArcoSalida", p.AzulSalida, null, p, decorado);
        CreateArch(track, finishDistance, "ArcoMeta", p.Negro, p.Blanco, p, decorado);

        // --- Barreras detrás de la salida y después de la meta.
        CreateBarrier(track, 1.5f, "BarreraSalida", p, decorado);
        CreateBarrier(track, length - 2f, "BarreraFinal", p, decorado);

        // --- Rampas: la del tramo 4 en recta, y la grande antes del final.
        Transform rampas = new GameObject("Rampas").transform;
        rampas.SetParent(decorado, false);
        CreateRamp(track, TramoStart(tramoStarts, "Antes del salto") + 22f, 5f, 3.5f, "PrimerSalto", p, rampas);
        CreateRamp(track, TramoStart(tramoStarts, "Aproximación salto final") + 20f, 8f, 5f, "SaltoFinal", p, rampas);

        // --- Zona de rocas: en los bordes, angostando el camino.
        Transform rocas = new GameObject("Rocas").transform;
        rocas.SetParent(decorado, false);
        float rocksStart = TramoStart(tramoStarts, "Rocas");
        float rocksEnd = TramoStart(tramoStarts, "Antes de la cerrada");
        int side = 1;
        for (float d = rocksStart + 2f; d < rocksEnd; d += Range(rng, 2.5f, 4f))
        {
            int i = track.GetIndexAtDistance(d);
            float hw = track.GetHalfWidth(i);
            side = rng.NextDouble() < 0.7 ? -side : side;
            CreateRock(track, i, side * (hw + Range(rng, -0.6f, 1.2f)), Range(rng, 0.8f, 1.6f), rng, p, rocas);
            if (rng.NextDouble() < 0.5)
            {
                CreateRock(track, i, side * (hw + Range(rng, 1.5f, 3f)), Range(rng, 0.6f, 1.2f), rng, p, rocas);
            }
        }

        // --- Árboles: bosque denso en el tramo 5 y algunos sueltos en el resto.
        Transform arboles = new GameObject("Arboles").transform;
        arboles.SetParent(decorado, false);
        float forestStart = TramoStart(tramoStarts, "Bosque A");
        float forestEnd = TramoStart(tramoStarts, "Rocas");
        for (float d = 0f; d < length; d += 3f)
        {
            bool forest = d >= forestStart - 5f && d <= forestEnd + 5f;
            int i = track.GetIndexAtDistance(d);
            for (int s = -1; s <= 1; s += 2)
            {
                int rows = forest ? 3 : 1;
                for (int row = 0; row < rows; row++)
                {
                    if (!forest && rng.NextDouble() > 0.3) continue;
                    float foot = track.GetBermFootOffset(i, s);
                    float lateral = s * (foot + 2.5f + row * 3.2f + Range(rng, 0f, forest ? 2f : 9f));
                    TryCreateTree(track, i, lateral, rng, p, arboles);
                }
            }
        }

        // Decorado estático: permite agrupar los dibujados y ahorrar trabajo a la GPU.
        foreach (Transform t in decorado.GetComponentsInChildren<Transform>()) MarkStatic(t.gameObject);
    }

    private static float TramoStart(List<float> starts, string nombre)
    {
        for (int i = 0; i < Tramos.Length; i++)
        {
            if (Tramos[i].Nombre == nombre) return starts[i];
        }
        Debug.LogError("PrototypeSceneBuilder: no existe el tramo " + nombre);
        return 0f;
    }

    private static void CreateTrigger(TrackGenerator track, float distance, TrackTrigger.TriggerType type, int index, string name, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(track.GetSurfacePoint(i, 0f), Quaternion.LookRotation(Flat(track.GetForward(i))));

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        // Tan ancho como todo el pasto, para que cuente aunque se pase por fuera del camino.
        float width = (Mathf.Max(track.GetOuterOffset(i, -1), track.GetOuterOffset(i, 1)) + 1f) * 2f;
        box.size = new Vector3(width, 10f, 2f);
        box.center = new Vector3(0f, 3f, 0f);

        go.AddComponent<TrackTrigger>().Configure(type, index);
    }

    private static void CreateArch(TrackGenerator track, float distance, string name, Material banner, Material bannerAlt, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        float span = track.GetHalfWidth(i) + 1.2f;
        Vector3 left = track.GetSurfacePoint(i, -span);
        Vector3 right = track.GetSurfacePoint(i, span);
        float top = Mathf.Max(left.y, right.y) + 4.5f;
        Quaternion rotation = Quaternion.LookRotation(Flat(track.GetForward(i)));

        Transform arch = new GameObject(name).transform;
        arch.SetParent(parent, false);
        arch.SetPositionAndRotation(track.GetSurfacePoint(i, 0f), rotation);

        WorldTube("Poste", arch, left, new Vector3(left.x, top, left.z), 0.35f, p.Blanco);
        WorldTube("Poste", arch, right, new Vector3(right.x, top, right.z), 0.35f, p.Blanco);

        Vector3 center = (new Vector3(left.x, top, left.z) + new Vector3(right.x, top, right.z)) * 0.5f;
        float width = Vector3.Distance(left, right) + 0.6f;
        if (bannerAlt == null)
        {
            WorldPart(PrimitiveType.Cube, "Cartel", arch, center - Vector3.up * 0.5f, new Vector3(width, 1.0f, 0.15f), rotation, banner);
        }
        else
        {
            // Cartel a cuadros, en dos filas.
            const int columns = 14;
            float cell = width / columns;
            for (int row = 0; row < 2; row++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector3 local = new Vector3(-width * 0.5f + cell * (c + 0.5f), -0.25f - row * 0.5f, 0f);
                    Material m = (row + c) % 2 == 0 ? banner : bannerAlt;
                    WorldPart(PrimitiveType.Cube, "Cuadro", arch, center + rotation * local, new Vector3(cell, 0.5f, 0.15f), rotation, m);
                }
            }
        }
    }

    private static void CreateBarrier(TrackGenerator track, float distance, string name, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        Quaternion rotation = Quaternion.LookRotation(Flat(track.GetForward(i)));
        float width = (Mathf.Max(track.GetOuterOffset(i, -1), track.GetOuterOffset(i, 1)) + 1f) * 2f;
        Vector3 basePoint = track.GetSurfacePoint(i, 0f);

        GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = name;
        barrier.transform.SetParent(parent, false);
        barrier.transform.SetPositionAndRotation(basePoint + Vector3.up * 0.6f, rotation);
        barrier.transform.localScale = new Vector3(width, 2.4f, 0.8f); // Se entierra un poco por si el terreno no es plano.
        barrier.GetComponent<Renderer>().sharedMaterial = p.Blanco;

        WorldPart(PrimitiveType.Cube, "Franja", barrier.transform, basePoint + Vector3.up * 1.55f, new Vector3(width, 0.35f, 0.82f), rotation, p.Rojo);
    }

    /// <summary>Rampa: bloque inclinado sobre el camino, con su borde delantero a ras del suelo.</summary>
    private static void CreateRamp(TrackGenerator track, float distance, float length, float width, string name, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        const float thickness = 3f; // Grueso para que la parte trasera llegue bajo el suelo.
        Quaternion trackRotation = Quaternion.LookRotation(track.GetForward(i), Vector3.up);
        Quaternion rampRotation = trackRotation * Quaternion.Euler(-RampAngle, 0f, 0f);
        Vector3 frontEdge = track.GetSurfacePoint(i, 0f) - Vector3.up * 0.05f;

        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = name;
        ramp.transform.SetParent(parent, false);
        ramp.transform.SetPositionAndRotation(frontEdge + rampRotation * new Vector3(0f, -thickness * 0.5f, length * 0.5f), rampRotation);
        ramp.transform.localScale = new Vector3(width, thickness, length);
        ramp.GetComponent<Renderer>().sharedMaterial = p.Rampa;
    }

    private static void CreateRock(TrackGenerator track, int i, float lateral, float size, System.Random rng, Palette p, Transform parent)
    {
        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Roca";
        rock.transform.SetParent(parent, false);
        Vector3 scale = new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.55f, 0.9f), size * Range(rng, 0.9f, 1.4f));
        rock.transform.SetPositionAndRotation(
            track.GetSurfacePoint(i, lateral) - Vector3.up * scale.y * 0.15f,
            Quaternion.Euler(Range(rng, -15f, 15f), Range(rng, 0f, 360f), Range(rng, -15f, 15f)));
        rock.transform.localScale = scale;
        rock.GetComponent<Renderer>().sharedMaterial = p.Roca;

        // Collider convexo en lugar de la esfera: con escala desigual, la esfera sería más grande que la roca.
        Mesh sphereMesh = rock.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(rock.GetComponent<SphereCollider>());
        MeshCollider meshCollider = rock.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = sphereMesh;
        meshCollider.convex = true;
    }

    private static void TryCreateTree(TrackGenerator track, int i, float lateral, System.Random rng, Palette p, Transform parent)
    {
        int side = lateral >= 0f ? 1 : -1;
        if (Mathf.Abs(lateral) > track.GetOuterOffset(i, side) - 1f) return; // Fuera del pasto.

        Vector3 position = track.GetSurfacePoint(i, lateral);

        // Al menos 2 m de distancia del borde de cualquier tramo (contando las bermas).
        for (int j = 0; j < track.SampleCount; j += 2)
        {
            Vector3 d = position - track.GetPosition(j);
            d.y = 0f;
            float clearance = Mathf.Max(track.GetBermFootOffset(j, -1), track.GetBermFootOffset(j, 1)) + 2f;
            if (d.sqrMagnitude < clearance * clearance) return;
        }

        float trunkHeight = Range(rng, 1.2f, 1.9f);
        float crownHeight = Range(rng, 3f, 4.6f);
        float crownRadius = Range(rng, 1.1f, 1.7f);

        Transform tree = new GameObject("Arbol").transform;
        tree.SetParent(parent, false);
        tree.SetPositionAndRotation(position - Vector3.up * 0.1f, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Tronco";
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        trunk.transform.SetParent(tree, false);
        trunk.transform.localPosition = Vector3.up * trunkHeight * 0.5f;
        trunk.transform.localScale = new Vector3(0.35f, trunkHeight * 0.5f, 0.35f);
        trunk.GetComponent<Renderer>().sharedMaterial = p.Tronco;

        Material leaves = rng.NextDouble() < 0.5 ? p.Hojas : p.HojasClaras;
        Cone("Copa", tree, new Vector3(0f, trunkHeight * 0.7f, 0f), crownRadius, crownHeight, leaves, p);
        Cone("Copa", tree, new Vector3(0f, trunkHeight * 0.7f + crownHeight * 0.45f, 0f), crownRadius * 0.7f, crownHeight * 0.7f, leaves, p);
    }

    private static void Cone(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material, Palette p)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
        go.AddComponent<MeshFilter>().sharedMesh = p.Cono;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void ConfigureFallHeight(BikeController bike, TrackGenerator track)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < track.SampleCount; i++) lowest = Mathf.Min(lowest, track.GetPosition(i).y);
        SetFloat(bike, "fallHeight", lowest - 25f);
    }

    // =====================================================================
    // Fase 5: cámara y UI
    // =====================================================================

    private static void BuildCamera(BikeController bike)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        go.AddComponent<AudioListener>();

        // FXAA es barato y en URP necesita el post-procesado activo (sin efectos pesados).
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        data.renderPostProcessing = true;
        data.renderShadows = true;

        Vector3 back = -bike.transform.forward;
        go.transform.position = bike.transform.position + back * 5f + Vector3.up * 2.1f;
        go.transform.LookAt(bike.transform.position + Vector3.up * 1.2f);

        CameraFollow follow = go.AddComponent<CameraFollow>();
        SetReference(follow, "target", bike);
    }

    private static void BuildUI(BikeController bike)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasGo = new GameObject("UI", typeof(RectTransform));
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        // Sin GraphicRaycaster ni EventSystem: la UI no tiene botones, todo va por GameInput.

        Color white = Color.white;
        Color accent = new Color(1f, 0.62f, 0.1f);
        Color gold = new Color(1f, 0.85f, 0.2f);

        // --- Pantalla de inicio.
        RectTransform title = Stretch(UIObject("PantallaInicio", canvasGo.transform));
        Panel("Velo", title, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.25f));
        Label("Titulo", title, font, "DOWNHILL RACE", 150, new Vector2(0.5f, 1f), new Vector2(0f, -230f), white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Label("Subtitulo", title, font, "Prototipo v0.1", 40, new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Color(1f, 1f, 1f, 0.85f), FontStyle.Italic, TextAnchor.MiddleCenter, true);

        RectTransform mode = Panel("Modo", title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(620f, 84f), new Color(accent.r, accent.g, accent.b, 0.9f));
        Label("ModoTexto", mode, font, "Contrarreloj  ·  Pista 1", 46, new Vector2(0.5f, 0.5f), Vector2.zero, white, FontStyle.Bold, TextAnchor.MiddleCenter, true);

        Text pressStart = Label("PresionaEnter", title, font, "Presiona ENTER para comenzar", 44, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Text titleBest = Label("MejorTiempo", title, font, string.Empty, 32, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), gold, FontStyle.Normal, TextAnchor.MiddleCenter, true);

        RectTransform controls = Panel("Controles", title, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1240f, 220f), new Color(0f, 0f, 0f, 0.55f));
        Label("ControlesIzquierda", controls, font,
            "<b>Pedalear</b>   W / ↑   ·   RT\n" +
            "<b>Frenar</b>   S / ↓   ·   LT\n" +
            "<b>Girar</b>   A D / ← →   ·   Stick izquierdo\n" +
            "<b>Agacharse</b>   Shift izq.   ·   X / Cuadrado",
            28, new Vector2(0.5f, 0.5f), new Vector2(-300f, 0f), white, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Vector2(560f, 200f));
        Label("ControlesDerecha", controls, font,
            "<b>Saltar</b>   Espacio   ·   A / Cruz\n" +
            "<b>Reaparecer</b>   R   ·   Y / Triángulo\n" +
            "<b>Empezar</b>   Enter   ·   Start\n" +
            "<b>Volver / reiniciar</b>   Esc   ·   Select",
            28, new Vector2(0.5f, 0.5f), new Vector2(320f, 0f), white, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Vector2(560f, 200f));

        // --- HUD de carrera.
        RectTransform hud = Stretch(UIObject("HUD", canvasGo.transform));
        Text time = Label("Tiempo", hud, font, "0:00.00", 76, new Vector2(0.5f, 1f), new Vector2(0f, -70f), white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Text best = Label("Mejor", hud, font, "Mejor  --:--.--", 30, new Vector2(0.5f, 1f), new Vector2(0f, -135f), gold, FontStyle.Normal, TextAnchor.MiddleCenter, true);
        Text speed = Label("Velocidad", hud, font, "0", 110, new Vector2(1f, 0f), new Vector2(-300f, 115f), white, FontStyle.Bold, TextAnchor.MiddleRight, true, new Vector2(320f, 130f));
        Label("Unidad", hud, font, "km/h", 36, new Vector2(1f, 0f), new Vector2(-75f, 85f), new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold, TextAnchor.MiddleLeft, true, new Vector2(120f, 60f));
        Label("Ayuda", hud, font, "R: reaparecer   ·   Esc: reiniciar", 26, new Vector2(0f, 0f), new Vector2(260f, 50f), new Color(1f, 1f, 1f, 0.7f), FontStyle.Normal, TextAnchor.MiddleLeft, true, new Vector2(480f, 50f));
        Text countdown = Label("Cuenta", hud, font, string.Empty, 230, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), accent, FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(900f, 300f));

        // --- Pantalla de meta (dentro del HUD).
        RectTransform finish = Panel("PantallaMeta", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f), new Color(0f, 0f, 0f, 0.6f));
        Label("MetaTitulo", finish, font, "¡META!", 140, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), gold, FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(900f, 180f));
        Text finishTime = Label("MetaTiempo", finish, font, "Tiempo: 0:00.00", 64, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Text finishBest = Label("MetaMejor", finish, font, "Mejor: 0:00.00", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Color(1f, 1f, 1f, 0.9f), FontStyle.Normal, TextAnchor.MiddleCenter, true);
        Text record = Label("NuevoRecord", finish, font, "¡Nuevo récord!", 54, new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), gold, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Label("MetaAyuda", finish, font, "ENTER para correr otra vez   ·   ESC para volver al inicio", 30, new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), white, FontStyle.Normal, TextAnchor.MiddleCenter, true, new Vector2(950f, 60f));

        TitleScreen titleScreen = canvasGo.AddComponent<TitleScreen>();
        SetReference(titleScreen, "root", title.gameObject);
        SetReference(titleScreen, "pressStartText", pressStart);
        SetReference(titleScreen, "bestTimeText", titleBest);

        RaceHUD raceHud = canvasGo.AddComponent<RaceHUD>();
        SetReference(raceHud, "bike", bike);
        SetReference(raceHud, "hudRoot", hud.gameObject);
        SetReference(raceHud, "timeText", time);
        SetReference(raceHud, "bestTimeText", best);
        SetReference(raceHud, "speedText", speed);
        SetReference(raceHud, "countdownText", countdown);
        SetReference(raceHud, "finishRoot", finish.gameObject);
        SetReference(raceHud, "finishTimeText", finishTime);
        SetReference(raceHud, "finishBestText", finishBest);
        SetReference(raceHud, "newRecordText", record);

        // Estado inicial correcto ya en el Editor, antes de dar Play.
        hud.gameObject.SetActive(false);
        finish.gameObject.SetActive(false);
    }

    private static GameObject UIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform Stretch(GameObject go)
    {
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = UIObject(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static Text Label(string name, Transform parent, Font font, string content, int size, Vector2 anchor,
        Vector2 position, Color color, FontStyle style, TextAnchor alignment, bool outline, Vector2? boxSize = null)
    {
        GameObject go = UIObject(name, parent);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = boxSize ?? new Vector2(1400f, size * 1.4f);

        Text text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = true;
        text.lineSpacing = 1.15f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        if (outline)
        {
            // Borde oscuro para que el texto se lea sobre cualquier fondo.
            Outline border = go.AddComponent<Outline>();
            border.effectColor = new Color(0f, 0f, 0f, 0.75f);
            border.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    // =====================================================================
    // Fase 6: luces y rendimiento
    // =====================================================================

    private static void BuildLighting()
    {
        GameObject sun = new GameObject("Sol");
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.3f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f;

        RenderSettings.sun = light;
        RenderSettings.skybox = null;
        // Luz ambiente de tres colores: no necesita hornear iluminación.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.52f, 0.56f, 0.56f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.24f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 350f;
    }

    /// <summary>Ajustes para PC media: sombras hasta unos 60 m en todos los perfiles de URP.</summary>
    private static void ApplyPerformanceSettings()
    {
        HashSet<UniversalRenderPipelineAsset> assets = new HashSet<UniversalRenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset) assets.Add(defaultAsset);
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset levelAsset) assets.Add(levelAsset);
        }

        foreach (UniversalRenderPipelineAsset asset in assets)
        {
            asset.shadowDistance = 60f;
            EditorUtility.SetDirty(asset);
        }
        if (assets.Count == 0) Debug.LogWarning("PrototypeSceneBuilder: no se encontró un asset de URP; revisa Project Settings → Graphics.");
    }

    private static void AddSceneFirstInBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(path, true) };
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (existing.path != path) scenes.Add(existing);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // =====================================================================
    // Materiales y assets
    // =====================================================================

    private static Palette CreatePalette()
    {
        EnsureFolder(MaterialsFolder);
        EnsureFolder(ModelsFolder);

        Palette p = new Palette
        {
            Tierra = GetMaterial("Tierra", new Color(0.56f, 0.40f, 0.25f)),
            Berma = GetMaterial("Berma", new Color(0.46f, 0.32f, 0.20f)),
            Pasto = GetMaterial("Pasto", new Color(0.32f, 0.55f, 0.22f)),
            Tronco = GetMaterial("Tronco", new Color(0.38f, 0.25f, 0.14f)),
            Hojas = GetMaterial("Hojas", new Color(0.12f, 0.38f, 0.16f)),
            HojasClaras = GetMaterial("HojasClaras", new Color(0.20f, 0.48f, 0.20f)),
            Roca = GetMaterial("Roca", new Color(0.52f, 0.52f, 0.54f)),
            Rampa = GetMaterial("Rampa", new Color(0.78f, 0.60f, 0.36f)),
            Blanco = GetMaterial("Blanco", new Color(0.92f, 0.92f, 0.92f)),
            Rojo = GetMaterial("Rojo", new Color(0.85f, 0.15f, 0.12f)),
            Negro = GetMaterial("Negro", new Color(0.06f, 0.06f, 0.07f)),
            AzulSalida = GetMaterial("AzulSalida", new Color(0.15f, 0.38f, 0.85f)),
            Cuadro = GetMaterial("BiciCuadro", new Color(0.86f, 0.12f, 0.10f), 0.6f),
            Llanta = GetMaterial("BiciLlanta", new Color(0.08f, 0.08f, 0.08f)),
            Aro = GetMaterial("BiciAro", new Color(0.62f, 0.63f, 0.66f), 0.7f),
            Metal = GetMaterial("BiciMetal", new Color(0.35f, 0.36f, 0.38f), 0.6f),
            Jersey = GetMaterial("CorredorJersey", new Color(0.0f, 0.58f, 0.58f)),
            Casco = GetMaterial("CorredorCasco", new Color(1.0f, 0.50f, 0.05f), 0.6f),
            Piel = GetMaterial("CorredorPiel", new Color(0.93f, 0.74f, 0.60f)),
            RopaOscura = GetMaterial("CorredorRopaOscura", new Color(0.15f, 0.16f, 0.19f)),
        };

        p.SinFriccion = GetPhysicsMaterial("BiciSinFriccion");
        p.Cono = SaveMeshAsset(CreateConeMesh(10), ModelsFolder + "/Cono.asset");
        return p;
    }

    private static Material GetMaterial(string name, Color color, float smoothness = 0.15f)
    {
        string path = MaterialsFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("PrototypeSceneBuilder: no se encontró el shader URP/Lit; se usa Standard.");
                shader = Shader.Find("Standard");
            }
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        material.color = color;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial GetPhysicsMaterial(string name)
    {
        string path = MaterialsFolder + "/" + name + ".physicMaterial";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null)
        {
            material = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(material, path);
        }
        // Sin fricción ni rebote: el agarre lo controla BikeController.
        material.dynamicFriction = 0f;
        material.staticFriction = 0f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>Guarda una malla como asset; si ya existe, la sobrescribe conservando la referencia.</summary>
    private static Mesh SaveMeshAsset(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    /// <summary>Cono de radio 0.5 y altura 1 con la base en y = 0 (Unity no trae cono entre sus primitivas).</summary>
    private static Mesh CreateConeMesh(int sides)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        Vector3 apex = Vector3.up;

        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            Vector3 b0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
            Vector3 b1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);

            // Cara lateral (vértices propios para un sombreado facetado).
            int v = vertices.Count;
            vertices.Add(b0); vertices.Add(apex); vertices.Add(b1);
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);

            // Base.
            v = vertices.Count;
            vertices.Add(Vector3.zero); vertices.Add(b0); vertices.Add(b1);
            triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 2);
        }

        Mesh mesh = new Mesh { name = "Cono" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // =====================================================================
    // Utilidades de construcción
    // =====================================================================

    /// <summary>Objeto vacío usado como pivote, ubicado en coordenadas del modelo de la bici.</summary>
    private static Transform Pivot(string name, Transform parent, Vector3 bikePosition)
    {
        Transform t = new GameObject(name).transform;
        t.position = bikePosition; // El modelo se arma en el origen, así que mundo = local de la bici.
        t.SetParent(parent, true);
        return t;
    }

    /// <summary>Primitiva visual sin collider, ubicada en coordenadas del modelo de la bici.</summary>
    private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 bikePosition, Vector3 scale, Quaternion rotation, Material material)
    {
        return WorldPart(type, name, parent, bikePosition, scale, rotation, material);
    }

    private static GameObject WorldPart(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.transform.SetParent(parent, true);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    /// <summary>Cilindro entre dos puntos (tubos del cuadro, brazos, piernas).</summary>
    private static GameObject Tube(string name, Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
    {
        return WorldTube(name, parent, from, to, thickness, material);
    }

    private static GameObject WorldTube(string name, Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
    {
        Vector3 direction = to - from;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        // El cilindro de Unity mide 2 de alto: la escala Y es la mitad del largo.
        return WorldPart(PrimitiveType.Cylinder, name, parent, (from + to) * 0.5f,
            new Vector3(thickness, direction.magnitude * 0.5f, thickness), rotation, material);
    }

    /// <summary>Caja alargada entre dos puntos (el torso).</summary>
    private static GameObject Box(string name, Transform parent, Vector3 from, Vector3 to, Vector2 section, Material material)
    {
        Vector3 direction = to - from;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        return WorldPart(PrimitiveType.Cube, name, parent, (from + to) * 0.5f,
            new Vector3(section.x, direction.magnitude, section.y), rotation, material);
    }

    private static void MarkStatic(GameObject go)
    {
        // Sin ContributeGI: no queremos hornear luz en el prototipo.
        GameObjectUtility.SetStaticEditorFlags(go,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    private static float Range(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private static void SetReference(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError("PrototypeSceneBuilder: " + target.GetType().Name + " no tiene el campo '" + field + "'.");
            return;
        }
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError("PrototypeSceneBuilder: " + target.GetType().Name + " no tiene el campo '" + field + "'.");
            return;
        }
        property.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Progress(string message, float progress)
    {
        EditorUtility.DisplayProgressBar("DownhillRace", message, progress);
    }
}
