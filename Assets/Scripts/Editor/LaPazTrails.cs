using UnityEngine;
using Decor = RealMapTrails.Decor;
using Segment = RealMapTrails.Segment;

/// <summary>
/// Pista de La Paz — Yunguilla (Santa Isabel, Azuay) sobre el heightmap LaPaz_Alturas.png
/// (recuadro de 4,12 km girado 8,8°; 1 265 m de desnivel). Trazada con la ruta que marcaste en Google Earth
/// (Docs/LaPaz_Ruta_GoogleEarth.kml). Pista de Avalancha: la salida es ancha para que arranque el grupo,
/// los zigzags son de uno solo (con tres líneas alternativas) y la cresta vuelve a ser ancha.
/// Se recortó al primer tramo de la bajada larga para que dure unos 4 minutos; el resto de la ruta
/// (bajada larga completa, zigzags de abajo y llegada al río) queda en el KML para usarlo después.
/// Coordenadas en décimas de píxel del heightmap (0–20470), con el norte del recuadro arriba.
/// </summary>
public static class LaPazTrails
{
    private const int Salida = 0, Zigzags = 1, Cresta = 2;

    public static RealMapTrails.Set Create()
    {
        RealMapTrails.Set set = new RealMapTrails.Set { MapName = "La Paz" };
        set.Segments.AddRange(new[]
        {
            Make("Salida en el pueblo", 8.0f, 19169,15509, 19111,15608, 19118,15677, 19194,15653, 19236,15656, 19357,15709, 19413,15704, 19570,15615, 19663,15527, 19707,15497, 19759,15473, 19707,15561, 19633,15653, 19514,15761, 19403,15846, 19317,15884, 19163,15925, 18962,16066, 18880,16101, 18785,16108, 18672,16071, 18631,16017, 18604,15939, 18546,15845, 18540,15807, 18474,15751, 18376,15766, 18326,15765).Climb(0.05f),
            Make("Zigzags (uno solo)", 2.5f, 18326,15765, 18286,15723, 18254,15702, 18091,15672, 18049,15680, 17993,15666, 17958,15668, 17882,15685, 17820,15670, 17769,15679, 17756,15673, 17748,15635, 17812,15556, 17824,15533, 17821,15510, 17702,15536, 17691,15533, 17679,15541, 17672,15534, 17667,15517, 17704,15367, 17692,15358, 17635,15374, 17582,15371, 17570,15363, 17578,15324, 17594,15304, 17584,15281, 17416,15200, 17375,15109, 17344,15091, 17337,15073, 17391,15022, 17422,15007, 17408,14980, 17373,14959, 17337,14963, 17312,14991, 17291,14971, 17294,14956, 17336,14933, 17333,14906, 17298,14899, 17255,14922, 17260,14850, 17248,14810, 17230,14782, 17138,14752, 17069,14685, 16984,14628, 16972,14609, 16922,14575, 16860,14483, 16844,14442, 16866,14422, 16908,14430, 16975,14414, 17017,14414, 17027,14402, 16996,14375, 17005,14361, 17035,14362, 17067,14350, 17113,14371, 17120,14360, 17114,14328, 17121,14289, 17043,14218, 17059,14164, 17151,14176, 17169,14147, 17104,14118, 17093,14094, 17106,14069, 17125,13992, 17123,13971, 17082,13936, 17049,13922, 17043,13895, 16991,13860, 16953,13850, 16959,13822, 16985,13817, 17011,13828, 17007,13796, 17043,13788, 17061,13760, 17080,13704, 17065,13670, 17010,13635, 16963,13579, 16935,13503, 16876,13431, 16894,13320),
            Make("Cresta rápida", 7.0f, 16894,13320, 16891,13253, 16762,13012, 16754,12951, 16767,12911, 16725,12860, 16642,12731, 16464,12679, 16355,12582, 16098,12542, 16005,12482, 15888,12250, 15843,12126, 15832,11902, 15818,11800, 15830,11598, 15772,11586, 15697,11595, 15657,11617, 15592,11690, 15504,11701, 15481,11718, 15439,11720, 15422,11753, 15376,11765, 15309,11722, 15239,11768, 15164,11773, 15130,11754, 15030,11771, 15051,11907, 15026,11959, 14985,12011, 14997,12073, 14946,12120, 14946,12158, 14892,12151, 14873,12237, 14816,12191, 14786,12246, 14726,12230, 14608,12278, 14556,12277, 14458,12239, 14448,12280, 14455,12387, 14400,12399, 14344,12317, 14323,12261, 14278,12249, 14219,12251, 14185,12283, 14132,12252, 14090,12296, 14048,12284, 13977,12351, 13864,12358, 13773,12261, 13786,12382, 13756,12408, 13768,12446, 13832,12479, 13898,12537, 13920,12600, 13867,12592, 13711,12542, 13603,12547, 13532,12477, 13472,12393, 13447,12343, 13275,12210, 13218,12137),
            Make("Línea alternativa 1", 2.0f, 18326,15765, 18155,15742, 18091,15721, 18039,15711, 17907,15703, 17894,15687, 17902,15704, 17803,15708, 17601,15742, 17583,15736, 17577,15709, 17593,15675, 17654,15582, 17667,15517),
            Make("Línea alternativa 2", 2.0f, 17578,15324, 17416,15200),
            Make("Línea alternativa 3", 2.0f, 17069,14685, 17046,14607, 17008,14579, 16984,14578, 16860,14483),
        });
        set.Routes.Add(new RealMapTrails.Route { Name = "La Paz", Color = new Color(1f, 0.55f, 0.1f), Segments = new[] { Salida, Zigzags, Cresta } });

        // Ambientación según los videos de la carrera: pueblo en la salida, ladera de pasto seco,
        // matorral y pedregal en los zigzags, y la cresta abierta y rápida.
        set.Zones.Add(new RealMapTrails.Zone { Name = "Pueblo de la salida", Style = Decor.Pueblo, From = 0f, To = 230f });
        set.Zones.Add(new RealMapTrails.Zone { Name = "Ladera de pasto seco", Style = Decor.LaderaAbierta, From = 230f, To = 557f });
        set.Zones.Add(new RealMapTrails.Zone { Name = "Matorral", Style = Decor.Matorral, From = 557f, To = 1050f });
        set.Zones.Add(new RealMapTrails.Zone { Name = "Pedregal", Style = Decor.Pedregal, From = 1050f, To = 1510f });
        set.Zones.Add(new RealMapTrails.Zone { Name = "Cresta abierta", Style = Decor.LaderaAbierta, From = 1510f, To = 2837f });
        set.Zones.Add(new RealMapTrails.Zone { Name = "Piedras de la cresta", Style = Decor.Pedregal, From = 2100f, To = 2350f });
        return set;
    }

    private static Segment Make(string name, float halfWidth, params int[] xy)
    {
        return RealMapTrails.Make(name, halfWidth, 0f, 0f, 20470f, 20470f, xy);
    }
}
