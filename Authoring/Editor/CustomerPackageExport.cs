namespace Threadlight.Authoring.Editor
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

/// <summary>
/// Converts supported creator documents inside a temporary Unity package.
/// Source assets, their GUIDs, file IDs, and project imports remain untouched.
/// </summary>
public static class CustomerPackageExport
{
    public static void Export(string[] assetPaths, string destination)
    {
        if (assetPaths == null || assetPaths.Length == 0)
            throw new ArgumentException("Select product assets before exporting.");
        var converters = DiscoverConverters();
        var replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var sourceCopies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in assetPaths.Distinct(StringComparer.Ordinal))
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("Only selected project assets may be exported: " + path);
            if (AssetDatabase.IsValidFolder(path)) continue;
            string[] dependencies = AssetDatabase.GetDependencies(path, true);
            bool creatorDependency = dependencies.Any(IsCreatorAsset);
            if (!creatorDependency) continue;
            if (Path.GetExtension(path) != ".prefab")
                throw new InvalidOperationException("Customer conversion currently supports saved prefabs. " +
                    "This asset still requires creator tools: " + path);
            string source = File.ReadAllText(path);
            if (!source.StartsWith("%YAML", StringComparison.Ordinal))
                throw new InvalidOperationException("Save this prefab using Unity text serialization before exporting: " + path);
            // Nested/variant overrides can replace schema fields on inherited
            // documents. Do not silently emit an unconverted override contract.
            if (Regex.IsMatch(source, @"(?m)^--- !u!1001 "))
                throw new InvalidOperationException("This prefab contains nested or variant creator state. " +
                    "Create a separate unpacked customer export prefab first: " + path);
            string converted = ConvertPrefabText(source, converters);
            foreach (Match reference in Regex.Matches(converted, @"\{fileID: -?\d+, guid: ([a-f0-9]{32}), type: \d+\}"))
            {
                string dependency = AssetDatabase.GUIDToAssetPath(reference.Groups[1].Value);
                if (IsCreatorAsset(dependency))
                    throw new InvalidOperationException("Customer export still references creator-only content: " +
                        dependency + " in " + path + ". Finish authoring or copy the required product resource into Assets.");
            }
            if (converted != source)
            {
                replacements.Add(AssetDatabase.AssetPathToGUID(path) + "/asset", new UTF8Encoding(false).GetBytes(converted));
                sourceCopies.Add(path, source);
            }
        }

        string fullDestination = Path.GetFullPath(destination);
        string directory = Path.GetDirectoryName(fullDestination);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        string token = ".threadlight-export-" + Guid.NewGuid().ToString("N");
        string nativePackage = Path.Combine(directory, token + ".unitypackage");
        string convertedPackage = Path.Combine(directory, token + ".tmp");
        try
        {
            // Default is synchronous: conversion must finish before a package
            // replaces any existing customer deliverable.
            AssetDatabase.ExportPackage(assetPaths, nativePackage, ExportPackageOptions.Default);
            foreach (var original in sourceCopies)
                if (File.ReadAllText(original.Key) != original.Value)
                    throw new InvalidOperationException("A creator prefab changed during export. Save it and export again: " + original.Key);
            CustomerPackageArchive.Rewrite(nativePackage, convertedPackage, replacements);
            if (File.Exists(fullDestination)) File.Replace(convertedPackage, fullDestination, null);
            else File.Move(convertedPackage, fullDestination);
        }
        finally
        {
            if (File.Exists(nativePackage)) File.Delete(nativePackage);
            if (File.Exists(convertedPackage)) File.Delete(convertedPackage);
        }
    }

    public static string ConvertPrefabText(string source) => ConvertPrefabText(source, DiscoverConverters());

    private static string ConvertPrefabText(string source,
        Dictionary<string, ICustomerExportDocumentConverter> converters)
    {
        string[] documents = Regex.Split(source, @"(?m)(?=^--- !u!)");
        for (int i = 0; i < documents.Length; i++)
        {
            Match script = Regex.Match(documents[i], @"(?m)^  m_Script: \{fileID: 11500000, guid: ([a-f0-9]{32}), type: 3\}\r?$");
            if (!script.Success || !converters.TryGetValue(script.Groups[1].Value, out var converter)) continue;
            if (!documents[i].StartsWith("--- !u!114 ", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsupported creator component document type.");
            var document = new CustomerExportDocument(documents[i]);
            converter.Convert(document);
            document.Set("m_Script", "  m_Script: {fileID: 11500000, guid: " + converter.CustomerScriptGuid + ", type: 3}");
            document.Set("m_EditorClassIdentifier", "  m_EditorClassIdentifier:");
            documents[i] = document.ToString();
        }
        return string.Concat(documents);
    }

    private static Dictionary<string, ICustomerExportDocumentConverter> DiscoverConverters()
    {
        var result = new Dictionary<string, ICustomerExportDocumentConverter>(StringComparer.Ordinal);
        foreach (Type type in TypeCache.GetTypesDerivedFrom<ICustomerExportDocumentConverter>().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!type.IsPublic || type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException("Invalid customer export converter: " + type.FullName);
            var converter = (ICustomerExportDocumentConverter)Activator.CreateInstance(type);
            if (!Regex.IsMatch(converter.CreatorScriptGuid ?? "", @"\A[a-f0-9]{32}\z") ||
                !Regex.IsMatch(converter.CustomerScriptGuid ?? "", @"\A[a-f0-9]{32}\z") ||
                result.ContainsKey(converter.CreatorScriptGuid))
                throw new InvalidOperationException("Invalid or ambiguous customer export script contract: " + type.FullName);
            result.Add(converter.CreatorScriptGuid, converter);
        }
        return result;
    }

    private static bool IsCreatorAsset(string path) => !string.IsNullOrEmpty(path) &&
        (path.StartsWith("Packages/com.wolfyvr.threadlight.authoring/", StringComparison.Ordinal) ||
         path.StartsWith("Packages/com.wolfyvr.threadlight.builder/", StringComparison.Ordinal) ||
         path.StartsWith("Packages/com.wolfyvr.threadlight.mirroring/", StringComparison.Ordinal) ||
         path.StartsWith("Packages/com.wolfyvr.threadlight.development/", StringComparison.Ordinal));
}
}
