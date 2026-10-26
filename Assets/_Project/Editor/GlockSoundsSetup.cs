using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Liga os sons da pasta Arts/Sounds na Glock: <see cref="GlockSounds"/> na raiz (ferrolho, carregador,
/// peças saindo) e <see cref="ImpactSound"/> nas peças com corpo (arma, carregador, ferrolho, mola,
/// cano). Acerta o import dos sons (mono, descomprimido ao carregar) e marca a mesa da cena aberta
/// como <see cref="ImpactSurface"/>.
///
/// Pode rodar de novo: refaz as fontes e as referências.
/// </summary>
internal static class GlockSoundsSetup
{
    private const string SoundsPath = "Assets/_Project/Arts/Sounds";
    private const string TableName = "ENV_Metal_Desk_01a";

    [MenuItem("Tools/Glock/Montar sons")]
    private static void Build()
    {
        ConfigureImport();
        GlockGripSetup.EditPrefab(BuildPrefab);

        GameObject table = GameObject.Find(TableName);
        if (table != null && table.GetComponent<ImpactSurface>() == null)
        {
            Undo.AddComponent<ImpactSurface>(table);
            EditorSceneManager.MarkSceneDirty(table.scene);
            EditorSceneManager.SaveScene(table.scene);
        }

        Debug.Log("Sons da Glock montados." + (table == null ? $" Não achei a mesa '{TableName}' na cena aberta." : ""));
    }

    private static AudioClip Clip(string name)
    {
        string path = Directory.GetFiles(SoundsPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name && !p.EndsWith(".meta"));
        if (path == null)
        {
            Debug.LogWarning($"Som não encontrado: {name}");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/'));
    }

    private static void ConfigureImport()
    {
        foreach (string path in Directory.GetFiles(SoundsPath).Where(p => !p.EndsWith(".meta")))
        {
            if (AssetImporter.GetAtPath(path.Replace('\\', '/')) is not AudioImporter importer)
            {
                continue;
            }

            // Efeitos curtos: mono (som 3D), descomprimidos ao carregar, para tocar sem atraso.
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }

    private static AudioSource Source(Transform target)
    {
        var source = target.GetComponent<AudioSource>();
        if (source == null)
        {
            source = target.gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 0.4f;
        source.maxDistance = 10f;
        return source;
    }

    private static void Impact(Transform target, AudioClip onTable, AudioClip onFloor, float volume = 1f)
    {
        var impact = target.GetComponent<ImpactSound>();
        if (impact == null)
        {
            impact = target.gameObject.AddComponent<ImpactSound>();
        }

        var data = new SerializedObject(impact);
        data.FindProperty("_source").objectReferenceValue = Source(target);
        data.FindProperty("_onTable").objectReferenceValue = onTable;
        data.FindProperty("_onFloor").objectReferenceValue = onFloor;
        data.FindProperty("_volume").floatValue = volume;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildPrefab(Transform root)
    {
        Transform slide = root.Find("SlideGroup");
        Transform magazine = root.Find("MagwellAxis/Magazine");
        Transform spring = root.Find("RecoilSpringAssembly");
        Transform barrel = root.Find("BarrelDriver");

        AudioClip other = Clip("Drop Other Objects on Table");
        Impact(root, Clip("Drop Pistol On Table"), Clip("Drop pistol"));
        Impact(magazine, Clip("Drop Magazine On Table"), null);
        Impact(slide, other, null);
        Impact(spring, other, null, 0.6f);
        Impact(barrel, other, null, 0.8f);

        var sounds = root.GetComponent<GlockSounds>();
        if (sounds == null)
        {
            sounds = root.gameObject.AddComponent<GlockSounds>();
        }

        var data = new SerializedObject(sounds);
        data.FindProperty("_source").objectReferenceValue = Source(root);
        data.FindProperty("_slideSource").objectReferenceValue = Source(slide);
        data.FindProperty("_slide").objectReferenceValue = root.GetComponent<GlockSlide>();
        data.FindProperty("_magazine").objectReferenceValue = magazine.GetComponent<GlockMagazine>();
        data.FindProperty("_trigger").objectReferenceValue = root.GetComponent<GlockTrigger>();
        data.FindProperty("_dryFire").objectReferenceValue = Clip("Dryfire");
        data.FindProperty("_spring").objectReferenceValue = spring.GetComponent<GlockSlidePart>();
        data.FindProperty("_barrel").objectReferenceValue = barrel.GetComponent<GlockSlidePart>();
        data.FindProperty("_slidePull").objectReferenceValue = Clip("Pistol_Reload_Type_1_Slide_Pull");
        data.FindProperty("_slideRelease").objectReferenceValue = Clip("Pistol_Reload_Type_1_Slide_Release");
        data.FindProperty("_magazineEject").objectReferenceValue = Clip("Pistol_Reload_Type_7_Eject_Mag");
        data.FindProperty("_slideRemoved").objectReferenceValue = Clip("Remove Slide From Gun");
        data.FindProperty("_partRemoved").objectReferenceValue = Clip("Barrel_Removal");
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
