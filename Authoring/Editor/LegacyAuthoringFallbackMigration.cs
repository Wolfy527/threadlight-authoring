namespace Threadlight.Authoring.Editor
{
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Quarantines the owned pre-Threadlight fallback package when the managed
/// Authoring package is present. The backup remains recoverable outside
/// Unity's compilation path, and unrelated folders are never claimed.
/// </summary>
[InitializeOnLoad]
internal static class LegacyAuthoringFallbackMigration
{
    private const string LegacyFallbackPath =
        "Assets/Prefab Components/Fallback";
    private const string LegacyPackageName =
        "com.wolfy527.prefab-components.fallback";

    private static readonly string[] RequiredAssemblyPaths =
    {
        "Live Mirroring/Editor/Wolfy.PropTools.Customer.LiveMirroring.Editor.asmdef",
        "Shared/Authoring/Runtime/Wolfy.PropTools.Customer.Authoring.asmdef",
        "Shared/Editor UI/Wolfy.PropTools.EditorUI.asmdef"
    };

    [Serializable]
    private sealed class PackageManifest
    {
        public string name;
        public string version;
    }

    static LegacyAuthoringFallbackMigration()
    {
        EditorApplication.delayCall += TryQuarantine;
    }

    private static void TryQuarantine()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;

        string fallbackRoot = Path.GetFullPath(
            Path.Combine(projectRoot, LegacyFallbackPath));
        if (!Directory.Exists(fallbackRoot))
            return;

        if (!IsRecognizedFallback(fallbackRoot, out string version))
        {
            Debug.LogWarning(
                "[ThreadLight Authoring] A folder exists at the legacy fallback " +
                "location, but it is not the recognized Prefab Components " +
                "fallback package. It was left unchanged:\n" + LegacyFallbackPath);
            return;
        }

        string backupRoot = Path.Combine(
            projectRoot,
            "Library",
            "Threadlight",
            "Legacy Package Backups",
            "Prefab Components Authoring Fallback",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
            Guid.NewGuid().ToString("N"));
        string backupPackage = Path.Combine(backupRoot, "Fallback");
        string fallbackMeta = fallbackRoot + ".meta";

        try
        {
            Directory.CreateDirectory(backupRoot);
            Directory.Move(fallbackRoot, backupPackage);
            if (File.Exists(fallbackMeta))
                File.Move(fallbackMeta, backupPackage + ".meta");

            Debug.Log(
                "[ThreadLight Authoring] Moved the obsolete Prefab Components " +
                "authoring fallback out of Assets. ThreadLight packages are now " +
                "authoritative. A recoverable backup was kept at:\n" +
                backupPackage + " (fallback " + version + ")");
            EditorApplication.delayCall += () =>
                AssetDatabase.Refresh(ImportAssetOptions.Default);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ThreadLight Authoring] Could not quarantine the recognized " +
                "legacy fallback. It was left in place for recovery.\n" + exception);
        }
    }

    private static bool IsRecognizedFallback(
        string fallbackRoot,
        out string version)
    {
        version = null;
        string manifestPath = Path.Combine(fallbackRoot, "package.json");
        if (!File.Exists(manifestPath))
            return false;

        PackageManifest manifest;
        try
        {
            manifest = JsonUtility.FromJson<PackageManifest>(
                File.ReadAllText(manifestPath));
        }
        catch
        {
            return false;
        }

        if (manifest == null ||
            !string.Equals(
                manifest.name,
                LegacyPackageName,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(manifest.version))
        {
            return false;
        }

        for (int index = 0; index < RequiredAssemblyPaths.Length; index++)
        {
            string relativePath = RequiredAssemblyPaths[index].Replace(
                '/',
                Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(fallbackRoot, relativePath)))
                return false;
        }

        version = manifest.version;
        return true;
    }
}
}
