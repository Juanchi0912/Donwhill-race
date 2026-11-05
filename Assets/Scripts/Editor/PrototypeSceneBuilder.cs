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
/// Construye por código la escena de una pista a partir de su "Diseño de pista" (TrackLayout): el camino,
/// la bici GY RAVEN con su corredor, el entorno según el ambiente (bosque nublado o valle seco),
/// cámara, UI, luces y post-procesado. Así nadie tiene que armar la escena a mano ni editar archivos .unity.
/// Menú: DownhillRace → Crear escena del prototipo / Crear escena: La Paz — Yunguilla,
/// o el botón "Construir escena" en el Inspector de cualquier diseño.
/// </summary>
public static class PrototypeSceneBuilder
{
    private const float StartDistance = 6f;       // Dónde arranca la bici (m desde el inicio de la pista).
    private const float StartArchDistance = 10f;
    private const float FinishBeforeEnd = 28f;    // La meta queda a 28 m del final, para frenar antes de la barrera.
    private const float RampAngle = 14f;

    /// <summary>Materiales, mallas y ajustes de ambiente compartidos por toda la escena.</summary>
    private class Palette
    {
        public TrackLayout.Theme Theme;
        public bool Dry => Theme == TrackLayout.Theme.ValleSeco;
        public Material Tierra, Berma, Pasto, Roca, Rampa, Tronco, Agujas, Hojas, Helecho, Montana, Valle;
        public Material Blanco, Rojo, Negro, CartelSalida, CartelMeta, Barro, Polvo, Cielo, Agua;
        public Material Grava, Concreto, Pared, Techo, TechoZinc, Ventana, Poste, Agave, HojasVerdes, Cana, Gotas;
        public PhysicsMaterial SinFriccion;
        public Mesh[] Pinos, Frondosos, Rocas, Helechos, Montanas, Arbustos;
        public Mesh Cartel, PiedraRampa, PlanoAgua, RampaMalla, PosteLuz;
        public Mesh[] Casas, Agaves, Canas;
        public Texture2D TexPasto, TexPastoNormal, TexRoca, TexRocaNormal;
        public Color FogColor;
        public float FogStart, FogEnd;
    }

    [MenuItem("DownhillRace/Crear escena del prototipo", priority = 0)]
    public static void CreatePrototypeScene()
    {
        BuildScene(TrackLayoutLibrary.GetPista01());
    }

    [MenuItem("DownhillRace/Crear escena: La Paz — Yunguilla", priority = 1)]
    public static void CreateLaPazScene()
    {
        BuildScene(TrackLayoutLibrary.GetLaPazYunguilla());
    }

    /// <summary>Construye y guarda la escena de un diseño de pista (Assets/Scenes/&lt;escena&gt;.unity).</summary>
    public static void BuildScene(TrackLayout layout)
    {
        if (layout == null || layout.Sections.Count == 0 || string.IsNullOrWhiteSpace(layout.SceneName))
        {
            EditorUtility.DisplayDialog("Construir escena", "El diseño de pista no tiene tramos o le falta el nombre de la escena.", "Entendido");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string scenePath = "Assets/Scenes/" + layout.SceneName + ".unity";
        if (File.Exists(scenePath) && !EditorUtility.DisplayDialog(
                "Construir " + layout.DisplayName,
                "Ya existe " + scenePath + ".\n¿Quieres reemplazarla por una escena nueva?",
                "Reemplazar", "Cancelar"))
        {
            return;
        }

        try
        {
            Progress("Generando texturas y materiales", 0.05f);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Palette palette = CreatePalette(layout.Ambience);

            GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            SetString(gameManager, "trackId", layout.SceneName);

            Progress("Generando la pista", 0.3f);
            List<float> sectionStarts;
            TrackGenerator track = BuildTrack(layout, palette, out sectionStarts);

            Progress("Armando la bici GY RAVEN y el corredor", 0.45f);
            BikeController bike = BuildBikeAndRider(palette, track);

            Progress("Colocando atajos, checkpoints, saltos, rocas y vegetación", 0.55f);
            BuildShortcuts(layout, track, sectionStarts, new System.Random(1717), palette);
            PopulateTrack(layout, track, sectionStarts, palette);
            Progress("Levantando las montañas a los lados", 0.75f);
            if (layout.MountainWalls) BuildTerrain(layout, track, palette);
            BuildSurroundings(layout, track, palette);
            ConfigureFallHeight(bike, track);

            Progress("Creando cámara, luces, post-procesado y UI", 0.85f);
            BuildCamera(bike, palette);
            BuildLighting(palette);
            BuildPostProcessing(layout, palette);
            BuildUI(bike);
            ApplyPerformanceSettings();

            Progress("Guardando la escena", 0.95f);
            VisualAssetFactory.EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            BuildSceneList.Register(scenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = bike.gameObject;
            Debug.Log("DownhillRace: escena '" + layout.DisplayName + "' creada en " + scenePath + " (" +
                      track.TotalLength.ToString("0") + " m). Presiona Play para probarla.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // =====================================================================
    // Materiales, texturas y mallas
    // =====================================================================

    private static Palette CreatePalette(TrackLayout.Theme theme)
    {
        Palette p = new Palette { Theme = theme };
        bool dry = p.Dry;

        // Superficies de la pista (anisotrópico: se ven bien aunque la cámara las mire rasantes).
        // Cada ambiente usa nombres de asset propios, así las dos pistas conviven.
        PT.Image dirt, dirtN, berm, bermN, grass, grassN;
        if (dry)
        {
            PT.DryDirt(512, false, out dirt, out dirtN);
            PT.DryDirt(512, true, out berm, out bermN);
            PT.DryGrass(512, out grass, out grassN);
        }
        else
        {
            PT.Dirt(512, out dirt, out dirtN);
            PT.Berm(512, out berm, out bermN);
            PT.ForestFloor(512, out grass, out grassN);
        }
        string tierra = dry ? "TierraSeca" : "Tierra";
        string bermaName = dry ? "BermaSeca" : "Berma";
        string pasto = dry ? "PastoSeco" : "Pasto";
        p.Tierra = VisualAssetFactory.Lit(tierra, Color.white,
            VisualAssetFactory.SaveTexture(dirt, tierra, Kind.ColorSmoothness, 1024, 4),
            VisualAssetFactory.SaveTexture(dirtN, tierra + "Normal", Kind.Normal, 1024, 4), 1f, true);
        p.Berma = VisualAssetFactory.Lit(bermaName, Color.white,
            VisualAssetFactory.SaveTexture(berm, bermaName, Kind.ColorSmoothness, 512, 4),
            VisualAssetFactory.SaveTexture(bermN, bermaName + "Normal", Kind.Normal, 512, 4), 1f, true);
        Texture2D grassTex = VisualAssetFactory.SaveTexture(grass, pasto, Kind.ColorSmoothness, 512, 4);
        Texture2D grassNormal = VisualAssetFactory.SaveTexture(grassN, pasto + "Normal", Kind.Normal, 512, 4);
        p.Pasto = VisualAssetFactory.Lit(pasto, Color.white, grassTex, grassNormal, 1f, true, 0f, 0.8f);
        p.TexPasto = grassTex;
        p.TexPastoNormal = grassNormal;

        // Rocas, madera de las rampas y árboles.
        PT.Image rock, rockN;
        if (dry) PT.DryRock(256, out rock, out rockN);
        else PT.Rock(256, out rock, out rockN);
        string roca = dry ? "RocaSeca" : "Roca";
        p.TexRoca = VisualAssetFactory.SaveTexture(rock, roca, Kind.ColorSmoothness, 256);
        p.TexRocaNormal = VisualAssetFactory.SaveTexture(rockN, roca + "Normal", Kind.Normal, 256);
        p.Roca = VisualAssetFactory.Lit(roca, Color.white, p.TexRoca, p.TexRocaNormal, 1f, true);
        PT.WoodPlanks(256, out PT.Image wood, out PT.Image woodN);
        p.Rampa = VisualAssetFactory.Lit("Rampa", Color.white,
            VisualAssetFactory.SaveTexture(wood, "Madera", Kind.ColorSmoothness, 256),
            VisualAssetFactory.SaveTexture(woodN, "MaderaNormal", Kind.Normal, 256), 1f, true);

        PT.Bark(256, out PT.Image bark, out PT.Image barkN);
        PT.Foliage(256, true, out PT.Image needles, out PT.Image needlesN);
        PT.Image leaves, leavesN;
        if (dry) PT.DryFoliage(256, out leaves, out leavesN);
        else PT.Foliage(256, false, out leaves, out leavesN);
        string hojas = dry ? "HojasSecas" : "Hojas";
        Texture2D barkTex = VisualAssetFactory.SaveTexture(bark, "Corteza", Kind.ColorSmoothness, 256);
        Texture2D barkNormal = VisualAssetFactory.SaveTexture(barkN, "CortezaNormal", Kind.Normal, 256);
        Texture2D needlesTex = VisualAssetFactory.SaveTexture(needles, "Agujas", Kind.ColorSmoothness, 256);
        Texture2D needlesNormal = VisualAssetFactory.SaveTexture(needlesN, "AgujasNormal", Kind.Normal, 256);
        Texture2D leavesTex = VisualAssetFactory.SaveTexture(leaves, hojas, Kind.ColorSmoothness, 256);
        Texture2D leavesNormal = VisualAssetFactory.SaveTexture(leavesN, hojas + "Normal", Kind.Normal, 256);
        p.Tronco = VisualAssetFactory.Lit("Tronco", Color.white, barkTex, barkNormal, 1f, true);
        p.Agujas = VisualAssetFactory.Lit("Agujas", Color.white, needlesTex, needlesNormal, 1f, true);
        p.Hojas = VisualAssetFactory.Lit(hojas, Color.white, leavesTex, leavesNormal, 1f, true);
        // Plantas bajas: helechos en el bosque; matas de pasto seco en el valle.
        p.Helecho = dry
            ? VisualAssetFactory.Lit("MataSeca", new Color(1f, 0.95f, 0.8f), grassTex, null, 1f, true)
            : VisualAssetFactory.Lit("Helecho", new Color(0.85f, 1f, 0.8f), leavesTex, null, 1f, true);

        // Paisaje lejano: la niebla les da el tono, así que basta un color.
        p.Montana = dry
            ? VisualAssetFactory.Lit("MontanaSeca", new Color(0.56f, 0.50f, 0.40f), null, null, 0f)
            : VisualAssetFactory.Lit("Montana", new Color(0.40f, 0.44f, 0.48f), null, null, 0f);
        p.Valle = dry
            ? VisualAssetFactory.Lit("ValleSeco", new Color(0.48f, 0.42f, 0.30f), null, null, 0f)
            : VisualAssetFactory.Lit("Valle", new Color(0.18f, 0.27f, 0.14f), null, null, 0f);

        // Decorado simple.
        p.Blanco = VisualAssetFactory.Lit("Blanco", new Color(0.92f, 0.92f, 0.92f), null, null, 0.3f);
        p.Rojo = VisualAssetFactory.Lit("Rojo", new Color(0.85f, 0.15f, 0.12f), null, null, 0.3f);
        p.Negro = VisualAssetFactory.Lit("Negro", new Color(0.06f, 0.06f, 0.07f), null, null, 0.3f);
        p.CartelSalida = VisualAssetFactory.Lit("CartelSalida", Color.white,
            VisualAssetFactory.SaveTexture(PT.Banner(1024, 128, "SALIDA", new PT.Rgb(0.12f, 0.35f, 0.82f), false), "CartelSalida", Kind.Color, 1024), null, 0.3f);
        p.CartelMeta = VisualAssetFactory.Lit("CartelMeta", Color.white,
            VisualAssetFactory.SaveTexture(PT.Banner(1024, 128, "META", new PT.Rgb(0.08f, 0.08f, 0.08f), true), "CartelMeta", Kind.Color, 1024), null, 0.3f);
        PT.RiverWater(256, out PT.Image water, out PT.Image waterN);
        p.Agua = VisualAssetFactory.Transparent("Agua", new Color(1f, 1f, 1f, 0.95f), 0.92f,
            VisualAssetFactory.SaveTexture(water, "Agua", Kind.Decal, 256),
            VisualAssetFactory.SaveTexture(waterN, "AguaNormal", Kind.Normal, 256));

        // Pueblo y carretera: lastre, concreto, casas y postes.
        PT.Gravel(512, out PT.Image gravel, out PT.Image gravelN);
        p.Grava = VisualAssetFactory.Lit("Carretera", Color.white,
            VisualAssetFactory.SaveTexture(gravel, "Carretera", Kind.ColorSmoothness, 1024, 4),
            VisualAssetFactory.SaveTexture(gravelN, "CarreteraNormal", Kind.Normal, 1024, 4), 1f, true);
        PT.Concrete(256, out PT.Image concrete, out PT.Image concreteN);
        p.Concreto = VisualAssetFactory.Lit("Concreto", Color.white,
            VisualAssetFactory.SaveTexture(concrete, "Concreto", Kind.ColorSmoothness, 256),
            VisualAssetFactory.SaveTexture(concreteN, "ConcretoNormal", Kind.Normal, 256), 1f, true);
        p.Pared = VisualAssetFactory.Lit("CasaPared", new Color(0.90f, 0.86f, 0.76f), null, null, 0.1f);
        p.Techo = VisualAssetFactory.Lit("CasaTeja", new Color(0.70f, 0.32f, 0.20f), null, null, 0.15f);
        p.TechoZinc = VisualAssetFactory.Lit("CasaZinc", new Color(0.62f, 0.64f, 0.66f), null, null, 0.6f, false, 0.6f);
        p.Ventana = VisualAssetFactory.Lit("CasaVentana", new Color(0.12f, 0.12f, 0.13f), null, null, 0.5f);
        p.Poste = VisualAssetFactory.Lit("PosteConcreto", new Color(0.65f, 0.64f, 0.62f), null, null, 0.1f);
        // Plantas del valle: agaves verde azulado, follaje tropical y caña de azúcar.
        PT.Foliage(256, false, out PT.Image greenLeaves, out PT.Image greenLeavesN);
        Texture2D greenTex = VisualAssetFactory.SaveTexture(greenLeaves, "HojasVerdes", Kind.ColorSmoothness, 256);
        p.HojasVerdes = VisualAssetFactory.Lit("HojasVerdes", Color.white, greenTex,
            VisualAssetFactory.SaveTexture(greenLeavesN, "HojasVerdesNormal", Kind.Normal, 256), 1f, true);
        p.Agave = VisualAssetFactory.Lit("Agave", new Color(0.62f, 0.80f, 0.62f), greenTex, null, 1f, true);
        p.Cana = VisualAssetFactory.Lit("Cana", new Color(0.85f, 1f, 0.6f), greenTex, null, 1f, true);

        // Partículas y cielo. En el valle seco hay más polvo y más claro.
        Texture2D blob = VisualAssetFactory.SaveTexture(PT.SoftBlob(64), "Particula", Kind.Decal, 64);
        p.Barro = dry
            ? VisualAssetFactory.Particles("ParticulaTierraSeca", blob, new Color(0.55f, 0.44f, 0.32f, 1f))
            : VisualAssetFactory.Particles("ParticulaBarro", blob, new Color(0.24f, 0.17f, 0.11f, 1f));
        p.Polvo = dry
            ? VisualAssetFactory.Particles("ParticulaPolvoSeco", blob, new Color(0.80f, 0.70f, 0.55f, 0.55f))
            : VisualAssetFactory.Particles("ParticulaPolvo", blob, new Color(0.55f, 0.47f, 0.38f, 0.45f));
        p.Gotas = VisualAssetFactory.Particles("ParticulaGotas", blob, new Color(0.85f, 0.92f, 1f, 0.8f));
        p.Cielo = dry
            ? VisualAssetFactory.PanoramicSky("CieloValle", VisualAssetFactory.SaveTexture(PT.HazySky(1024, 512), "CieloValle", Kind.Sky, 1024), 1f)
            : VisualAssetFactory.PanoramicSky("CieloNublado", VisualAssetFactory.SaveTexture(PT.OvercastSky(1024, 512), "CieloNublado", Kind.Sky, 1024), 1f);

        // Niebla: en el bosque, densa y gris; en el valle, una calima cálida que deja ver lejos.
        p.FogColor = dry ? new Color(0.82f, 0.80f, 0.74f) : new Color(0.70f, 0.73f, 0.76f);
        p.FogStart = dry ? 70f : 25f;
        p.FogEnd = dry ? 520f : 380f;

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
        p.Arbustos = new Mesh[3];
        for (int i = 0; i < p.Arbustos.Length; i++) p.Arbustos[i] = VisualAssetFactory.SaveMesh(PM.BuildBush(61 + i), "Arbusto" + (i + 1));
        p.PiedraRampa = VisualAssetFactory.SaveMesh(PM.BuildRockKicker(71), "PiedraRampa");
        p.PlanoAgua = VisualAssetFactory.SaveMesh(PM.BuildWaterPlane(), "Agua");
        p.RampaMalla = VisualAssetFactory.SaveMesh(PM.BuildKicker(), "RampaNatural");
        p.PosteLuz = VisualAssetFactory.SaveMesh(PM.BuildPowerPole(), "PosteLuz");
        p.Casas = new Mesh[3];
        for (int i = 0; i < p.Casas.Length; i++) p.Casas[i] = VisualAssetFactory.SaveMesh(PM.BuildHouse(81 + i), "Casa" + (i + 1));
        p.Agaves = new Mesh[2];
        for (int i = 0; i < p.Agaves.Length; i++) p.Agaves[i] = VisualAssetFactory.SaveMesh(PM.BuildBlades(91 + i, 1.1f, 14, 0.11f, 0.9f), "Agave" + (i + 1));
        p.Canas = new Mesh[2];
        for (int i = 0; i < p.Canas.Length; i++) p.Canas[i] = VisualAssetFactory.SaveMesh(PM.BuildBlades(95 + i, 3.0f, 12, 0.05f, 0.35f), "Cana" + (i + 1));
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
        SetReference(visuals, "bielas", parts.Bielas);
        SetReference(visuals, "pedalIzquierdo", parts.PedalIzquierdo);
        SetReference(visuals, "pedalDerecho", parts.PedalDerecho);
        SetReference(visuals, "corredor", parts.Corredor);
        SetReference(visuals, "torso", parts.Torso);
        SetFloat(visuals, "wheelRadius", PM.WheelRadius);

        // --- Piernas que siguen a los pedales (las medidas salen del mismo modelo).
        RiderLegIK legs = visual.AddComponent<RiderLegIK>();
        SetReference(legs, "frame", parts.Inclinacion);
        SetReference(legs, "torso", parts.Torso);
        SetReference(legs, "leftThigh", parts.Left.Thigh);
        SetReference(legs, "leftShin", parts.Left.Shin);
        SetReference(legs, "leftFoot", parts.Left.Foot);
        SetReference(legs, "leftPedal", parts.PedalIzquierdo);
        SetReference(legs, "rightThigh", parts.Right.Thigh);
        SetReference(legs, "rightShin", parts.Right.Shin);
        SetReference(legs, "rightFoot", parts.Right.Foot);
        SetReference(legs, "rightPedal", parts.PedalDerecho);
        SetFloat(legs, "thighLength", PM.ThighLength);
        SetFloat(legs, "shinLength", PM.ShinLength);
        SetFloat(legs, "ankleHeight", PM.AnkleHeight);
        SetFloat(legs, "ankleBack", PM.AnkleBack);
        SetVector3(legs, "hipOffset", VisualAssetFactory.ToUnity(PM.HipOffset));

        // --- Brazos que siguen a los puños del manubrio.
        RiderArmIK arms = visual.AddComponent<RiderArmIK>();
        SetReference(arms, "frame", parts.Inclinacion);
        SetReference(arms, "torso", parts.Torso);
        SetReference(arms, "leftUpperArm", parts.LeftArm.UpperArm);
        SetReference(arms, "leftForearm", parts.LeftArm.Forearm);
        SetReference(arms, "leftHand", parts.LeftArm.Hand);
        SetReference(arms, "leftGrip", parts.LeftArm.Grip);
        SetReference(arms, "rightUpperArm", parts.RightArm.UpperArm);
        SetReference(arms, "rightForearm", parts.RightArm.Forearm);
        SetReference(arms, "rightHand", parts.RightArm.Hand);
        SetReference(arms, "rightGrip", parts.RightArm.Grip);
        SetFloat(arms, "upperArmLength", PM.UpperArmLength);
        SetFloat(arms, "forearmLength", PM.ForearmLength);
        SetVector3(arms, "shoulderOffset", VisualAssetFactory.ToUnity(PM.ShoulderOffset));

        // --- Barro y polvo de la rueda trasera.
        Vector3 rearContact = new Vector3(0f, 0.06f, PM.RearAxle.Z - 0.05f);
        ParticleSystem mud = CreateSpray("Barro", parts.Root, rearContact, p.Barro, false);
        ParticleSystem dust = CreateSpray("Polvo", parts.Root, rearContact + new Vector3(0f, 0.05f, -0.1f), p.Polvo, true);
        ParticleSystem splash = CreateSplash(parts.Root, rearContact + new Vector3(0f, 0.1f, 0f), p.Gotas);
        WheelSpray spray = visual.AddComponent<WheelSpray>();
        SetReference(spray, "bike", controller);
        SetReference(spray, "mud", mud);
        SetReference(spray, "dust", dust);
        SetReference(spray, "splash", splash);
        if (p.Dry)
        {
            SetFloat(spray, "maxDustRate", 30f);
            SetFloat(spray, "maxMudRate", 35f);
        }

        parts.Root.SetPositionAndRotation(startPoint, startRotation);
        return controller;
    }

    /// <summary>Gotas de agua que salen de las ruedas al cruzar el río.</summary>
    private static ParticleSystem CreateSplash(Transform parent, Vector3 localPosition, Material material)
    {
        ParticleSystem ps = CreateSpray("Salpicadura", parent, localPosition, material, false);
        ParticleSystem.MainModule main = ps.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.92f, 1f, 0.9f), new Color(0.6f, 0.75f, 0.85f, 0.7f));
        main.gravityModifier = 1.4f;
        main.maxParticles = 200;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.angle = 45f; // Las gotas se abren hacia los lados.
        ps.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 1f, -0.6f));
        return ps;
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

    private static TrackGenerator BuildTrack(TrackLayout layout, Palette p, out List<float> sectionStarts)
    {
        // Se recorren los tramos como una "tortuga": cada 5 m se avanza, se gira y se baja.
        List<Vector3> points = new List<Vector3>();
        List<float> widths = new List<float>();
        List<int> grounds = new List<int>();
        sectionStarts = new List<float>();

        Vector3 position = Vector3.zero;
        float heading = 0f;
        float distance = 0f;
        points.Add(position);
        widths.Add(layout.HalfWidthOf(layout.Sections[0]));
        grounds.Add(layout.Sections[0].Surface == TrackSection.Ground.Carretera ? 1 : 0);

        foreach (TrackSection section in layout.Sections)
        {
            sectionStarts.Add(distance);
            int steps = Mathf.Max(1, Mathf.CeilToInt(section.Length / 5f));
            float stepLength = section.Length / steps;
            float stepTurn = section.Turn / steps;
            float slope = section.Slope * Mathf.Deg2Rad;
            for (int s = 0; s < steps; s++)
            {
                heading += stepTurn * 0.5f;
                Vector3 flatDir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                position += flatDir * stepLength * Mathf.Cos(slope) + Vector3.down * stepLength * Mathf.Sin(slope);
                heading += stepTurn * 0.5f;
                distance += stepLength;
                points.Add(position);
                widths.Add(layout.HalfWidthOf(section));
                grounds.Add(section.Surface == TrackSection.Ground.Carretera ? 1 : 0);
            }
        }

        GameObject go = new GameObject("Pista");
        go.AddComponent<MeshFilter>();
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();
        TrackGenerator track = go.AddComponent<TrackGenerator>();
        track.SetLayout(points, widths, grounds);
        SetFloat(track, "defaultHalfWidth", layout.DefaultHalfWidth);
        if (p.Dry)
        {
            // Sendero natural de ladera: bermas más bajas y menos peralte que en la pista construida del bosque.
            SetFloat(track, "bermBaseHeight", 0.3f);
            SetFloat(track, "bermMaxExtraHeight", 1.1f);
            SetFloat(track, "bermPerCurvature", 0.5f);
            SetFloat(track, "maxBankAngle", 8f);
            SetFloat(track, "grassWidth", 16f);
        }

        Mesh generated = track.Generate();
        Mesh saved = VisualAssetFactory.SaveMeshAsset(generated, VisualAssetFactory.ModelsFolder + "/" + layout.SceneName + ".asset");
        go.GetComponent<MeshFilter>().sharedMesh = saved;
        MeshCollider meshCollider = go.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = saved;

        renderer.sharedMaterials = new[] { p.Tierra, p.Berma, p.Pasto, p.Grava };
        MarkStatic(go);
        return track;
    }

    /// <summary>Coloca todo lo que va sobre la pista según los tramos del diseño.</summary>
    private static void PopulateTrack(TrackLayout layout, TrackGenerator track, List<float> starts, Palette p)
    {
        System.Random rng = new System.Random(20260101); // Semilla fija: la pista sale igual cada vez.
        float length = track.TotalLength;
        float finishDistance = length - FinishBeforeEnd;

        // --- Checkpoints y meta.
        Transform checkpoints = new GameObject("Checkpoints").transform;
        int index = 1;
        for (float d = layout.CheckpointSpacing; d < finishDistance - 10f; d += layout.CheckpointSpacing)
        {
            CreateTrigger(track, d, TrackTrigger.TriggerType.Checkpoint, index, "Checkpoint " + index, checkpoints);
            index++;
        }
        CreateTrigger(track, finishDistance, TrackTrigger.TriggerType.Meta, 0, "Meta", checkpoints);

        // --- Arcos de salida y meta con carteles (solo decorado, sin collider) y barreras en los extremos.
        Transform decorado = new GameObject("Decorado").transform;
        CreateArch(track, StartArchDistance, "ArcoSalida", p.CartelSalida, p, decorado);
        CreateArch(track, finishDistance, "ArcoMeta", p.CartelMeta, p, decorado);
        CreateBarrier(track, 1.5f, "BarreraSalida", p, decorado);
        CreateBarrier(track, length - 2f, "BarreraFinal", p, decorado);

        Transform rampas = Group("Rampas", decorado);
        Transform saltos = Group("SaltosNaturales", decorado);
        Transform raices = Group("Raices", decorado);
        Transform rocas = Group("Rocas", decorado);
        Transform agua = Group("Rio", decorado);

        // --- Lo que pide cada tramo.
        for (int k = 0; k < layout.Sections.Count; k++)
        {
            TrackSection section = layout.Sections[k];
            float s0 = Mathf.Min(starts[k], length);
            float s1 = Mathf.Min(k + 1 < starts.Count ? starts[k + 1] : length, length);
            if (s1 - s0 < 1f) continue;

            if (section.WoodRamp != TrackSection.Ramp.Ninguna)
            {
                CreateRamp(track, Mathf.Lerp(s0, s1, section.RampPosition), section.WoodRamp, "Rampa " + section.Name, p, rampas);
            }
            if (section.RockGarden) CreateRockGarden(track, s0, s1, rng, p, rocas);
            if (section.Buildings) CreateVillage(track, s0, s1, rng, p, Group("Pueblo " + section.Name, decorado));

            float jumpAt = Mathf.Lerp(s0, s1, section.JumpPosition);
            switch (section.Jump)
            {
                case TrackSection.NaturalJump.PiedraIzquierda: CreateRockKicker(track, jumpAt, -1, p, saltos); break;
                case TrackSection.NaturalJump.PiedraDerecha: CreateRockKicker(track, jumpAt, 1, p, saltos); break;
                case TrackSection.NaturalJump.Raiz: CreateRoot(track, jumpAt, true, rng, p, saltos); break;
            }

            if (section.Roots)
            {
                for (float d = s0 + 3f; d < s1 - 1f; d += Range(rng, 4f, 7f)) CreateRoot(track, d, false, rng, p, raices);
            }

            if (section.RocksOnEdges)
            {
                // Rocas en los bordes, angostando el camino (con collider). En senderos angostos, más chicas.
                int side = 1;
                for (float d = s0 + 2f; d < s1; d += Range(rng, 2.5f, 4f))
                {
                    int i = track.GetIndexAtDistance(d);
                    float hw = track.GetHalfWidth(i);
                    float scale = Mathf.Clamp(hw / 4.5f, 0.55f, 1f);
                    side = rng.NextDouble() < 0.7 ? -side : side;
                    CreateRock(track, i, side * (hw + Range(rng, -0.6f, 1.2f) * scale), Range(rng, 0.8f, 1.6f) * scale, true, rng, p, rocas);
                    if (rng.NextDouble() < 0.5)
                    {
                        CreateRock(track, i, side * (hw + Range(rng, 1.5f, 3f) * scale), Range(rng, 0.6f, 1.2f) * scale, true, rng, p, rocas);
                    }
                }
            }

            if (section.Water) CreateRiver(track, s0, s1, rng, p, agua);
        }

        // --- Rocas sueltas en el pasto (solo decorado), fuera de las zonas de rocas y del río.
        for (float d = 15f; d < length - 10f; d += Range(rng, 8f, 16f))
        {
            TrackSection section = SectionAt(layout, starts, d);
            if (section.RocksOnEdges || section.Water || section.Buildings || section.Surface == TrackSection.Ground.Carretera) continue;
            int i = track.GetIndexAtDistance(d);
            int s = rng.NextDouble() < 0.5 ? -1 : 1;
            float lateral = s * (track.GetBermFootOffset(i, s) + Range(rng, 0.5f, 6f));
            if (Mathf.Abs(lateral) < track.GetOuterOffset(i, s) - 1f && IsClearOfRoad(track, track.GetSurfacePoint(i, lateral), 1f))
            {
                CreateRock(track, i, lateral, Range(rng, 0.4f, 1.1f), false, rng, p, rocas);
            }
        }

        PlaceTrees(layout, track, starts, rng, p, Group("Arboles", decorado));
        PlaceLowPlants(layout, track, starts, rng, p, Group(p.Dry ? "Matorral" : "Helechos", decorado));

        // Decorado estático: permite agrupar los dibujados y ahorrar trabajo a la GPU.
        foreach (Transform t in decorado.GetComponentsInChildren<Transform>())
        {
            if (t.GetComponent<SlowZone>() == null) MarkStatic(t.gameObject);
        }
    }

    /// <summary>
    /// Árboles: en el bosque, pinos y árboles de hoja ancha; en el valle seco, pocos árboles bajos;
    /// en la zona verde, árboles tropicales; junto al cañaveral, alguno suelto.
    /// </summary>
    private static void PlaceTrees(TrackLayout layout, TrackGenerator track, List<float> starts, System.Random rng, Palette p, Transform parent)
    {
        float length = track.TotalLength;
        for (float d = 0f; d < length; d += 3f)
        {
            TrackSection section = SectionAt(layout, starts, d);
            TrackSection.Vegetation plants = section.Plants;
            if (plants == TrackSection.Vegetation.Ninguna || section.Water) continue;
            bool dense = plants == TrackSection.Vegetation.Densa || plants == TrackSection.Vegetation.Verde;
            bool green = plants == TrackSection.Vegetation.Verde || plants == TrackSection.Vegetation.Canaveral;
            float chance;
            int rows;
            if (plants == TrackSection.Vegetation.Verde) { chance = 0.4f; rows = 2; }
            else if (plants == TrackSection.Vegetation.Canaveral) { chance = 0.06f; rows = 1; }
            else if (p.Dry)
            {
                chance = dense ? 0.35f : (plants == TrackSection.Vegetation.Normal ? 0.12f : 0.05f);
                rows = 2;
            }
            else
            {
                chance = dense ? 1f : (plants == TrackSection.Vegetation.Normal ? 0.3f : 0.12f);
                rows = dense ? 3 : 2;
            }
            if (section.Buildings) chance *= 0.4f; // En el pueblo, las casas ocupan el lugar.

            int i = track.GetIndexAtDistance(d);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int row = 0; row < rows; row++)
                {
                    if (rng.NextDouble() > chance) continue;
                    float foot = track.GetBermFootOffset(i, s);
                    // Se dejan 3.5 m o más tras la berma: las ramas bajas no tapan el camino.
                    float lateral = s * (foot + 3.5f + row * 3.5f + Range(rng, 0f, dense ? 2f : 6f));
                    TryCreateTree(track, i, lateral, dense, green, rng, p, parent);
                }
            }
        }
    }

    /// <summary>
    /// Plantas bajas junto al camino: helechos en el bosque; matorral, matas de pasto seco y agaves en el valle;
    /// arbustos verdes en la zona tropical y caña de azúcar alta en el cañaveral.
    /// </summary>
    private static void PlaceLowPlants(TrackLayout layout, TrackGenerator track, List<float> starts, System.Random rng, Palette p, Transform parent)
    {
        float length = track.TotalLength;
        for (float d = 4f; d < length; d += 2.2f)
        {
            TrackSection section = SectionAt(layout, starts, d);
            if (section.Water) continue;
            TrackSection.Vegetation plants = section.Plants;
            float chance;
            switch (plants)
            {
                case TrackSection.Vegetation.Densa: chance = p.Dry ? 0.85f : 0.75f; break;
                case TrackSection.Vegetation.Escasa: chance = 0.2f; break;
                case TrackSection.Vegetation.Ninguna: chance = 0f; break;
                case TrackSection.Vegetation.Verde: chance = 0.85f; break;
                case TrackSection.Vegetation.Canaveral: chance = 1f; break;
                default: chance = p.Dry ? 0.45f : 0.55f; break;
            }
            if (section.Buildings) chance *= 0.3f;

            int i = track.GetIndexAtDistance(d);
            for (int s = -1; s <= 1; s += 2)
            {
                // El cañaveral es una pared de caña a ambos lados: dos o tres matas por lado.
                int clumps = plants == TrackSection.Vegetation.Canaveral ? 3 : 1;
                for (int c = 0; c < clumps; c++)
                {
                    if (rng.NextDouble() > chance) continue;
                    PlaceLowPlant(track, i, s, plants, rng, p, parent);
                }
            }
        }
    }

    private static void PlaceLowPlant(TrackGenerator track, int i, int side, TrackSection.Vegetation plants, System.Random rng, Palette p, Transform parent)
    {
        bool cane = plants == TrackSection.Vegetation.Canaveral;
        bool green = plants == TrackSection.Vegetation.Verde;
        bool agave = !cane && !green && p.Dry && rng.NextDouble() < 0.12;
        bool bush = !cane && !agave && (green || p.Dry) && rng.NextDouble() < 0.6;
        // El matorral del valle crece pegado al sendero, como un túnel de arbustos.
        float near = cane ? Range(rng, 0.2f, 4f) : (bush ? Range(rng, -0.4f, 2.5f) : Range(rng, 0.2f, 4f));
        float lateral = side * (track.GetBermFootOffset(i, side) + near);
        if (Mathf.Abs(lateral) > track.GetOuterOffset(i, side) - 0.5f) return;
        Vector3 position = track.GetSurfacePoint(i, lateral);
        if (!IsClearOfRoad(track, position, bush ? -0.4f : 0.2f)) return;
        Quaternion yaw = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);

        if (cane)
        {
            MeshObject("Cana", parent, p.Canas[rng.Next(p.Canas.Length)], position, yaw, Vector3.one * Range(rng, 0.9f, 1.3f), false, p.Cana);
        }
        else if (agave)
        {
            MeshObject("Agave", parent, p.Agaves[rng.Next(p.Agaves.Length)], position, yaw, Vector3.one * Range(rng, 0.8f, 1.4f), false, p.Agave);
        }
        else if (bush)
        {
            MeshObject("Arbusto", parent, p.Arbustos[rng.Next(p.Arbustos.Length)], position, yaw,
                Vector3.one * Range(rng, 0.8f, 1.5f), true, p.Tronco, green ? p.HojasVerdes : p.Hojas);
        }
        else
        {
            MeshObject(p.Dry ? "MataSeca" : "Helecho", parent, p.Helechos[rng.Next(p.Helechos.Length)], position, yaw,
                Vector3.one * Range(rng, 0.8f, 1.4f), false, p.Helecho);
        }
    }

    /// <summary>Tramo del diseño que contiene la distancia d (desde la salida).</summary>
    private static TrackSection SectionAt(TrackLayout layout, List<float> starts, float d)
    {
        for (int k = starts.Count - 1; k >= 0; k--)
        {
            if (d >= starts[k]) return layout.Sections[k];
        }
        return layout.Sections[0];
    }

    /// <summary>Salto natural: una piedra en un borde del camino que hace de rampa (opcional, se puede esquivar).</summary>
    private static void CreateRockKicker(TrackGenerator track, float distance, int side, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        float hw = track.GetHalfWidth(i);
        Vector3 position = track.GetSurfacePoint(i, side * Mathf.Max(0.4f, hw - 0.75f)) - Vector3.up * 0.03f;
        Quaternion rotation = Quaternion.LookRotation(track.GetForward(i), Vector3.up);
        GameObject rock = MeshObject("PiedraRampa", parent, p.PiedraRampa, position, rotation, new Vector3(1.4f, 0.75f, 2.4f), true, p.Roca);
        MeshCollider meshCollider = rock.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = p.PiedraRampa;
        meshCollider.convex = true;
    }

    /// <summary>Raíz cruzando el camino, medio enterrada. Las grandes sirven para saltar una parte trabada.</summary>
    private static void CreateRoot(TrackGenerator track, float distance, bool big, System.Random rng, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        float hw = track.GetHalfWidth(i);
        float radius = big ? 0.2f : Range(rng, 0.06f, 0.11f);
        float angle = big ? Range(rng, -10f, 10f) : Range(rng, -30f, 30f);
        float length = hw * 2f + 1.5f;
        Quaternion rotation = Quaternion.LookRotation(Flat(track.GetForward(i))) * Quaternion.Euler(0f, angle, 90f);

        // El cilindro de Unity trae CapsuleCollider: la bici lo pisa como un bulto redondo.
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = big ? "RaizGrande" : "Raiz";
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(track.GetSurfacePoint(i, 0f) + Vector3.up * radius * 0.35f, rotation);
        root.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        root.GetComponent<Renderer>().sharedMaterial = p.Tronco;
    }

    /// <summary>Cruce de río: agua que frena la bici, orillas de barro y piedras alrededor.</summary>
    private static void CreateRiver(TrackGenerator track, float s0, float s1, System.Random rng, Palette p, Transform parent)
    {
        int mid = track.GetIndexAtDistance((s0 + s1) * 0.5f);
        Vector3 forward = Flat(track.GetForward(mid));
        Quaternion rotation = Quaternion.LookRotation(forward);
        float outer = Mathf.Max(track.GetOuterOffset(mid, -1), track.GetOuterOffset(mid, 1));
        float width = outer * 2f + 60f;
        float length = s1 - s0 + 8f;
        Vector3 center = track.GetSurfacePoint(mid, 0f);

        GameObject water = MeshObject("Agua", parent, p.PlanoAgua, center + Vector3.up * 0.25f, rotation, new Vector3(width, 1f, length), false, p.Agua);
        water.AddComponent<WaterFlow>(); // Las ondas se desplazan: el agua corre.
        Rivers.Add(new RiverBed
        {
            Center = center, Forward = forward, Right = Vector3.Cross(Vector3.up, forward),
            HalfLength = length * 0.5f, HalfWidth = width * 0.5f, WaterY = center.y + 0.25f
        });

        // Orillas de barro a la entrada y salida del agua.
        foreach (float d in new[] { s0 - 1.5f, s1 + 1.5f })
        {
            int i = track.GetIndexAtDistance(d);
            MeshObject("Orilla", parent, p.PlanoAgua, track.GetSurfacePoint(i, 0f) + Vector3.up * 0.03f,
                Quaternion.LookRotation(Flat(track.GetForward(i))), new Vector3(outer * 2f, 1f, 2.2f), false, p.Berma);
        }

        // El agua frena: zona invisible que avisa a la bici al entrar y salir.
        GameObject zone = new GameObject("ZonaAgua");
        zone.transform.SetParent(parent, false);
        zone.transform.SetPositionAndRotation(center, rotation);
        BoxCollider box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width, 4f, length - 4f);
        box.center = new Vector3(0f, 1.5f, 0f);
        // Dentro del agua la bici se frena mucho: cuesta cruzar el río.
        zone.AddComponent<SlowZone>().Configure(6f);

        // Piedras del río: chicas dentro del camino (se pisan o se esquivan) y grandes por todo el cauce.
        for (int n = 0; n < 50; n++)
        {
            float d = Range(rng, s0 - 1f, s1 + 1f);
            int i = track.GetIndexAtDistance(d);
            float hw = track.GetHalfWidth(i);
            bool onTrail = n < 10;
            float lateral = onTrail ? Range(rng, -hw + 0.3f, hw - 0.3f) : (rng.NextDouble() < 0.5 ? -1f : 1f) * Range(rng, hw + 0.4f, outer);
            float size = onTrail ? Range(rng, 0.25f, 0.45f) : Range(rng, 0.5f, 1.8f);
            CreateRock(track, i, lateral, size, true, rng, p, parent);
        }
    }

    /// <summary>Montañas lejanas que asoman entre la niebla y un valle muy abajo, para que no se vea el vacío.</summary>
    private static void BuildSurroundings(TrackLayout layout, TrackGenerator track, Palette p)
    {
        System.Random rng = new System.Random(777);
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < track.SampleCount; i++)
        {
            min = Vector3.Min(min, track.GetPosition(i));
            max = Vector3.Max(max, track.GetPosition(i));
        }
        Vector3 center = (min + max) * 0.5f;
        float lowest = min.y;
        // Las montañas van más allá de la pista entera (no solo de su centro): así ninguna cae encima del camino.
        float ring = new Vector2(max.x - min.x, max.z - min.z).magnitude * 0.5f + (layout.MountainWalls ? 380f : 250f);

        Transform paisaje = new GameObject("Paisaje").transform;
        const int mountains = 10;
        for (int k = 0; k < mountains; k++)
        {
            float angle = (k / (float)mountains * 360f + Range(rng, -12f, 12f)) * Mathf.Deg2Rad;
            float distance = ring + Range(rng, 0f, 60f);
            Vector3 position = new Vector3(center.x + Mathf.Sin(angle) * distance, lowest - 30f, center.z + Mathf.Cos(angle) * distance);
            float radius = Range(rng, 120f, 170f);
            GameObject mountain = MeshObject("Montana", paisaje, p.Montanas[k % p.Montanas.Length], position,
                Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(radius, Range(rng, 90f, 150f), radius), false, p.Montana);
            mountain.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // Con paredes de montaña no hace falta el valle: el terreno ya cubre todo.
        if (layout.MountainWalls)
        {
            foreach (Transform t in paisaje.GetComponentsInChildren<Transform>()) MarkStatic(t.gameObject);
            return;
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

    /// <summary>
    /// Rampa de salto con curva de transición: tablones de madera (o concreto) apoyados en un montículo
    /// de tierra con taludes a los lados y atrás, como las que se construyen en las pistas reales.
    /// </summary>
    private static void CreateRamp(TrackGenerator track, float distance, TrackSection.Ramp type, string name, Palette p, Transform parent)
    {
        int i = track.GetIndexAtDistance(distance);
        float hw = track.GetHalfWidth(i);
        Vector3 size;
        switch (type)
        {
            case TrackSection.Ramp.Grande: size = new Vector3(4f, 1.8f, 6.5f); break;
            case TrackSection.Ramp.Concreto: size = new Vector3(3.2f, 1.2f, 5f); break;
            default: size = new Vector3(3f, 1.1f, 4.5f); break;
        }
        // En senderos angostos la rampa no puede ser más ancha que el camino.
        size.x = Mathf.Min(size.x, hw * 2f - 0.2f);
        Quaternion rotation = Quaternion.LookRotation(track.GetForward(i), Vector3.up);
        Vector3 start = track.GetSurfacePoint(i, 0f) - Vector3.up * 0.04f;
        Material surface = type == TrackSection.Ramp.Concreto ? p.Concreto : p.Rampa;

        GameObject ramp = MeshObject(name, parent, p.RampaMalla, start, rotation, size, true, surface, p.Berma);
        MeshCollider meshCollider = ramp.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = p.RampaMalla;
        meshCollider.convex = true;
    }

    /// <summary>Pedregal: piedras sueltas dentro del camino, de distintos tamaños, que hay que esquivar o pasar por encima.</summary>
    private static void CreateRockGarden(TrackGenerator track, float s0, float s1, System.Random rng, Palette p, Transform parent)
    {
        for (float d = s0 + 1.5f; d < s1 - 0.5f; d += Range(rng, 1.4f, 2.6f))
        {
            int i = track.GetIndexAtDistance(d);
            float hw = track.GetHalfWidth(i);
            int count = rng.NextDouble() < 0.4 ? 2 : 1;
            for (int n = 0; n < count; n++)
            {
                float lateral = Range(rng, -hw + 0.2f, hw - 0.2f);
                CreateRock(track, i, lateral, Range(rng, 0.28f, 0.5f), true, rng, p, parent);
            }
        }
    }

    /// <summary>Pueblo: casas y galpones a los lados del camino, con la fachada hacia él, y postes de luz.</summary>
    private static void CreateVillage(TrackGenerator track, float s0, float s1, System.Random rng, Palette p, Transform parent)
    {
        for (float d = s0 + Range(rng, 2f, 8f); d < s1; d += Range(rng, 10f, 16f))
        {
            int i = track.GetIndexAtDistance(d);
            int side = rng.NextDouble() < 0.5 ? -1 : 1;
            float outer = track.GetOuterOffset(i, side);
            float lateral = side * Mathf.Min(track.GetBermFootOffset(i, side) + Range(rng, 3.5f, 6f), outer);
            Vector3 position = track.GetSurfacePoint(i, lateral);
            if (!IsClearOfRoad(track, position, 3.2f)) continue;
            // La fachada (con la puerta) mira hacia el camino.
            Vector3 toRoad = Flat(track.GetPosition(i) - position);
            Quaternion rotation = Quaternion.LookRotation(-toRoad) * Quaternion.Euler(0f, Range(rng, -8f, 8f), 0f);
            bool shed = rng.NextDouble() < 0.3;
            MeshObject(shed ? "Galpon" : "Casa", parent, p.Casas[rng.Next(p.Casas.Length)], position - Vector3.up * 0.1f, rotation,
                Vector3.one, true, p.Pared, shed ? p.TechoZinc : p.Techo, p.Ventana);
        }
        for (float d = s0 + 5f; d < s1; d += 28f)
        {
            int i = track.GetIndexAtDistance(d);
            float lateral = track.GetBermFootOffset(i, 1) + 1.2f;
            MeshObject("PosteLuz", parent, p.PosteLuz, track.GetSurfacePoint(i, lateral),
                Quaternion.LookRotation(Flat(track.GetForward(i))), Vector3.one, true, p.Poste);
        }
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

    private static void TryCreateTree(TrackGenerator track, int i, float lateral, bool forest, bool green, System.Random rng, Palette p, Transform parent)
    {
        int side = lateral >= 0f ? 1 : -1;
        if (Mathf.Abs(lateral) > track.GetOuterOffset(i, side) - 1f) return; // Fuera del pasto.

        Vector3 position = track.GetSurfacePoint(i, lateral);
        // Lejos del borde de cualquier tramo (contando las bermas), para que las ramas no tapen el camino.
        if (!IsClearOfRoad(track, position, 3f)) return;

        // En el valle no hay pinos: árboles de hoja ancha más bajos (secos, o verdes en la zona tropical).
        bool pine = !p.Dry && !green && rng.NextDouble() < (forest ? 0.8 : 0.6);
        Mesh mesh = pine ? p.Pinos[rng.Next(p.Pinos.Length)] : p.Frondosos[rng.Next(p.Frondosos.Length)];
        float scale = p.Dry ? Range(rng, 0.6f, 1.0f) : Range(rng, 0.85f, 1.25f);
        Material leaves = pine ? p.Agujas : (green ? p.HojasVerdes : p.Hojas);
        MeshObject(pine ? "Pino" : "ArbolFrondoso", parent, mesh, position - Vector3.up * 0.1f,
            Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * scale, true, p.Tronco, leaves);
    }

    /// <summary>True si el punto está a "margin" metros o más del pie de las bermas de todos los tramos.</summary>
    private static bool IsClearOfRoad(TrackGenerator track, Vector3 position, float margin)
    {
        foreach (TrackGenerator extra in ExtraTracks)
        {
            if (!IsClearOf(extra, position, margin)) return false;
        }
        return IsClearOf(track, position, margin);
    }

    private static bool IsClearOf(TrackGenerator track, Vector3 position, float margin)
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
    // Atajos ocultos
    // =====================================================================

    /// <summary>Pistas extra (los atajos) que la vegetación y el terreno también deben respetar.</summary>
    private static readonly List<TrackGenerator> ExtraTracks = new List<TrackGenerator>();

    /// <summary>Cauce de un río: el terreno se hunde ahí para que el agua corra entre orillas.</summary>
    private struct RiverBed
    {
        public Vector3 Center, Forward, Right;
        public float HalfLength, HalfWidth, WaterY;
    }

    private static readonly List<RiverBed> Rivers = new List<RiverBed>();

    /// <summary>
    /// Crea los atajos del diseño: senderos angostos y empinados que salen de la pista y vuelven más abajo.
    /// La entrada queda entre matorrales para que no se note a primera vista.
    /// </summary>
    private static void BuildShortcuts(TrackLayout layout, TrackGenerator track, List<float> starts, System.Random rng, Palette p)
    {
        ExtraTracks.Clear();
        Rivers.Clear();
        if (layout.Shortcuts.Count == 0) return;
        Transform group = new GameObject("Atajos").transform;
        int number = 0;
        foreach (TrackShortcut shortcut in layout.Shortcuts)
        {
            int from = FindSection(layout, shortcut.FromSection);
            int to = FindSection(layout, shortcut.ToSection);
            if (from < 0 || to < 0)
            {
                Debug.LogWarning("PrototypeSceneBuilder: el atajo '" + shortcut.Name + "' usa un tramo que no existe ('" +
                                 shortcut.FromSection + "' o '" + shortcut.ToSection + "'). Revisa los nombres en el diseño.");
                continue;
            }
            float da = starts[from] + layout.Sections[from].Length * shortcut.FromPosition;
            float db = starts[to] + layout.Sections[to].Length * shortcut.ToPosition;
            int ia = track.GetIndexAtDistance(da), ib = track.GetIndexAtDistance(db);
            Vector3 pa = track.GetPosition(ia), pb = track.GetPosition(ib);
            if (db <= da + 10f || pb.y >= pa.y)
            {
                Debug.LogWarning("PrototypeSceneBuilder: el atajo '" + shortcut.Name + "' debe terminar más abajo y más adelante que donde empieza.");
                continue;
            }

            // Sale por el lado de la pista que mira hacia el final del atajo (y entra igual).
            int sideA = Vector3.Dot(pb - pa, track.GetRight(ia)) >= 0f ? 1 : -1;
            int sideB = Vector3.Dot(pa - pb, track.GetRight(ib)) >= 0f ? 1 : -1;
            Vector3 start = track.GetSurfacePoint(ia, sideA * (track.GetBermFootOffset(ia, sideA) + 0.3f));
            Vector3 end = track.GetSurfacePoint(ib, sideB * (track.GetBermFootOffset(ib, sideB) + 0.3f));
            Vector3 across = Vector3.Cross(Vector3.up, Flat(end - start));

            List<Vector3> points = new List<Vector3>();
            List<float> widths = new List<float>();
            int steps = Mathf.Max(3, Mathf.CeilToInt(Vector3.Distance(start, end) / 4f));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                Vector3 point = Vector3.Lerp(start, end, t) + across * Mathf.Sin(t * Mathf.PI) * shortcut.Bend;
                point.y = Mathf.Lerp(start.y, end.y, t) + 0.04f;
                points.Add(point);
                widths.Add(shortcut.HalfWidth);
            }

            GameObject go = new GameObject("Atajo " + shortcut.Name);
            go.transform.SetParent(group, false);
            go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();
            TrackGenerator path = go.AddComponent<TrackGenerator>();
            path.SetLayout(points, widths);
            // Sendero sin construir: casi sin bermas ni peralte, con poco pasto alrededor.
            SetFloat(path, "defaultHalfWidth", shortcut.HalfWidth);
            SetFloat(path, "bermBaseHeight", 0.12f);
            SetFloat(path, "bermMaxExtraHeight", 0.15f);
            SetFloat(path, "maxBankAngle", 0f);
            SetFloat(path, "grassWidth", 1.5f);
            Mesh mesh = VisualAssetFactory.SaveMeshAsset(path.Generate(), VisualAssetFactory.ModelsFolder + "/" + layout.SceneName + "_Atajo" + (++number) + ".asset");
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshCollider meshCollider = go.GetComponent<MeshCollider>();
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { p.Tierra, p.Berma, p.Pasto, p.Grava };
            MarkStatic(go);
            ExtraTracks.Add(path);

            if (shortcut.Roots)
            {
                for (float d = 4f; d < path.TotalLength - 3f; d += Range(rng, 3.5f, 6f)) CreateRoot(path, d, false, rng, p, go.transform);
            }

            // Entrada camuflada: matorral a los lados de la boca del atajo, sobre la pista principal.
            HideEntrance(track, ia, sideA, rng, p, go.transform);
            HideEntrance(track, ib, sideB, rng, p, go.transform);
            Debug.Log("DownhillRace: atajo '" + shortcut.Name + "' de " + path.TotalLength.ToString("0") + " m (ahorra " +
                      (db - da - path.TotalLength).ToString("0") + " m).");
        }
    }

    private static void HideEntrance(TrackGenerator track, int index, int side, System.Random rng, Palette p, Transform parent)
    {
        foreach (float along in new[] { -3.2f, -2.2f, 2.2f, 3.2f })
        {
            int i = track.GetIndexAtDistance(track.GetDistance(index) + along);
            float lateral = side * (track.GetBermFootOffset(i, side) + Range(rng, 0f, 0.8f));
            Mesh bush = p.Arbustos[rng.Next(p.Arbustos.Length)];
            Material leaves = p.Dry ? p.Hojas : p.HojasVerdes;
            MeshObject("ArbustoEntrada", parent, bush, track.GetSurfacePoint(i, lateral),
                Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 1.1f, 1.5f), true, p.Tronco, leaves);
        }
    }

    private static int FindSection(TrackLayout layout, string name)
    {
        for (int k = 0; k < layout.Sections.Count; k++)
        {
            if (layout.Sections[k].Name == name) return k;
        }
        return -1;
    }

    // =====================================================================
    // Paredes de montaña (terreno)
    // =====================================================================

    /// <summary>
    /// Terreno alrededor de toda la pista: bajo el camino queda escondido, entre tramos cercanos forma
    /// una ladera continua y, lejos del camino, sube en paredes empinadas. Así parece que la pista va
    /// por dentro de la montaña y no se ve vacío a los lados.
    /// </summary>
    private static void BuildTerrain(TrackLayout layout, TrackGenerator track, Palette p)
    {
        List<TrackGenerator> all = new List<TrackGenerator> { track };
        all.AddRange(ExtraTracks);
        List<Vector3> points = new List<Vector3>();
        List<float> wallStarts = new List<float>();
        List<float> sinks = new List<float>();
        List<int> riverOf = new List<int>();
        foreach (TrackGenerator t in all)
        {
            bool shortcut = t != track;
            for (int i = 0; i < t.SampleCount; i += 2)
            {
                Vector3 point = t.GetPosition(i);
                points.Add(point);
                // La pared empieza dentro del pasto (tapa el borde de la malla) pero nunca sobre el camino.
                float edge = Mathf.Max(t.GetOuterOffset(i, -1), t.GetOuterOffset(i, 1));
                wallStarts.Add(Mathf.Max(edge - 4f, t.GetHalfWidth(i) + (shortcut ? 1.5f : 2.5f)));
                // Bajo la pista principal el terreno va hondo (bermas y peralte); junto a un atajo casi a ras.
                sinks.Add(shortcut ? 0.35f : 1.6f);
                riverOf.Add(shortcut ? -1 : RiverAt(point));
            }
        }

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 point in points)
        {
            minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
            minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
        }
        const float margin = 220f;
        float originX = minX - margin, originZ = minZ - margin;
        float sizeX = maxX - minX + margin * 2f, sizeZ = maxZ - minZ + margin * 2f;
        float baseY = minY - 30f;
        float heightRange = maxY + layout.WallHeight * 1.6f + 20f - baseY;
        int resolution = Mathf.Max(sizeX, sizeZ) > 1400f ? 1025 : 513;

        // Cuadrícula para encontrar rápido los puntos de pista cercanos.
        const float cell = 20f;
        int gridW = Mathf.CeilToInt(sizeX / cell) + 1, gridH = Mathf.CeilToInt(sizeZ / cell) + 1;
        List<int>[] grid = new List<int>[gridW * gridH];
        for (int k = 0; k < points.Count; k++)
        {
            int gx = Mathf.FloorToInt((points[k].x - originX) / cell), gz = Mathf.FloorToInt((points[k].z - originZ) / cell);
            int g = gz * gridW + gx;
            if (grid[g] == null) grid[g] = new List<int>();
            grid[g].Add(k);
        }

        float[,] heights = new float[resolution, resolution];
        for (int zi = 0; zi < resolution; zi++)
        {
            float z = originZ + zi / (float)(resolution - 1) * sizeZ;
            for (int xi = 0; xi < resolution; xi++)
            {
                float x = originX + xi / (float)(resolution - 1) * sizeX;
                float h = TerrainHeight(x, z, points, wallStarts, sinks, riverOf, grid, gridW, gridH, originX, originZ, cell, layout);
                heights[zi, xi] = Mathf.Clamp01((h - baseY) / heightRange);
            }
        }

        string dataPath = VisualAssetFactory.ModelsFolder + "/" + layout.SceneName + "_Terreno.asset";
        AssetDatabase.DeleteAsset(dataPath);
        TerrainData data = new TerrainData();
        data.heightmapResolution = resolution;
        data.size = new Vector3(sizeX, heightRange, sizeZ);
        data.SetHeights(0, 0, heights);

        // Texturas: pasto en lo suave y roca en lo empinado.
        TerrainLayer grass = VisualAssetFactory.TerrainLayerAsset(layout.SceneName + "_CapaPasto", p.TexPasto, p.TexPastoNormal, 8f);
        TerrainLayer rock = VisualAssetFactory.TerrainLayerAsset(layout.SceneName + "_CapaRoca", p.TexRoca, p.TexRocaNormal, 6f);
        data.terrainLayers = new[] { grass, rock };
        int alphaRes = 256;
        data.alphamapResolution = alphaRes;
        float[,,] alpha = new float[alphaRes, alphaRes, 2];
        for (int y = 0; y < alphaRes; y++)
        {
            for (int x = 0; x < alphaRes; x++)
            {
                float steep = data.GetSteepness(x / (float)(alphaRes - 1), y / (float)(alphaRes - 1));
                float rockAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 42f, steep));
                alpha[y, x, 0] = 1f - rockAmount;
                alpha[y, x, 1] = rockAmount;
            }
        }
        data.SetAlphamaps(0, 0, alpha);
        AssetDatabase.CreateAsset(data, dataPath);

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Montañas";
        go.transform.position = new Vector3(originX, baseY, originZ);
        Terrain terrain = go.GetComponent<Terrain>();
        terrain.materialTemplate = VisualAssetFactory.TerrainMaterial("Terreno");
        terrain.heightmapPixelError = 6f;
        terrain.basemapDistance = 350f;
        terrain.drawInstanced = true;
        MarkStatic(go);

        ScatterOnWalls(track, terrain, p);
    }

    /// <summary>Altura del terreno en (x, z): debajo de la pista cerca de ella; paredes que suben al alejarse.</summary>
    private static float TerrainHeight(float x, float z, List<Vector3> points, List<float> wallStarts, List<float> sinks,
        List<int> riverOf, List<int>[] grid, int gridW, int gridH, float originX, float originZ, float cell, TrackLayout layout)
    {
        int gx = Mathf.FloorToInt((x - originX) / cell), gz = Mathf.FloorToInt((z - originZ) / cell);
        const int reach = 3; // 3 celdas = 60 m alrededor.
        double weightSum = 0, heightSum = 0, sinkSum = 0;
        float best = float.MaxValue;
        int nearest = -1;
        for (int oz = -reach; oz <= reach; oz++)
        {
            int cz = gz + oz;
            if (cz < 0 || cz >= gridH) continue;
            for (int ox = -reach; ox <= reach; ox++)
            {
                int cx = gx + ox;
                if (cx < 0 || cx >= gridW) continue;
                List<int> bucket = grid[cz * gridW + cx];
                if (bucket == null) continue;
                foreach (int k in bucket)
                {
                    float dx = points[k].x - x, dz = points[k].z - z;
                    float d2 = dx * dx + dz * dz;
                    // Peso muy fuerte a lo cercano: así el terreno pasa suave entre dos tramos a distinta altura.
                    double w = 1.0 / (d2 * d2 + 1.0);
                    weightSum += w;
                    heightSum += w * points[k].y;
                    sinkSum += w * sinks[k];
                    if (d2 < best) { best = d2; nearest = k; }
                }
            }
        }
        if (nearest < 0)
        {
            for (int k = 0; k < points.Count; k += 4)
            {
                float dx = points[k].x - x, dz = points[k].z - z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; nearest = k; }
            }
            heightSum = points[nearest].y;
            sinkSum = sinks[nearest];
            weightSum = 1.0;
        }

        float distance = Mathf.Sqrt(best);
        float ground = (float)((heightSum - sinkSum) / weightSum); // Queda bajo el pasto de la pista.
        float beyond = distance - wallStarts[nearest];
        float result = ground;
        if (beyond > 0f)
        {
            // Pared que sube rápido y se va aplanando arriba, con cerros y quebradas irregulares.
            float large = Mathf.PerlinNoise(x * 0.006f + 13.1f, z * 0.006f + 7.7f);
            float small = Mathf.PerlinNoise(x * 0.03f + 41.3f, z * 0.03f + 3.9f);
            float height = layout.WallHeight * (0.55f + large * 0.9f);
            float rise = height * (1f - Mathf.Exp(-beyond * layout.WallSteepness / Mathf.Max(1f, height)));
            result = ground + rise + (small - 0.5f) * 6f * Mathf.Clamp01(beyond / 15f);
        }

        // Cauce del río: solo donde lo más cercano es el cruce del río (no hunde otras partes de la pista).
        int river = riverOf[nearest];
        if (river >= 0)
        {
            RiverBed bed = Rivers[river];
            Vector3 offset = new Vector3(x - bed.Center.x, 0f, z - bed.Center.z);
            float along = Mathf.Abs(Vector3.Dot(offset, bed.Forward));
            float across = Mathf.Abs(Vector3.Dot(offset, bed.Right));
            float carve = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bed.HalfLength - 6f, bed.HalfLength + 6f, along)))
                        * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(bed.HalfWidth - 10f, bed.HalfWidth, across)));
            float bottom = bed.WaterY - 1.2f;
            if (result > bottom) result = Mathf.Lerp(result, bottom, carve);
        }
        return result;
    }

    /// <summary>Índice del río cuyo cruce contiene este punto de la pista, o -1.</summary>
    private static int RiverAt(Vector3 point)
    {
        for (int r = 0; r < Rivers.Count; r++)
        {
            Vector3 offset = point - Rivers[r].Center;
            offset.y = 0f;
            if (Mathf.Abs(Vector3.Dot(offset, Rivers[r].Forward)) <= Rivers[r].HalfLength + 12f &&
                Mathf.Abs(Vector3.Dot(offset, Rivers[r].Right)) <= 6f) return r;
        }
        return -1;
    }

    /// <summary>¿Cae dentro del agua de algún río? (ahí no se planta vegetación de ladera).</summary>
    private static bool InRiver(Vector3 position)
    {
        foreach (RiverBed bed in Rivers)
        {
            Vector3 offset = position - bed.Center;
            offset.y = 0f;
            if (Mathf.Abs(Vector3.Dot(offset, bed.Forward)) <= bed.HalfLength + 4f &&
                Mathf.Abs(Vector3.Dot(offset, bed.Right)) <= bed.HalfWidth + 2f) return true;
        }
        return false;
    }

    /// <summary>Matorral y árboles sueltos en las laderas, para que las paredes no se vean peladas.</summary>
    private static void ScatterOnWalls(TrackGenerator track, Terrain terrain, Palette p)
    {
        System.Random rng = new System.Random(4242);
        Transform group = new GameObject("VegetacionLaderas").transform;
        float terrainY = terrain.transform.position.y;
        for (int n = 0; n < 700; n++)
        {
            int i = rng.Next(track.SampleCount);
            int side = rng.NextDouble() < 0.5 ? -1 : 1;
            Vector3 basePoint = track.GetPosition(i) + track.GetRight(i) * side * (track.GetOuterOffset(i, side) + Range(rng, 2f, 60f));
            Vector3 position = new Vector3(basePoint.x, terrain.SampleHeight(basePoint) + terrainY - 0.1f, basePoint.z);
            if (!IsClearOfRoad(track, position, 2f) || InRiver(position)) continue;
            Quaternion yaw = Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f);
            double pick = rng.NextDouble();
            if (p.Dry)
            {
                if (pick < 0.6) MeshObject("Arbusto", group, p.Arbustos[rng.Next(p.Arbustos.Length)], position, yaw, Vector3.one * Range(rng, 1f, 1.8f), true, p.Tronco, p.Hojas);
                else if (pick < 0.9) MeshObject("ArbolSeco", group, p.Frondosos[rng.Next(p.Frondosos.Length)], position, yaw, Vector3.one * Range(rng, 0.6f, 1f), true, p.Tronco, p.Hojas);
                else MeshObject("Agave", group, p.Agaves[rng.Next(p.Agaves.Length)], position, yaw, Vector3.one * Range(rng, 1f, 1.5f), false, p.Agave);
            }
            else
            {
                bool pine = pick < 0.7;
                MeshObject(pine ? "Pino" : "ArbolFrondoso", group, pine ? p.Pinos[rng.Next(p.Pinos.Length)] : p.Frondosos[rng.Next(p.Frondosos.Length)],
                    position, yaw, Vector3.one * Range(rng, 0.85f, 1.3f), true, p.Tronco, pine ? p.Agujas : p.Hojas);
            }
        }
        foreach (Transform t in group.GetComponentsInChildren<Transform>()) MarkStatic(t.gameObject);
    }

    // =====================================================================
    // Cámara y UI
    // =====================================================================

    private static void BuildCamera(BikeController bike, Palette p)
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        Camera cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = p.FogEnd + 80f;
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

    /// <summary>
    /// Bosque: día nublado con sol suave y niebla densa. Valle seco: sol fuerte y cálido con calima.
    /// En ambos el cielo es panorámico y la niebla esconde la distancia.
    /// </summary>
    private static void BuildLighting(Palette p)
    {
        GameObject sun = new GameObject("Sol");
        sun.transform.rotation = p.Dry ? Quaternion.Euler(52f, -40f, 0f) : Quaternion.Euler(42f, -30f, 0f);
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = p.Dry ? new Color(1f, 0.95f, 0.86f) : new Color(0.95f, 0.97f, 1f);
        light.intensity = p.Dry ? 1.4f : 1.05f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = p.Dry ? 0.8f : 0.6f;

        RenderSettings.sun = light;
        RenderSettings.skybox = p.Cielo;
        // Luz ambiente de tres colores: no necesita hornear iluminación.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = p.Dry ? new Color(0.66f, 0.70f, 0.76f) : new Color(0.58f, 0.62f, 0.68f);
        RenderSettings.ambientEquatorColor = p.Dry ? new Color(0.62f, 0.58f, 0.50f) : new Color(0.47f, 0.50f, 0.50f);
        RenderSettings.ambientGroundColor = p.Dry ? new Color(0.38f, 0.32f, 0.24f) : new Color(0.24f, 0.22f, 0.19f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = p.FogColor;
        RenderSettings.fogStartDistance = p.FogStart;
        RenderSettings.fogEndDistance = p.FogEnd;
    }

    /// <summary>Post-procesado ligero: tonos neutros, algo de contraste, bloom suave y viñeta.</summary>
    private static void BuildPostProcessing(TrackLayout layout, Palette p)
    {
        string profilePath = "Assets/Settings/" + layout.SceneName + "_PostProceso.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
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
        // Bosque: frío y apagado. Valle: cálido y algo más saturado, como el sol de la tarde.
        color.postExposure.Override(p.Dry ? 0.05f : 0.1f);
        color.contrast.Override(p.Dry ? 10f : 15f);
        color.saturation.Override(p.Dry ? -4f : -12f);
        color.colorFilter.Override(p.Dry ? new Color(1f, 0.98f, 0.94f) : new Color(0.97f, 0.99f, 1f));

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

    private static void SetString(Object target, string field, string value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError("PrototypeSceneBuilder: " + target.GetType().Name + " no tiene el campo '" + field + "'.");
            return;
        }
        property.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVector3(Object target, string field, Vector3 value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError("PrototypeSceneBuilder: " + target.GetType().Name + " no tiene el campo '" + field + "'.");
            return;
        }
        property.vector3Value = value;
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
