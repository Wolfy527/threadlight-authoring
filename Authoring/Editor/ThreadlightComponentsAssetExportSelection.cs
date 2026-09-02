namespace Threadlight.Authoring.Editor
{
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>UI-independent inclusion and expansion state for one export.</summary>
internal sealed class ThreadlightComponentsAssetExportSelection
{
    internal enum AssetKind { Dependency, Product, Bootstrapper }
    internal enum CheckState { None, Partial, All }

    internal sealed class Asset
    {
        public string Path;
        public AssetKind Kind;
    }

    internal sealed class Node
    {
        public string Name, Path;
        public bool IsFolder;
        public Asset Value;
        public readonly List<Node> Children = new List<Node>();
        public int AssetCount, SelectedAssetCount;
        public AssetKind HighestAssetKind;
        public bool ContainsBootstrapper, ContainsOnlyBootstrapper;
    }

    private readonly Dictionary<string, Asset> assets =
        new Dictionary<string, Asset>(StringComparer.Ordinal);
    private readonly HashSet<string> excluded = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
    private Node builtRoot;

    public int ExcludedCount => assets.Values.Count(asset =>
        asset.Kind != AssetKind.Bootstrapper && excluded.Contains(asset.Path));

    public void ClearAssets() { assets.Clear(); builtRoot = null; }

    public void ClearExclusions()
    {
        excluded.Clear();
        if (builtRoot != null) Summarize(builtRoot);
    }

    public void Add(string path, AssetKind kind)
    {
        builtRoot = null;
        if (assets.TryGetValue(path, out Asset asset))
        { if (kind > asset.Kind) asset.Kind = kind; return; }
        assets.Add(path, new Asset { Path = path, Kind = kind });
    }

    public Node BuildTree(Func<string, bool> isFolder)
    {
        Node root = new Node { Name = "Assets", Path = "Assets", IsFolder = true };
        Dictionary<string, Node> nodes = new Dictionary<string, Node>(StringComparer.Ordinal)
            { [root.Path] = root };
        foreach (Asset asset in assets.Values.OrderBy(value => value.Path, StringComparer.OrdinalIgnoreCase))
        {
            string[] segments = asset.Path.Split('/');
            string path = segments[0];
            Node parent = root;
            for (int index = 1; index < segments.Length; index++)
            {
                path += "/" + segments[index];
                if (!nodes.TryGetValue(path, out Node node))
                {
                    node = new Node { Name = segments[index], Path = path,
                        IsFolder = index < segments.Length - 1 || isFolder(path) };
                    nodes.Add(path, node); parent.Children.Add(node);
                }
                parent = node;
            }
            parent.Value = asset;
        }
        Sort(root); Summarize(root); return builtRoot = root;
    }

    public bool IsExpanded(string path) => expanded.Contains(path);
    public void ToggleExpanded(string path) { if (!expanded.Remove(path)) expanded.Add(path); }
    public void SetExpanded(string path, bool value)
    {
        if (value) expanded.Add(path); else expanded.Remove(path);
    }

    public CheckState GetCheckState(Node node) => node == null || node.SelectedAssetCount == 0
        ? CheckState.None : node.SelectedAssetCount == node.AssetCount ? CheckState.All : CheckState.Partial;

    public void ToggleSelected(Node node)
    {
        bool select = GetCheckState(node) != CheckState.All;
        foreach (Asset asset in EnumerateAssets(node))
            if (asset.Kind != AssetKind.Bootstrapper)
            { if (select) excluded.Remove(asset.Path); else excluded.Add(asset.Path); }
        if (builtRoot != null) Summarize(builtRoot);
    }

    public int CountSelected(AssetKind kind) => assets.Values.Count(asset =>
        asset.Kind == kind && IsSelected(asset));
    public List<string> SelectedAssets(AssetKind kind) => assets.Values.Where(asset =>
        asset.Kind == kind && IsSelected(asset)).Select(asset => asset.Path).ToList();
    public List<string> SelectedPaths() => assets.Values.Where(IsSelected).Select(asset => asset.Path).ToList();

    public static IEnumerable<Asset> EnumerateAssets(Node node)
    {
        if (node?.Value != null) yield return node.Value;
        if (node == null) yield break;
        foreach (Node child in node.Children)
        foreach (Asset asset in EnumerateAssets(child)) yield return asset;
    }

    public static AssetKind HighestKind(Node node) => node?.HighestAssetKind ?? AssetKind.Dependency;
    public static bool ContainsKind(Node node, AssetKind kind) => kind == AssetKind.Bootstrapper
        ? node?.ContainsBootstrapper ?? false : EnumerateAssets(node).Any(asset => asset.Kind == kind);
    public static bool ContainsOnlyKind(Node node, AssetKind kind) => kind == AssetKind.Bootstrapper
        ? node?.ContainsOnlyBootstrapper ?? false : EnumerateAssets(node).Any() &&
            EnumerateAssets(node).All(asset => asset.Kind == kind);

    private bool IsSelected(Asset asset) => asset.Kind == AssetKind.Bootstrapper || !excluded.Contains(asset.Path);

    private void Summarize(Node node)
    {
        node.AssetCount = node.Value == null ? 0 : 1;
        node.SelectedAssetCount = node.Value != null && IsSelected(node.Value) ? 1 : 0;
        node.HighestAssetKind = node.Value?.Kind ?? AssetKind.Dependency;
        node.ContainsBootstrapper = node.Value?.Kind == AssetKind.Bootstrapper;
        bool nonBootstrapper = node.Value != null && !node.ContainsBootstrapper;
        foreach (Node child in node.Children)
        {
            Summarize(child);
            node.AssetCount += child.AssetCount;
            node.SelectedAssetCount += child.SelectedAssetCount;
            if (child.HighestAssetKind > node.HighestAssetKind) node.HighestAssetKind = child.HighestAssetKind;
            node.ContainsBootstrapper |= child.ContainsBootstrapper;
            nonBootstrapper |= child.AssetCount > 0 && !child.ContainsOnlyBootstrapper;
        }
        node.ContainsOnlyBootstrapper = node.AssetCount > 0 && node.ContainsBootstrapper && !nonBootstrapper;
    }

    private static void Sort(Node node)
    {
        node.Children.Sort((left, right) => left.IsFolder == right.IsFolder
            ? StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name) : left.IsFolder ? -1 : 1);
        foreach (Node child in node.Children) Sort(child);
    }
}
}
