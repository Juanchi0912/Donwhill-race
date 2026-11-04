using UnityEngine;

/// <summary>
/// Mueve las piernas rígidas del corredor (muslo → canilla → pie) para que los pies sigan a los pedales.
/// Usa IK analítica de dos segmentos: con la cadera y el pedal se calcula dónde queda la rodilla,
/// que siempre se dobla hacia adelante y un poco hacia afuera. Corre después de BikeVisuals,
/// que es quien mueve las bielas y el torso.
/// </summary>
[DefaultExecutionOrder(100)]
public class RiderLegIK : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Pivote de la bici que da las direcciones adelante y derecha (Inclinacion).")]
    [SerializeField] private Transform frame = null;

    [Tooltip("Pivote del torso: las caderas se mueven con él al agacharse o pedalear.")]
    [SerializeField] private Transform torso = null;

    [Tooltip("Pierna izquierda: muslo, canilla y pie, más el pedal que sigue.")]
    [SerializeField] private Transform leftThigh = null;
    [SerializeField] private Transform leftShin = null;
    [SerializeField] private Transform leftFoot = null;
    [SerializeField] private Transform leftPedal = null;

    [Tooltip("Pierna derecha: muslo, canilla y pie, más el pedal que sigue.")]
    [SerializeField] private Transform rightThigh = null;
    [SerializeField] private Transform rightShin = null;
    [SerializeField] private Transform rightFoot = null;
    [SerializeField] private Transform rightPedal = null;

    [Header("Medidas (en metros)")]
    [Tooltip("Largo del muslo, de la cadera a la rodilla.")]
    [SerializeField] private float thighLength = 0.41f;

    [Tooltip("Largo de la canilla, de la rodilla al tobillo.")]
    [SerializeField] private float shinLength = 0.41f;

    [Tooltip("Cadera derecha respecto del pivote del torso (la izquierda usa x negativa).")]
    [SerializeField] private Vector3 hipOffset = new Vector3(0.10f, -0.01f, 0f);

    [Tooltip("Altura del tobillo sobre el eje del pedal.")]
    [SerializeField] private float ankleHeight = 0.092f;

    [Tooltip("Cuánto más atrás que el eje del pedal queda el tobillo (se pisa con la parte delantera del pie).")]
    [SerializeField] private float ankleBack = 0.045f;

    [Tooltip("Cuánto se abren las rodillas hacia afuera al doblarse (0 = rectas hacia adelante).")]
    [SerializeField, Range(0f, 1f)] private float kneeOutward = 0.15f;

    private void LateUpdate()
    {
        if (frame == null || torso == null) return;
        SolveLeg(-1f, leftThigh, leftShin, leftFoot, leftPedal);
        SolveLeg(1f, rightThigh, rightShin, rightFoot, rightPedal);
    }

    private void SolveLeg(float side, Transform thigh, Transform shin, Transform foot, Transform pedal)
    {
        if (thigh == null || shin == null || foot == null || pedal == null) return;

        Vector3 forward = frame.forward;
        Vector3 hip = torso.TransformPoint(new Vector3(hipOffset.x * side, hipOffset.y, hipOffset.z));
        Vector3 ankleTarget = pedal.position + pedal.up * ankleHeight - pedal.forward * ankleBack;

        // La rodilla se dobla hacia adelante y un poco hacia afuera.
        Vector3 bendHint = forward + frame.right * side * kneeOutward;
        TwoBoneIK.Solve(hip, ankleTarget, thighLength, shinLength, bendHint, out Vector3 knee, out Vector3 ankle);

        // Cada segmento va a lo largo de su -Y local, con el frente (+Z) hacia adelante.
        thigh.SetPositionAndRotation(hip, TwoBoneIK.SegmentRotation(knee - hip, forward));
        shin.SetPositionAndRotation(knee, TwoBoneIK.SegmentRotation(ankle - knee, forward));
        foot.SetPositionAndRotation(ankle, pedal.rotation);
    }
}
