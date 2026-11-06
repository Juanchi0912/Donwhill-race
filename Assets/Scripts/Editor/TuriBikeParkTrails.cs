using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Senderos del Turi Bike Park (Cuenca, Azuay) sobre el heightmap MapaReal_Alturas.png.
/// Se trazaron con el mapa de calor de rutas y los senderos de OpenStreetMap; no es una copia exacta:
/// la salida y la meta son comunes y las cuatro líneas se separan en el bosque, como en la pista real.
/// «El Bosque» baja por el oeste; las otras tres bajan por la loma de la Vía a Ictocruz y se separan ahí.
/// Las coordenadas son píxeles de la captura de OpenStreetMap (la misma base de Docs/MapaReal_Guia.png):
/// el recuadro del heightmap va de (52, 25) a (422, 394), con el norte arriba.
/// </summary>
public static class TuriBikeParkTrails
{
    private const float BoxX0 = 52f, BoxY0 = 25f, BoxX1 = 422f, BoxY1 = 394f;

    /// <summary>Tramo de sendero que se talla en el terreno.</summary>
    public class Segment
    {
        public string Name;
        public float HalfWidth;   // Medio ancho en metros.
        public float MaxClimb = 0.08f;   // Subida máxima (8 % = se sube pedaleando).
        public Vector2[] Image;   // Puntos en la imagen: x de 0 (oeste) a 1 (este), y de 0 (norte) a 1 (sur).
    }

    /// <summary>Ruta completa de salida a meta, armada con tramos.</summary>
    public class Route
    {
        public string Name;
        public Color Color;
        public int[] Segments;
    }

    // Tramos (índices usados por las rutas).
    private const int SalidaOeste = 0, SalidaLoma = 1, Bosque = 2, Zigzag = 3, Cresta = 4, Rapida = 5, Llegada = 6;

    public static List<Segment> Segments()
    {
        // Los tramos compartidos (salidas y llegada) son más anchos: ahí se juntan los corredores.
        // Las salidas casi no suben (3 %): arrancan con un sprint, no con una cuesta.
        return new List<Segment>
        {
            // Rodea la hondonada por su borde oeste (casi plano) en vez de bajar a ella y tener que salir subiendo.
            Make("Salida por el oeste", 4f, 224,341, 225,330, 226,316, 226,302, 225,288, 224,274, 223,264, 230,259,
                241,259, 247,252, 250,244, 251,236, 252,228).Climb(0.03f),
            Make("Salida por la loma", 4f, 224,341, 232,334, 244,330, 256,328, 266,324, 276,316, 284,308, 292,300,
                299,290, 301,278, 300,266, 299,254, 298,242, 299,232, 302,224).Climb(0.03f),
            Make("El Bosque", 3f, 252,228, 256,221, 260,214, 263,206, 264,198, 264,193, 264,183, 263,175, 258,167,
                251,161, 249,155, 253,148, 260,141, 265,134, 262,126, 258,118, 254,111, 250,106),
            Make("El Zigzag", 3f, 302,224, 296,216, 291,209, 289,203, 289,196, 285,187, 282,177, 282,168,
                286,160, 292,153, 293,149, 283,149, 272,146, 268,141, 271,134, 276,126, 273,118, 269,111, 262,107, 250,106),
            Make("La Cresta", 3f, 302,224, 299,214, 299,203, 301,190, 300,178, 298,165, 297,153, 300,143, 304,136,
                300,130, 290,126, 280,121, 273,112, 265,107, 250,106),
            Make("La Rápida", 3f, 302,224, 305,212, 305,200, 306,188, 307,175, 305,162, 306,150, 309,141, 314,133,
                308,129, 296,127, 285,123, 277,117, 271,109, 260,107, 250,106),
            Make("Llegada", 4.5f, 250,106, 246,104, 242,99, 241,92, 241,88),
        };
    }

    public static List<Route> Routes()
    {
        return new List<Route>
        {
            new Route { Name = "El Bosque", Color = new Color(0.25f, 0.9f, 0.35f), Segments = new[] { SalidaOeste, Bosque, Llegada } },
            new Route { Name = "El Zigzag", Color = new Color(1f, 0.78f, 0.15f), Segments = new[] { SalidaLoma, Zigzag, Llegada } },
            new Route { Name = "La Cresta", Color = new Color(1f, 0.3f, 0.8f), Segments = new[] { SalidaLoma, Cresta, Llegada } },
            new Route { Name = "La Rápida", Color = new Color(1f, 0.28f, 0.2f), Segments = new[] { SalidaLoma, Rapida, Llegada } },
        };
    }

    private static Segment Climb(this Segment segment, float maxClimb)
    {
        segment.MaxClimb = maxClimb;
        return segment;
    }

    private static Segment Make(string name, float halfWidth, params int[] xy)
    {
        Vector2[] points = new Vector2[xy.Length / 2];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2((xy[i * 2] - BoxX0) / (BoxX1 - BoxX0), (xy[i * 2 + 1] - BoxY0) / (BoxY1 - BoxY0));
        }
        return new Segment { Name = name, HalfWidth = halfWidth, Image = points };
    }
}
