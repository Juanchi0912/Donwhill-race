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

    /// <summary>Turi – Ictocruz (Cuenca): 1,77 km, 331 m de desnivel, heightmap de 8 bits.</summary>
    private static HeightmapMapBuilder.Settings TuriDefaults()
    {
        return new HeightmapMapBuilder.Settings();
    }

    /// <summary>
    /// La Paz — Yunguilla (Santa Isabel): 4,12 km y 1 265 m de desnivel reales, heightmap de 16 bits.
    /// La bajada ya baja casi 1 200 m, así que no se exagera; solo se acentúan un poco las quebradas.
    /// Valle seco con pocos árboles: el mapa es grande y así no pesa.
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
            ReliefExaggeration = 1f,
            ReliefAccent = 0.3f,
            SmoothMeters = 4f,
            Ambience = TrackLayout.Theme.ValleSeco,
            TreesPerHectare = 6f,
            Seed = 3,
        };
    }
}
