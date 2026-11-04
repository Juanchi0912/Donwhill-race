using UnityEngine;

/// <summary>
/// IK analítica de dos segmentos (muslo-canilla o brazo-antebrazo). Dado el punto de inicio
/// (cadera u hombro) y el objetivo (pedal o puño), calcula dónde queda la articulación del medio,
/// doblada hacia la dirección indicada. La usan RiderLegIK y RiderArmIK.
/// </summary>
public static class TwoBoneIK
{
    /// <summary>
    /// Resuelve la cadena. Si el objetivo queda fuera de alcance, el extremo se estira al máximo hacia él.
    /// </summary>
    public static void Solve(Vector3 root, Vector3 target, float upperLength, float lowerLength, Vector3 bendHint,
        out Vector3 joint, out Vector3 end)
    {
        Vector3 toTarget = target - root;
        float distance = toTarget.magnitude;
        Vector3 dir = distance > 1e-5f ? toTarget / distance : Vector3.down;
        distance = Mathf.Clamp(distance, Mathf.Abs(upperLength - lowerLength) + 0.01f, (upperLength + lowerLength) * 0.999f);

        Vector3 bend = Vector3.ProjectOnPlane(bendHint, dir);
        bend = bend.sqrMagnitude > 1e-8f ? bend.normalized : Vector3.Cross(dir, Vector3.right).normalized;

        // Ley de cosenos: ángulo en la raíz entre la línea raíz-objetivo y el primer segmento.
        float cos = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * upperLength * distance);
        cos = Mathf.Clamp(cos, -1f, 1f);
        float sin = Mathf.Sqrt(1f - cos * cos);
        joint = root + dir * (upperLength * cos) + bend * (upperLength * sin);
        end = root + dir * distance;
    }

    /// <summary>Rotación de un segmento modelado a lo largo de su -Y local, con +Z mirando hacia "front".</summary>
    public static Quaternion SegmentRotation(Vector3 segment, Vector3 front)
    {
        Vector3 dir = segment.normalized;
        Vector3 facing = Vector3.ProjectOnPlane(front, dir);
        if (facing.sqrMagnitude < 1e-6f) facing = Vector3.Cross(dir, Vector3.right);
        return Quaternion.LookRotation(facing.normalized, -dir);
    }
}
