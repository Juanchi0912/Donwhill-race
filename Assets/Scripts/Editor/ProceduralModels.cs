using System;
using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Modelos rígidos del juego hechos por código: bici GY RAVEN, corredor, árboles, helechos,
/// rocas y montañas. Cada pieza que se anima por separado (ruedas, dirección, piernas, torso)
/// es una malla aparte, relativa a su pivote. No depende de UnityEngine.
/// Coordenadas de la bici: origen en el punto de contacto con el suelo, x = derecha, y = arriba, z = adelante.
/// </summary>
public static class ProceduralModels
{
    // --- Geometría compartida con el constructor de escena.
    public const float WheelRadius = 0.36f;
    public static readonly Vector3 RearAxle = new Vector3(0f, 0.36f, -0.58f);
    public static readonly Vector3 FrontAxle = new Vector3(0f, 0.36f, 0.62f);
    /// <summary>Eje de dirección inclinado 27° hacia atrás (ángulo de dirección de 63°, típico de DH).</summary>
    public static readonly Quaternion SteeringRotation = MeshBuilder.AxisAngle(Vector3.UnitX, -27f);
    public static readonly Vector3 LegsPivot = new Vector3(0f, 0.34f, 0f);
    public static readonly Vector3 TorsoPivot = new Vector3(0f, 0.98f, -0.20f);

    // --- Submallas (cada una lleva su material).
    public const int FrameLivery = 0, FrameBlack = 1, FrameMetal = 2, FrameOrange = 3, FrameGold = 4, FrameDecal = 5, FrameSubmeshes = 6;
    public const int SteerBlack = 0, SteerGold = 1, SteerOrange = 2, SteerBlue = 3, SteerMetal = 4, SteerSubmeshes = 5;
    public const int WheelTire = 0, WheelRim = 1, WheelMetal = 2, WheelSubmeshes = 3;
    public const int LegShorts = 0, LegPads = 1, LegShoes = 2, LegBlack = 3, LegSubmeshes = 4;
    public const int UpperJersey = 0, UpperShorts = 1, UpperGloves = 2, UpperHelmet = 3, UpperLens = 4, UpperBlack = 5, UpperDecal = 6, UpperSubmeshes = 7;
    public const int TreeBark = 0, TreeLeaves = 1, TreeSubmeshes = 2;

    private static Vector3 SteeringAxis => Vector3.Transform(Vector3.UnitY, SteeringRotation);

    /// <summary>Punto sobre el eje de dirección, a "t" metros del eje de la rueda delantera.</summary>
    private static Vector3 SteerPoint(float t) => FrontAxle + SteeringAxis * t;

    // =====================================================================
    // Bici
    // =====================================================================

    /// <summary>Cuadro de DH con suspensión trasera, transmisión y asiento (pivote: origen de la bici).</summary>
    public static MeshBuilder BuildFrame()
    {
        MeshBuilder m = new MeshBuilder(FrameSubmeshes);
        Vector3 up = Vector3.UnitY;
        Vector3 x = Vector3.UnitX;
        Vector3 bb = new Vector3(0f, 0.34f, -0.05f);
        Vector3 headBottom = SteerPoint(0.52f);
        Vector3 headTop = SteerPoint(0.70f);

        // Tubo de dirección, tubo inferior grueso y tubo superior.
        m.AddTube(FrameLivery, headBottom, headTop, 0.035f, 0.035f, 16);
        Vector3 downA = new Vector3(0f, 0.37f, -0.01f);
        Vector3 downB = headBottom + new Vector3(0f, 0f, -0.02f);
        m.AddBeam(FrameLivery, downA, downB, 0.072f, 0.068f, up);
        Vector3 topA = new Vector3(0f, 0.74f, -0.21f);
        Vector3 topB = headTop + new Vector3(0f, -0.035f, 0f);
        m.AddBeam(FrameLivery, topA, topB, 0.05f, 0.045f, up);
        Vector3 seatTop = new Vector3(0f, 0.79f, -0.245f);
        m.AddTube(FrameLivery, bb, seatTop, 0.022f, 0.022f, 12);

        // Calcomanías "GY RAVEN" a ambos lados del tubo inferior (se leen hacia adelante y hacia atrás).
        Vector3 downDir = Vector3.Normalize(downB - downA);
        Vector3 downUp = Vector3.Normalize(Vector3.Cross(downDir, Vector3.Normalize(Vector3.Cross(up, downDir))));
        Vector3 downMid = Vector3.Lerp(downA, downB, 0.52f);
        m.AddPanel(FrameDecal, downMid + x * 0.0375f, downDir, downUp, 0.38f, 0.052f);
        m.AddPanel(FrameDecal, downMid - x * 0.0375f, -downDir, downUp, 0.38f, 0.052f);

        // Asiento.
        m.AddTube(FrameBlack, seatTop, new Vector3(0f, 0.85f, -0.275f), 0.015f, 0.015f, 10);
        m.AddEllipsoid(FrameBlack, new Vector3(0f, 0.875f, -0.29f), MeshBuilder.AxisAngle(x, 4f), new Vector3(0.07f, 0.025f, 0.135f), 14, 8);

        // Basculante: pivote, vainas, tirantes y balancín.
        Vector3 pivot = new Vector3(0f, 0.44f, -0.11f);
        m.AddTube(FrameBlack, pivot - x * 0.065f, pivot + x * 0.065f, 0.028f, 0.028f, 12);
        Vector3 rocker = new Vector3(0f, 0.69f, -0.31f);
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 dropout = RearAxle + x * 0.065f * side;
            m.AddBeam(FrameLivery, pivot + x * 0.05f * side, dropout, 0.032f, 0.045f, up);
            m.AddBeam(FrameLivery, dropout, rocker + x * 0.05f * side, 0.026f, 0.03f, up);
            m.AddBeam(FrameBlack, rocker + x * 0.05f * side, new Vector3(0.05f * side, 0.73f, -0.13f), 0.02f, 0.05f, up);
            m.AddBox(FrameBlack, dropout + x * 0.004f * side, Quaternion.Identity, new Vector3(0.014f, 0.06f, 0.06f));
        }

        // Amortiguador de resorte: cuerpo negro, resorte naranja y depósito dorado.
        Vector3 shockLow = new Vector3(0f, 0.46f, 0.02f);
        Vector3 shockHigh = new Vector3(0f, 0.72f, -0.13f);
        Vector3 shockAxis = Vector3.Normalize(shockHigh - shockLow);
        m.AddTube(FrameBlack, shockLow, shockHigh, 0.02f, 0.02f, 12);
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = Vector3.Lerp(shockLow, shockHigh, 0.18f + i * 0.085f);
            m.AddTorus(FrameOrange, c, shockAxis, 0.034f, 0.0058f, 16, 6);
        }
        m.AddTube(FrameGold, Vector3.Lerp(shockLow, shockHigh, 0.12f), Vector3.Lerp(shockLow, shockHigh, 0.16f), 0.04f, 0.04f, 14);
        m.AddTube(FrameGold, Vector3.Lerp(shockLow, shockHigh, 0.82f) + x * 0.035f, shockHigh + x * 0.035f - shockAxis * 0.01f, 0.016f, 0.016f, 10);
        m.AddTube(FrameMetal, shockLow - x * 0.02f, shockLow + x * 0.02f, 0.012f, 0.012f, 8);
        m.AddTube(FrameMetal, shockHigh - x * 0.02f, shockHigh + x * 0.02f, 0.012f, 0.012f, 8);

        // Caja de pedalier, bielas, pedales (nivelados: posición de ataque) y transmisión.
        m.AddTube(FrameBlack, bb - x * 0.05f, bb + x * 0.05f, 0.025f, 0.025f, 12);
        Vector3 pedalFront = new Vector3(-0.135f, 0.34f, 0.12f);
        Vector3 pedalBack = new Vector3(0.135f, 0.34f, -0.22f);
        m.AddBeam(FrameBlack, bb - x * 0.075f, pedalFront + x * 0.05f, 0.016f, 0.026f, x);
        m.AddBeam(FrameBlack, bb + x * 0.075f, pedalBack - x * 0.05f, 0.016f, 0.026f, x);
        m.AddBox(FrameBlack, pedalFront, Quaternion.Identity, new Vector3(0.10f, 0.018f, 0.10f));
        m.AddBox(FrameBlack, pedalBack, Quaternion.Identity, new Vector3(0.10f, 0.018f, 0.10f));
        m.AddTube(FrameBlack, bb + x * 0.06f, bb + x * 0.065f, 0.09f, 0.09f, 24);
        m.AddTube(FrameMetal, RearAxle + x * 0.055f, RearAxle + x * 0.075f, 0.05f, 0.05f, 20);
        m.AddBeam(FrameMetal, bb + new Vector3(0.066f, 0.09f, 0f), RearAxle + new Vector3(0.066f, 0.05f, 0f), 0.008f, 0.01f, x);
        m.AddBeam(FrameMetal, bb + new Vector3(0.066f, -0.09f, 0f), RearAxle + new Vector3(0.066f, -0.05f, 0f), 0.008f, 0.01f, x);
        m.AddBox(FrameBlack, new Vector3(0.078f, 0.27f, -0.585f), Quaternion.Identity, new Vector3(0.02f, 0.07f, 0.04f));

        // Pinza del freno trasero (el disco va con la rueda).
        m.AddBox(FrameBlack, RearAxle + new Vector3(-0.062f, 0.07f, 0.06f), MeshBuilder.AxisAngle(x, 40f), new Vector3(0.03f, 0.06f, 0.06f));
        return m;
    }

    /// <summary>Horquilla de doble corona, potencia y manubrio (pivote: eje delantero, inclinado como la dirección).</summary>
    public static MeshBuilder BuildSteering()
    {
        MeshBuilder m = new MeshBuilder(SteerSubmeshes);
        m.SetFrame(FrontAxle, SteeringRotation);
        Vector3 x = Vector3.UnitX;

        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 o = x * 0.078f * side;
            m.AddTube(SteerBlack, SteerPoint(-0.03f) + o, SteerPoint(0.32f) + o, 0.027f, 0.027f, 14);
            m.AddTube(SteerOrange, SteerPoint(0.19f) + o, SteerPoint(0.26f) + o, 0.0285f, 0.0285f, 14, false);
            m.AddTube(SteerGold, SteerPoint(0.30f) + o, SteerPoint(0.76f) + o, 0.019f, 0.019f, 14);
        }
        m.AddBox(SteerBlack, SteerPoint(0.48f), SteeringRotation, new Vector3(0.21f, 0.03f, 0.06f));
        m.AddBox(SteerBlack, SteerPoint(0.73f), SteeringRotation, new Vector3(0.21f, 0.03f, 0.06f));
        m.AddBox(SteerBlack, SteerPoint(0.76f) + new Vector3(0f, 0f, 0.025f), SteeringRotation, new Vector3(0.05f, 0.03f, 0.08f));

        // Manubrio con subida y algo de caída hacia atrás; puños azules.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 center = new Vector3(0.07f * side, 1.04f, 0.30f);
            Vector3 rise = new Vector3(0.16f * side, 1.065f, 0.295f);
            Vector3 end = new Vector3(0.40f * side, 1.075f, 0.285f);
            m.AddTube(SteerBlack, center, rise, 0.011f, 0.011f, 10);
            m.AddTube(SteerBlack, rise, end, 0.011f, 0.011f, 10);
            m.AddTube(SteerBlue, new Vector3(0.31f * side, 1.073f, 0.287f), new Vector3(0.405f * side, 1.075f, 0.285f), 0.017f, 0.017f, 12);
            m.AddBox(SteerBlack, new Vector3(0.24f * side, 1.087f, 0.292f), Quaternion.Identity, new Vector3(0.03f, 0.025f, 0.035f));
            m.AddBeam(SteerBlack, new Vector3(0.26f * side, 1.08f, 0.30f), new Vector3(0.19f * side, 1.072f, 0.37f), 0.012f, 0.008f, Vector3.UnitY);
        }
        m.AddTube(SteerBlack, new Vector3(-0.07f, 1.04f, 0.30f), new Vector3(0.07f, 1.04f, 0.30f), 0.016f, 0.016f, 10);

        // Pinza del freno delantero.
        m.AddBox(SteerBlack, FrontAxle + new Vector3(-0.062f, 0.075f, -0.05f), MeshBuilder.AxisAngle(x, -20f), new Vector3(0.03f, 0.06f, 0.06f));
        return m;
    }

    /// <summary>Rueda con neumático de tacos, aro con calcomanía, rayos, buje y disco (pivote: eje, que es el eje X).</summary>
    public static MeshBuilder BuildWheel()
    {
        MeshBuilder m = new MeshBuilder(WheelSubmeshes);
        Vector3 x = Vector3.UnitX;
        Vector3 Dir(float a) => new Vector3(0f, MathF.Cos(a), MathF.Sin(a));
        Vector3 Tangent(float a) => new Vector3(0f, -MathF.Sin(a), MathF.Cos(a));

        // Neumático y tacos.
        m.AddTorus(WheelTire, Vector3.Zero, x, 0.318f, 0.046f, 48, 10, 0f, MathF.PI * 2f, 8f);
        const int knobs = 44;
        for (int k = 0; k < knobs; k++)
        {
            float a = k / (float)knobs * MathF.PI * 2f;
            Vector3 d = Dir(a), t = Tangent(a);
            if (k % 2 == 0)
            {
                m.AddBoxAxes(WheelTire, d * 0.366f, x * 0.013f, d * 0.008f, t * 0.011f, 0f);
            }
            else
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 n = Vector3.Normalize(d + x * side * 0.9f);
                    Vector3 c = d * 0.348f + x * 0.034f * side;
                    Vector3 across = Vector3.Normalize(Vector3.Cross(t, n));
                    m.AddBoxAxes(WheelTire, c, across * 0.009f, n * 0.008f, t * 0.011f, 0f);
                }
            }
        }

        // Aro: superficie exterior y costados con la calcomanía (el texto se repite 2 veces por vuelta).
        m.AddLathe(WheelRim, -x * 0.017f, x, new[] { new Vector2(0f, 0.286f), new Vector2(0.034f, 0.286f) }, 48);
        m.AddLathe(WheelMetal, -x * 0.017f, x, new[] { new Vector2(0f, 0.255f), new Vector2(0.034f, 0.255f) }, 48, 1f, 0f, 1f, default, null, true);
        m.AddAnnulus(WheelRim, x * 0.017f, x, 0.255f, 0.287f, 48);
        m.AddAnnulus(WheelRim, -x * 0.017f, -x, 0.255f, 0.287f, 48);

        // Rayos cruzados, buje y disco de freno (lado izquierdo).
        const int spokes = 28;
        for (int i = 0; i < spokes; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            float a = i / (float)spokes * MathF.PI * 2f;
            Vector3 hub = x * 0.032f * side + Dir(a + side * 0.35f) * 0.026f;
            Vector3 rim = x * 0.006f * side + Dir(a) * 0.256f;
            m.AddBeam(WheelMetal, hub, rim, 0.0045f, 0.0045f, x, 0f);
        }
        m.AddTube(WheelMetal, -x * 0.05f, x * 0.05f, 0.02f, 0.02f, 12);
        m.AddTube(WheelMetal, -x * 0.034f, -x * 0.030f, 0.033f, 0.033f, 14);
        m.AddTube(WheelMetal, x * 0.030f, x * 0.034f, 0.033f, 0.033f, 14);
        m.AddAnnulus(WheelMetal, -x * 0.046f, -x, 0.064f, 0.102f, 32);
        m.AddAnnulus(WheelMetal, -x * 0.043f, x, 0.064f, 0.102f, 32);
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * MathF.PI * 2f;
            m.AddBeam(WheelMetal, -x * 0.044f + Dir(a) * 0.02f, -x * 0.044f + Dir(a) * 0.066f, 0.004f, 0.012f, x, 0f);
        }
        return m;
    }

    // =====================================================================
    // Corredor
    // =====================================================================

    /// <summary>Piernas en posición de ataque (pivote a la altura de los pedales; se encogen al agacharse).</summary>
    public static MeshBuilder BuildRiderLegs()
    {
        MeshBuilder m = new MeshBuilder(LegSubmeshes);
        m.SetFrame(LegsPivot, Quaternion.Identity);
        BuildLeg(m, new Vector3(-0.10f, 0.98f, -0.20f), new Vector3(-0.15f, 0.69f, 0.09f), new Vector3(-0.125f, 0.42f, 0.09f));
        BuildLeg(m, new Vector3(0.10f, 0.98f, -0.20f), new Vector3(0.17f, 0.67f, -0.04f), new Vector3(0.125f, 0.42f, -0.25f));
        return m;
    }

    private static void BuildLeg(MeshBuilder m, Vector3 hip, Vector3 knee, Vector3 ankle)
    {
        m.AddCapsule(LegShorts, hip, knee, 0.078f, 0.06f, 14);

        // Rodillera en el frente de la rodilla, continuando como canillera.
        Vector3 shin = Vector3.Normalize(ankle - knee);
        Vector3 kneeFront = Vector3.Normalize(knee - (hip + ankle) * 0.5f);
        m.AddEllipsoid(LegPads, knee + kneeFront * 0.03f, FromAxes(shin, kneeFront), new Vector3(0.062f, 0.08f, 0.052f), 12, 8);
        m.AddCapsule(LegPads, knee, Vector3.Lerp(knee, ankle, 0.72f), 0.053f, 0.045f, 12);
        m.AddCapsule(LegBlack, Vector3.Lerp(knee, ankle, 0.65f), ankle, 0.043f, 0.038f, 10);

        // Zapatilla plana de DH sobre el pedal.
        m.AddBox(LegShoes, ankle + new Vector3(0f, -0.03f, 0.035f), Quaternion.Identity, new Vector3(0.10f, 0.075f, 0.26f));
        m.AddEllipsoid(LegShoes, ankle + new Vector3(0f, -0.028f, 0.15f), Quaternion.Identity, new Vector3(0.05f, 0.036f, 0.05f), 10, 6);
    }

    /// <summary>Torso, brazos y cabeza con casco integral (pivote en la cadera; se inclina al agacharse).</summary>
    public static MeshBuilder BuildRiderUpper()
    {
        MeshBuilder m = new MeshBuilder(UpperSubmeshes);
        m.SetFrame(TorsoPivot, Quaternion.Identity);
        Vector3 x = Vector3.UnitX;

        m.AddEllipsoid(UpperShorts, new Vector3(0f, 0.99f, -0.20f), Quaternion.Identity, new Vector3(0.165f, 0.10f, 0.125f), 14, 8);

        // Torso inclinado hacia adelante.
        Vector3 waist = new Vector3(0f, 1.0f, -0.17f);
        Vector3 chest = new Vector3(0f, 1.36f, 0.06f);
        Vector3 axis = Vector3.Normalize(chest - waist);
        Vector3 forward = Vector3.Cross(x, axis);
        Quaternion torsoRotation = FromAxes(axis, forward);
        Vector3 torsoCenter = (waist + chest) * 0.5f;
        float half = Vector3.Distance(waist, chest) * 0.5f;
        m.AddEllipsoid(UpperJersey, torsoCenter, torsoRotation, new Vector3(0.17f, half + 0.06f, 0.115f), 16, 12);
        m.AddEllipsoid(UpperJersey, new Vector3(0f, 1.355f, 0.045f), torsoRotation, new Vector3(0.215f, 0.075f, 0.10f), 16, 8);
        m.AddCapsule(UpperBlack, new Vector3(0f, 1.40f, 0.07f), new Vector3(0f, 1.47f, 0.12f), 0.05f, 0.05f, 10);

        // Número en la espalda: es lo que el jugador ve desde la cámara.
        Vector3 back = -forward;
        m.AddPanel(UpperDecal, torsoCenter + back * 0.117f + axis * 0.03f, x, axis, 0.14f, 0.14f);

        // Brazos con los codos abiertos, como en posición de ataque.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 shoulder = new Vector3(0.20f * side, 1.35f, 0.03f);
            Vector3 elbow = new Vector3(0.34f * side, 1.19f, 0.19f);
            Vector3 wrist = new Vector3(0.355f * side, 1.08f, 0.27f);
            m.AddCapsule(UpperJersey, shoulder, elbow, 0.055f, 0.045f, 12);
            m.AddCapsule(UpperJersey, elbow, wrist, 0.045f, 0.036f, 12);
            m.AddEllipsoid(UpperGloves, new Vector3(0.355f * side, 1.075f, 0.287f), Quaternion.Identity, new Vector3(0.055f, 0.042f, 0.05f), 10, 6);
        }

        // Casco integral con mentonera, visera y antiparras naranjas.
        Vector3 head = new Vector3(0f, 1.565f, 0.14f);
        Quaternion headRotation = MeshBuilder.AxisAngle(x, -8f);
        Vector3 headUp = Vector3.Transform(Vector3.UnitY, headRotation);
        Vector3 headForward = Vector3.Transform(Vector3.UnitZ, headRotation);
        m.AddEllipsoid(UpperHelmet, head, headRotation, new Vector3(0.135f, 0.145f, 0.165f), 20, 12);
        float range = 70f * MathF.PI / 180f;
        m.AddTorus(UpperHelmet, head - headUp * 0.075f + headForward * 0.035f, headUp, 0.115f, 0.032f, 14, 8, -range, range, 1f, headForward);
        m.AddBox(UpperHelmet, head + headUp * 0.105f + headForward * 0.15f, headRotation * MeshBuilder.AxisAngle(x, -12f), new Vector3(0.20f, 0.012f, 0.11f));
        // Antiparras: marco negro y lente naranja en la abertura del casco, más la correa.
        Vector3 goggles = head + headForward * 0.148f + headUp * 0.005f;
        m.AddEllipsoid(UpperBlack, goggles - headForward * 0.008f, headRotation, new Vector3(0.118f, 0.05f, 0.04f), 16, 8);
        m.AddEllipsoid(UpperLens, goggles + headForward * 0.008f, headRotation, new Vector3(0.1f, 0.038f, 0.034f), 16, 8);
        m.AddTorus(UpperBlack, head + headUp * 0.005f, headUp, 0.142f, 0.009f, 24, 4);
        return m;
    }

    // =====================================================================
    // Entorno
    // =====================================================================

    /// <summary>Pino alto con capas de ramas caídas (tronco + agujas).</summary>
    public static MeshBuilder BuildPine(int seed)
    {
        Random rng = new Random(seed);
        MeshBuilder m = new MeshBuilder(TreeSubmeshes);
        float height = Range(rng, 8f, 11.5f);
        float spread = Range(rng, 0.85f, 1.15f);

        m.AddLathe(TreeBark, Vector3.Zero, Vector3.UnitY,
            new[] { new Vector2(-0.3f, 0.26f), new Vector2(0.4f, 0.21f), new Vector2(height * 0.9f, 0.05f), new Vector2(height, 0f) }, 8, 2f, 1.5f);

        int tiers = rng.Next(6, 9);
        for (int t = 0; t < tiers; t++)
        {
            float f = t / (float)(tiers - 1);
            float y = Lerp(height * 0.25f, height * 0.86f, f) + Range(rng, -0.15f, 0.15f);
            float radius = Lerp(2.5f, 0.7f, f) * spread * Range(rng, 0.9f, 1.1f);
            float h = Lerp(1.9f, 1.1f, f);
            const int segments = 10;
            float[] jitter = new float[segments];
            for (int i = 0; i < segments; i++) jitter[i] = i % 2 == 0 ? Range(rng, 0.95f, 1.1f) : Range(rng, 0.7f, 0.85f);
            // Perfil de arriba hacia abajo: punta, ramas y borde caído.
            m.AddLathe(TreeLeaves, new Vector3(0f, y, 0f), Vector3.UnitY,
                new[] { new Vector2(h, 0f), new Vector2(h * 0.45f, radius * 0.62f), new Vector2(0f, radius), new Vector2(-0.3f, radius * 0.9f) },
                segments, 2f, 0f, 1f, default, jitter);
            m.AddDisc(TreeLeaves, new Vector3(0f, y - 0.25f, 0f), -Vector3.UnitY, radius * 0.85f, segments);
        }
        return m;
    }

    /// <summary>Árbol de hoja ancha: tronco y copa de varias masas irregulares.</summary>
    public static MeshBuilder BuildBroadleaf(int seed)
    {
        Random rng = new Random(seed);
        MeshBuilder m = new MeshBuilder(TreeSubmeshes);
        float trunk = Range(rng, 3.2f, 4.5f);
        m.AddLathe(TreeBark, Vector3.Zero, Vector3.UnitY,
            new[] { new Vector2(-0.3f, 0.24f), new Vector2(0.5f, 0.18f), new Vector2(trunk + 0.8f, 0.08f) }, 8, 2f, 1.5f);
        Vector3 branch = new Vector3(Range(rng, -0.6f, 0.6f), trunk + 0.5f, Range(rng, -0.6f, 0.6f));
        m.AddTube(TreeBark, new Vector3(0f, trunk * 0.7f, 0f), branch, 0.1f, 0.05f, 6, false);

        int blobs = rng.Next(5, 8);
        for (int i = 0; i < blobs; i++)
        {
            float a = i / (float)blobs * MathF.PI * 2f + Range(rng, 0f, 0.6f);
            float r = i == 0 ? 0f : Range(rng, 0.9f, 1.6f);
            Vector3 c = new Vector3(MathF.Cos(a) * r, trunk + 1.2f + Range(rng, 0f, 1.6f) + (i == 0 ? 0.8f : 0f), MathF.Sin(a) * r);
            float s = Range(rng, 1.3f, 2.0f);
            m.AddLumpySphere(TreeLeaves, c, new Vector3(s, s * 0.8f, s), 1, 0.25f, seed * 13 + i);
        }
        return m;
    }

    /// <summary>Helecho: hojas largas que salen del centro y se arquean hacia afuera (dos caras).</summary>
    public static MeshBuilder BuildFern(int seed)
    {
        Random rng = new Random(seed);
        MeshBuilder m = new MeshBuilder(1);
        int fronds = rng.Next(7, 11);
        const int steps = 6;
        for (int f = 0; f < fronds; f++)
        {
            float angle = f / (float)fronds * MathF.PI * 2f + Range(rng, -0.2f, 0.2f);
            float length = Range(rng, 0.6f, 1.0f);
            float lift = Range(rng, 0.25f, 0.45f);
            Vector3 dir = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            Vector3 side = Vector3.Cross(Vector3.UnitY, dir);
            int[] top = new int[(steps + 1) * 2];
            int[] bottom = new int[(steps + 1) * 2];
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector3 c = dir * t * length + Vector3.UnitY * MathF.Sin(t * MathF.PI * 0.85f) * lift * length;
                float w = 0.13f * length * MathF.Sin(MathF.PI * MathF.Min(1f, t * 1.15f + 0.05f));
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = c + side * (k == 0 ? -w : w);
                    Vector2 uv = new Vector2(k, t);
                    top[s * 2 + k] = m.AddVertex(p, uv);
                    bottom[s * 2 + k] = m.AddVertex(p, uv);
                }
            }
            for (int s = 0; s < steps; s++)
            {
                float t = (s + 0.5f) / steps;
                Vector3 normal = Vector3.Normalize(Vector3.UnitY - dir * MathF.Cos(t * MathF.PI * 0.85f) * lift);
                m.AddQuad(0, top[s * 2], top[s * 2 + 1], top[s * 2 + 3], top[s * 2 + 2], normal);
                m.AddQuad(0, bottom[s * 2], bottom[s * 2 + 1], bottom[s * 2 + 3], bottom[s * 2 + 2], -normal);
            }
        }
        return m;
    }

    /// <summary>Roca facetada de tamaño ~1 m, con la base aplanada para apoyarse en el suelo.</summary>
    public static MeshBuilder BuildRock(int seed)
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddLumpySphere(0, Vector3.Zero, new Vector3(1f, 0.7f, 0.9f), 2, 0.22f, seed, 0.55f);
        return m;
    }

    /// <summary>Montaña lejana de radio y altura 1 (se escala en la escena); facetada para que la niebla dibuje su silueta.</summary>
    public static MeshBuilder BuildMountain(int seed)
    {
        MeshBuilder m = new MeshBuilder(1);
        const int angles = 20, rings = 7;
        Vector3[,] p = new Vector3[rings + 1, angles];
        for (int r = 0; r <= rings; r++)
        {
            float rr = r / (float)rings;
            for (int a = 0; a < angles; a++)
            {
                float ang = a / (float)angles * MathF.PI * 2f;
                Vector3 dir = new Vector3(MathF.Cos(ang), 0f, MathF.Sin(ang));
                float noise = MeshBuilder.ValueNoise3(dir * 2f + new Vector3(rr * 3f, 0f, 0f), seed);
                float h = MathF.Pow(1f - rr, 1.5f) * (0.75f + noise * 0.5f);
                float radius = rr * (0.85f + MeshBuilder.ValueNoise3(dir * 3f, seed + 5) * 0.3f);
                p[r, a] = dir * radius + Vector3.UnitY * (r == rings ? -0.02f : h);
            }
        }
        for (int r = 0; r < rings; r++)
        {
            for (int a = 0; a < angles; a++)
            {
                int b = (a + 1) % angles;
                Vector3 p00 = p[r, a], p01 = p[r, b], p10 = p[r + 1, a], p11 = p[r + 1, b];
                AddFlatTriangle(m, p00, p10, p11);
                AddFlatTriangle(m, p00, p11, p01);
            }
        }
        return m;
    }

    private static void AddFlatTriangle(MeshBuilder m, Vector3 a, Vector3 b, Vector3 c)
    {
        int ia = m.AddVertex(a, new Vector2(a.X, a.Z));
        int ib = m.AddVertex(b, new Vector2(b.X, b.Z));
        int ic = m.AddVertex(c, new Vector2(c.X, c.Z));
        Vector3 center = (a + b + c) / 3f;
        m.AddTriangle(0, ia, ib, ic, new Vector3(center.X, 0.5f, center.Z));
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    /// <summary>Rotación que lleva el eje Y local a "up" y el Z local (aprox.) a "forward".</summary>
    public static Quaternion FromAxes(Vector3 up, Vector3 forward)
    {
        up = Vector3.Normalize(up);
        Vector3 right = Vector3.Normalize(Vector3.Cross(up, forward));
        forward = Vector3.Cross(right, up);
        Matrix4x4 m = new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f, 0f, 0f, 1f);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(m));
    }

    private static float Range(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
