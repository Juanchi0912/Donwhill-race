using UnityEngine;

/// <summary>
/// Zona que frena la bici mientras está dentro (por ejemplo, el agua de un río).
/// Necesita un collider marcado como trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SlowZone : MonoBehaviour
{
    [Tooltip("Desaceleración extra en m/s² dentro de la zona.")]
    [SerializeField, Min(0f)] private float deceleration = 3f;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    /// <summary>Configura la zona; lo usa el constructor de escena.</summary>
    public void Configure(float decel)
    {
        deceleration = decel;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent(out BikeController bike)) bike.AddZoneDeceleration(deceleration);
    }

    private void OnTriggerExit(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent(out BikeController bike)) bike.AddZoneDeceleration(-deceleration);
    }
}
