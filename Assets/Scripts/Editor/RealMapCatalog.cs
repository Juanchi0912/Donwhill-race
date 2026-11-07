using System;

/// <summary>
/// Mapas reales que sabe crear la herramienta (DownhillRace → Crear mapa desde heightmap).
/// Cada uno tiene su escena, su heightmap, sus valores recomendados y sus senderos, así crear uno no borra al otro.
/// </summary>
public static class RealMapCatalog
{
    public class Entry
    {
        public string Id;
        public string DisplayName;
        public Func<HeightmapMapBuilder.Settings> Defaults;
        public Func<RealMapTrails.Set> Trails;
    }

    public static readonly Entry[] All =
    {
        new Entry { Id = "turi", DisplayName = "Turi Bike Park", Defaults = TuriDefaults, Trails = TuriBikeParkTrails.Create },
        new Entry { Id = "lapaz", DisplayName = "La Paz — Yunguilla", Defaults = LaPazDefaults, Trails = LaPazTrails.Create },
    };

    /// <summary>
    /// Turi – Ictocruz (Cuenca): 1,77 km, 331 m de desnivel, heightmap de 8 bits.
    /// Pista terminada: resolución 4097 para que los saltos y drops queden nítidos (cada celda mide 43 cm).
    /// </summary>
    private static HeightmapMapBuilder.Settings TuriDefaults()
    {
        return new HeightmapMapBuilder.Settings { Resolution = 4097, TreesPerHectare = 22f };
    }

    /// <summary>
    /// La Paz — Yunguilla (Santa Isabel): 4,12 km y 1 265 m de desnivel reales, heightmap de 16 bits.
    /// Desnivel ×1,5 y quebradas acentuadas para que la cresta y las bajadas sean más rápidas.
    /// Recortado al borde de la pista, sendero color tierra clara (#D2B48C) y día caluroso, como en la carrera.
    /// </summary>
    private static HeightmapMapBuilder.Settings LaPazDefaults()
    {
        return new HeightmapMapBuilder.Settings
        {
            MapName = "LaPaz",
            HeightmapPath = HeightmapMapBuilder.MapsFolder + "/LaPaz_Alturas.png",
            Width = 4120f,
            Length = 4120f,
            HeightDifference = 1265f,
            ReliefExaggeration = 1.5f,
            ReliefAccent = 0.35f,
            SmoothMeters = 4f,
            Ambience = TrackLayout.Theme.ValleSeco,
            TreesPerHectare = 12f,
            Seed = 3,
            // Recorte al borde gris del dibujo (con 50 m de margen): mucho más detalle que el mapa completo.
            CropX = new UnityEngine.Vector2(0.588f, 0.990f),
            CropZ = new UnityEngine.Vector2(0.185f, 0.459f),
            TrailColor = "#D2B48C",
            // Pasto seco más oscuro y oliva: si no, el sendero claro se pierde en el pasto amarillo.
            GroundTint = "#A8B47A",
            HotDay = true,
        };
    }
}
