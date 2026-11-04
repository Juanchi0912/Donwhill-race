using System;
using System.Collections.Generic;

/// <summary>
/// Genera las texturas del juego por código (ruido, celdas, texto), sin archivos externos
/// ni paquetes. No depende de UnityEngine: así se puede probar y previsualizar fuera del Editor.
/// Todas las texturas de superficie son "tileables" (se repiten sin costuras).
/// Los colores están en espacio sRGB (como se pintan en un programa de dibujo).
/// </summary>
public static class ProceduralTextures
{
    /// <summary>Imagen RGBA en floats de 0 a 1. La fila 0 es la de abajo, como en Unity.</summary>
    public sealed class Image
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float[] Data;

        public Image(int width, int height)
        {
            Width = width;
            Height = height;
            Data = new float[width * height * 4];
        }

        public void Set(int x, int y, float r, float g, float b, float a = 1f)
        {
            int i = (y * Width + x) * 4;
            Data[i] = Clamp01(r);
            Data[i + 1] = Clamp01(g);
            Data[i + 2] = Clamp01(b);
            Data[i + 3] = Clamp01(a);
        }

        public void Blend(int x, int y, Rgb c, float alpha)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height || alpha <= 0f) return;
            int i = (y * Width + x) * 4;
            Data[i] = Lerp(Data[i], c.R, alpha);
            Data[i + 1] = Lerp(Data[i + 1], c.G, alpha);
            Data[i + 2] = Lerp(Data[i + 2], c.B, alpha);
            Data[i + 3] = Math.Max(Data[i + 3], alpha);
        }
    }

    /// <summary>Color RGB sencillo (sRGB, 0 a 1).</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
        public static Rgb Lerp(Rgb a, Rgb b, float t) => new Rgb(ProceduralTextures.Lerp(a.R, b.R, t), ProceduralTextures.Lerp(a.G, b.G, t), ProceduralTextures.Lerp(a.B, b.B, t));
        public Rgb Scale(float s) => new Rgb(R * s, G * s, B * s);
    }

    // =====================================================================
    // Superficies de la pista
    // =====================================================================

    /// <summary>Camino de tierra con barro: charcos húmedos (brillantes), surcos de ruedas y piedritas.</summary>
    public static void Dirt(int size, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb dry = new Rgb(0.50f, 0.39f, 0.28f);
        Rgb dark = new Rgb(0.33f, 0.25f, 0.17f);
        Rgb wet = new Rgb(0.21f, 0.16f, 0.11f);
        Rgb stone = new Rgb(0.46f, 0.43f, 0.38f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float n = Fbm(u, v, 6, 5, 11);
            float fine = Fbm(u, v, 32, 2, 12);
            float puddle = SmoothStep(0.56f, 0.66f, Fbm(u, v, 3, 3, 13));

            // Surcos a lo largo de la pista (v), deformados con ruido para que no sean rectos.
            float wobble = (Fbm(u, v, 2, 2, 14) - 0.5f) * 0.08f;
            float rut = Math.Max(Gauss(u + wobble, 0.3f, 0.035f), Gauss(u + wobble, 0.7f, 0.035f));

            Cellular(u, v, 14, 15, out float f1, out _, out int cell);
            float pebble = (Hash01(cell, 16) > 0.7f) ? SmoothStep(0.2f, 0.1f, f1) * 0.8f : 0f;

            Rgb c = Rgb.Lerp(dark, dry, SmoothStep(0.3f, 0.7f, n));
            c = c.Scale(0.9f + fine * 0.2f);
            float wetness = Math.Max(puddle, rut * 0.75f);
            c = Rgb.Lerp(c, wet, wetness * 0.85f);
            c = Rgb.Lerp(c, stone.Scale(0.85f + Hash01(cell, 17) * 0.3f), pebble);

            float smooth = Lerp(0.10f, 0.62f, wetness);
            smooth = Lerp(smooth, 0.25f, pebble);
            albedo.Set(x, y, c.R, c.G, c.B, smooth);

            height[y * size + x] = n * 0.6f + fine * 0.15f + pebble * 0.6f - rut * 0.5f - puddle * 0.15f;
        }
        normal = NormalFromHeight(height, size, size, 3f);
    }

    /// <summary>Tierra compactada de las bermas: más arcillosa y con grietas.</summary>
    public static void Berm(int size, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb light = new Rgb(0.55f, 0.40f, 0.26f);
        Rgb dark = new Rgb(0.36f, 0.26f, 0.17f);
        Rgb crack = new Rgb(0.22f, 0.16f, 0.11f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float n = Fbm(u, v, 5, 5, 21);
            Cellular(u, v, 9, 22, out float f1, out float f2, out _);
            float edge = SmoothStep(0.05f, 0.0f, f2 - f1);
            float streak = Fbm(u * 1f, v, 24, 2, 23);

            Rgb c = Rgb.Lerp(dark, light, SmoothStep(0.25f, 0.75f, n));
            c = c.Scale(0.92f + streak * 0.16f);
            c = Rgb.Lerp(c, crack, edge * 0.7f);
            albedo.Set(x, y, c.R, c.G, c.B, 0.14f);
            height[y * size + x] = n * 0.7f - edge * 0.4f + streak * 0.1f;
        }
        normal = NormalFromHeight(height, size, size, 2.5f);
    }

    /// <summary>Suelo de bosque: pasto con parches de hojas secas.</summary>
    public static void ForestFloor(int size, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb grassDark = new Rgb(0.13f, 0.22f, 0.08f);
        Rgb grassLight = new Rgb(0.27f, 0.39f, 0.14f);
        Rgb litter = new Rgb(0.36f, 0.27f, 0.15f);
        Rgb leafRed = new Rgb(0.45f, 0.25f, 0.12f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float n = Fbm(u, v, 4, 4, 31);
            float blades = Fbm(u, v, 48, 2, 32);
            float patches = SmoothStep(0.6f, 0.72f, Fbm(u, v, 5, 3, 33)) * 0.8f;
            Cellular(u, v, 28, 34, out float f1, out _, out int cell);
            float leaf = Hash01(cell, 35) > 0.6f ? SmoothStep(0.25f, 0.12f, f1) : 0f;

            Rgb c = Rgb.Lerp(grassDark, grassLight, SmoothStep(0.3f, 0.75f, n * 0.6f + blades * 0.4f));
            c = Rgb.Lerp(c, litter.Scale(0.85f + blades * 0.3f), patches);
            c = Rgb.Lerp(c, Rgb.Lerp(litter, leafRed, Hash01(cell, 36)), leaf * (0.4f + patches * 0.6f));
            albedo.Set(x, y, c.R, c.G, c.B, 0.06f);
            height[y * size + x] = blades * 0.5f + n * 0.3f + leaf * 0.2f;
        }
        normal = NormalFromHeight(height, size, size, 2f);
    }

    /// <summary>Roca gris con grietas y líquenes.</summary>
    public static void Rock(int size, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb light = new Rgb(0.55f, 0.54f, 0.52f);
        Rgb dark = new Rgb(0.30f, 0.30f, 0.30f);
        Rgb lichen = new Rgb(0.38f, 0.42f, 0.26f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float n = Fbm(u, v, 4, 5, 41);
            Cellular(u, v, 6, 42, out float f1, out float f2, out _);
            float crack = SmoothStep(0.04f, 0.0f, f2 - f1);
            float moss = SmoothStep(0.58f, 0.7f, Fbm(u, v, 5, 3, 43));

            Rgb c = Rgb.Lerp(dark, light, SmoothStep(0.25f, 0.8f, n));
            c = Rgb.Lerp(c, lichen, moss * 0.7f);
            c = Rgb.Lerp(c, dark.Scale(0.5f), crack * 0.8f);
            albedo.Set(x, y, c.R, c.G, c.B, 0.22f);
            height[y * size + x] = n - crack * 0.5f;
        }
        normal = NormalFromHeight(height, size, size, 4f);
    }

    /// <summary>Corteza de pino: vetas verticales (v es la altura del tronco).</summary>
    public static void Bark(int size, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb light = new Rgb(0.34f, 0.24f, 0.16f);
        Rgb dark = new Rgb(0.14f, 0.10f, 0.07f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float ridges = Fbm2(u, v, 10, 2, 4, 51);
            float plates = Fbm(u, v, 6, 3, 52);
            float h = SmoothStep(0.35f, 0.65f, ridges) * 0.7f + plates * 0.3f;
            Rgb c = Rgb.Lerp(dark, light, h);
            albedo.Set(x, y, c.R, c.G, c.B, 0.08f);
            height[y * size + x] = h;
        }
        normal = NormalFromHeight(height, size, size, 5f);
    }

    /// <summary>Follaje: agujas de pino o follaje de hoja ancha, según la paleta.</summary>
    public static void Foliage(int size, bool pine, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb dark = pine ? new Rgb(0.07f, 0.17f, 0.09f) : new Rgb(0.13f, 0.25f, 0.08f);
        Rgb light = pine ? new Rgb(0.17f, 0.31f, 0.15f) : new Rgb(0.30f, 0.44f, 0.15f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float needles = pine ? Fbm2(u, v, 40, 8, 3, 61) : Fbm(u, v, 24, 3, 61);
            float clumps = Fbm(u, v, 4, 3, 62);
            float h = needles * 0.6f + clumps * 0.4f;
            Rgb c = Rgb.Lerp(dark, light, SmoothStep(0.3f, 0.75f, h));
            albedo.Set(x, y, c.R, c.G, c.B, 0.1f);
            height[y * size + x] = h;
        }
        normal = NormalFromHeight(height, size, size, 3f);
    }

    /// <summary>Tablones de madera para las rampas (las juntas cruzan la rampa).</summary>
    public static void WoodPlanks(int size, out Image albedo, out Image normal)
    {
        albedo = new Image(size, size);
        float[] height = new float[size * size];
        Rgb light = new Rgb(0.62f, 0.46f, 0.29f);
        Rgb dark = new Rgb(0.40f, 0.28f, 0.16f);
        const int planks = 8;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            int plank = (int)(v * planks);
            float local = v * planks - plank;
            float gap = SmoothStep(0.06f, 0.0f, Math.Min(local, 1f - local));
            float grain = Fbm2(u + Hash01(plank, 71), v, 2, 24, 3, 72);
            float tone = Hash01(plank, 73);

            Rgb c = Rgb.Lerp(dark, light, 0.4f + grain * 0.4f + tone * 0.2f);
            c = Rgb.Lerp(c, dark.Scale(0.4f), gap);
            albedo.Set(x, y, c.R, c.G, c.B, 0.2f);
            height[y * size + x] = grain * 0.3f - gap;
        }
        normal = NormalFromHeight(height, size, size, 3f);
    }

    // =====================================================================
    // Bici GY RAVEN y corredor
    // =====================================================================

    /// <summary>Pintura de la bici: camuflaje geométrico azul y blanco (brillo en alfa).</summary>
    public static Image BikeCamo(int size)
    {
        Image img = new Image(size, size);
        Rgb[] palette =
        {
            new Rgb(0.08f, 0.33f, 0.78f), // azul rey
            new Rgb(0.08f, 0.33f, 0.78f),
            new Rgb(0.33f, 0.58f, 0.93f), // celeste
            new Rgb(0.04f, 0.11f, 0.32f), // azul marino
            new Rgb(0.90f, 0.93f, 0.97f), // blanco
        };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            Cellular(u, v, 7, 81, out float f1, out float f2, out int cell);
            Rgb c = palette[(int)(Hash01(cell, 82) * palette.Length) % palette.Length];
            // Línea fina oscura entre fragmentos: da el aspecto de calcomanía.
            float edge = SmoothStep(0.012f, 0.0f, f2 - f1);
            c = Rgb.Lerp(c, palette[3], edge * 0.6f);
            img.Set(x, y, c.R, c.G, c.B, 0.65f);
        }
        return img;
    }

    /// <summary>Calcomanía con texto (fondo transparente, letras con borde).</summary>
    public static Image TextDecal(int width, int height, string text, Rgb fill, Rgb outline, float italic)
    {
        Image img = new Image(width, height);
        DrawText(img, text, 0.06f * width, 0.15f * height, 0.88f * width, 0.7f * height, fill, outline, italic, true);
        return img;
    }

    /// <summary>Calcomanía del aro: fondo negro y "GY RAVEN" azul repetido (u da la vuelta al aro).</summary>
    public static Image RimDecal(int width, int height)
    {
        Image img = new Image(width, height);
        Rgb black = new Rgb(0.04f, 0.04f, 0.05f);
        Rgb blue = new Rgb(0.15f, 0.42f, 0.92f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            img.Set(x, y, black.R, black.G, black.B, 0.5f);
        }
        float half = width * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            DrawText(img, "GY RAVEN", i * half + half * 0.12f, height * 0.2f, half * 0.6f, height * 0.6f, blue, black, 0.15f, false);
            // Rayas decorativas después del texto.
            for (int x = (int)(i * half + half * 0.76f); x < (int)(i * half + half * 0.95f); x++)
            for (int y = (int)(height * 0.42f); y < (int)(height * 0.58f); y++)
            {
                img.Blend(x, y, blue, 1f);
            }
        }
        return img;
    }

    /// <summary>Banda de rodadura: goma oscura con algo de polvo.</summary>
    public static Image Tire(int width, int height)
    {
        Image img = new Image(width, height);
        Rgb rubber = new Rgb(0.07f, 0.07f, 0.07f);
        Rgb dust = new Rgb(0.32f, 0.25f, 0.18f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            float d = SmoothStep(0.55f, 0.8f, Fbm(u, v, 8, 3, 91));
            Rgb c = Rgb.Lerp(rubber, dust, d * 0.6f);
            img.Set(x, y, c.R, c.G, c.B, 0.2f);
        }
        return img;
    }

    /// <summary>Jersey naranja con paneles oscuros, textura de malla y salpicaduras de barro.</summary>
    public static Image Jersey(int size)
    {
        Image img = new Image(size, size);
        Rgb orange = new Rgb(0.95f, 0.42f, 0.08f);
        Rgb charcoal = new Rgb(0.18f, 0.16f, 0.15f);
        Rgb mud = new Rgb(0.30f, 0.22f, 0.14f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            // Paneles diagonales oscuros a los costados (u da la vuelta al cuerpo).
            // u = 0 es la espalda: los paneles oscuros quedan a los costados y la espalda naranja.
            float side = Math.Abs(((u * 2f + 0.5f) % 1f) - 0.5f) * 2f;
            float panel = SmoothStep(0.62f, 0.66f, side + (v - 0.5f) * 0.3f);
            float mesh = (((int)(u * 64) + (int)(v * 64)) % 2 == 0) ? 0.97f : 1.03f;

            Rgb c = Rgb.Lerp(orange, charcoal, panel).Scale(mesh);
            float splat = MudSplat(u, v, 0.35f, 93);
            c = Rgb.Lerp(c, mud, splat);
            img.Set(x, y, c.R, c.G, c.B, Lerp(0.18f, 0.35f, splat));
        }
        return img;
    }

    /// <summary>Tela lisa con barro (shorts, rodilleras, guantes).</summary>
    public static Image MuddyFabric(int size, Rgb baseColor, float mudAmount, int seed)
    {
        Image img = new Image(size, size);
        Rgb mud = new Rgb(0.32f, 0.24f, 0.15f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float weave = Fbm(u, v, 32, 2, seed);
            Rgb c = baseColor.Scale(0.92f + weave * 0.16f);
            float splat = MudSplat(u, v, mudAmount, seed + 1);
            c = Rgb.Lerp(c, mud, splat);
            img.Set(x, y, c.R, c.G, c.B, Lerp(0.15f, 0.35f, splat));
        }
        return img;
    }

    /// <summary>Casco integral (mapa latitud-longitud): blanco con franjas azules y parte baja negra.</summary>
    public static Image Helmet(int width, int height)
    {
        Image img = new Image(width, height);
        Rgb white = new Rgb(0.92f, 0.93f, 0.95f);
        Rgb blue = new Rgb(0.12f, 0.38f, 0.85f);
        Rgb navy = new Rgb(0.05f, 0.12f, 0.32f);
        Rgb black = new Rgb(0.06f, 0.06f, 0.07f);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            Rgb c = white;
            // Franjas que van de adelante hacia atrás por arriba del casco.
            float lon = Math.Abs(((u * 4f) % 1f) - 0.5f);
            if (v > 0.55f && lon < 0.18f) c = blue;
            if (v > 0.55f && lon < 0.07f) c = navy;
            if (v < 0.36f) c = black;
            else if (v < 0.42f) c = blue;
            // Ventilaciones arriba.
            Cellular(u, v, 10, 101, out float f1, out _, out int cell);
            if (v > 0.7f && Hash01(cell, 102) > 0.7f && f1 < 0.12f) c = black;
            img.Set(x, y, c.R, c.G, c.B, 0.75f);
        }
        return img;
    }

    /// <summary>Partícula: mancha redonda y suave (blanca, con alfa).</summary>
    public static Image SoftBlob(int size)
    {
        Image img = new Image(size, size);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
            float r = (float)Math.Sqrt(dx * dx + dy * dy);
            float n = Fbm((x + 0.5f) / size, (y + 0.5f) / size, 4, 3, 111);
            float a = SmoothStep(1f, 0.3f, r + (n - 0.5f) * 0.4f);
            img.Set(x, y, 1f, 1f, 1f, a);
        }
        return img;
    }

    /// <summary>Cielo nublado (mapa latitud-longitud para un skybox panorámico).</summary>
    public static Image OvercastSky(int width, int height)
    {
        Image img = new Image(width, height);
        Rgb horizon = new Rgb(0.72f, 0.75f, 0.78f);
        Rgb zenith = new Rgb(0.42f, 0.46f, 0.52f);
        Rgb cloudLight = new Rgb(0.86f, 0.87f, 0.89f);
        Rgb cloudDark = new Rgb(0.36f, 0.38f, 0.42f);
        Rgb ground = new Rgb(0.40f, 0.42f, 0.42f);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            float elevation = v * 2f - 1f; // -1 abajo, 0 horizonte, 1 arriba
            Rgb c;
            if (elevation < 0f)
            {
                c = Rgb.Lerp(horizon, ground, SmoothStep(0f, 0.3f, -elevation));
            }
            else
            {
                c = Rgb.Lerp(horizon, zenith, SmoothStep(0f, 0.8f, elevation));
                // Las nubes se aplastan hacia el horizonte, como en perspectiva.
                float cloudV = (float)Math.Sqrt(elevation);
                float clouds = Fbm2(u, cloudV, 8, 3, 5, 121);
                float detail = Fbm2(u, cloudV, 24, 8, 2, 122);
                float density = SmoothStep(0.3f, 0.62f, clouds * 0.8f + detail * 0.2f);
                Rgb cloud = Rgb.Lerp(cloudDark, cloudLight, SmoothStep(0.3f, 0.8f, clouds + detail * 0.3f));
                c = Rgb.Lerp(c, cloud, density * SmoothStep(0f, 0.08f, elevation));
            }
            img.Set(x, y, c.R, c.G, c.B, 1f);
        }
        return img;
    }

    /// <summary>
    /// Fondo de menú: degradado vertical con un leve viñeteado y algo de ruido
    /// (evita las "bandas" que se ven en los degradados lisos).
    /// </summary>
    public static Image MenuBackground(int width, int height, Rgb bottom, Rgb top)
    {
        Image img = new Image(width, height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = (x + 0.5f) / width, v = (y + 0.5f) / height;
            Rgb c = Rgb.Lerp(bottom, top, SmoothStep(0f, 1f, v));
            float dx = u - 0.5f, dy = v - 0.55f;
            float vignette = 1f - Clamp01((dx * dx + dy * dy) * 1.6f) * 0.45f;
            float grain = (Hash01(x * 7919 + y * 104729, 131) - 0.5f) * 0.02f;
            c = c.Scale(vignette);
            img.Set(x, y, c.R + grain, c.G + grain, c.B + grain, 1f);
        }
        return img;
    }

    /// <summary>Cartel de los arcos: fondo de color con letras blancas.</summary>
    public static Image Banner(int width, int height, string text, Rgb background, bool checkered)
    {
        Image img = new Image(width, height);
        Rgb white = new Rgb(0.95f, 0.95f, 0.95f);
        Rgb black = new Rgb(0.05f, 0.05f, 0.05f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Rgb c = background;
            if (checkered)
            {
                // Bordes a cuadros arriba y abajo, como una bandera de meta.
                int cell = height / 5;
                bool band = y < cell || y >= height - cell;
                if (band) c = ((x / cell + y / cell) % 2 == 0) ? white : black;
            }
            img.Set(x, y, c.R, c.G, c.B, 0.3f);
        }
        float textHeight = height * (checkered ? 0.5f : 0.66f);
        DrawText(img, text, width * 0.2f, (height - textHeight) * 0.5f, width * 0.6f, textHeight, white, black, 0.12f, false);
        return img;
    }

    // =====================================================================
    // Herramientas
    // =====================================================================

    /// <summary>Convierte una altura en mapa normal (convención de Unity: +Y hacia arriba en la textura).</summary>
    public static Image NormalFromHeight(float[] height, int width, int heightPx, float strength)
    {
        Image img = new Image(width, heightPx);
        for (int y = 0; y < heightPx; y++)
        for (int x = 0; x < width; x++)
        {
            float l = height[y * width + (x - 1 + width) % width];
            float r = height[y * width + (x + 1) % width];
            float d = height[((y - 1 + heightPx) % heightPx) * width + x];
            float u = height[((y + 1) % heightPx) * width + x];
            float nx = (l - r) * strength;
            float ny = (d - u) * strength;
            float nz = 1f;
            float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
            img.Set(x, y, nx / len * 0.5f + 0.5f, ny / len * 0.5f + 0.5f, nz / len * 0.5f + 0.5f, 1f);
        }
        return img;
    }

    /// <summary>Salpicaduras de barro, más abundantes abajo (v pequeño).</summary>
    private static float MudSplat(float u, float v, float amount, int seed)
    {
        float blobs = Fbm(u, v, 6, 4, seed);
        float dots;
        Cellular(u, v, 18, seed + 7, out float f1, out _, out int cell);
        dots = Hash01(cell, seed + 8) > 0.85f ? SmoothStep(0.18f, 0.08f, f1) : 0f;
        float bias = (1f - v) * 0.12f;
        float edge = 0.78f - amount * 0.12f - bias;
        float splat = SmoothStep(edge, edge + 0.05f, blobs) * 0.85f;
        return Math.Min(1f, splat + dots * amount * 0.6f);
    }

    // --- Texto con una fuente de píxeles 5×7 (sin depender de archivos de fuentes).
    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
        ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
        ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
        ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
        ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
        ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
        ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
        ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
        ['6'] = new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." },
        ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
        ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
        ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
    };

    /// <summary>
    /// Dibuja texto dentro de un rectángulo (en píxeles, origen abajo a la izquierda).
    /// Las letras se suavizan muestreando la fuente con interpolación, así no se ven pixeladas.
    /// </summary>
    public static void DrawText(Image img, string text, float rectX, float rectY, float rectW, float rectH,
        Rgb fill, Rgb outline, float italic, bool transparentBackground)
    {
        const float advance = 6f; // 5 celdas de letra + 1 de espacio
        float cellsWide = 0f;
        foreach (char ch in text) cellsWide += ch == ' ' ? 3f : advance;
        cellsWide -= 1f;
        float cell = Math.Min(rectH / 7f, rectW / cellsWide);
        float startX = rectX + (rectW - cellsWide * cell) * 0.5f;
        float startY = rectY + (rectH - 7f * cell) * 0.5f;

        int x0 = Math.Max(0, (int)(startX - cell * 2));
        int x1 = Math.Min(img.Width - 1, (int)(startX + cellsWide * cell + cell * 3));
        int y0 = Math.Max(0, (int)(startY - cell));
        int y1 = Math.Min(img.Height - 1, (int)(startY + 8f * cell));

        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            // Coordenadas en celdas de la fuente (fila 0 arriba), con inclinación itálica.
            float gy = 7f - (y + 0.5f - startY) / cell;
            float gx = (x + 0.5f - startX) / cell - (7f - gy) * italic;
            float value = SampleText(text, gx, gy, advance);
            // Umbral bajo: así las diagonales de la fuente (que se tocan solo en una esquina) quedan unidas.
            float body = SmoothStep(0.2f, 0.4f, value);
            float border = SmoothStep(0.02f, 0.13f, value);

            if (border > 0f) img.Blend(x, y, outline, border);
            if (body > 0f) img.Blend(x, y, fill, body);
        }
    }

    private static float SampleText(string text, float gx, float gy, float advance)
    {
        // Interpolación bilineal de la "imagen" de todas las letras puestas en fila.
        float sx = gx - 0.5f, sy = gy - 0.5f;
        int ix = (int)Math.Floor(sx), iy = (int)Math.Floor(sy);
        float fx = sx - ix, fy = sy - iy;
        float a = TextCell(text, ix, iy, advance), b = TextCell(text, ix + 1, iy, advance);
        float c = TextCell(text, ix, iy + 1, advance), d = TextCell(text, ix + 1, iy + 1, advance);
        return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fy);
    }

    private static float TextCell(string text, int cx, int cy, float advance)
    {
        if (cy < 0 || cy > 6 || cx < 0) return 0f;
        int pos = 0;
        foreach (char ch in text)
        {
            int width = ch == ' ' ? 3 : (int)advance;
            if (cx < pos + width)
            {
                int col = cx - pos;
                if (ch == ' ' || col > 4 || !Glyphs.TryGetValue(char.ToUpperInvariant(ch), out string[] rows)) return 0f;
                return rows[cy][col] == '#' ? 1f : 0f;
            }
            pos += width;
        }
        return 0f;
    }

    // --- Ruido tileable.

    /// <summary>Ruido fractal tileable en [0,1]. freq = repeticiones por textura en u y v.</summary>
    public static float Fbm(float u, float v, int freq, int octaves, int seed) => Fbm2(u, v, freq, freq, octaves, seed);

    /// <summary>Ruido fractal tileable con frecuencia distinta en u y v (vetas, agujas).</summary>
    public static float Fbm2(float u, float v, int freqU, int freqV, int octaves, int seed)
    {
        float sum = 0f, amp = 1f, total = 0f;
        int fu = freqU, fv = freqV;
        for (int o = 0; o < octaves; o++)
        {
            sum += Perlin(u * fu, v * fv, fu, fv, seed + o * 131) * amp;
            total += amp;
            amp *= 0.5f;
            fu *= 2;
            fv *= 2;
        }
        return Clamp01(sum / total * 0.75f + 0.5f);
    }

    /// <summary>Ruido de gradiente (Perlin) periódico; resultado aproximado en [-1,1].</summary>
    public static float Perlin(float x, float y, int periodX, int periodY, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        float fx = x - x0, fy = y - y0;
        int ax = Mod(x0, periodX), bx = Mod(x0 + 1, periodX);
        int ay = Mod(y0, periodY), by = Mod(y0 + 1, periodY);

        float n00 = Gradient(Hash(ax, ay, seed), fx, fy);
        float n10 = Gradient(Hash(bx, ay, seed), fx - 1f, fy);
        float n01 = Gradient(Hash(ax, by, seed), fx, fy - 1f);
        float n11 = Gradient(Hash(bx, by, seed), fx - 1f, fy - 1f);

        float sx = Fade(fx), sy = Fade(fy);
        return Lerp(Lerp(n00, n10, sx), Lerp(n01, n11, sx), sy) * 1.4f;
    }

    /// <summary>Ruido celular (Worley) tileable: distancia al punto más cercano y al segundo.</summary>
    public static void Cellular(float u, float v, int cells, int seed, out float f1, out float f2, out int cellId)
    {
        float x = u * cells, y = v * cells;
        int cx = (int)Math.Floor(x), cy = (int)Math.Floor(y);
        f1 = 10f;
        f2 = 10f;
        cellId = 0;
        for (int oy = -1; oy <= 1; oy++)
        for (int ox = -1; ox <= 1; ox++)
        {
            int gx = cx + ox, gy = cy + oy;
            int wx = Mod(gx, cells), wy = Mod(gy, cells);
            int h = Hash(wx, wy, seed);
            float px = gx + Hash01(h, 1), py = gy + Hash01(h, 2);
            float dx = px - x, dy = py - y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < f1)
            {
                f2 = f1;
                f1 = d;
                cellId = h;
            }
            else if (d < f2)
            {
                f2 = d;
            }
        }
    }

    private static float Gradient(int hash, float x, float y)
    {
        switch (hash & 7)
        {
            case 0: return x + y;
            case 1: return -x + y;
            case 2: return x - y;
            case 3: return -x - y;
            case 4: return x;
            case 5: return -x;
            case 6: return y;
            default: return -y;
        }
    }

    private static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = seed * 374761393 + x * 668265263 + y * 1274126177;
            h = (h ^ (h >> 13)) * 1103515245;
            return h ^ (h >> 16);
        }
    }

    /// <summary>Número pseudoaleatorio en [0,1) a partir de un hash y una semilla.</summary>
    public static float Hash01(int value, int seed)
    {
        unchecked
        {
            int h = Hash(value, seed, 977);
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;
    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    private static float Gauss(float x, float center, float width) => (float)Math.Exp(-((x - center) * (x - center)) / (2f * width * width));
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
