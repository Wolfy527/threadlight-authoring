namespace Threadlight.Authoring.Editor
{
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>AssetDatabase discovery and normalized project paths for exports.</summary>
internal static class ThreadlightComponentsAssetExportCatalog
{
    public static HashSet<string> CollectAssets(IEnumerable<string> roots)
    {
        HashSet<string> output = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in NormalizeExisting(roots))
        {
            output.Add(path);
            if (!AssetDatabase.IsValidFolder(path)) continue;
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { path }))
            {
                string child = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
                if (IsAssetsPath(child)) output.Add(child);
            }
        }
        return output;
    }

    public static List<string> SelectedProjectPaths()
    {
        List<string> paths = new List<string>();
        foreach (UnityEngine.Object selected in Selection.objects)
        {
            string path = NormalizePath(AssetDatabase.GetAssetPath(selected));
            if (!IsAssetsPath(path) && selected is GameObject gameObject)
                path = NormalizePath(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject));
            if (IsAssetsPath(path)) paths.Add(path);
        }
        return NormalizeRoots(paths);
    }

    public static List<string> NormalizeRoots(IEnumerable<string> roots) =>
        (roots ?? Array.Empty<string>()).Select(NormalizePath).Where(IsAssetsPath)
        .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool AssetPathExists(string path) => AssetDatabase.IsValidFolder(path) ||
        AssetDatabase.LoadMainAssetAtPath(path) != null;
    public static bool IsAssetsPath(string path) => path == "Assets" ||
        !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/", StringComparison.Ordinal);
    public static string NormalizePath(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');

    private static IEnumerable<string> NormalizeExisting(IEnumerable<string> paths) =>
        NormalizeRoots(paths).Where(AssetPathExists);
}
}
