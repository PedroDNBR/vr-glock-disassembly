using UnityEditor;
using UnityEngine;

/// <summary>
/// Atalho para o "Fix All" da Project Setup Tool da Meta nas duas plataformas usadas:
/// Android (Quest) e Standalone (Quest Link no editor).
/// </summary>
internal static class MetaProjectSetupMenu
{
    [MenuItem("Tools/Glock/Meta: Fix All (Android + PC)")]
    private static async void FixAll()
    {
        await OVRProjectSetup.FixAllAsync(BuildTargetGroup.Android);
        await OVRProjectSetup.FixAllAsync(BuildTargetGroup.Standalone);
        Debug.Log("[Glock] Fix All da Meta concluído para Android e Standalone.");
    }
}
