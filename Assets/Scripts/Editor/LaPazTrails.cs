using UnityEngine;
using Segment = RealMapTrails.Segment;

/// <summary>
/// Pista de La Paz — Yunguilla (Santa Isabel, Azuay) sobre el heightmap LaPaz_Alturas.png
/// (recuadro de 4,12 km girado 8,8°; 1 265 m de desnivel). Pista de Avalancha: ancha casi todo el
/// recorrido para que bajen varios corredores lado a lado, con dos cuellos de botella angostos.
/// Primer trazado: la bajada más natural de la salida (2 196 m) a la meta (1 004 m) por la loma larga
/// que baja al noroeste; se afinará con la ruta real por partes.
/// Las coordenadas son píxeles del heightmap (2048 × 2048), con el norte del recuadro arriba.
/// </summary>
public static class LaPazTrails
{
    private const float Wide = 7f;     // 14 m: entran tres o cuatro corredores juntos.
    private const float Narrow = 4f;   // 8 m: cuello de botella, se pelea la posición.

    public static RealMapTrails.Set Create()
    {
        RealMapTrails.Set set = new RealMapTrails.Set { MapName = "La Paz" };
        set.Segments.AddRange(new[]
        {
            Make("Salida y loma alta", Wide, 1912,1528, 1856,1488, 1832,1496, 1768,1408, 1736,1400, 1672,1344, 1576,1340,
                1480,1336, 1408,1264, 1392,1264, 1288,1160, 1272,1064).Climb(0.05f),
            Make("Cuello de la quebrada", Narrow, 1272,1064, 1216,992, 1216,960),
            Make("Bajada larga del lomo", Wide, 1216,960, 1160,904, 1144,872, 1144,816, 1059,728, 973,640, 888,552, 800,552, 776,528),
            Make("Cuello del pueblo", Narrow, 776,528, 744,528, 696,480),
            Make("Llegada al valle", Wide, 696,480, 624,456, 568,408, 488,408, 432,352, 392,328, 312,320, 240,240, 160,208),
        });
        set.Routes.Add(new RealMapTrails.Route { Name = "La Paz", Color = new Color(1f, 0.55f, 0.1f), Segments = new[] { 0, 1, 2, 3, 4 } });
        return set;
    }

    private static Segment Make(string name, float halfWidth, params int[] xy)
    {
        return RealMapTrails.Make(name, halfWidth, 0f, 0f, 2047f, 2047f, xy);
    }
}
