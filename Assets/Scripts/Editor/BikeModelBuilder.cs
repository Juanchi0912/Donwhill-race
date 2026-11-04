using UnityEngine;
using UnityEngine.Rendering;
using PM = ProceduralModels;
using PT = ProceduralTextures;
using SN = System.Numerics;

/// <summary>
/// Arma el modelo visual rígido de la bici GY RAVEN y su corredor (casco integral, jersey naranja)
/// con las mallas de ProceduralModels. La jerarquía permite animar por código:
/// Inclinacion → (Cuadro, Bielas → Pedales, RuedaTrasera, EjeDireccion → Direccion → RuedaDelantera,
/// Corredor → (Torso, Muslo → Canilla → Pie de cada lado)).
/// </summary>
public static class BikeModelBuilder
{
    /// <summary>Muslo, canilla y pie de una pierna.</summary>
    public sealed class Leg
    {
        public Transform Thigh;
        public Transform Shin;
        public Transform Foot;
    }

    /// <summary>Pivotes que necesitan BikeVisuals y RiderLegIK.</summary>
    public sealed class Parts
    {
        public Transform Root;
        public Transform Inclinacion;
        public Transform RuedaTrasera;
        public Transform RuedaDelantera;
        public Transform Direccion;
        public Transform Bielas;
        public Transform PedalIzquierdo;
        public Transform PedalDerecho;
        public Transform Corredor;
        public Transform Torso;
        public Leg Left;
        public Leg Right;
    }

    public static Parts Build()
    {
        // --- Texturas.
        Texture2D camo = VisualAssetFactory.SaveTexture(PT.BikeCamo(512), "BiciCamuflaje", VisualAssetFactory.TextureKind.ColorSmoothness, 512);
        Texture2D decal = VisualAssetFactory.SaveTexture(
            PT.TextDecal(1024, 128, "GY RAVEN", new PT.Rgb(0.95f, 0.96f, 0.98f), new PT.Rgb(0.04f, 0.1f, 0.3f), 0.15f),
            "BiciCalcomania", VisualAssetFactory.TextureKind.Decal, 1024);
        Texture2D rim = VisualAssetFactory.SaveTexture(PT.RimDecal(1024, 64), "BiciAroCalcomania", VisualAssetFactory.TextureKind.ColorSmoothness, 1024);
        Texture2D tire = VisualAssetFactory.SaveTexture(PT.Tire(256, 64), "BiciNeumatico", VisualAssetFactory.TextureKind.ColorSmoothness, 256);
        Texture2D jersey = VisualAssetFactory.SaveTexture(PT.Jersey(512), "CorredorJersey", VisualAssetFactory.TextureKind.ColorSmoothness, 512);
        Texture2D shorts = VisualAssetFactory.SaveTexture(PT.MuddyFabric(256, new PT.Rgb(0.12f, 0.14f, 0.22f), 0.5f, 201), "CorredorShorts", VisualAssetFactory.TextureKind.ColorSmoothness, 256);
        Texture2D pads = VisualAssetFactory.SaveTexture(PT.MuddyFabric(256, new PT.Rgb(0.62f, 0.38f, 0.16f), 0.6f, 211), "CorredorRodilleras", VisualAssetFactory.TextureKind.ColorSmoothness, 256);
        Texture2D gloves = VisualAssetFactory.SaveTexture(PT.MuddyFabric(128, new PT.Rgb(0.12f, 0.12f, 0.13f), 0.3f, 221), "CorredorGuantes", VisualAssetFactory.TextureKind.ColorSmoothness, 128);
        Texture2D helmet = VisualAssetFactory.SaveTexture(PT.Helmet(512, 256), "CorredorCasco", VisualAssetFactory.TextureKind.ColorSmoothness, 512);
        Texture2D number = VisualAssetFactory.SaveTexture(
            PT.TextDecal(256, 256, "22", new PT.Rgb(0.12f, 0.11f, 0.1f), new PT.Rgb(0.95f, 0.95f, 0.95f), 0.1f),
            "CorredorNumero", VisualAssetFactory.TextureKind.Decal, 256);

        // --- Materiales de la bici.
        Material camoMat = VisualAssetFactory.Lit("BiciCamuflaje", Color.white, camo, null, 1f, true);
        Material blackMat = VisualAssetFactory.Lit("BiciNegro", new Color(0.06f, 0.06f, 0.07f), null, null, 0.35f);
        Material metalMat = VisualAssetFactory.Lit("BiciMetal", new Color(0.62f, 0.63f, 0.65f), null, null, 0.7f, false, 0.8f);
        Material orangeMat = VisualAssetFactory.Lit("BiciResorte", new Color(1f, 0.45f, 0.05f), null, null, 0.6f);
        Material goldMat = VisualAssetFactory.Lit("BiciDorado", new Color(0.86f, 0.6f, 0.16f), null, null, 0.85f, false, 0.9f);
        Material blueMat = VisualAssetFactory.Lit("BiciPunos", new Color(0.15f, 0.45f, 0.95f), null, null, 0.3f);
        Material decalMat = VisualAssetFactory.Cutout("BiciCalcomania", decal, Color.white, 0.6f, false);
        Material tireMat = VisualAssetFactory.Lit("BiciNeumatico", Color.white, tire, null, 1f, true);
        Material rimMat = VisualAssetFactory.Lit("BiciAro", Color.white, rim, null, 1f, true);

        // --- Materiales del corredor.
        Material jerseyMat = VisualAssetFactory.Lit("CorredorJersey", Color.white, jersey, null, 1f, true);
        Material shortsMat = VisualAssetFactory.Lit("CorredorShorts", Color.white, shorts, null, 1f, true);
        Material padsMat = VisualAssetFactory.Lit("CorredorRodilleras", Color.white, pads, null, 1f, true);
        Material glovesMat = VisualAssetFactory.Lit("CorredorGuantes", Color.white, gloves, null, 1f, true);
        Material shoesMat = VisualAssetFactory.Lit("CorredorZapatillas", new Color(0.1f, 0.1f, 0.11f), null, null, 0.25f);
        Material helmetMat = VisualAssetFactory.Lit("CorredorCasco", Color.white, helmet, null, 1f, true);
        Material lensMat = VisualAssetFactory.Lit("CorredorAntiparras", new Color(1f, 0.5f, 0.1f), null, null, 0.95f, false, 0.3f);
        Material numberMat = VisualAssetFactory.Cutout("CorredorNumero", number, Color.white, 0.3f, false);

        // --- Mallas.
        Mesh frame = VisualAssetFactory.SaveMesh(PM.BuildFrame(), "BiciCuadro");
        Mesh steering = VisualAssetFactory.SaveMesh(PM.BuildSteering(), "BiciDireccion");
        Mesh wheel = VisualAssetFactory.SaveMesh(PM.BuildWheel(), "BiciRueda");
        Mesh upper = VisualAssetFactory.SaveMesh(PM.BuildRiderUpper(), "CorredorTorso");
        Mesh cranks = VisualAssetFactory.SaveMesh(PM.BuildCranks(), "BiciBielas");
        Mesh pedal = VisualAssetFactory.SaveMesh(PM.BuildPedal(), "BiciPedal");
        Mesh thigh = VisualAssetFactory.SaveMesh(PM.BuildRiderThigh(), "CorredorMuslo");
        Mesh shin = VisualAssetFactory.SaveMesh(PM.BuildRiderShin(), "CorredorCanilla");
        Mesh foot = VisualAssetFactory.SaveMesh(PM.BuildRiderFoot(), "CorredorPie");

        // --- Jerarquía (el orden de los materiales sigue las submallas de ProceduralModels).
        Parts parts = new Parts();
        parts.Root = new GameObject("BiciVisual").transform;
        parts.Inclinacion = Child("Inclinacion", parts.Root, Vector3.zero, Quaternion.identity);

        Transform cuadro = Child("Cuadro", parts.Inclinacion, Vector3.zero, Quaternion.identity);
        AddRenderer(cuadro, frame, camoMat, blackMat, metalMat, orangeMat, goldMat, decalMat);

        Material[] wheelMats = { tireMat, rimMat, metalMat };
        parts.RuedaTrasera = Child("RuedaTrasera", parts.Inclinacion, VisualAssetFactory.ToUnity(PM.RearAxle), Quaternion.identity);
        AddRenderer(parts.RuedaTrasera, wheel, wheelMats);

        // El eje de dirección está inclinado como el de una bici de DH; Direccion gira sobre ese eje.
        Transform ejeDireccion = Child("EjeDireccion", parts.Inclinacion, VisualAssetFactory.ToUnity(PM.FrontAxle), VisualAssetFactory.ToUnity(PM.SteeringRotation));
        parts.Direccion = Child("Direccion", ejeDireccion, Vector3.zero, Quaternion.identity);
        AddRenderer(parts.Direccion, steering, blackMat, goldMat, orangeMat, blueMat, metalMat);
        parts.RuedaDelantera = Child("RuedaDelantera", parts.Direccion, Vector3.zero, Quaternion.identity);
        AddRenderer(parts.RuedaDelantera, wheel, wheelMats);

        // Bielas: en ángulo 0 el pedal izquierdo va adelante y el derecho atrás (posición de ataque).
        parts.Bielas = Child("Bielas", parts.Inclinacion, VisualAssetFactory.ToUnity(PM.BottomBracket), Quaternion.identity);
        AddRenderer(parts.Bielas, cranks, blackMat, metalMat);
        parts.PedalIzquierdo = Child("PedalIzquierdo", parts.Bielas, new Vector3(-PM.PedalOffsetX, 0f, PM.CrankLength), Quaternion.identity);
        AddRenderer(parts.PedalIzquierdo, pedal, blackMat, metalMat);
        parts.PedalDerecho = Child("PedalDerecho", parts.Bielas, new Vector3(PM.PedalOffsetX, 0f, -PM.CrankLength), Quaternion.identity);
        AddRenderer(parts.PedalDerecho, pedal, blackMat, metalMat);

        parts.Corredor = Child("Corredor", parts.Inclinacion, Vector3.zero, Quaternion.identity);
        parts.Torso = Child("Torso", parts.Corredor, VisualAssetFactory.ToUnity(PM.TorsoPivot), Quaternion.identity);
        AddRenderer(parts.Torso, upper, jerseyMat, shortsMat, glovesMat, helmetMat, lensMat, blackMat, numberMat);

        // Piernas articuladas: RiderLegIK las mueve en el juego; aquí se dejan ya apoyadas en los pedales.
        parts.Left = BuildLeg("Izquierda", -1f, parts, thigh, shin, foot, shortsMat, padsMat, blackMat, shoesMat);
        parts.Right = BuildLeg("Derecha", 1f, parts, thigh, shin, foot, shortsMat, padsMat, blackMat, shoesMat);
        return parts;
    }

    /// <summary>Muslo → canilla → pie, colocados con la misma IK que usa el juego (pedales nivelados).</summary>
    private static Leg BuildLeg(string side, float sign, Parts parts, Mesh thighMesh, Mesh shinMesh, Mesh footMesh,
        Material shorts, Material pads, Material black, Material shoes)
    {
        SN.Vector3 hip = PM.TorsoPivot + new SN.Vector3(PM.HipOffset.X * sign, PM.HipOffset.Y, PM.HipOffset.Z);
        SN.Vector3 pedal = PM.BottomBracket + new SN.Vector3(PM.PedalOffsetX * sign, 0f, -sign * PM.CrankLength);
        SN.Vector3 target = pedal + SN.Vector3.UnitY * PM.AnkleHeight - SN.Vector3.UnitZ * PM.AnkleBack;
        PM.SolveLeg(hip, target, SN.Vector3.UnitZ + SN.Vector3.UnitX * sign * 0.15f, out SN.Vector3 knee, out SN.Vector3 ankle);

        Leg leg = new Leg();
        leg.Thigh = Child("Muslo" + side, parts.Corredor, VisualAssetFactory.ToUnity(hip),
            VisualAssetFactory.ToUnity(PM.FromAxes(SN.Vector3.Normalize(hip - knee), SN.Vector3.UnitZ)));
        AddRenderer(leg.Thigh, thighMesh, shorts);
        leg.Shin = Child("Canilla" + side, leg.Thigh, Vector3.zero, Quaternion.identity);
        leg.Shin.SetPositionAndRotation(VisualAssetFactory.ToUnity(knee),
            VisualAssetFactory.ToUnity(PM.FromAxes(SN.Vector3.Normalize(knee - ankle), SN.Vector3.UnitZ)));
        AddRenderer(leg.Shin, shinMesh, pads, black);
        leg.Foot = Child("Pie" + side, leg.Shin, Vector3.zero, Quaternion.identity);
        leg.Foot.SetPositionAndRotation(VisualAssetFactory.ToUnity(ankle), Quaternion.identity);
        AddRenderer(leg.Foot, footMesh, shoes);
        return leg;
    }

    private static Transform Child(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        t.localRotation = localRotation;
        return t;
    }

    private static void AddRenderer(Transform target, Mesh mesh, params Material[] materials)
    {
        target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = target.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.On;
    }
}
