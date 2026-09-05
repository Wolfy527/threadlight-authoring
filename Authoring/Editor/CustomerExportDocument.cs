namespace Threadlight.Authoring.Editor
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Explicit serialized contracts, discovered without customer assembly dependencies.</summary>
public interface ICustomerExportDocumentConverter
{
    string CreatorScriptGuid { get; }
    string CustomerScriptGuid { get; }
    void Convert(CustomerExportDocument document);
}

/// <summary>
/// A Unity text-serialization document. Retains object IDs and reference text;
/// only explicitly supported component fields may be translated.
/// </summary>
public sealed class CustomerExportDocument
{
    private readonly string prefix;
    private readonly List<string> order = new List<string>();
    private readonly Dictionary<string, string> fields = new Dictionary<string, string>();

    public CustomerExportDocument(string text)
    {
        MatchCollection matches = Regex.Matches(text, @"(?m)^  ([A-Za-z_][A-Za-z_0-9]*):");
        if (matches.Count == 0) throw new InvalidOperationException("Unsupported serialized component document.");
        prefix = text.Substring(0, matches[0].Index);
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            string key = match.Groups[1].Value;
            int end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            fields.Add(key, text.Substring(match.Index, end - match.Index));
            order.Add(key);
        }
    }

    public string Get(string name) => fields.TryGetValue(name, out string value)
        ? value : throw new InvalidOperationException("Missing customer export field: " + name);

    public void Set(string name, string value)
    {
        if (!fields.ContainsKey(name)) order.Add(name);
        fields[name] = value.EndsWith("\n", StringComparison.Ordinal) ? value : value + "\n";
    }

    public void RequireVersion(string name, int supported)
    {
        if (Get(name).Trim() != name + ": " + supported)
            throw new InvalidOperationException("Customer export requires " + name + " " + supported +
                ". Update Threadlight and rebuild the creator prefab before exporting.");
    }

    public void RetainFields(string supported, string creatorOnly = "")
    {
        var keep = new HashSet<string>(supported.Split(' '));
        var omit = new HashSet<string>(creatorOnly.Split(' '));
        foreach (string name in order.ToArray())
        {
            if (name.StartsWith("m_", StringComparison.Ordinal) || keep.Contains(name)) continue;
            if (!omit.Contains(name))
                throw new InvalidOperationException("Unsupported customer export field: " + name);
            fields.Remove(name);
            order.Remove(name);
        }
    }

    public override string ToString() => prefix + string.Concat(order.Select(name => fields[name]));
}

public sealed class CustomerHierarchyExportConverter : ICustomerExportDocumentConverter
{
    public string CreatorScriptGuid => "5a703ae49e0f42b5ab96022d38ffb070";
    public string CustomerScriptGuid => "0bc840ca6f774dfc91cc06af65dc5b45";
    public void Convert(CustomerExportDocument document) => document.RetainFields(
        "ownerId moduleId stableId role createdByBuilder");
}

public sealed class CustomerEditorOnlyExportConverter : ICustomerExportDocumentConverter
{
    public string CreatorScriptGuid => "5badee5c0bd9488a85308c387aa4f7cf";
    public string CustomerScriptGuid => "6a339ada66db0524bb16d5ed1fbe64bc";
    public void Convert(CustomerExportDocument document) => document.RetainFields("");
}

public sealed class CustomerTargetExportConverter : ICustomerExportDocumentConverter
{
    public string CreatorScriptGuid => "88059d1b004a4e808a64c1b05ac05716";
    public string CustomerScriptGuid => "48742d3549a555842844b99523feab8f";
    public void Convert(CustomerExportDocument document) => document.RetainFields(
        "stableId role displayName ownerId moduleId removeGeneratedObject createdByBuilder");
}

public sealed class CustomerSnapshotExportConverter : ICustomerExportDocumentConverter
{
    public string CreatorScriptGuid => "918e1d248f4f41f39a1354d3b5e84daf";
    public string CustomerScriptGuid => "5d8f4687c2084716afeb3da11a1b050d";
    public void Convert(CustomerExportDocument document)
    {
        document.RequireVersion("prefabSchema", 2);
        document.RetainFields("prefabId prefabSchema builderDataVersion builderPackageVersion " +
            "builderState objectReferences builderOwnedPaths snapshotFingerprint");
    }
}
}
