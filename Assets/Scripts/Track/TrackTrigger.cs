using UnityEngine;

/// <summary>
/// Zona invisible de la pista. Un Checkpoint guarda el punto de reaparición de la bici;
/// la Meta detiene el cronómetro. El origen del objeto va sobre el camino y mira en
/// dirección de la bajada: de ahí salen la posición y el rumbo al reaparecer.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TrackTrigger : MonoBehaviour
{
    public enum TriggerType
    {
        Checkpoint,
        Meta
    }

    [Tooltip("Qué hace esta zona al cruzarla.")]
    [SerializeField] private TriggerType type = TriggerType.Checkpoint;

    [Tooltip("Orden del checkpoint en la bajada (1, 2, 3...). Los anteriores al último cruzado se ignoran.")]
    [SerializeField, Min(0)] private int checkpointIndex = 1;

    public TriggerType Type => type;
    public int CheckpointIndex => checkpointIndex;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        // Si alguien desmarca "Is Trigger" por error, la bici chocaría contra una pared invisible.
        GetComponent<BoxCollider>().isTrigger = true;
    }

    /// <summary>Configura la zona; lo usa el constructor de escena.</summary>
    public void Configure(TriggerType triggerType, int index)
    {
        type = triggerType;
        checkpointIndex = index;
    }

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent(out BikeController bike)) return;

        switch (type)
        {
            case TriggerType.Checkpoint:
                bike.SetCheckpoint(checkpointIndex, transform.position, transform.eulerAngles.y);
                break;

            case TriggerType.Meta:
                if (GameManager.Instance != null) GameManager.Instance.FinishRace();
                break;
        }
    }

    private void OnDrawGizmos()
    {
        // Se dibuja en la vista de escena para ubicarlos, aunque en el juego no se vean.
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = type == TriggerType.Meta ? new Color(1f, 0.2f, 0.2f, 0.35f) : new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
    }
}
