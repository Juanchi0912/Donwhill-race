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
    /// <summary>Cuánto más alto va el cuerpo al pedalear de pie que sentado.</summary>
    public const float StandingLift = 0.07f;
    public static readonly Vector3 TorsoPivot = new Vector3(0f, 0.98f + StandingLift, -0.20f);

    // --- Pedaleo: eje del pedalier, bielas y piernas articuladas (muslo → canilla → pie).
    public static readonly Vector3 BottomBracket = new Vector3(0f, 0.34f, -0.05f);
    public const float CrankLength = 0.17f;
    /// <summary>Distancia lateral del centro de cada pedal al plano de la bici.</summary>
    public const float PedalOffsetX = 0.135f;
    public const float ThighLength = 0.41f;
    public const float ShinLength = 0.41f;
    /// <summary>Altura del tobillo sobre el eje del pedal (la suela apoya en la plataforma).</summary>
    public const float AnkleHeight = 0.092f;
    /// <summary>El tobillo queda algo detrás del eje del pedal: se pedalea con la parte delantera del pie.</summary>
    public const float AnkleBack = 0.045f;
    /// <summary>Hombro derecho respecto del pivote del torso (el izquierdo es el mismo con x negativa).</summary>
    public static readonly Vector3 ShoulderOffset = new Vector3(0.20f, 0.37f, 0.23f);
    public const float UpperArmLength = 0.29f;
    public const float ForearmLength = 0.27f;
    /// <summary>Centro del puño derecho del manubrio, en coordenadas de la bici (el izquierdo con x negativa).</summary>
    public static readonly Vector3 GripRight = new Vector3(0.355f, 1.075f, 0.287f);

    /// <summary>Cadera derecha respecto del pivote del torso (la izquierda es la misma con x negativa).</summary>
    public static readonly Vector3 HipOffset = new Vector3(0.10f, -0.01f, 0f);

    // --- Submallas (cada una lleva su material).
    public const int FrameLivery = 0, FrameBlack = 1, FrameMetal = 2, FrameOrange = 3, FrameGold = 4, FrameDecal = 5, FrameSubmeshes = 6;
    public const int SteerBlack = 0, SteerGold = 1, SteerOrange = 2, SteerBlue = 3, SteerMetal = 4, SteerSubmeshes = 5;
    public const int WheelTire = 0, WheelRim = 1, WheelMetal = 2, WheelSubmeshes = 3;
    public const int CrankBlack = 0, CrankMetal = 1, CrankSubmeshes = 2;
    public const int ShinPads = 0, ShinBlack = 1, ShinSubmeshes = 2;
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

        // Caja de pedalier y transmisión (las bielas y los pedales son piezas aparte: giran al pedalear).
        m.AddTube(FrameBlack, bb - x * 0.05f, bb + x * 0.05f, 0.025f, 0.025f, 12);
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

    /// <summary>
    /// Bielas con el plato (pivote: eje del pedalier; gira sobre X). En ángulo 0 la biela izquierda
    /// apunta hacia adelante y la derecha hacia atrás: los pedales nivelados de la posición de ataque.
    /// </summary>
    public static MeshBuilder BuildCranks()
    {
        MeshBuilder m = new MeshBuilder(CrankSubmeshes);
        m.SetFrame(BottomBracket, Quaternion.Identity);
        Vector3 x = Vector3.UnitX;
        Vector3 bb = BottomBracket;
        Vector3 forward = Vector3.UnitZ;

        m.AddTube(CrankBlack, bb - x * 0.08f, bb + x * 0.08f, 0.012f, 0.012f, 10);
        m.AddTube(CrankBlack, bb + x * 0.06f, bb + x * 0.065f, 0.09f, 0.09f, 24);
        m.AddTube(CrankMetal, bb + x * 0.058f, bb + x * 0.067f, 0.035f, 0.035f, 12);
        m.AddBeam(CrankBlack, bb - x * 0.075f, bb - x * 0.085f + forward * CrankLength, 0.016f, 0.026f, x);
        m.AddBeam(CrankBlack, bb + x * 0.075f, bb + x * 0.085f - forward * CrankLength, 0.016f, 0.026f, x);
        return m;
    }

    /// <summary>Pedal de plataforma (pivote: su eje). Se mantiene nivelado mientras la biela gira.</summary>
    public static MeshBuilder BuildPedal()
    {
        MeshBuilder m = new MeshBuilder(CrankSubmeshes);
        m.AddBox(CrankBlack, Vector3.Zero, Quaternion.Identity, new Vector3(0.10f, 0.018f, 0.10f));
        // Pines de agarre en la cara de arriba.
        for (int i = 0; i < 4; i++)
        {
            float px = i % 2 == 0 ? -0.035f : 0.035f;
            float pz = i < 2 ? -0.035f : 0.035f;
            m.AddBox(CrankMetal, new Vector3(px, 0.012f, pz), Quaternion.Identity, new Vector3(0.008f, 0.008f, 0.008f));
        }
        return m;
    }

    /// <summary>Brazo (pivote: hombro). Va a lo largo de -Y local.</summary>
    public static MeshBuilder BuildRiderUpperArm()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddCapsule(0, Vector3.Zero, new Vector3(0f, -UpperArmLength, 0f), 0.055f, 0.045f, 12);
        return m;
    }

    /// <summary>Antebrazo (pivote: codo). Va a lo largo de -Y local.</summary>
    public static MeshBuilder BuildRiderForearm()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddCapsule(0, Vector3.Zero, new Vector3(0f, -ForearmLength, 0f), 0.045f, 0.036f, 12);
        return m;
    }

    /// <summary>Guante cerrado sobre el puño (pivote: el puño del manubrio).</summary>
    public static MeshBuilder BuildRiderHand()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddEllipsoid(0, Vector3.Zero, Quaternion.Identity, new Vector3(0.055f, 0.042f, 0.05f), 10, 6);
        return m;
    }

    /// <summary>Muslo (pivote: cadera). Va a lo largo de -Y local; +Z local es el frente de la pierna.</summary>
    public static MeshBuilder BuildRiderThigh()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddCapsule(0, Vector3.Zero, new Vector3(0f, -ThighLength, 0f), 0.08f, 0.062f, 14);
        return m;
    }

    /// <summary>Canilla con rodillera (pivote: rodilla). Va a lo largo de -Y local; la rodillera mira hacia +Z.</summary>
    public static MeshBuilder BuildRiderShin()
    {
        MeshBuilder m = new MeshBuilder(ShinSubmeshes);
        m.AddEllipsoid(ShinPads, new Vector3(0f, -0.02f, 0.04f), Quaternion.Identity, new Vector3(0.062f, 0.08f, 0.052f), 12, 8);
        m.AddCapsule(ShinPads, new Vector3(0f, 0f, 0.005f), new Vector3(0f, -ShinLength * 0.7f, 0.005f), 0.054f, 0.046f, 12);
        m.AddCapsule(ShinBlack, new Vector3(0f, -ShinLength * 0.62f, 0f), new Vector3(0f, -ShinLength, 0f), 0.044f, 0.039f, 10);
        return m;
    }

    /// <summary>Zapatilla plana de DH (pivote: tobillo). Se orienta igual que el pedal.</summary>
    public static MeshBuilder BuildRiderFoot()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddBox(0, new Vector3(0f, -0.045f, AnkleBack), Quaternion.Identity, new Vector3(0.10f, 0.075f, 0.26f));
        m.AddEllipsoid(0, new Vector3(0f, -0.043f, AnkleBack + 0.12f), Quaternion.Identity, new Vector3(0.05f, 0.036f, 0.05f), 10, 6);
        return m;
    }

    /// <summary>
    /// Resuelve una pierna de dos segmentos (IK analítica): dada la cadera y el tobillo deseado,
    /// devuelve dónde queda la rodilla. La rodilla se dobla hacia "bendDirection" (adelante y algo afuera).
    /// Si el pedal queda fuera de alcance, el tobillo se acerca lo necesario.
    /// </summary>
    public static void SolveLeg(Vector3 hip, Vector3 ankleTarget, Vector3 bendDirection, out Vector3 knee, out Vector3 ankle)
    {
        SolveTwoBone(hip, ankleTarget, ThighLength, ShinLength, bendDirection, out knee, out ankle);
    }

    /// <summary>IK de dos segmentos genérica (piernas y brazos); igual a la de TwoBoneIK en el juego.</summary>
    public static void SolveTwoBone(Vector3 root, Vector3 target, float upper, float lower, Vector3 bendDirection, out Vector3 joint, out Vector3 end)
    {
        Vector3 toTarget = target - root;
        float distance = toTarget.Length();
        Vector3 dir = distance > 1e-5f ? toTarget / distance : -Vector3.UnitY;
        distance = Math.Clamp(distance, MathF.Abs(upper - lower) + 0.01f, (upper + lower) * 0.999f);

        Vector3 bend = bendDirection - dir * Vector3.Dot(bendDirection, dir);
        bend = bend.LengthSquared() > 1e-8f ? Vector3.Normalize(bend) : MeshBuilder.Perpendicular(dir);

        // Ley de cosenos: ángulo en la raíz entre la línea raíz-objetivo y el primer segmento.
        float cos = (upper * upper + distance * distance - lower * lower) / (2f * upper * distance);
        cos = Math.Clamp(cos, -1f, 1f);
        float sin = MathF.Sqrt(1f - cos * cos);
        joint = root + dir * (upper * cos) + bend * (upper * sin);
        end = root + dir * distance;
    }

    /// <summary>Torso, brazos y cabeza con casco integral (pivote en la cadera; se inclina al agacharse).</summary>
    public static MeshBuilder BuildRiderUpper()
    {
        MeshBuilder m = new MeshBuilder(UpperSubmeshes);
        m.SetFrame(TorsoPivot, Quaternion.Identity);
        Vector3 x = Vector3.UnitX;

        // De pie sobre los pedales: el cuerpo va más alto que el asiento; las manos siguen en los puños.
        Vector3 lift = new Vector3(0f, StandingLift, 0f);
        m.AddEllipsoid(UpperShorts, new Vector3(0f, 0.99f, -0.20f) + lift, Quaternion.Identity, new Vector3(0.165f, 0.10f, 0.125f), 14, 8);

        // Torso inclinado hacia adelante.
        Vector3 waist = new Vector3(0f, 1.0f, -0.17f) + lift;
        Vector3 chest = new Vector3(0f, 1.36f, 0.06f) + lift;
        Vector3 axis = Vector3.Normalize(chest - waist);
        Vector3 forward = Vector3.Cross(x, axis);
        Quaternion torsoRotation = FromAxes(axis, forward);
        Vector3 torsoCenter = (waist + chest) * 0.5f;
        float half = Vector3.Distance(waist, chest) * 0.5f;
        m.AddEllipsoid(UpperJersey, torsoCenter, torsoRotation, new Vector3(0.17f, half + 0.06f, 0.115f), 16, 12);
        m.AddEllipsoid(UpperJersey, new Vector3(0f, 1.355f, 0.045f) + lift, torsoRotation, new Vector3(0.215f, 0.075f, 0.10f), 16, 8);
        m.AddCapsule(UpperBlack, new Vector3(0f, 1.40f, 0.07f) + lift, new Vector3(0f, 1.47f, 0.12f) + lift, 0.05f, 0.05f, 10);

        // Número en la espalda: es lo que el jugador ve desde la cámara.
        Vector3 back = -forward;
        m.AddPanel(UpperDecal, torsoCenter + back * 0.117f + axis * 0.03f, x, axis, 0.14f, 0.14f);

        // Los brazos son piezas aparte (BuildRiderUpperArm/Forearm/Hand): los mueve RiderArmIK.
        // Aquí solo van los hombros redondeados, para que la unión con el torso no se note.
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 shoulder = TorsoPivot + new Vector3(ShoulderOffset.X * side, ShoulderOffset.Y, ShoulderOffset.Z);
            m.AddEllipsoid(UpperJersey, shoulder, Quaternion.Identity, new Vector3(0.062f, 0.062f, 0.062f), 12, 8);
        }

        // Casco integral con mentonera, visera y antiparras naranjas.
        Vector3 head = new Vector3(0f, 1.565f, 0.14f) + lift;
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

    /// <summary>Matorral seco: varias masas bajas e irregulares y algunas ramitas (corteza + follaje).</summary>
    public static MeshBuilder BuildBush(int seed)
    {
        Random rng = new Random(seed);
        MeshBuilder m = new MeshBuilder(TreeSubmeshes);
        int blobs = rng.Next(3, 6);
        float spread = Range(rng, 0.5f, 0.9f);
        for (int i = 0; i < blobs; i++)
        {
            float a = i / (float)blobs * MathF.PI * 2f + Range(rng, 0f, 0.8f);
            float r = i == 0 ? 0f : Range(rng, 0.3f, spread);
            float s = Range(rng, 0.45f, 0.85f);
            Vector3 c = new Vector3(MathF.Cos(a) * r, s * 0.55f + Range(rng, 0f, 0.25f), MathF.Sin(a) * r);
            m.AddLumpySphere(TreeLeaves, c, new Vector3(s, s * 0.75f, s), 1, 0.3f, seed * 7 + i, 0.6f);
        }
        // Ramitas secas que asoman: dan el aspecto de arbusto de ladera.
        int twigs = rng.Next(3, 6);
        for (int i = 0; i < twigs; i++)
        {
            float a = Range(rng, 0f, MathF.PI * 2f);
            Vector3 tip = new Vector3(MathF.Cos(a) * Range(rng, 0.6f, 1.1f), Range(rng, 0.7f, 1.3f), MathF.Sin(a) * Range(rng, 0.6f, 1.1f));
            m.AddTube(TreeBark, new Vector3(0f, 0.1f, 0f), tip, 0.03f, 0.008f, 5, false);
        }
        return m;
    }

    /// <summary>
    /// Piedra que sirve de rampa natural: una cuña irregular de largo 1 (en +Z), ancho 1 y alto 1 en la parte
    /// de atrás; el borde delantero queda a ras del suelo. Se escala en la escena y lleva collider convexo.
    /// </summary>
    public static MeshBuilder BuildRockKicker(int seed)
    {
        MeshBuilder m = new MeshBuilder(1);
        const int nx = 4, nz = 5;
        // Puntos de la cara de arriba: sube de 0 (adelante) a 1 (atrás) con algo de ruido de roca.
        Vector3[,] top = new Vector3[nx + 1, nz + 1];
        for (int i = 0; i <= nx; i++)
        for (int j = 0; j <= nz; j++)
        {
            float x = i / (float)nx - 0.5f, z = j / (float)nz;
            float noise = (MeshBuilder.ValueNoise3(new Vector3(x * 4f, z * 4f, 0.5f), seed) - 0.5f) * 0.12f;
            float shoulder = 1f - MathF.Pow(MathF.Abs(x) * 2f, 3f) * 0.25f; // Bordes laterales algo más bajos.
            float h = j == 0 ? -0.02f : MathF.Max(0f, z * shoulder + noise * z);
            top[i, j] = new Vector3(x * (1f - 0.15f * z), h, z);
        }
        for (int i = 0; i < nx; i++)
        for (int j = 0; j < nz; j++)
        {
            FacetTriangle(m, top[i, j], top[i + 1, j], top[i + 1, j + 1], Vector3.UnitY);
            FacetTriangle(m, top[i, j], top[i + 1, j + 1], top[i, j + 1], Vector3.UnitY);
        }
        // Cara trasera, costados y base (cierran la piedra; la base queda bajo el suelo).
        Vector3 floorOffset = new Vector3(0f, -0.15f, 0f);
        for (int i = 0; i < nx; i++)
        {
            Vector3 a = top[i, nz], b = top[i + 1, nz];
            Vector3 a0 = new Vector3(a.X, floorOffset.Y, a.Z), b0 = new Vector3(b.X, floorOffset.Y, b.Z);
            FacetTriangle(m, a, b, b0, Vector3.UnitZ);
            FacetTriangle(m, a, b0, a0, Vector3.UnitZ);
        }
        for (int side = 0; side <= nx; side += nx)
        {
            for (int j = 0; j < nz; j++)
            {
                Vector3 a = top[side, j], b = top[side, j + 1];
                Vector3 a0 = new Vector3(a.X, floorOffset.Y, a.Z), b0 = new Vector3(b.X, floorOffset.Y, b.Z);
                Vector3 outward = side == 0 ? -Vector3.UnitX : Vector3.UnitX;
                FacetTriangle(m, a, b, b0, outward);
                FacetTriangle(m, a, b0, a0, outward);
            }
        }
        Vector3 fl = new Vector3(top[0, 0].X, floorOffset.Y, 0f), fr = new Vector3(top[nx, 0].X, floorOffset.Y, 0f);
        Vector3 bl = new Vector3(top[0, nz].X, floorOffset.Y, 1f), br = new Vector3(top[nx, nz].X, floorOffset.Y, 1f);
        FacetTriangle(m, fl, fr, br, -Vector3.UnitY);
        FacetTriangle(m, fl, br, bl, -Vector3.UnitY);
        return m;
    }

    /// <summary>Superficie de agua plana de 1 × 1 m mirando hacia arriba (se escala en la escena).</summary>
    public static MeshBuilder BuildWaterPlane()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddPanel(0, Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, 1f, 1f);
        return m;
    }

    /// <summary>Triángulo con vértices propios (facetado) cuya cara mira hacia "outward".</summary>
    private static void FacetTriangle(MeshBuilder m, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        int ia = m.AddVertex(a, new Vector2(a.X + a.Z, a.Y));
        int ib = m.AddVertex(b, new Vector2(b.X + b.Z, b.Y));
        int ic = m.AddVertex(c, new Vector2(c.X + c.Z, c.Y));
        m.AddTriangle(0, ia, ib, ic, outward);
    }

    public const int KickerSurface = 0, KickerDirt = 1, KickerSubmeshes = 2;
    public const int HouseWall = 0, HouseRoof = 1, HouseDark = 2, HouseSubmeshes = 3;

    /// <summary>
    /// Rampa de salto natural: una curva de transición (tablones o concreto) apoyada en un montículo
    /// de tierra con los costados en talud y la parte de atrás bajando al suelo. Medidas unitarias:
    /// largo 1 hasta el borde de salida (z), alto 1 en el borde, ancho 1 arriba; se escala en la escena.
    /// </summary>
    public static MeshBuilder BuildKicker()
    {
        MeshBuilder m = new MeshBuilder(KickerSubmeshes);
        const int steps = 10;
        const float topHalf = 0.5f, footHalf = 0.8f, back = 0.45f;
        // Perfil de la transición: empieza plano y se va curvando hacia arriba.
        Vector3[] left = new Vector3[steps + 1], right = new Vector3[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float z = i / (float)steps;
            float y = MathF.Pow(z, 1.7f);
            left[i] = new Vector3(-topHalf, y, z);
            right[i] = new Vector3(topHalf, y, z);
        }
        for (int i = 0; i < steps; i++)
        {
            // Superficie de la rampa (UV: los tablones van de lado a lado).
            int a = m.AddVertex(left[i], new Vector2(0f, left[i].Z));
            int b = m.AddVertex(right[i], new Vector2(1f, right[i].Z));
            int c = m.AddVertex(right[i + 1], new Vector2(1f, right[i + 1].Z));
            int d = m.AddVertex(left[i + 1], new Vector2(0f, left[i + 1].Z));
            m.AddQuad(KickerSurface, a, b, c, d, Vector3.UnitY - Vector3.UnitZ * 0.5f);

            // Costados de tierra en talud, del borde de la rampa al suelo.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 top0 = side < 0 ? left[i] : right[i], top1 = side < 0 ? left[i + 1] : right[i + 1];
                Vector3 foot0 = new Vector3(footHalf * side, 0f, top0.Z), foot1 = new Vector3(footHalf * side, 0f, top1.Z);
                int t0 = m.AddVertex(top0, new Vector2(top0.Z, top0.Y)), t1 = m.AddVertex(top1, new Vector2(top1.Z, top1.Y));
                int f0 = m.AddVertex(foot0, new Vector2(foot0.Z, 0f)), f1 = m.AddVertex(foot1, new Vector2(foot1.Z, 0f));
                m.AddQuad(KickerDirt, t0, t1, f1, f0, new Vector3(side, 0.6f, 0f));
            }
        }
        // Atrás: la tierra baja del borde de salida al suelo (no queda una pared vertical).
        Vector3 lipL = left[steps], lipR = right[steps];
        Vector3 backL = new Vector3(-footHalf, 0f, 1f + back), backR = new Vector3(footHalf, 0f, 1f + back);
        Vector3 cornerL = new Vector3(-footHalf, 0f, 1f), cornerR = new Vector3(footHalf, 0f, 1f);
        int lL = m.AddVertex(lipL, new Vector2(0f, 1f)), lR = m.AddVertex(lipR, new Vector2(1f, 1f));
        int bR = m.AddVertex(backR, new Vector2(1f, 0f)), bL = m.AddVertex(backL, new Vector2(0f, 0f));
        m.AddQuad(KickerDirt, lL, lR, bR, bL, Vector3.UnitZ + Vector3.UnitY);
        int cL = m.AddVertex(cornerL, new Vector2(0f, 0f)), lL2 = m.AddVertex(lipL, new Vector2(0f, 1f)), bL2 = m.AddVertex(backL, new Vector2(1f, 0f));
        m.AddTriangle(KickerDirt, cL, lL2, bL2, -Vector3.UnitX);
        int cR = m.AddVertex(cornerR, new Vector2(0f, 0f)), lR2 = m.AddVertex(lipR, new Vector2(0f, 1f)), bR2 = m.AddVertex(backR, new Vector2(1f, 0f));
        m.AddTriangle(KickerDirt, cR, lR2, bR2, Vector3.UnitX);
        return m;
    }

    /// <summary>Casa de pueblo de una planta: paredes, techo a dos aguas con alero, puerta y ventanas.</summary>
    public static MeshBuilder BuildHouse(int seed)
    {
        Random rng = new Random(seed);
        MeshBuilder m = new MeshBuilder(HouseSubmeshes);
        float w = Range(rng, 5f, 8f), d = Range(rng, 4.5f, 6.5f), h = Range(rng, 2.6f, 3.2f);
        float roof = Range(rng, 1.0f, 1.6f), eave = 0.45f;
        m.AddBox(HouseWall, new Vector3(0f, h * 0.5f - 0.2f, 0f), Quaternion.Identity, new Vector3(w, h + 0.4f, d), 1.5f);

        // Techo a dos aguas: dos planos inclinados con alero y los dos triángulos de los extremos.
        Vector3 ridgeA = new Vector3(-w * 0.5f - eave, h + roof, 0f), ridgeB = new Vector3(w * 0.5f + eave, h + roof, 0f);
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 eaveA = new Vector3(-w * 0.5f - eave, h - 0.15f, side * (d * 0.5f + eave));
            Vector3 eaveB = new Vector3(w * 0.5f + eave, h - 0.15f, side * (d * 0.5f + eave));
            int a = m.AddVertex(ridgeA, new Vector2(0f, 1f)), b = m.AddVertex(ridgeB, new Vector2(w, 1f));
            int c = m.AddVertex(eaveB, new Vector2(w, 0f)), e = m.AddVertex(eaveA, new Vector2(0f, 0f));
            m.AddQuad(HouseRoof, a, b, c, e, new Vector3(0f, 1f, side));
            // Cara de abajo del alero (se ve desde la pista).
            int a2 = m.AddVertex(ridgeA, Vector2.Zero), b2 = m.AddVertex(ridgeB, Vector2.Zero);
            int c2 = m.AddVertex(eaveB, Vector2.Zero), e2 = m.AddVertex(eaveA, Vector2.Zero);
            m.AddQuad(HouseRoof, a2, b2, c2, e2, new Vector3(0f, -1f, -side));
        }
        foreach (float side in new[] { -1f, 1f })
        {
            int a = m.AddVertex(new Vector3(side * w * 0.5f, h, -d * 0.5f), Vector2.Zero);
            int b = m.AddVertex(new Vector3(side * w * 0.5f, h, d * 0.5f), Vector2.Zero);
            int c = m.AddVertex(new Vector3(side * w * 0.5f, h + roof, 0f), Vector2.Zero);
            m.AddTriangle(HouseWall, a, b, c, Vector3.UnitX * side);
        }

        // Puerta y ventanas oscuras en la fachada (-z) y ventanas atrás.
        m.AddBox(HouseDark, new Vector3(Range(rng, -w * 0.25f, w * 0.25f), 1.0f, -d * 0.5f - 0.02f), Quaternion.Identity, new Vector3(0.9f, 2f, 0.05f));
        foreach (float x in new[] { -w * 0.33f, w * 0.33f })
        {
            m.AddBox(HouseDark, new Vector3(x, 1.5f, -d * 0.5f - 0.02f), Quaternion.Identity, new Vector3(0.9f, 0.8f, 0.05f));
            m.AddBox(HouseDark, new Vector3(x, 1.5f, d * 0.5f + 0.02f), Quaternion.Identity, new Vector3(0.9f, 0.8f, 0.05f));
        }
        return m;
    }

    /// <summary>Poste de luz de concreto con cruceta (8 m).</summary>
    public static MeshBuilder BuildPowerPole()
    {
        MeshBuilder m = new MeshBuilder(1);
        m.AddTube(0, new Vector3(0f, -0.3f, 0f), new Vector3(0f, 8f, 0f), 0.16f, 0.1f, 8);
        m.AddBeam(0, new Vector3(-0.9f, 7.4f, 0f), new Vector3(0.9f, 7.4f, 0f), 0.12f, 0.12f, Vector3.UnitY);
        return m;
    }

    /// <summary>
    /// Mata de hojas largas que salen del centro: agave (pocas hojas gruesas y bajas)
    /// o caña de azúcar (muchas hojas finas y altas). Dos caras por hoja.
    /// </summary>
    public static MeshBuilder BuildBlades(int seed, float height, int count, float width, float lean)
    {
        Random rng = new Random(seed);
        MeshBuilder m = new MeshBuilder(1);
        const int steps = 5;
        for (int f = 0; f < count; f++)
        {
            float angle = f / (float)count * MathF.PI * 2f + Range(rng, -0.3f, 0.3f);
            float h = height * Range(rng, 0.7f, 1.1f);
            float out_ = h * lean * Range(rng, 0.6f, 1.2f);
            Vector3 dir = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle));
            Vector3 side = Vector3.Cross(Vector3.UnitY, dir);
            int[] top = new int[(steps + 1) * 2], bottom = new int[(steps + 1) * 2];
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                // La hoja sube y se abre hacia afuera, más inclinada en la punta.
                Vector3 c = Vector3.UnitY * h * t + dir * out_ * t * t;
                float w = width * (1f - t * 0.9f);
                for (int k = 0; k < 2; k++)
                {
                    Vector3 p = c + side * (k == 0 ? -w : w);
                    top[s * 2 + k] = m.AddVertex(p, new Vector2(k, t));
                    bottom[s * 2 + k] = m.AddVertex(p, new Vector2(k, t));
                }
            }
            Vector3 normal = Vector3.Normalize(-dir + Vector3.UnitY * lean);
            for (int s = 0; s < steps; s++)
            {
                m.AddQuad(0, top[s * 2], top[s * 2 + 1], top[s * 2 + 3], top[s * 2 + 2], normal);
                m.AddQuad(0, bottom[s * 2], bottom[s * 2 + 1], bottom[s * 2 + 3], bottom[s * 2 + 2], -normal);
            }
        }
        return m;
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
