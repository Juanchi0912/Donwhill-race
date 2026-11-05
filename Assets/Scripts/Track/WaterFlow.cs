using UnityEngine;

/// <summary>
/// Hace que el agua se vea líquida: desplaza las ondas del material para que parezca que corre.
/// Va en el objeto del agua del río.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class WaterFlow : MonoBehaviour
{
    [Tooltip("Velocidad de la corriente (repeticiones de la textura por segundo) en cada eje.")]
    [SerializeField] private Vector2 flowSpeed = new Vector2(0.06f, 0.015f);

    [Tooltip("Velocidad de una segunda capa de ondas más lenta, para que no se vea repetitivo.")]
    [SerializeField] private Vector2 detailSpeed = new Vector2(-0.02f, 0.03f);

    private Material material;
    private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

    private void Awake()
    {
        // Instancia propia del material: así solo este río se mueve.
        material = GetComponent<Renderer>().material;
    }

    private void Update()
    {
        if (material == null) return;
        float t = Time.time;
        material.SetTextureOffset(BumpMap, flowSpeed * t);
        material.SetTextureOffset(BaseMap, detailSpeed * t);
    }
}
