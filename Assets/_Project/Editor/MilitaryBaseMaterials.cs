using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

/// <summary>
/// Monta os materiais do cenário (MilitaryBase.fbx) no padrão da Glock: URP Lit com BaseColor, Normal e
/// Mask (R metálico, G AO, A smoothness), mais Emission quando existe. As texturas seguem o nome do
/// material: M_&lt;nome&gt; usa T_&lt;nome&gt;_BaseColor/_Normal/_Mask/_Emission.png da pasta Textures.
///
/// Também acerta o import das texturas (normal map, sRGB da máscara, ASTC no Android) e do FBX
/// (normais importadas e tangentes MikkTSpace, que é como os normal maps foram assados) e troca os
/// materiais do FBX pelos criados aqui.
///
/// Pode rodar de novo: atualiza os materiais que já existem, mas ajustes feitos à mão nas propriedades
/// que este script define se perdem.
/// </summary>
internal static class MilitaryBaseMaterials
{
    private const string Root = "Assets/_Project/Arts/Models/MilitaryBase";
    private const string ModelPath = Root + "/MilitaryBase.fbx";
    private const string TexturePath = Root + "/Textures";
    private const string MaterialPath = Root + "/Materials";

    // Vidros de verdade (os "Glasses" são óculos, ficam opacos).
    private static readonly HashSet<string> Transparent = new HashSet<string>
    {
        "M_Glass_01a", "M_Glass_01d", "M_ReinforcedGlass_01a",
    };

    // Grade da luminária: transparência no alpha da BaseColor.
    private static readonly HashSet<string> AlphaClip = new HashSet<string> { "M_Hanging_Light_LP" };

    // Vidro aramado: a opacidade vem do alpha da BaseColor (vidro limpo quase transparente, manchas mais
    // opacas, arame opaco), em vez do alpha fixo dos outros vidros.
    private static readonly HashSet<string> TextureAlpha = new HashSet<string> { "M_ReinforcedGlass_01a" };

    // Fica longe ou no teto: 1024 no Quest basta.
    private static readonly string[] FarAway =
    {
        "Ceiling_Concrete", "Pipe", "Wires_Pipes", "Hanging_Light", "Counter", "Lane_Divider", "Security_Camera",
        "Roller_Door",
    };

    private const float GlassAlpha = 0.25f;
    private const float DefaultSmoothness = 0.2f;

    [MenuItem("Tools/Ambiente/Montar materiais do MilitaryBase")]
    private static void Build()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError($"Não achei {ModelPath}.");
            return;
        }

        var names = new List<string>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is Material material && !names.Contains(material.name))
            {
                names.Add(material.name);
            }
        }

        foreach (Object remapped in importer.GetExternalObjectMap().Values)
        {
            if (remapped is Material material && !names.Contains(material.name))
            {
                names.Add(material.name);
            }
        }

        ConfigureTextures(names);

        if (!AssetDatabase.IsValidFolder(MaterialPath))
        {
            AssetDatabase.CreateFolder(Root, "Materials");
        }

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var missing = new List<string>();
        foreach (string name in names)
        {
            Material material = BuildMaterial(name, lit, missing);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
        }

        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();

        Debug.Log($"MilitaryBase: {names.Count} materiais montados em {MaterialPath}." +
            (missing.Count > 0 ? $" Sem textura (ficaram cinza): {string.Join(", ", missing)}." : ""));
    }

    private static Material BuildMaterial(string name, Shader lit, List<string> missing)
    {
        string path = $"{MaterialPath}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(lit);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = lit;
        string baseName = TextureBase(name);
        Texture2D baseColor = Load(baseName, "BaseColor");
        Texture2D normal = Load(baseName, "Normal");
        Texture2D mask = Load(baseName, "Mask");
        Texture2D emission = Load(baseName, "Emission");
        if (baseColor == null)
        {
            missing.Add(name);
        }

        material.SetTexture("_BaseMap", baseColor);
        material.SetColor("_BaseColor", Transparent.Contains(name) && !TextureAlpha.Contains(name)
            ? new Color(1f, 1f, 1f, GlassAlpha)
            : Color.white);
        material.SetTexture("_BumpMap", normal);
        material.SetFloat("_BumpScale", 1f);

        // Com a máscara, metálico e smoothness vêm dela (o slider vira multiplicador, como na Glock).
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetTexture("_OcclusionMap", mask);
        material.SetFloat("_Metallic", mask != null ? 1f : 0f);
        material.SetFloat("_Smoothness", mask != null ? 1f : Transparent.Contains(name) ? 0.9f : DefaultSmoothness);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.SetFloat("_OcclusionStrength", 1f);

        material.SetTexture("_EmissionMap", emission);
        material.SetColor("_EmissionColor", emission != null ? Color.white : Color.black);
        material.globalIlluminationFlags = emission != null
            ? MaterialGlobalIlluminationFlags.BakedEmissive
            : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

        material.SetFloat("_Surface", Transparent.Contains(name) ? 1f : 0f);
        material.SetFloat("_Blend", 0f);
        // Sem isso o reflexo do vidro não é multiplicado pelo alpha: a parte lisa (que devia ser a mais
        // transparente) reflete com força total e parece fosca, e a áspera parece transparente. Desligado,
        // fica como no Blender.
        material.SetFloat("_BlendModePreserveSpecular", 0f);
        material.SetFloat("_AlphaClip", AlphaClip.Contains(name) ? 1f : 0f);
        material.SetFloat("_Cutoff", 0.5f);

        // Mesmo caminho do inspetor do URP: acerta keywords, blend, ZWrite e fila de render.
        if (emission != null)
        {
            material.EnableKeyword("_EMISSION");
        }

        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTextures(List<string> names)
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string name in names)
            {
                string baseName = TextureBase(name);
                int maxSize = 2048;
                foreach (string far in FarAway)
                {
                    if (baseName.Contains(far))
                    {
                        maxSize = 1024;
                    }
                }

                foreach (string kind in new[] { "BaseColor", "Normal", "Mask", "Emission" })
                {
                    string path = $"{TexturePath}/T_{baseName}_{kind}.png";
                    var texture = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (texture == null)
                    {
                        continue;
                    }

                    texture.textureType = kind == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.sRGBTexture = kind == "BaseColor" || kind == "Emission";
                    bool baseAlpha = AlphaClip.Contains(name) || TextureAlpha.Contains(name);
                    texture.alphaSource = kind == "Mask" || baseAlpha && kind == "BaseColor"
                        ? TextureImporterAlphaSource.FromInput
                        : TextureImporterAlphaSource.None;
                    texture.maxTextureSize = 2048;
                    texture.mipmapEnabled = true;
                    texture.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                    {
                        name = "Android",
                        overridden = true,
                        maxTextureSize = maxSize,
                        format = TextureImporterFormat.ASTC_6x6,
                        textureCompression = TextureImporterCompression.Compressed,
                    });
                    texture.SaveAndReimport();
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    private static string TextureBase(string materialName)
    {
        return materialName.StartsWith("M_") ? materialName.Substring(2) : materialName;
    }

    private static Texture2D Load(string baseName, string kind)
    {
        string path = $"{TexturePath}/T_{baseName}_{kind}.png";
        return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
    }
}
