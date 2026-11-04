using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kind = VisualAssetFactory.TextureKind;
using PM = ProceduralModels;
using PT = ProceduralTextures;

/// <summary>
/// Construye por código la escena completa del prototipo (pista, bici GY RAVEN con su corredor,
/// bosque, cámara, UI, luces y post-procesado) y la guarda en Assets/Scenes/Pista01.unity.
/// Así nadie tiene que armar la escena a mano ni editar archivos .unity.
/// Menú: DownhillRace → Crear escena del prototipo.
/// </summary>
public static class PrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Pista01.unity";
    private const string VolumeProfilePath = "Assets/Settings/Pista01_PostProceso.asset";

    // Ambiente nublado de montaña: la niebla tiene el color del horizonte del cielo.
    private static readonly Color FogColor = new Color(0.70f, 0.73f, 0.76f);

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

    /// <summary>Materiales y mallas compartidos por toda la escena.</summary>
    private class Palette
    {
        public Material Tierra, Berma, Pasto, Roca, Rampa, Tronco, Agujas, Hojas, Helecho, Montana, Valle;
        public Material Blanco, Rojo, Negro, CartelSalida, CartelMeta, Barro, Polvo, Cielo;
        public PhysicsMaterial SinFriccion;
        public Mesh[] Pinos, Frondosos, Rocas, Helechos, Montanas;
        public Mesh Cartel;
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
            Progress("Generando texturas y materiales", 0.05f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Palette palette = CreatePalette();

            new GameObject("GameManager").AddComponent<GameManager>();

            Progress("Generando la pista", 0.3f);
            List<float> tramoStarts;
            TrackGenerator track = BuildTrack(palette, out tramoStarts);

            Progress("Armando la bici GY RAVEN y el corredor", 0.45f);
            BikeController bike = BuildBikeAndRider(palette, track);

            Progress("Colocando checkpoints, rampas, rocas y bosque", 0.6f);
            PopulateTrack(track, tramoStarts, palette);
            BuildSurroundings(track, palette);
            ConfigureFallHeight(bike, track);

            Progress("Creando cámara, luces, post-procesado y UI", 0.85f);
            BuildCamera(bike);
            BuildLighting(palette);
            BuildPostProcessing();
            BuildUI(bike);
            ApplyPerformanceSettings();

            Progress("Guardando la escena", 0.95f);
            VisualAssetFactory.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSceneList.Register(ScenePath);
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
    // Materiales, texturas y mallas
    // =====================================================================

    private static Palette CreatePalette()
    {
        Palette p = new Palette();

        // Superficies de la pista (anisotrópico: se ven bien aunque la cámara las mire rasantes).
        PT.Dirt(512, out PT.Image dirt, out PT.Image dirtN);
        PT.Berm(512, out PT.Image berm, out PT.Image bermN);
        PT.ForestFloor(512, out PT.Image grass, out PT.Image grassN);
        p.Tierra = VisualAssetFactory.Lit("Tierra", Color.white,
            VisualAssetFactory.SaveTexture(dirt, "Tierra", Kind.ColorSmoothness, 1024, 4),
            VisualAssetFactory.SaveTexture(dirtN, "TierraNormal", Kind.Normal, 1024, 4), 1f, true);
        p.Berma = VisualAssetFactory.Lit("Berma", Color.white,
            VisualAssetFactory.SaveTexture(berm, "Berma", Kind.ColorSmoothness, 512, 4),
            VisualAssetFactory.SaveTexture(bermN, "BermaNormal", Kind.Normal, 512, 4), 1f, true);
        p.Pasto = VisualAssetFactory.Lit("Pasto", Color.white,
            VisualAssetFactory.SaveTexture(grass, "Pasto", Kind.ColorSmoothness, 512, 4),
            VisualAssetFactory.SaveTexture(grassN, "PastoNormal", Kind.Normal, 512, 4), 1f, true, 0f, 0.8f);

        // Rocas, madera de las rampas y árboles.
        PT.Rock(256, out PT.Image rock, out PT.Image rockN);
        PT.WoodPlanks(256, out PT.Image wood, out PT.Image woodN);
        PT.Bark(256, out PT.Image bark, out PT.Image barkN);
        PT.Foliage(256, true, out PT.Image needles, out PT.Image needlesN);
        PT.Foliage(256, false, out PT.Image leaves, out PT.Image leavesN);
        p.Roca = VisualAssetFactory.Lit("Roca", Color.white,
            VisualAssetFactory.SaveTexture(rock, "Roca", Kind.ColorSmoothness, 256),
            VisualAssetFactory.SaveTexture(rockN, "RocaNormal", Kind.Normal, 256), 1f, true);
        p.Rampa = VisualAssetFactory.Lit("Rampa", Color.white,
            VisualAssetFactory.SaveTexture(wood, "Madera", Kind.ColorSmoothness, 256),
            VisualAssetFactory.SaveTexture(woodN, "MaderaNormal", Kind.Normal, 256), 1f, true);
        Texture2D barkTex = VisualAssetFactory.SaveTexture(bark, "Corteza", Kind.ColorSmoothness, 256);
        Texture2D barkNormal = VisualAssetFactory.SaveTexture(barkN, "CortezaNormal", Kind.Normal, 256);
        Texture2D needlesTex = VisualAssetFactory.SaveTexture(needles, "Agujas", Kind.ColorSmoothness, 256);
        Texture2D needlesNormal = VisualAssetFactory.SaveTexture(needlesN, "AgujasNormal", Kind.Normal, 256);
        Texture2D leavesTex = VisualAssetFactory.SaveTexture(leaves, "Hojas", Kind.ColorSmoothness, 256);
        Texture2D leavesNormal = VisualAssetFactory.SaveTexture(leavesN, "HojasNormal", Kind.Normal, 256);
        p.Tronco = VisualAssetFactory.Lit("Tronco", Color.white, barkTex, barkNormal, 1f, true);
        p.Agujas = VisualAssetFactory.Lit("Agujas", Color.white, needlesTex, needlesNormal, 1f, true);
        p.Hojas = VisualAssetFactory.Lit("Hojas", Color.white, leavesTex, leavesNormal, 1f, true);
        p.Helecho = VisualAssetFactory.Lit("Helecho", new Color(0.85f, 1f, 0.8f), leavesTex, null, 1f, true);

        // Paisaje lejano: la niebla les da el tono, así que basta un color.
        p.Montana = VisualAssetFactory.Lit("Montana", new Color(0.40f, 0.44f, 0.48f), null, null, 0f);
        p.Valle = VisualAssetFactory.Lit("Valle", new Color(0.18f, 0.27f, 0.14f), null, null, 0f);

        // Decorado simple.
        p.Blanco = VisualAssetFactory.Lit("Blanco", new Color(0.92f, 0.92f, 0.92f), null, null, 0.3f);
        p.Rojo = VisualAssetFactory.Lit("Rojo", new Color(0.85f, 0.15f, 0.12f), null, null, 0.3f);
        p.Negro = VisualAssetFactory.Lit("Negro", new Color(0.06f, 0.06f, 0.07f), null, null, 0.3f);
        p.CartelSalida = VisualAssetFactory.Lit("CartelSalida", Color.white,
            VisualAssetFactory.SaveTexture(PT.Banner(1024, 128, "SALIDA", new PT.Rgb(0.12f, 0.35f, 0.82f), false), "CartelSalida", Kind.Color, 1024), null, 0.3f);
        p.CartelMeta = VisualAssetFactory.Lit("CartelMeta", Color.white,
            VisualAssetFactory.SaveTexture(PT.Banner(1024, 128, "META", new PT.Rgb(0.08f, 0.08f, 0.08f), true), "CartelMeta", Kind.Color, 1024), null, 0.3f);

        // Partículas y cielo.
        Texture2D blob = VisualAssetFactory.SaveTexture(PT.SoftBlob(64), "Particula", Kind.Decal, 64);
        p.Barro = VisualAssetFactory.Particles("ParticulaBarro", blob, new Color(0.24f, 0.17f, 0.11f, 1f));
        p.Polvo = VisualAssetFactory.Particles("ParticulaPolvo", blob, new Color(0.55f, 0.47f, 0.38f, 0.45f));
        p.Cielo = VisualAssetFactory.PanoramicSky("CieloNublado",
            VisualAssetFactory.SaveTexture(PT.OvercastSky(1024, 512), "CieloNublado", Kind.Sky, 1024), 1f);

        p.SinFriccion = GetPhysicsMaterial("BiciSinFriccion");

        // Mallas del entorno: varias variantes para que el bosque no se vea repetido.
        p.Pinos = new Mesh[4];
        for (int i = 0; i < p.Pinos.Length; i++) p.Pinos[i] = VisualAssetFactory.SaveMesh(PM.BuildPine(11 + i), "Pino" + (i + 1));
        p.Frondosos = new Mesh[2];
        for (int i = 0; i < p.Frondosos.Length; i++) p.Frondosos[i] = VisualAssetFactory.SaveMesh(PM.BuildBroadleaf(21 + i), "ArbolFrondoso" + (i + 1));
        p.Rocas = new Mesh[4];
        for (int i = 0; i < p.Rocas.Length; i++) p.Rocas[i] = VisualAssetFactory.SaveMesh(PM.BuildRock(31 + i), "Roca" + (i + 1));
        p.Helechos = new Mesh[3];
        for (int i = 0; i < p.Helechos.Length; i++) p.Helechos[i] = VisualAssetFactory.SaveMesh(PM.BuildFern(41 + i), "Helecho" + (i + 1));
        p.Montanas = new Mesh[3];
        for (int i = 0; i < p.Montanas.Length; i++) p.Montanas[i] = VisualAssetFactory.SaveMesh(PM.BuildMountain(51 + i), "Montana" + (i + 1));
        MeshBuilder panel = new MeshBuilder(1);
        panel.AddPanel(0, System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitX, System.Numerics.Vector3.UnitY, 1f, 1f);
        p.Cartel = VisualAssetFactory.SaveMesh(panel, "Cartel");
        return p;
    }

    private static PhysicsMaterial GetPhysicsMaterial(string name)
    {
        VisualAssetFactory.EnsureFolder(VisualAssetFactory.MaterialsFolder);
        string path = VisualAssetFactory.MaterialsFolder + "/" + name + ".physicMaterial";
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

    // =====================================================================
    // Bici GY RAVEN y corredor
    // =====================================================================

    /// <summary>Crea la esfera física y el modelo visual rígido, y los conecta a BikeController, BikeVisuals y WheelSpray.</summary>
    private static BikeController BuildBikeAndRider(Palette p, TrackGenerator track)
    {
        int startIndex = track.GetIndexAtDistance(StartDistance);
        Vector3 startPoint = track.GetSurfacePoint(startIndex, 0f);
        Quaternion startRotation = Quaternion.LookRotation(Flat(track.GetForward(startIndex)), Vector3.up);

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
        RiderStamina stamina = physics.AddComponent<RiderStamina>();
        BikeController controller = physics.AddComponent<BikeController>();
        SetReference(controller, "stamina", stamina);

        // --- Modelo visual (se arma en el origen y luego se lleva a la salida).
        BikeModelBuilder.Parts parts = BikeModelBuilder.Build();
        GameObject visual = parts.Root.gameObject;

        BikeVisuals visuals = visual.AddComponent<BikeVisuals>();
        SetReference(visuals, "controller", controller);
        SetReference(visuals, "inclinacion", parts.Inclinacion);
        SetReference(visuals, "ruedaTrasera", parts.RuedaTrasera);
        SetReference(visuals, "ruedaDelantera", parts.RuedaDelantera);
        SetReference(visuals, "direccion", parts.Direccion);
        SetReference(visuals, "piernas", parts.Piernas);
        SetReference(visuals, "torso", parts.Torso);
        SetFloat(visuals, "wheelRadius", PM.WheelRadius);

        // --- Barro y polvo de la rueda trasera.
        Vector3 rearContact = new Vector3(0f, 0.06f, PM.RearAxle.Z - 0.05f);
        ParticleSystem mud = CreateSpray("Barro", parts.Root, rearContact, p.Barro, false);
        ParticleSystem dust = CreateSpray("Polvo", parts.Root, rearContact + new Vector3(0f, 0.05f, -0.1f), p.Polvo, true);
        WheelSpray spray = visual.AddComponent<WheelSpray>();
        SetReference(spray, "bike", controller);
        SetReference(spray, "mud", mud);
        SetReference(spray, "dust", dust);

        parts.Root.SetPositionAndRotation(startPoint, startRotation);
        return controller;
    }

    /// <summary>Sistema de partículas para la rueda: barro (pequeño, pesado) o polvo (grande, suave).</summary>
    private static ParticleSystem CreateSpray(string name, Transform parent, Vector3 localPosition, Material material, bool dust)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        // Sale hacia atrás y hacia arriba, como la tierra que levanta la rueda.
        go.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, dust ? 0.3f : 0.7f, -1f));

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = dust ? new ParticleSystem.MinMaxCurve(0.8f, 1.4f) : new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = dust ? new ParticleSystem.MinMaxCurve(0.4f, 1.4f) : new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize = dust ? new ParticleSystem.MinMaxCurve(0.5f, 1.1f) : new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = dust
            ? new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.47f, 0.38f, 0.5f), new Color(0.45f, 0.38f, 0.3f, 0.35f))
            : new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.18f, 0.12f), new Color(0.38f, 0.28f, 0.18f));
        main.gravityModifier = dust ? -0.03f : 1.3f;
        main.maxParticles = dust ? 60 : 150;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = dust ? 35f : 22f;
        shape.radius = 0.08f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            dust
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(fade);

        if (dust)
        {
            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        }

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // =====================================================================
    // Pista
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
        Mesh saved = VisualAssetFactory.SaveMeshAsset(generated, VisualAssetFactory.ModelsFolder + "/Pista01.asset");
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

        // --- Arcos de salida y meta con carteles (solo decorado, sin collider).
        Transform decorado = new GameObject("Decorado").transform;
        CreateArch(track, StartArchDistance, "ArcoSalida", p.CartelSalida, p, decorado);
        CreateArch(track, finishDistance, "ArcoMeta", p.CartelMeta, p, decorado);

        // --- Barreras detrás de la salida y después de la meta.
        CreateBarrier(track, 1.5f, "BarreraSalida", p, decorado);
        CreateBarrier(track, length - 2f, "BarreraFinal", p, decorado);

        // --- Rampas de madera: la del tramo 4 en recta, y la grande antes del final.
        Transform rampas = Group("Rampas", decorado);
        CreateRamp(track, TramoStart(tramoStarts, "Antes del salto") + 22f, 5f, 3.5f, "PrimerSalto", p, rampas);
        CreateRamp(track, TramoStart(tramoStarts, "Aproximación salto final") + 20f, 8f, 5f, "SaltoFinal", p, rampas);

        // --- Zona de rocas: en los bordes, angostando el camino (con collider).
        Transform rocas = Group("Rocas", decorado);
        float rocksStart = TramoStart(tramoStarts, "Rocas");
        float rocksEnd = TramoStart(tramoStarts, "Antes de la cerrada");
        int side = 1;
        for (float d = rocksStart + 2f; d < rocksEnd; d += Range(rng, 2.5f, 4f))
        {
            int i = track.GetIndexAtDistance(d);
            float hw = track.GetHalfWidth(i);
            side = rng.NextDouble() < 0.7 ? -side : side;
            CreateRock(track, i, side * (hw + Range(rng, -0.6f, 1.2f)), Range(rng, 0.8f, 1.6f), true, rng, p, rocas);
            if (rng.NextDouble() < 0.5)
            {
                CreateRock(track, i, side * (hw + Range(rng, 1.5f, 3f)), Range(rng, 0.6f, 1.2f), true, rng, p, rocas);
            }
        }

        // --- Rocas sueltas en el pasto del resto de la pista (solo decorado).
        for (float d = 15f; d < length - 10f; d += Range(rng, 8f, 16f))
        {
            if (d > rocksStart - 5f && d < rocksEnd + 5f) continue;
            int i = track.GetIndexAtDistance(d);
            int s = rng.NextDouble() < 0.5 ? -1 : 1;
            float lateral = s * (track.GetBermFootOffset(i, s) + Range(rng, 0.5f, 6f));
            if (Mathf.Abs(lateral) < track.GetOuterOffset(i, s) - 1f && IsClearOfRoad(track, track.GetSurfacePoint(i, lateral), 1f))
            {
                CreateRock(track, i, lateral, Range(rng, 0.4f, 1.1f), false, rng, p, rocas);
            }
        }

        // --- Árboles: bosque denso en el tramo 5 y pinos y árboles sueltos en el resto.
        Transform arboles = Group("Arboles", decorado);
        float forestStart = TramoStart(tramoStarts, "Bosque A");
        float forestEnd = TramoStart(tramoStarts, "Rocas");
        for (float d = 0f; d < length; d += 3f)
        {
            bool forest = d >= forestStart - 5f && d <= forestEnd + 5f;
            int i = track.GetIndexAtDistance(d);
            for (int s = -1; s <= 1; s += 2)
            {
                int rows = forest ? 3 : 2;
                for (int row = 0; row < rows; row++)
                {
                    if (!forest && rng.NextDouble() > 0.3) continue;
                    float foot = track.GetBermFootOffset(i, s);
                    // Se dejan 3.5 m o más tras la berma: las ramas bajas de los pinos no tapan el camino.
                    float lateral = s * (foot + 3.5f + row * 3.5f + Range(rng, 0f, forest ? 2f : 6f));
                    TryCreateTree(track, i, lateral, forest, rng, p, arboles);
                }
            }
        }

        // --- Helechos al borde del camino: dan sensación de velocidad al pasar cerca.
        Transform helechos = Group("Helechos", decorado);
        for (float d = 4f; d < length; d += 2.2f)
        {
            int i = track.GetIndexAtDistance(d);
            for (int s = -1; s <= 1; s += 2)
            {
                if (rng.NextDouble() > 0.55) continue;
                float lateral = s * (track.GetBermFootOffset(i, s) + Range(rng, 0.2f, 4f));
                if (Mathf.Abs(lateral) > track.GetOuterOffset(i, s) - 0.5f) continue;
                Vector3 position = track.GetSurfacePoint(i, lateral);
                if (!IsClearOfRoad(track, position, 0.2f)) continue;
                MeshObject("Helecho", helechos, p.Helechos[rng.Next(p.Helechos.Length)], position,
                    Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.8f, 1.4f), false, p.Helecho);
            }
        }

        // Decorado estático: permite agrupar los dibujados y ahorrar trabajo a la GPU.
        foreach (Transform t in decorado.GetComponentsInChildren<Transform>()) MarkStatic(t.gameObject);
    }

    /// <summary>Montañas lejanas que asoman entre la niebla y un valle muy abajo, para que no se vea el vacío.</summary>
    private static void BuildSurroundings(TrackGenerator track, Palette p)
    {
        System.Random rng = new System.Random(777);
        Vector3 center = Vector3.zero;
        float lowest = float.MaxValue;
        for (int i = 0; i < track.SampleCount; i++)
        {
            Vector3 point = track.GetPosition(i);
            center += point;
            lowest = Mathf.Min(lowest, point.y);
        }
        center /= Mathf.Max(1, track.SampleCount);

        Transform paisaje = new GameObject("Paisaje").transform;
        const int mountains = 10;
        for (int k = 0; k < mountains; k++)
        {
            float angle = (k / (float)mountains * 360f + Range(rng, -12f, 12f)) * Mathf.Deg2Rad;
            float distance = Range(rng, 280f, 330f);
            Vector3 position = new Vector3(center.x + Mathf.Sin(angle) * distance, lowest - 30f, center.z + Mathf.Cos(angle) * distance);
            float radius = Range(rng, 120f, 170f);
            GameObject mountain = MeshObject("Montana", paisaje, p.Montanas[k % p.Montanas.Length], position,
                Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(radius, Range(rng, 90f, 150f), radius), false, p.Montana);
            mountain.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // El valle queda por debajo de la altura de reaparición, así una caída nunca "aterriza" en él.
        GameObject valley = GameObject.CreatePrimitive(PrimitiveType.Plane);
        valley.name = "Valle";
        valley.transform.SetParent(paisaje, false);
        valley.transform.position = new Vector3(center.x, lowest - 40f, center.z);
        valley.transform.localScale = new Vector3(120f, 1f, 120f);
        Object.DestroyImmediate(valley.GetComponent<Collider>());
        MeshRenderer valleyRenderer = valley.GetComponent<MeshRenderer>();
        valleyRenderer.sharedMaterial = p.Valle;
        valleyRenderer.shadowCastingMode = ShadowCastingMode.Off;

        foreach (Transform t in paisaje.GetComponentsInChildren<Transform>()) MarkStatic(t.gameObject);
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

    private static void CreateArch(TrackGenerator track, float distance, string name, Material banner, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        float span = track.GetHalfWidth(i) + 1.2f;
        Vector3 left = track.GetSurfacePoint(i, -span);
        Vector3 right = track.GetSurfacePoint(i, span);
        float top = Mathf.Max(left.y, right.y) + 4.5f;
        Vector3 forward = Flat(track.GetForward(i));
        Quaternion rotation = Quaternion.LookRotation(forward);

        Transform arch = new GameObject(name).transform;
        arch.SetParent(parent, false);
        arch.SetPositionAndRotation(track.GetSurfacePoint(i, 0f), rotation);

        WorldTube("Poste", arch, left, new Vector3(left.x, top, left.z), 0.35f, p.Blanco);
        WorldTube("Poste", arch, right, new Vector3(right.x, top, right.z), 0.35f, p.Blanco);

        Vector3 center = (new Vector3(left.x, top, left.z) + new Vector3(right.x, top, right.z)) * 0.5f - Vector3.up * 0.6f;
        float width = Vector3.Distance(left, right) + 0.6f;
        WorldPart(PrimitiveType.Cube, "Tablero", arch, center, new Vector3(width, 1.3f, 0.15f), rotation, p.Negro);
        // El cartel mira hacia los corredores que se acercan (su cara visible apunta hacia atrás de la pista).
        MeshObject("Cartel", arch, p.Cartel, center - forward * 0.085f, rotation, new Vector3(width - 0.2f, 1.1f, 1f), false, banner);
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

    /// <summary>Rampa: bloque inclinado de madera sobre el camino, con su borde delantero a ras del suelo.</summary>
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

    private static void CreateRock(TrackGenerator track, int i, float lateral, float size, bool withCollider, System.Random rng, Palette p, Transform parent)
    {
        Mesh mesh = p.Rocas[rng.Next(p.Rocas.Length)];
        Vector3 scale = new Vector3(size * Range(rng, 0.9f, 1.4f), size * Range(rng, 0.7f, 1.0f), size * Range(rng, 0.9f, 1.3f));
        GameObject rock = MeshObject("Roca", parent, mesh,
            track.GetSurfacePoint(i, lateral) - Vector3.up * scale.y * 0.15f,
            Quaternion.Euler(Range(rng, -10f, 10f), Range(rng, 0f, 360f), Range(rng, -10f, 10f)), scale, true, p.Roca);

        if (withCollider)
        {
            // Collider convexo con la forma de la roca (la malla ya viene facetada y simple).
            MeshCollider meshCollider = rock.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
            meshCollider.convex = true;
        }
    }

    private static void TryCreateTree(TrackGenerator track, int i, float lateral, bool forest, System.Random rng, Palette p, Transform parent)
    {
        int side = lateral >= 0f ? 1 : -1;
        if (Mathf.Abs(lateral) > track.GetOuterOffset(i, side) - 1f) return; // Fuera del pasto.

        Vector3 position = track.GetSurfacePoint(i, lateral);
        // Lejos del borde de cualquier tramo (contando las bermas), para que las ramas no tapen el camino.
        if (!IsClearOfRoad(track, position, 3f)) return;

        bool pine = rng.NextDouble() < (forest ? 0.8 : 0.6);
        Mesh mesh = pine ? p.Pinos[rng.Next(p.Pinos.Length)] : p.Frondosos[rng.Next(p.Frondosos.Length)];
        float scale = Range(rng, 0.85f, 1.25f);
        MeshObject(pine ? "Pino" : "ArbolFrondoso", parent, mesh, position - Vector3.up * 0.1f,
            Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * scale, true, p.Tronco, pine ? p.Agujas : p.Hojas);
    }

    /// <summary>True si el punto está a "margin" metros o más del pie de las bermas de todos los tramos.</summary>
    private static bool IsClearOfRoad(TrackGenerator track, Vector3 position, float margin)
    {
        for (int j = 0; j < track.SampleCount; j += 2)
        {
            Vector3 d = position - track.GetPosition(j);
            d.y = 0f;
            float clearance = Mathf.Max(track.GetBermFootOffset(j, -1), track.GetBermFootOffset(j, 1)) + margin;
            if (d.sqrMagnitude < clearance * clearance) return false;
        }
        return true;
    }

    private static void ConfigureFallHeight(BikeController bike, TrackGenerator track)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < track.SampleCount; i++) lowest = Mathf.Min(lowest, track.GetPosition(i).y);
        SetFloat(bike, "fallHeight", lowest - 25f);
    }

    // =====================================================================
    // Cámara y UI
    // =====================================================================

    private static void BuildCamera(BikeController bike)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 450f;
        cam.clearFlags = CameraClearFlags.Skybox;
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
        Font font = UiBuilder.DefaultFont;
        // Sin GraphicRaycaster ni EventSystem: en la carrera la UI no tiene botones, todo va por GameInput.
        Canvas canvas = UiBuilder.Canvas("UI", false);
        GameObject canvasGo = canvas.gameObject;

        Color white = UiBuilder.White;
        Color accent = new Color(1f, 0.62f, 0.1f);
        Color gold = UiBuilder.Gold;

        // --- HUD de carrera.
        RectTransform hud = UiBuilder.Stretch(UiBuilder.UIObject("HUD", canvasGo.transform));
        Text time = UiBuilder.Label("Tiempo", hud, font, "0:00.00", 76, new Vector2(0.5f, 1f), new Vector2(0f, -70f), white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Text best = UiBuilder.Label("Mejor", hud, font, "Mejor  --:--.--", 30, new Vector2(0.5f, 1f), new Vector2(0f, -135f), gold, FontStyle.Normal, TextAnchor.MiddleCenter, true);
        Text speed = UiBuilder.Label("Velocidad", hud, font, "0", 110, new Vector2(1f, 0f), new Vector2(-300f, 115f), white, FontStyle.Bold, TextAnchor.MiddleRight, true, new Vector2(320f, 130f));
        UiBuilder.Label("Unidad", hud, font, "km/h", 36, new Vector2(1f, 0f), new Vector2(-75f, 85f), new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold, TextAnchor.MiddleLeft, true, new Vector2(120f, 60f));
        UiBuilder.Label("Ayuda", hud, font, "R: reaparecer   ·   Esc: reiniciar", 26, new Vector2(0f, 0f), new Vector2(260f, 50f), new Color(1f, 1f, 1f, 0.7f), FontStyle.Normal, TextAnchor.MiddleLeft, true, new Vector2(480f, 50f));
        Text countdown = UiBuilder.Label("Cuenta", hud, font, string.Empty, 230, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), accent, FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(900f, 300f));

        // --- Barra de energía, abajo a la izquierda (sobre la ayuda de teclas).
        Text energyLabel = UiBuilder.Label("EnergiaTexto", hud, font, "ENERGÍA", 26, new Vector2(0f, 0f), new Vector2(240f, 140f),
            new Color(1f, 1f, 1f, 0.8f), FontStyle.Bold, TextAnchor.MiddleLeft, true, new Vector2(400f, 36f));
        RectTransform energyBar = UiBuilder.Panel("EnergiaBarra", hud, Vector2.zero, Vector2.zero, new Vector2(240f, 108f),
            new Vector2(400f, 22f), new Color(0f, 0f, 0f, 0.55f));
        RectTransform energyFill = UiBuilder.Panel("EnergiaRelleno", energyBar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.35f, 0.85f, 0.35f));
        energyFill.pivot = new Vector2(0f, 0.5f);
        // Margen solo arriba y abajo: así con energía 0 el relleno queda en ancho 0 (y no negativo).
        energyFill.offsetMin = new Vector2(0f, 3f);
        energyFill.offsetMax = new Vector2(0f, -3f);

        // --- Controles: se muestran durante la cuenta regresiva (antes estaban en la pantalla de inicio).
        RectTransform controls = UiBuilder.Panel("Controles", hud, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1240f, 220f), new Color(0f, 0f, 0f, 0.55f));
        UiBuilder.Label("ControlesIzquierda", controls, font,
            "<b>Pedalear</b>   W / ↑   ·   RT\n" +
            "<b>Frenar</b>   S / ↓   ·   LT\n" +
            "<b>Girar</b>   A D / ← →   ·   Stick izquierdo\n" +
            "<b>Agacharse</b>   Shift izq.   ·   X / Cuadrado",
            28, new Vector2(0.5f, 0.5f), new Vector2(-300f, 0f), white, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Vector2(560f, 200f));
        UiBuilder.Label("ControlesDerecha", controls, font,
            "<b>Saltar</b>   Espacio   ·   A / Cruz\n" +
            "<b>Reaparecer</b>   R   ·   Y / Triángulo\n" +
            "<b>Correr otra vez</b>   Enter   ·   Start\n" +
            "<b>Reiniciar / menú</b>   Esc   ·   Select",
            28, new Vector2(0.5f, 0.5f), new Vector2(320f, 0f), white, FontStyle.Normal, TextAnchor.MiddleLeft, false, new Vector2(560f, 200f));

        // --- Pantalla de meta (dentro del HUD).
        RectTransform finish = UiBuilder.Panel("PantallaMeta", hud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f), new Color(0f, 0f, 0f, 0.6f));
        UiBuilder.Label("MetaTitulo", finish, font, "¡META!", 140, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), gold, FontStyle.Bold, TextAnchor.MiddleCenter, true, new Vector2(900f, 180f));
        Text finishTime = UiBuilder.Label("MetaTiempo", finish, font, "Tiempo: 0:00.00", 64, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), white, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        Text finishBest = UiBuilder.Label("MetaMejor", finish, font, "Mejor: 0:00.00", 40, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Color(1f, 1f, 1f, 0.9f), FontStyle.Normal, TextAnchor.MiddleCenter, true);
        Text record = UiBuilder.Label("NuevoRecord", finish, font, "¡Nuevo récord!", 54, new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), gold, FontStyle.Bold, TextAnchor.MiddleCenter, true);
        UiBuilder.Label("MetaAyuda", finish, font, "ENTER para correr otra vez   ·   ESC para volver al menú", 30, new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), white, FontStyle.Normal, TextAnchor.MiddleCenter, true, new Vector2(950f, 60f));

        RaceHUD raceHud = canvasGo.AddComponent<RaceHUD>();
        SetReference(raceHud, "bike", bike);
        SetReference(raceHud, "hudRoot", hud.gameObject);
        SetReference(raceHud, "timeText", time);
        SetReference(raceHud, "bestTimeText", best);
        SetReference(raceHud, "speedText", speed);
        SetReference(raceHud, "countdownText", countdown);
        SetReference(raceHud, "controlsPanel", controls.gameObject);
        SetReference(raceHud, "energyFill", energyFill);
        SetReference(raceHud, "energyFillImage", energyFill.GetComponent<Image>());
        SetReference(raceHud, "energyLabel", energyLabel);
        SetReference(raceHud, "finishRoot", finish.gameObject);
        SetReference(raceHud, "finishTimeText", finishTime);
        SetReference(raceHud, "finishBestText", finishBest);
        SetReference(raceHud, "newRecordText", record);

        // Estado inicial correcto ya en el Editor, antes de dar Play.
        hud.gameObject.SetActive(false);
        finish.gameObject.SetActive(false);
    }

    // =====================================================================
    // Luz, cielo, post-procesado y rendimiento
    // =====================================================================

    /// <summary>Día nublado de montaña: sol suave, cielo panorámico y niebla que esconde la distancia.</summary>
    private static void BuildLighting(Palette p)
    {
        GameObject sun = new GameObject("Sol");
        sun.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.95f, 0.97f, 1f);
        light.intensity = 1.05f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.6f;

        RenderSettings.sun = light;
        RenderSettings.skybox = p.Cielo;
        // Luz ambiente de tres colores: no necesita hornear iluminación.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.62f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.47f, 0.50f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.22f, 0.19f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 380f;
    }

    /// <summary>Post-procesado ligero: tonos neutros, algo de contraste, bloom suave y viñeta.</summary>
    private static void BuildPostProcessing()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        }
        // Se vacía el perfil para no acumular efectos al reconstruir la escena.
        foreach (VolumeComponent old in profile.components.ToArray())
        {
            AssetDatabase.RemoveObjectFromAsset(old);
            Object.DestroyImmediate(old, true);
        }
        profile.components.Clear();

        Tonemapping tonemapping = AddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.Neutral);

        ColorAdjustments color = AddVolumeComponent<ColorAdjustments>(profile);
        color.postExposure.Override(0.1f);
        color.contrast.Override(15f);
        color.saturation.Override(-12f);
        color.colorFilter.Override(new Color(0.97f, 0.99f, 1f));

        Bloom bloom = AddVolumeComponent<Bloom>(profile);
        bloom.intensity.Override(0.25f);
        bloom.threshold.Override(1f);
        bloom.scatter.Override(0.6f);

        Vignette vignette = AddVolumeComponent<Vignette>(profile);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.4f);

        EditorUtility.SetDirty(profile);

        GameObject go = new GameObject("PostProceso");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
    }

    private static T AddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(false);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
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

    // =====================================================================
    // Utilidades de construcción
    // =====================================================================

    private static Transform Group(string name, Transform parent)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>Objeto con una malla y sus materiales (en el orden de las submallas).</summary>
    private static GameObject MeshObject(string name, Transform parent, Mesh mesh, Vector3 position, Quaternion rotation,
        Vector3 scale, bool castShadows, params Material[] materials)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
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

    /// <summary>Cilindro entre dos puntos (postes de los arcos).</summary>
    private static GameObject WorldTube(string name, Transform parent, Vector3 from, Vector3 to, float thickness, Material material)
    {
        Vector3 direction = to - from;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        // El cilindro de Unity mide 2 de alto: la escala Y es la mitad del largo.
        return WorldPart(PrimitiveType.Cylinder, name, parent, (from + to) * 0.5f,
            new Vector3(thickness, direction.magnitude * 0.5f, thickness), rotation, material);
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
