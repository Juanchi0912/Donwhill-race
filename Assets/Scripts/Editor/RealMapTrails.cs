using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Senderos de un mapa real: los tramos que se tallan en el terreno y las rutas (de la salida a la meta)
/// que se arman con esos tramos. Cada mapa (Turi, La Paz…) arma su propio conjunto.
/// </summary>
public static class RealMapTrails
{
    /// <summary>Tramo de sendero que se talla en el terreno.</summary>
    public class Segment
    {
        public string Name;
        public float HalfWidth;          // Medio ancho en metros.
        public float MaxClimb = 0.08f;   // Subida máxima (8 % = se sube pedaleando).
        public Vector2[] Image;          // Puntos en la imagen: x de 0 (oeste) a 1 (este), y de 0 (norte) a 1 (sur).
    }

    /// <summary>Ruta completa de salida a meta, armada con tramos (índices de Set.Segments).</summary>
    public class Route
    {
        public string Name;
        public Color Color;
        public int[] Segments;
    }

    /// <summary>Estilo de ambientación de una parte de la ruta (sacado de los videos de cada pista).</summary>
    public enum Decor
    {
        Pueblo,          // Casas y postes a los lados.
        LaderaAbierta,   // Pasto seco y vista: sin árboles cerca, agaves sueltos.
        Matorral,        // Arbustos que cierran el sendero por los dos lados.
        Pedregal,        // Rocas en los bordes y algunas sueltas en el camino, agaves.
        Arboleda,        // Más árboles a los dos lados (partes anchas).
        Cinta            // Cinta de carrera con postes: a los dos lados donde es angosto, y por fuera de las curvas.
    }

    /// <summary>Zona verde de costa (caña de azúcar, cacao, arbustos verdes): un círculo sobre el mapa.</summary>
    public class Patch
    {
        public string Name;
        public Vector2 Center;   // En la imagen (0–1), igual que los tramos.
        public float Radius;     // Metros.
    }

    /// <summary>Río: cauce hundido con agua que corre, piedras y una zona que frena la bici.</summary>
    public class River
    {
        public string Name;
        public float HalfWidth;  // Metros.
        public Vector2[] Image;  // Línea central en la imagen (0–1).
    }

    /// <summary>Zona de ambientación: de "From" a "To" metros a lo largo de la primera ruta.</summary>
    public class Zone
    {
        public string Name;
        public Decor Style;
        public float From, To;
        public bool BothSides;   // Cinta: a los dos lados aunque sea ancho (salida y llegada).
    }

    /// <summary>Todos los senderos de un mapa.</summary>
    public class Set
    {
        public string MapName;
        public List<Segment> Segments = new List<Segment>();
        public List<Route> Routes = new List<Route>();
        public List<Zone> Zones = new List<Zone>();
        public List<Patch> Patches = new List<Patch>();
        public List<River> Rivers = new List<River>();
        /// <summary>Límite de la pista en la imagen (0–1); null = sin límite. Fuera de él la bici reaparece.</summary>
        public Vector2[] Bounds;
    }

    /// <summary>Puntos en píxeles de una imagen de referencia (recuadro del heightmap de (x0, y0) a (x1, y1)) a 0–1.</summary>
    public static Vector2[] Points(float x0, float y0, float x1, float y1, params int[] xy)
    {
        Vector2[] points = new Vector2[xy.Length / 2];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2((xy[i * 2] - x0) / (x1 - x0), (xy[i * 2 + 1] - y0) / (y1 - y0));
        }
        return points;
    }

    /// <summary>
    /// Tramo con puntos en píxeles de una imagen de referencia, donde el heightmap completo
    /// ocupa el recuadro de (x0, y0) a (x1, y1).
    /// </summary>
    public static Segment Make(string name, float halfWidth, float x0, float y0, float x1, float y1, params int[] xy)
    {
        return new Segment { Name = name, HalfWidth = halfWidth, Image = Points(x0, y0, x1, y1, xy) };
    }

    /// <summary>Cambia la subida máxima del tramo (por ejemplo 3 % en una salida de sprint).</summary>
    public static Segment Climb(this Segment segment, float maxClimb)
    {
        segment.MaxClimb = maxClimb;
        return segment;
    }
}
