using UnityEngine;

/// <summary>
/// Retoques del relieve antes de tallar los senderos (solo usa UnityEngine, se puede probar fuera de Unity):
/// suavizar zonas con lomitas, bajar una loma para abrir un paso, e inclinar todo el mapa hacia el norte
/// para que las rutas bajen siempre (en el juego no gusta tener que subir pedaleando).
/// </summary>
public static class TerrainSculptor
{
    /// <summary>"heights" en metros [z, x]; el recorte y el tamaño son los del mapa (para ubicar los retoques).</summary>
    public static void Apply(float[,] heights, RealMapTrails.Set set, Vector2 cropX, Vector2 cropZ, float sizeX, float sizeZ, float imageAspect)
    {
        if (set == null) return;
        int res = heights.GetLength(0);
        float metersPerSample = Mathf.Max(sizeX, sizeZ) / (res - 1);
        foreach (RealMapTrails.Sculpt sculpt in set.Sculpts)
        {
            float[,] soft = null;
            if (!sculpt.Lower)
            {
                soft = (float[,])heights.Clone();
                Smooth(soft, Mathf.Max(1, Mathf.RoundToInt(sculpt.Amount / metersPerSample)));
            }
            for (int z = 0; z < res; z++)
            {
                float iy = 1f - Mathf.Lerp(cropZ.x, cropZ.y, z / (float)(res - 1));
                for (int x = 0; x < res; x++)
                {
                    float ix = Mathf.Lerp(cropX.x, cropX.y, x / (float)(res - 1));
                    float dx = ix - sculpt.Center.x, dy = (iy - sculpt.Center.y) * imageAspect;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1e-5f, sculpt.Radius);
                    if (d >= 1f) continue;
                    if (sculpt.Lower)
                    {
                        float c = Mathf.Cos(d * Mathf.PI * 0.5f);
                        heights[z, x] -= sculpt.Amount * c * c;
                    }
                    else
                    {
                        heights[z, x] = Mathf.Lerp(heights[z, x], soft[z, x], 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d)));
                    }
                }
            }
        }
        if (Mathf.Abs(set.SouthRise) > 0.01f)
        {
            for (int z = 0; z < res; z++)
            {
                float north = Mathf.Lerp(cropZ.x, cropZ.y, z / (float)(res - 1));
                float rise = set.SouthRise * (1f - north);
                for (int x = 0; x < res; x++) heights[z, x] += rise;
            }
        }
    }

    /// <summary>Tres pasadas de promedio móvil (parecido a un desenfoque gaussiano).</summary>
    public static void Smooth(float[,] heights, int radius)
    {
        if (radius <= 0) return;
        int n = heights.GetLength(0);
        float[] line = new float[n], blurred = new float[n];
        for (int pass = 0; pass < 3; pass++)
        {
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++) line[x] = heights[z, x];
                BoxBlurLine(line, blurred, radius);
                for (int x = 0; x < n; x++) heights[z, x] = blurred[x];
            }
            for (int x = 0; x < n; x++)
            {
                for (int z = 0; z < n; z++) line[z] = heights[z, x];
                BoxBlurLine(line, blurred, radius);
                for (int z = 0; z < n; z++) heights[z, x] = blurred[z];
            }
        }
    }

    private static void BoxBlurLine(float[] source, float[] result, int radius)
    {
        int n = source.Length;
        float sum = 0f;
        for (int k = -radius; k <= radius; k++) sum += source[Mathf.Clamp(k, 0, n - 1)];
        float inverse = 1f / (radius * 2 + 1);
        for (int i = 0; i < n; i++)
        {
            result[i] = sum * inverse;
            sum += source[Mathf.Min(i + radius + 1, n - 1)] - source[Mathf.Max(i - radius, 0)];
        }
    }
}
