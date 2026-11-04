using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using SN = System.Numerics;

/// <summary>
/// Convierte las texturas y mallas generadas por código en assets de Unity (PNG, .mat, .asset)
/// y crea los materiales de URP. Si un asset ya existe, lo actualiza en lugar de duplicarlo,
/// así las referencias de la escena no se rompen al reconstruirla.
/// </summary>
public static class VisualAssetFactory
{
    public const string TexturesFolder = "Assets/Art/Textures/Prototipo";
    public const string MaterialsFolder = "Assets/Art/Materials/Prototipo";
    public const string ModelsFolder = "Assets/Art/Models/Prototipo";

    public enum TextureKind
    {
        /// <summary>Color sin alfa.</summary>
        Color,
        /// <summary>Color con el brillo (smoothness) guardado en el alfa.</summary>
        ColorSmoothness,
        /// <summary>Calcomanía: el alfa es transparencia (recorte).</summary>
        Decal,
        /// <summary>Mapa normal.</summary>
        Normal,
        /// <summary>Cielo panorámico: se repite en horizontal y no en vertical.</summary>
        Sky
    }

    // =====================================================================
    // Texturas
    // =====================================================================

    public static Texture2D SaveTexture(ProceduralTextures.Image image, string name, TextureKind kind, int maxSize = 1024, int anisoLevel = 1)
    {
        EnsureFolder(TexturesFolder);
        string path = TexturesFolder + "/" + name + ".png";

        Texture2D temp = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[image.Width * image.Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            int j = i * 4;
            pixels[i] = new Color32(ToByte(image.Data[j]), ToByte(image.Data[j + 1]), ToByte(image.Data[j + 2]),
                kind == TextureKind.Color || kind == TextureKind.Normal || kind == TextureKind.Sky ? (byte)255 : ToByte(image.Data[j + 3]));
        }
        temp.SetPixels32(pixels);
        File.WriteAllBytes(ToFullPath(path), temp.EncodeToPNG());
        Object.DestroyImmediate(temp);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = kind == TextureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = kind != TextureKind.Normal;
        importer.alphaSource = kind == TextureKind.ColorSmoothness || kind == TextureKind.Decal
            ? TextureImporterAlphaSource.FromInput
            : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = kind == TextureKind.Decal;
        importer.mipmapEnabled = true;
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = kind == TextureKind.Sky || kind == TextureKind.Decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        importer.anisoLevel = anisoLevel;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // =====================================================================
    // Materiales (URP)
    // =====================================================================

    /// <summary>
    /// Material URP/Lit opaco, con textura, mapa normal y brillo opcionales.
    /// Si el brillo viene del alfa de la textura, "smoothness" lo multiplica (1 = usar el de la textura).
    /// </summary>
    public static Material Lit(string name, Color tint, Texture2D albedo = null, Texture2D normal = null,
        float smoothness = 0.2f, bool smoothnessFromAlbedoAlpha = false, float metallic = 0f, float normalScale = 1f)
    {
        Material m = GetOrCreateMaterial(name, "Universal Render Pipeline/Lit");
        ResetSurface(m);
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", tint);
        m.color = tint;
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);

        SetKeyword(m, "_NORMALMAP", normal != null);
        m.SetTexture("_BumpMap", normal);
        m.SetFloat("_BumpScale", normalScale);

        // Brillo desde el alfa del color: así el barro mojado brilla y la tierra seca no.
        m.SetFloat("_SmoothnessTextureChannel", smoothnessFromAlbedoAlpha ? 1f : 0f);
        SetKeyword(m, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A", smoothnessFromAlbedoAlpha && albedo != null);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Material URP/Lit con recorte por alfa (calcomanías, follaje fino).</summary>
    public static Material Cutout(string name, Texture2D albedo, Color tint, float smoothness, bool doubleSided)
    {
        Material m = GetOrCreateMaterial(name, "Universal Render Pipeline/Lit");
        ResetSurface(m);
        m.SetTexture("_BaseMap", albedo);
        m.SetColor("_BaseColor", tint);
        m.color = tint;
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        m.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
        m.doubleSidedGI = doubleSided;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Material de partículas transparente (polvo y barro).</summary>
    public static Material Particles(string name, Texture2D texture, Color tint)
    {
        Material m = GetOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit");
        m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Skybox panorámico (latitud-longitud) a partir de una textura de cielo.</summary>
    public static Material PanoramicSky(string name, Texture2D sky, float exposure)
    {
        Material m = GetOrCreateMaterial(name, "Skybox/Panoramic");
        m.SetTexture("_MainTex", sky);
        m.SetFloat("_Mapping", 1f);   // Latitud-longitud
        m.SetFloat("_ImageType", 0f); // 360°
        m.SetFloat("_Exposure", exposure);
        m.SetFloat("_Rotation", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material GetOrCreateMaterial(string name, string shaderName)
    {
        EnsureFolder(MaterialsFolder);
        string path = MaterialsFolder + "/" + name + ".mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning("VisualAssetFactory: no se encontró el shader '" + shaderName + "'; se usa URP/Lit.");
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader)
        {
            m.shader = shader;
        }
        return m;
    }

    /// <summary>Deja el material como opaco normal (por si antes fue de recorte).</summary>
    private static void ResetSurface(Material m)
    {
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = (int)RenderQueue.Geometry;
        m.SetFloat("_Cull", (float)CullMode.Back);
        m.doubleSidedGI = false;
    }

    private static void SetKeyword(Material m, string keyword, bool enabled)
    {
        if (enabled) m.EnableKeyword(keyword);
        else m.DisableKeyword(keyword);
    }

    // =====================================================================
    // Mallas
    // =====================================================================

    /// <summary>Convierte un MeshBuilder en Mesh de Unity y la guarda como asset.</summary>
    public static Mesh SaveMesh(MeshBuilder builder, string name)
    {
        return SaveMeshAsset(ToMesh(builder, name), ModelsFolder + "/" + name + ".asset");
    }

    public static Mesh ToMesh(MeshBuilder builder, string name)
    {
        int count = builder.Vertices.Count;
        Vector3[] vertices = new Vector3[count];
        Vector2[] uvs = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            SN.Vector3 v = builder.Vertices[i];
            vertices[i] = new Vector3(v.X, v.Y, v.Z);
            uvs[i] = new Vector2(builder.Uvs[i].X, builder.Uvs[i].Y);
        }

        Mesh mesh = new Mesh { name = name };
        if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = builder.Submeshes.Count;
        for (int s = 0; s < builder.Submeshes.Count; s++) mesh.SetTriangles(builder.Submeshes[s], s);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Guarda una malla como asset; si ya existe, la sobrescribe conservando la referencia.</summary>
    public static Mesh SaveMeshAsset(Mesh mesh, string path)
    {
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        EditorUtility.CopySerialized(mesh, existing);
        existing.name = Path.GetFileNameWithoutExtension(path);
        Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    // =====================================================================
    // Utilidades
    // =====================================================================

    public static Quaternion ToUnity(SN.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    public static Vector3 ToUnity(SN.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string ToFullPath(string assetPath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }
}
