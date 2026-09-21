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
    public sealed class PreflightResult
    {
        internal PreflightResult(bool canExport, string userMessage, string technicalDetails)
        {
            CanExport = canExport;
            UserMessage = userMessage ?? string.Empty;
            TechnicalDetails = technicalDetails ?? string.Empty;
        }

        public bool CanExport { get; }
        public string UserMessage { get; }
        public string TechnicalDetails { get; }
    }

    private sealed class PreparedExport
    {
        internal readonly Dictionary<string, byte[]> Replacements =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> SourceCopies =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class PreflightException : InvalidOperationException
    {
        internal PreflightException(string userMessage, string technicalDetails, Exception innerException = null)
            : base(technicalDetails, innerException) => UserMessage = userMessage;

        internal string UserMessage { get; }
    }

    public static PreflightResult Preflight(string[] assetPaths)
    {
        return Evaluate(assetPaths, out PreparedExport _);
    }

    public static void Export(string[] assetPaths, string destination)
    {
        PreflightResult preflight = Evaluate(assetPaths, out PreparedExport prepared);
        if (!preflight.CanExport)
            throw new InvalidOperationException(preflight.TechnicalDetails);

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
            foreach (var original in prepared.SourceCopies)
                if (File.ReadAllText(original.Key) != original.Value)
                    throw new InvalidOperationException("A creator prefab changed during export. Save it and export again: " + original.Key);
            CustomerPackageArchive.Rewrite(nativePackage, convertedPackage, prepared.Replacements);
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

    private static PreflightResult Evaluate(string[] assetPaths, out PreparedExport prepared)
    {
        prepared = null;
        try
        {
            prepared = Prepare(assetPaths);
            return new PreflightResult(true, string.Empty, string.Empty);
        }
        catch (PreflightException exception)
        {
            return new PreflightResult(false, exception.UserMessage, exception.Message);
        }
        catch (Exception exception)
        {
            return new PreflightResult(false,
                "ThreadLight could not verify this export safely. Check the Console for details, then refresh.",
                exception.Message);
        }
    }

    private static PreparedExport Prepare(string[] assetPaths)
    {
        if (assetPaths == null || assetPaths.Length == 0)
            throw Stop("Choose product content before exporting.", "Select product assets before exporting.");
        Dictionary<string, ICustomerExportDocumentConverter> converters;
        try { converters = DiscoverConverters(); }
        catch (Exception exception)
        {
            throw Stop("ThreadLight customer export is unavailable. Check the Console for details, then refresh.",
                exception.Message, exception);
        }

        var prepared = new PreparedExport();
        foreach (string path in assetPaths.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw Stop("Choose files from this project's Assets folder.",
                    "Only selected project assets may be exported: " + (path ?? "<null>"));
            if (AssetDatabase.IsValidFolder(path)) continue;
            string[] dependencies = AssetDatabase.GetDependencies(path, true);
            if (!dependencies.Any(IsCreatorAsset)) continue;
            if (!string.Equals(Path.GetExtension(path), ".prefab", StringComparison.OrdinalIgnoreCase))
                throw Stop("This file contains ThreadLight authoring data, but customer conversion supports " +
                    "prefabs only. Create a customer-ready prefab and select it.\n\n" + path,
                    "Customer conversion currently supports saved prefabs. This asset still requires creator tools: " + path);
            string source = File.ReadAllText(path);
            if (!source.StartsWith("%YAML", StringComparison.Ordinal))
                throw Stop("Set Asset Serialization Mode to Force Text, save the prefab, then refresh.\n\n" + path,
                    "Save this prefab using Unity text serialization before exporting: " + path);
            if (Regex.IsMatch(source, @"(?m)^--- !u!1001 "))
                throw Stop("This prefab contains inherited ThreadLight authoring data that cannot be converted " +
                    "safely. Create a separate unpacked customer prefab, then refresh.\n\n" + path,
                    "This prefab contains nested or variant creator state. Create a separate unpacked customer export prefab first: " + path);

            string converted;
            try { converted = ConvertPrefabText(source, converters); }
            catch (Exception exception)
            {
                throw Stop("This prefab uses ThreadLight authoring data that cannot be converted safely. " +
                    "Update ThreadLight, rebuild the creator prefab, then refresh.\n\n" + path,
                    exception.Message + " Asset: " + path, exception);
            }
            foreach (Match reference in Regex.Matches(converted, @"\{fileID: -?\d+, guid: ([a-f0-9]{32}), type: \d+\}"))
            {
                string dependency = AssetDatabase.GUIDToAssetPath(reference.Groups[1].Value);
                if (!IsCreatorAsset(dependency)) continue;
                throw Stop("This prefab still references ThreadLight authoring content. Finish authoring or " +
                    "copy the required product asset into Assets, then refresh.\n\n" + path,
                    "Customer export still references creator-only content: " + dependency + " in " + path +
                    ". Finish authoring or copy the required product resource into Assets.");
            }
            if (converted == source) continue;
            prepared.Replacements.Add(AssetDatabase.AssetPathToGUID(path) + "/asset",
                new UTF8Encoding(false).GetBytes(converted));
            prepared.SourceCopies.Add(path, source);
        }
        return prepared;
    }

    private static PreflightException Stop(string userMessage, string technicalDetails, Exception innerException = null) =>
        new PreflightException(userMessage, technicalDetails, innerException);

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
