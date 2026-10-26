using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta a caixa de desmuniciamento (ClearingBox.fbx) na cena aberta: material URP Lit no padrão da
/// Glock (BaseColor, Normal e Mask com R metálico, G AO e A smoothness), import das texturas (ASTC no
/// Android), colisor de malha, <see cref="ImpactSurface"/> (quedas nela tocam o som da mesa) e a área
/// segura do disparo a seco (SafeZone) dentro da caixa, em cima da areia.
///
/// Pode rodar de novo: atualiza o material e o colisor e mantém a posição da caixa se ela já estiver na cena.
/// </summary>
internal static class ClearingBoxSetup
{
    private const string Root = "Assets/_Project/Arts/Models/ClearingBox";
    private const string ModelPath = Root + "/ClearingBox.fbx";
    private const string MaterialPath = Root + "/M_ClearingBox.mat";
    private const string InstanceName = "ClearingBox";

    // Posição na cena de teste: à direita do jogador, perto de onde ficava a SafeZone, virada para ele.
    private static readonly Vector3 DefaultPosition = new Vector3(1.0f, 0f, -0.3f);

    // Vão em cima da areia, no espaço da caixa (a frente da caixa é +Z).
    private static readonly Vector3 SafeZoneCenter = new Vector3(0f, 0.6f, 0f);
    private static readonly Vector3 SafeZoneSize = new Vector3(0.46f, 0.2f, 0.46f);

    [MenuItem("Tools/Ambiente/Montar caixa de desmuniciamento na cena aberta")]
    private static void Build()
    {
        Material material = BuildMaterial();
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is Material source)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            }
        }

        importer.SaveAndReimport();

        GameObject box = GameObject.Find(InstanceName);
        if (box == null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            box = (GameObject)PrefabUtility.InstantiatePrefab(model);
            box.name = InstanceName;
            Undo.RegisterCreatedObjectUndo(box, InstanceName);
            box.transform.position = DefaultPosition;
            // A frente (+Z) para o jogador, que fica na origem.
            Vector3 toPlayer = -DefaultPosition;
            toPlayer.y = 0f;
            box.transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        box.isStatic = true;
        MeshFilter filter = box.GetComponentInChildren<MeshFilter>();
        GameObject meshObject = filter.gameObject;
        var collider = meshObject.GetComponent<MeshCollider>();
        if (collider == null)
        {
            collider = Undo.AddComponent<MeshCollider>(meshObject);
        }

        collider.sharedMesh = filter.sharedMesh;
        if (meshObject.GetComponent<ImpactSurface>() == null)
        {
            Undo.AddComponent<ImpactSurface>(meshObject);
        }

        // A área segura vira o vão da caixa, sem render (a caixa já mostra para onde apontar).
        GameObject safe = GameObject.Find("SafeZone");
        if (safe != null)
        {
            Undo.RecordObject(safe.transform, "SafeZone");
            safe.transform.SetParent(box.transform, false);
            safe.transform.localPosition = SafeZoneCenter;
            safe.transform.localRotation = Quaternion.identity;
            safe.transform.localScale = SafeZoneSize;
            var renderer = safe.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        EditorSceneManager.MarkSceneDirty(box.scene);
        EditorSceneManager.SaveScene(box.scene);
        Debug.Log("Caixa de desmuniciamento montada" + (safe == null ? " (não achei a SafeZone)." : ", com a SafeZone dentro dela."));
    }

    private static Material BuildMaterial()
    {
        Texture2D baseColor = Texture("BaseColor", true, false);
        Texture2D normal = Texture("Normal", false, true);
        Texture2D mask = Texture("Mask", false, false);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", baseColor);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", normal);
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetTexture("_OcclusionMap", mask);
        material.SetFloat("_Metallic", 1f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_OcclusionStrength", 1f);
        BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Texture2D Texture(string kind, bool srgb, bool normalMap)
    {
        string path = $"{Root}/Textures/T_ClearingBox_{kind}.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = kind == "Mask" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.maxTextureSize = 2048;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 1024,
            format = TextureImporterFormat.ASTC_6x6,
            textureCompression = TextureImporterCompression.Compressed,
        });
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
