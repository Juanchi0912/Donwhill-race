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
        public bool Locked;              // Parte real que edita el usuario (círculos grises): no se le agrega nada.
        public bool AutoJumps;           // En las rectas largas se ponen saltos, mesas o lomas solos.
        public bool Forest;              // Bosque cerrado: árboles pegados al sendero (chocar = caída).
        public bool Clay;                // Las curvas cerradas son de arcilla: color rojizo y menos agarre.
        public bool Crowd;               // Público y vallas con pancartas a los dos lados (recta final).
        public List<Spot> Spots = new List<Spot>();   // Saltos y obstáculos puestos a mano.
    }

    /// <summary>Tipos de salto u obstáculo que se tallan en el sendero.</summary>
    public enum Feature
    {
        Mirador,     // Plataforma de madera en la salida con drop.
        Drop,        // Escalón: la bajada se aplana y cae de golpe a una recepción larga.
        Kicker,      // Rampa de madera con caída.
        Tabletop,    // Mesa: rampa, plano y recepción.
        Doubles,     // Tres dobles seguidos (rampa, hueco y recepción).
        Rollers,     // Lomas seguidas para bombear o saltar.
        RoadGap,     // Gap sobre el camino de tierra.
        FenceGap,    // Salto sobre una cerca de piedra.
        RockDrop,    // Drop de roca en el bosque.
        BigJump,     // Salto enorme antes de la meta.
        RockGarden,  // Pedregal: el piso vibra y rebota.
        Roots        // Raíces cruzadas en el sendero.
    }

    /// <summary>Salto u obstáculo en un punto del tramo (en la imagen, 0–1).</summary>
    public class Spot
    {
        public Feature Type;
        public Vector2 At;
        public float Size = 1f;
    }

    /// <summary>Puente de madera: "Upper" pasa por arriba de "Lower" (índices de tramos) donde se cruzan.</summary>
    public class Bridge
    {
        public int Upper, Lower;
    }

    /// <summary>Loma empinada entre rutas (límite): tapa la vista de una ruta a otra. Se corre sola al medio y nunca tapa un sendero.</summary>
    public class Ridge
    {
        public Vector2[] Image;          // Trazo de la cumbre en la imagen (0–1).
        public float Height = 55f;       // Altura sobre el terreno (m).
        public float HalfWidth = 55f;    // De la cumbre al pie (m).
    }

    /// <summary>Retoque del relieve antes de tallar: suavizar una zona o bajarla (abrir un paso en una loma).</summary>
    public class Sculpt
    {
        public bool Lower;       // false = suavizar ("Amount" = desenfoque en m); true = bajar "Amount" metros.
        public Vector2 Center;   // En la imagen (0–1).
        public float Radius;     // En fracción del ancho de la imagen.
        public float Amount;
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
        public List<Segment> Roads = new List<Segment>();    // Caminos de tierra (se tallan primero, sin límite de subida).
        public List<Bridge> Bridges = new List<Bridge>();
        public List<Sculpt> Sculpts = new List<Sculpt>();
        public List<Ridge> Ridges = new List<Ridge>();
        public float SouthRise;     // Metros que sube el borde sur (inclina el mapa: todo baja hacia el norte).
        public bool Dressing;       // Pista terminada: cintas, pancartas, puente, público, ciudad a lo lejos.
        public bool Crashes;        // Chocar fuerte (árbol, roca, baranda) es caída y se reaparece.
        public float ViewDistance;  // Niebla lejana (m) para ver la ciudad; 0 = la de la ambientación.
        public Vector2[] Farms = new Vector2[0];   // Casas de campo (al final de los caminos).
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

    /// <summary>Salto u obstáculo en el punto (x, y) de la imagen de referencia del tramo.</summary>
    public static Segment With(this Segment segment, Feature type, Vector2 imagePoint, float size = 1f)
    {
        segment.Spots.Add(new Spot { Type = type, At = imagePoint, Size = size });
        return segment;
    }

    /// <summary>Cambia la subida máxima del tramo (por ejemplo 3 % en una salida de sprint).</summary>
    public static Segment Climb(this Segment segment, float maxClimb)
    {
        segment.MaxClimb = maxClimb;
        return segment;
    }
}
