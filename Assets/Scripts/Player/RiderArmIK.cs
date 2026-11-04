using UnityEngine;

/// <summary>
/// Mueve los brazos rígidos del corredor (brazo → antebrazo → mano) para que las manos sigan en los puños
/// del manubrio. Como el torso se mueve de lado a lado al pedalear fuerte y la bici se balancea,
/// los brazos se estiran y se doblan por turnos: tiran del manubrio como al pedalear con fuerza.
/// Corre después de BikeVisuals, que mueve el torso y el manubrio.
/// </summary>
[DefaultExecutionOrder(100)]
public class RiderArmIK : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Pivote de la bici que da las direcciones adelante, derecha y arriba (Inclinacion).")]
    [SerializeField] private Transform frame = null;

    [Tooltip("Pivote del torso: los hombros se mueven con él.")]
    [SerializeField] private Transform torso = null;

    [Tooltip("Brazo izquierdo: brazo, antebrazo y mano, más el puño del manubrio que sigue.")]
    [SerializeField] private Transform leftUpperArm = null;
    [SerializeField] private Transform leftForearm = null;
    [SerializeField] private Transform leftHand = null;
    [SerializeField] private Transform leftGrip = null;

    [Tooltip("Brazo derecho: brazo, antebrazo y mano, más el puño del manubrio que sigue.")]
    [SerializeField] private Transform rightUpperArm = null;
    [SerializeField] private Transform rightForearm = null;
    [SerializeField] private Transform rightHand = null;
    [SerializeField] private Transform rightGrip = null;

    [Header("Medidas (en metros)")]
    [Tooltip("Largo del brazo, del hombro al codo.")]
    [SerializeField] private float upperArmLength = 0.29f;

    [Tooltip("Largo del antebrazo, del codo a la muñeca.")]
    [SerializeField] private float forearmLength = 0.27f;

    [Tooltip("Hombro derecho respecto del pivote del torso (el izquierdo usa x negativa).")]
    [SerializeField] private Vector3 shoulderOffset = new Vector3(0.20f, 0.37f, 0.23f);

    [Header("Postura")]
    [Tooltip("Cuánto se abren los codos hacia afuera (posición de ataque).")]
    [SerializeField] private float elbowOutward = 1f;

    [Tooltip("Cuánto apuntan los codos hacia atrás.")]
    [SerializeField] private float elbowBack = 0.3f;

    [Tooltip("Cuánto apuntan los codos hacia abajo.")]
    [SerializeField] private float elbowDown = 0.3f;

    private void LateUpdate()
    {
        if (frame == null || torso == null) return;
        SolveArm(-1f, leftUpperArm, leftForearm, leftHand, leftGrip);
        SolveArm(1f, rightUpperArm, rightForearm, rightHand, rightGrip);
    }

    private void SolveArm(float side, Transform upperArm, Transform forearm, Transform hand, Transform grip)
    {
        if (upperArm == null || forearm == null || hand == null || grip == null) return;

        Vector3 shoulder = torso.TransformPoint(new Vector3(shoulderOffset.x * side, shoulderOffset.y, shoulderOffset.z));
        Vector3 bendHint = frame.right * side * elbowOutward - frame.forward * elbowBack - frame.up * elbowDown;
        TwoBoneIK.Solve(shoulder, grip.position, upperArmLength, forearmLength, bendHint, out Vector3 elbow, out Vector3 wrist);

        // Los segmentos se modelan a lo largo de -Y local; el frente (+Z) se apunta hacia arriba.
        Vector3 up = frame.up;
        upperArm.SetPositionAndRotation(shoulder, TwoBoneIK.SegmentRotation(elbow - shoulder, up));
        forearm.SetPositionAndRotation(elbow, TwoBoneIK.SegmentRotation(wrist - elbow, up));
        hand.SetPositionAndRotation(wrist, grip.rotation);
    }
}
