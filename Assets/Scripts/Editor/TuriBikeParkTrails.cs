using UnityEngine;
using Feature = RealMapTrails.Feature;
using Segment = RealMapTrails.Segment;

/// <summary>
/// Turi Bike Park (Cuenca, Azuay), versión de carrera terminada, sobre el heightmap MapaReal_Alturas.png.
/// Las dos partes reales (la salida en lo alto del cerro y la llegada: los círculos grises del dibujo)
/// quedan como estaban y no se les agrega nada: esas las edita el usuario. Todo lo demás es inventado
/// para que sea divertido: cuatro rutas que siempre bajan, con drops, mesas, dobles, gaps, un puente de madera
/// donde La Cresta pasa sobre La Rápida, un camino de tierra, una cerca de piedra, bosque cerrado,
/// curvas de arcilla, pedregales y un salto enorme antes de la meta.
/// Las rutas se trazaron con un buscador de caminos que solo bajan (con el relieve inclinado hacia el norte).
/// Coordenadas: décimas de píxel de la captura de OpenStreetMap (recuadro del heightmap de (52, 25) a (422, 394)).
/// </summary>
public static class TuriBikeParkTrails
{
    private const float BoxX0 = 520f, BoxY0 = 250f, BoxX1 = 4220f, BoxY1 = 3940f;

    // Tramos (índices usados por las rutas).
    private const int SalidaOeste = 0, SalidaLoma = 1, MiradorOeste = 2, MiradorEste = 3, Rapida = 4, Cresta = 5,
        Zigzag = 6, Bosque = 7, RectaFinal = 8, Entrada = 9, Llegada = 10;

    // Fuera de las partes reales todo baja al menos 4 %: no hay subidas.
    private const float MinDescent = -0.04f;

    public static RealMapTrails.Set Create()
    {
        RealMapTrails.Set set = new RealMapTrails.Set
        {
            MapName = "Turi Bike Park",
            // El mapa se inclina 180 m hacia el norte: así casi todo el cerro baja hacia la meta.
            SouthRise = 180f,
            Dressing = true,
            Crashes = true,
            // Niebla lejana: desde arriba se ve Cuenca abajo.
            ViewDistance = 2600f,
            Farms = new[] { P(2125, 1838) },
        };
        // Partes reales (círculos grises): las edita el usuario, no se les agrega nada.
        set.Segments.Add(Locked(Make("Salida por el oeste", 4f, 2240,3410, 2250,3300, 2250,3280).Climb(0.03f)));
        set.Segments.Add(Locked(Make("Salida por la loma", 4f, 2240,3410, 2320,3340, 2440,3300, 2560,3280, 2660,3240, 2760,3160, 2840,3080, 2920,3000).Climb(0.03f)));
        // Miradores: plataforma de madera con drop, justo al salir de la parte real.
        set.Segments.Add(Make("Mirador oeste", 4f, 2250,3280, 2240,3180, 2235,3090).Climb(MinDescent)
            .With(Feature.Mirador, P(2244, 3200)));
        set.Segments.Add(Make("Mirador este", 4f,
            2920,3000, 2977,3007, 2990,3006, 3003,3002, 3026,2987, 3090,2930, 3093,2885).Climb(MinDescent)
            .With(Feature.Mirador, P(3080, 2934)));
        // La Rápida: la más directa, por el filo de la loma; saltos grandes y pedregal.
        set.Segments.Add(Auto(Make("La Rápida", 3f,
            3093,2885, 3075,2831, 3071,2817, 3071,2807, 3074,2798, 3079,2789, 3101,2770, 3107,2763, 3113,2749, 3115,2735, 3112,2720, 3103,2697,
            3095,2685, 3073,2665, 3069,2656, 3066,2642, 3068,2627, 3072,2613, 3098,2571, 3103,2552, 3103,2542, 3099,2528, 3075,2484, 3070,2465,
            3073,2446, 3096,2397, 3098,2377, 3093,2363, 3080,2342, 3056,2317, 3032,2299, 3016,2269, 2990,2246, 2984,2239, 2981,2230, 2981,2210,
            2978,2196, 2969,2184, 2942,2155, 2937,2142, 2936,2132, 2940,2119, 2956,2100, 2963,2094, 2981,2087, 2988,2081, 2992,2073, 3000,2034,
            3008,2016, 3021,2001, 3058,1968, 3072,1948, 3075,1939, 3077,1924, 3073,1879, 3074,1864, 3079,1850, 3096,1827, 3107,1804, 3110,1780,
            3105,1760, 3098,1747, 3091,1740, 3059,1718, 3054,1710, 3046,1681, 3034,1666, 3022,1657, 3009,1651, 2969,1646, 2951,1638, 2933,1621,
            2906,1600, 2893,1575, 2867,1552, 2855,1537, 2852,1528, 2851,1518, 2857,1499, 2876,1466, 2893,1450, 2919,1420, 2923,1408, 2918,1403,
            2905,1400, 2870,1399, 2841,1403, 2827,1401, 2815,1393, 2799,1368, 2793,1361, 2775,1351, 2761,1349, 2742,1352, 2720,1364, 2710,1374,
            2695,1400, 2687,1406, 2674,1411, 2654,1413, 2639,1411, 2599,1394, 2590,1394, 2558,1405, 2544,1408, 2529,1407, 2510,1400).Climb(MinDescent))
            .With(Feature.RockGarden, P(3085, 2640))
            .With(Feature.Drop, P(3005, 2050), 1.2f));
        // La Cresta: abierta, por el filo del este; dobles encadenados y curvas de arcilla; pasa sobre La Rápida.
        Segment cresta = Auto(Make("La Cresta", 3.2f,
            3093,2885, 3112,2835, 3133,2769, 3149,2750, 3157,2737, 3162,2723, 3162,2714, 3160,2699, 3144,2658, 3145,2648, 3153,2627, 3149,2593,
            3153,2579, 3163,2569, 3176,2563, 3210,2557, 3233,2548, 3282,2542, 3291,2539, 3297,2532, 3307,2510, 3316,2499, 3324,2494, 3348,2487,
            3361,2480, 3381,2452, 3392,2442, 3410,2434, 3454,2427, 3469,2423, 3481,2416, 3492,2405, 3501,2388, 3510,2349, 3516,2335, 3529,2320,
            3561,2296, 3569,2284, 3573,2270, 3577,2246, 3577,2226, 3572,2212, 3566,2204, 3544,2185, 3539,2176, 3537,2167, 3537,2147, 3547,2114,
            3550,2099, 3546,2080, 3530,2056, 3526,2042, 3529,2018, 3536,1994, 3543,1981, 3550,1975, 3587,1961, 3596,1956, 3606,1945, 3616,1923,
            3626,1874, 3636,1857, 3652,1846, 3694,1833, 3705,1824, 3715,1808, 3731,1789, 3738,1775, 3741,1761, 3737,1742, 3727,1725, 3709,1707,
            3669,1678, 3650,1672, 3631,1676, 3595,1693, 3576,1696, 3557,1692, 3517,1671, 3498,1665, 3478,1665, 3435,1677, 3396,1678, 3371,1672,
            3336,1654, 3322,1650, 3312,1650, 3298,1653, 3289,1658, 3278,1668, 3259,1691, 3247,1699, 3233,1703, 3219,1701, 3143,1675, 3083,1640,
            3072,1630, 3063,1618, 3048,1576, 3041,1570, 3024,1562, 3006,1545, 2994,1537, 2980,1535, 2945,1541, 2931,1541, 2922,1538, 2903,1523,
            2890,1519, 2852,1528, 2837,1528, 2827,1526, 2819,1522, 2804,1504, 2771,1497, 2763,1491, 2740,1466, 2732,1461, 2718,1456, 2688,1453,
            2678,1454, 2638,1473, 2619,1476, 2599,1472, 2564,1455, 2541,1458, 2522,1455, 2500,1444, 2492,1438, 2487,1430, 2487,1422, 2491,1414,
            2498,1407, 2510,1400).Climb(MinDescent))
            .With(Feature.Doubles, P(3400, 2380), 1.1f);
        cresta.Clay = true;
        set.Segments.Add(cresta);
        // El Zigzag: curvas cerradas de arcilla con peralte y un salto sobre una cerca de piedra.
        Segment zigzag = Auto(Make("El Zigzag", 2.8f,
            2235,3090, 2223,3064, 2221,3040, 2225,3020, 2238,2988, 2243,2969, 2246,2934, 2243,2884, 2243,2869, 2248,2850, 2263,2813, 2265,2793,
            2260,2774, 2239,2740, 2230,2717, 2229,2698, 2238,2675, 2239,2661, 2236,2641, 2227,2624, 2219,2618, 2205,2613, 2190,2612, 2176,2613,
            2157,2620, 2140,2636, 2131,2641, 2117,2645, 2077,2649, 2063,2653, 2054,2658, 2043,2667, 2018,2710, 2008,2721, 2000,2727, 1992,2731,
            1977,2734, 1937,2730, 1918,2732, 1905,2738, 1887,2754, 1869,2761, 1854,2761, 1839,2758, 1826,2751, 1816,2741, 1808,2728, 1803,2714,
            1802,2699, 1802,2684, 1807,2671, 1822,2645, 1830,2592, 1837,2578, 1846,2567, 1858,2558, 1895,2542, 1912,2532, 1927,2519, 1960,2481,
            1976,2469, 1999,2461, 2053,2454, 2060,2449, 2071,2436, 2110,2415, 2119,2407, 2119,2404, 2114,2401, 2085,2398, 2066,2393, 2046,2380,
            2020,2371, 2003,2360, 1988,2347, 1982,2336, 1984,2329, 1997,2326, 2037,2325, 2076,2330, 2095,2325, 2111,2314, 2141,2281, 2161,2266,
            2185,2259, 2234,2253, 2257,2244, 2273,2232, 2303,2199, 2319,2187, 2343,2179, 2392,2174, 2415,2165, 2431,2153, 2461,2120, 2504,2095,
            2519,2075, 2529,2053, 2533,2033, 2532,2013, 2526,1994, 2515,1978, 2500,1965, 2474,1950, 2447,1938, 2427,1933, 2408,1934, 2389,1940,
            2371,1949, 2361,1959, 2348,1985, 2327,2007, 2313,2027, 2302,2037, 2289,2043, 2279,2044, 2270,2042, 2261,2039, 2249,2030, 2239,2019,
            2222,1994, 2217,1985, 2215,1976, 2217,1968, 2222,1960, 2247,1929, 2264,1913, 2277,1881, 2287,1864, 2306,1848, 2342,1831, 2352,1821,
            2365,1800, 2371,1786, 2373,1771, 2371,1757, 2360,1723, 2357,1709, 2358,1634, 2362,1615, 2385,1565, 2390,1557, 2418,1538, 2448,1521,
            2453,1515, 2454,1511, 2451,1503, 2423,1467, 2413,1457, 2389,1440, 2373,1416, 2357,1405, 2334,1397, 2327,1393, 2325,1385, 2327,1377,
            2336,1365, 2348,1357, 2363,1353, 2377,1353, 2425,1362, 2434,1367, 2455,1388, 2472,1398, 2487,1400, 2510,1400).Climb(MinDescent))
            .With(Feature.FenceGap, P(2365, 1720));
        zigzag.Clay = true;
        set.Segments.Add(zigzag);
        // El Bosque: el más largo, por el oeste; bosque cerrado (cuidado con los árboles), raíces y drops de roca.
        Segment bosque = Auto(Make("El Bosque", 2.5f,
            2235,3090, 2200,3068, 2166,3027, 2139,3014, 2125,3012, 2116,3015, 2100,3026, 2082,3034, 2064,3049, 2055,3051, 2051,3051, 2040,3043,
            2020,3020, 2005,2997, 1997,2992, 1965,2980, 1949,2969, 1939,2958, 1914,2920, 1897,2902, 1875,2891, 1812,2874, 1795,2864, 1789,2856,
            1783,2843, 1775,2799, 1768,2780, 1756,2765, 1726,2738, 1716,2721, 1713,2712, 1712,2687, 1704,2664, 1702,2646, 1696,2634, 1678,2626,
            1654,2621, 1640,2623, 1617,2632, 1598,2631, 1580,2622, 1555,2598, 1543,2589, 1530,2587, 1513,2593, 1499,2593, 1490,2590, 1463,2572,
            1459,2565, 1464,2543, 1461,2530, 1463,2523, 1469,2517, 1507,2494, 1512,2487, 1512,2483, 1504,2473, 1452,2438, 1446,2432, 1443,2418,
            1441,2373, 1433,2350, 1416,2331, 1376,2302, 1366,2291, 1362,2282, 1360,2272, 1359,2257, 1366,2218, 1363,2204, 1357,2190, 1349,2178,
            1339,2167, 1299,2137, 1296,2130, 1296,2126, 1301,2119, 1325,2102, 1341,2084, 1356,2073, 1374,2050, 1417,2018, 1427,2015, 1441,2016,
            1474,2029, 1488,2030, 1496,2027, 1509,2014, 1536,2003, 1552,1992, 1558,1984, 1563,1970, 1563,1936, 1565,1927, 1584,1905, 1610,1852,
            1610,1838, 1595,1814, 1596,1800, 1608,1773, 1614,1751, 1637,1735, 1648,1719, 1653,1700, 1658,1665, 1663,1651, 1671,1640, 1683,1630,
            1734,1610, 1750,1599, 1763,1584, 1777,1552, 1784,1545, 1804,1530, 1817,1523, 1826,1520, 1836,1519, 1851,1521, 1869,1529, 1888,1550,
            1896,1555, 1915,1559, 1955,1560, 1974,1563, 1993,1569, 2042,1593, 2066,1591, 2090,1595, 2105,1596, 2119,1591, 2126,1586, 2135,1574,
            2142,1555, 2143,1540, 2142,1496, 2138,1482, 2125,1461, 2118,1448, 2115,1433, 2113,1413, 2117,1394, 2139,1362, 2148,1339, 2148,1324,
            2146,1314, 2128,1285, 2123,1266, 2124,1252, 2127,1242, 2132,1234, 2142,1223, 2154,1214, 2168,1208, 2202,1203, 2220,1195, 2234,1182,
            2263,1147, 2275,1139, 2284,1135, 2308,1132, 2328,1133, 2342,1137, 2369,1151, 2383,1153, 2398,1151, 2440,1137, 2480,1135, 2500,1130).Climb(MinDescent))
            .With(Feature.RockDrop, P(1470, 2440))
            .With(Feature.Roots, P(1340, 2190))
            .With(Feature.RockDrop, P(1330, 2110), 1.2f)
            .With(Feature.RockGarden, P(1650, 1690))
            .With(Feature.Roots, P(2050, 1560))
            .With(Feature.RockDrop, P(2140, 1400));
        bosque.Forest = true;
        set.Segments.Add(bosque);
        // Recta final compartida, a fondo, con el salto enorme para hacer trucos y público a los lados.
        Segment recta = Make("Recta final", 5f, 2510,1400, 2500,1130).Climb(MinDescent)
            .With(Feature.BigJump, P(2505, 1290));
        recta.Crowd = true;
        set.Segments.Add(recta);
        set.Segments.Add(Locked(Make("Entrada a meta", 5f, 2500,1130, 2500,1060)));
        set.Segments.Add(Locked(Make("Llegada", 4.5f, 2500,1060, 2460,1040, 2420,990, 2410,920, 2410,880)));
        // Atajos (líneas alternativas, fuera de las rutas: más riesgo, menos tiempo).
        // Atajo del camino: sale de El Bosque y salta el camino de tierra (gap arriesgado).
        set.Segments.Add(Auto(Make("Atajo del camino", 2.2f,
            1563,1936, 1561,1905, 1564,1878, 1570,1850, 1578,1820, 1590,1795, 1608,1773).Climb(MinDescent))
            .With(Feature.RoadGap, P(1566, 1880)));

        set.Routes.Add(new RealMapTrails.Route { Name = "El Bosque", Color = new Color(0.25f, 0.9f, 0.35f), Segments = new[] { SalidaOeste, MiradorOeste, Bosque, Entrada, Llegada } });
        set.Routes.Add(new RealMapTrails.Route { Name = "El Zigzag", Color = new Color(1f, 0.78f, 0.15f), Segments = new[] { SalidaOeste, MiradorOeste, Zigzag, RectaFinal, Entrada, Llegada } });
        set.Routes.Add(new RealMapTrails.Route { Name = "La Cresta", Color = new Color(1f, 0.3f, 0.8f), Segments = new[] { SalidaLoma, MiradorEste, Cresta, RectaFinal, Entrada, Llegada } });
        set.Routes.Add(new RealMapTrails.Route { Name = "La Rápida", Color = new Color(1f, 0.28f, 0.2f), Segments = new[] { SalidaLoma, MiradorEste, Rapida, RectaFinal, Entrada, Llegada } });

        // Puente de madera: La Cresta pasa por arriba de La Rápida.
        set.Bridges.Add(new RealMapTrails.Bridge { Upper = Cresta, Lower = Rapida });
        // Camino de tierra que viene del oeste y termina en una casa de campo (El Bosque lo cruza).
        set.Roads.Add(Make("Camino de tierra", 3f,
            600,1520, 727,1588, 772,1628, 879,1763, 915,1786, 1018,1774, 1071,1783, 1175,1845, 1228,1862, 1276,1868, 1482,1888, 1516,1887,
            1782,1843, 2091,1841));
        // Lomas empinadas entre las rutas (límites): no se ve de una ruta a la otra. Se corren solas al medio.
        set.Ridges.Add(Ridge(60f, 2489,1218, 2361,1240, 2225,1345, 2089,1571, 1989,1762, 1894,1980, 1853,2161, 1826,2388, 1799,2615, 1844,2761, 1998,2783, 2125,2842, 2207,2951));
        set.Ridges.Add(Ridge(65f, 2516,1381, 2566,1662, 2625,1762, 2752,2161, 2670,2411, 2498,2751, 2361,3069, 2293,3273));
        set.Ridges.Add(Ridge(60f, 2883,1508, 3020,1526, 3178,1608, 3246,1935, 3246,2161, 3206,2397, 3110,2706, 3079,2742));
        // Retoques del relieve: la ladera del zigzag más pareja y un paso abierto en la loma al norte de la salida este.
        set.Sculpts.Add(new RealMapTrails.Sculpt { Center = P(1960, 2320), Radius = 420f / (BoxX1 - BoxX0), Amount = 45f });
        set.Sculpts.Add(new RealMapTrails.Sculpt { Lower = true, Center = P(3060, 2310), Radius = 180f / (BoxX1 - BoxX0), Amount = 32f });
        return set;
    }

    private static Segment Make(string name, float halfWidth, params int[] xy)
    {
        return RealMapTrails.Make(name, halfWidth, BoxX0, BoxY0, BoxX1, BoxY1, xy);
    }

    /// <summary>Punto de la imagen (décimas de píxel) en 0–1.</summary>
    private static Vector2 P(int x, int y)
    {
        return new Vector2((x - BoxX0) / (BoxX1 - BoxX0), (y - BoxY0) / (BoxY1 - BoxY0));
    }

    private static RealMapTrails.Ridge Ridge(float height, params int[] xy)
    {
        return new RealMapTrails.Ridge { Height = height, HalfWidth = 55f, Image = RealMapTrails.Points(BoxX0, BoxY0, BoxX1, BoxY1, xy) };
    }

    private static Segment Locked(Segment segment)
    {
        segment.Locked = true;
        return segment;
    }

    private static Segment Auto(Segment segment)
    {
        segment.AutoJumps = true;
        return segment;
    }
}
