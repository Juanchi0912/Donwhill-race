using System;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>
/// Lee heightmaps en PNG (gris, gris+alfa, RGB o RGBA; de 8 o 16 bits) directamente del archivo.
/// No pasa por el importador de texturas de Unity porque este comprime la imagen y pierde la precisión de 16 bits.
/// </summary>
public static class PngHeightmapReader
{
    private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>Lee solo la cabecera: tamaño y bits por canal (sirve para avisar en la ventana sin cargar todo).</summary>
    public static bool TryReadInfo(string path, out int width, out int height, out int bitDepth)
    {
        width = height = bitDepth = 0;
        try
        {
            byte[] head = new byte[33];
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Read(head, 0, head.Length) < head.Length) return false;
            }
            if (!HasSignature(head) || Encoding.ASCII.GetString(head, 12, 4) != "IHDR") return false;
            width = ReadInt(head, 16);
            height = ReadInt(head, 20);
            bitDepth = head[24];
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Devuelve las alturas de 0 a 1 como [fila, columna], con la fila 0 arriba de la imagen (el norte).
    /// Si la imagen tiene color, usa el canal rojo (los heightmaps en color traen el mismo valor en los tres).
    /// </summary>
    public static float[,] Read(string path, out int bitDepth)
    {
        byte[] file = File.ReadAllBytes(path);
        if (!HasSignature(file)) throw new InvalidDataException("El archivo no es un PNG.");

        int width = 0, height = 0, colorType = -1, interlace = 0;
        bitDepth = 0;
        MemoryStream compressed = new MemoryStream();
        int pos = 8;
        while (pos + 8 <= file.Length)
        {
            int length = ReadInt(file, pos);
            string type = Encoding.ASCII.GetString(file, pos + 4, 4);
            int body = pos + 8;
            if (type == "IHDR")
            {
                width = ReadInt(file, body);
                height = ReadInt(file, body + 4);
                bitDepth = file[body + 8];
                colorType = file[body + 9];
                interlace = file[body + 12];
            }
            else if (type == "IDAT")
            {
                compressed.Write(file, body, length);
            }
            else if (type == "IEND")
            {
                break;
            }
            pos = body + length + 4; // +4 del CRC.
        }

        int channels;
        switch (colorType)
        {
            case 0: channels = 1; break;
            case 2: channels = 3; break;
            case 4: channels = 2; break;
            case 6: channels = 4; break;
            default: throw new InvalidDataException("PNG con paleta de colores: exporta el heightmap en escala de grises.");
        }
        if (bitDepth != 8 && bitDepth != 16) throw new InvalidDataException("El PNG debe ser de 8 o 16 bits por canal.");
        if (interlace != 0) throw new InvalidDataException("El PNG está entrelazado: expórtalo sin entrelazado.");

        int bytesPerPixel = channels * bitDepth / 8;
        int stride = width * bytesPerPixel;
        byte[] raw = new byte[(stride + 1) * height];
        compressed.Position = 2; // Se salta la cabecera zlib; DeflateStream lee el resto.
        using (DeflateStream inflate = new DeflateStream(compressed, CompressionMode.Decompress))
        {
            int read = 0;
            while (read < raw.Length)
            {
                int n = inflate.Read(raw, read, raw.Length - read);
                if (n <= 0) break;
                read += n;
            }
            if (read < raw.Length) throw new InvalidDataException("El PNG está incompleto o dañado.");
        }

        float[,] heights = new float[height, width];
        float scale = bitDepth == 16 ? 1f / 65535f : 1f / 255f;
        byte[] line = new byte[stride];
        byte[] previous = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * (stride + 1);
            Buffer.BlockCopy(raw, rowStart + 1, line, 0, stride);
            Unfilter(raw[rowStart], line, previous, bytesPerPixel);
            for (int x = 0; x < width; x++)
            {
                int o = x * bytesPerPixel;
                int value = bitDepth == 16 ? (line[o] << 8) | line[o + 1] : line[o];
                heights[y, x] = value * scale;
            }
            byte[] swap = previous;
            previous = line;
            line = swap;
        }
        return heights;
    }

    /// <summary>Deshace el filtro de cada fila del PNG (cada fila se guarda como diferencia con sus vecinos).</summary>
    private static void Unfilter(int filter, byte[] line, byte[] previous, int bytesPerPixel)
    {
        for (int i = 0; i < line.Length; i++)
        {
            int a = i >= bytesPerPixel ? line[i - bytesPerPixel] : 0;
            int b = previous[i];
            int c = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
            int add;
            switch (filter)
            {
                case 0: add = 0; break;
                case 1: add = a; break;
                case 2: add = b; break;
                case 3: add = (a + b) >> 1; break;
                case 4: add = Paeth(a, b, c); break;
                default: throw new InvalidDataException("El PNG usa un filtro desconocido.");
            }
            line[i] = (byte)(line[i] + add);
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private static bool HasSignature(byte[] data)
    {
        if (data.Length < Signature.Length) return false;
        for (int i = 0; i < Signature.Length; i++)
        {
            if (data[i] != Signature[i]) return false;
        }
        return true;
    }

    private static int ReadInt(byte[] data, int offset)
    {
        return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }
}
