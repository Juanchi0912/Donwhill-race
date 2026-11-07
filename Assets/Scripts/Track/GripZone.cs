using UnityEngine;

/// <summary>
/// Zona resbalosa (curvas de arcilla): mientras la bici está dentro agarra menos de lado
/// y la llanta casi se va en las curvas. Necesita un collider marcado como trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GripZone : MonoBehaviour
{
    [Tooltip("Cuánto agarre lateral se pierde dentro (0,4 = queda el 60 %).")]
    [SerializeField, Range(0f, 0.7f)] private float gripLoss = 0.4f;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>Configura la zona; lo usa el constructor de escena.</summary>
    public void Configure(float loss)
    {
        gripLoss = loss;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent(out BikeController bike)) bike.AddZoneGripLoss(gripLoss);
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent(out BikeController bike)) bike.AddZoneGripLoss(-gripLoss);
    }
}
