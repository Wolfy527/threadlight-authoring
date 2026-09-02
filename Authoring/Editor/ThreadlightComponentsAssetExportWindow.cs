namespace Threadlight.Authoring.Editor
{
using Threadlight.EditorUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using CheckState = ThreadlightComponentsAssetExportSelection.CheckState;
using ExportKind = ThreadlightComponentsAssetExportSelection.AssetKind;
using ExportNode = ThreadlightComponentsAssetExportSelection.Node;

/// <summary>Creator-facing package exporter with an ownership-guarded bootstrapper.</summary>
public sealed class ThreadlightComponentsAssetExportWindow : EditorWindow
{
    private const string StyleSheetPath =
        "Packages/com.wolfyvr.threadlight.authoring/Authoring/Editor/ThreadlightComponentsAssetExportWindow.uss";
    private const string StackedClass = "wolfy-export-stacked";
    [SerializeField] private List<string> productRoots = new List<string>();
    [SerializeField] private string bootstrapFolderPath = ThreadlightComponentsBootstrapExportUtility.DefaultFolderPath;
    [SerializeField] private bool includeBootstrapper = true;
    [SerializeField] private bool includeDependencies = true;

    private readonly ThreadlightComponentsAssetExportSelection selection = new ThreadlightComponentsAssetExportSelection();
    private readonly HashSet<string> initializedTopLevelExpansion = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<TreeItem> visibleItems = new List<TreeItem>();
    private ScrollView rootList;
    private ListView tree;
    private Label summary, contentsSummary;
    private VisualElement errorHost;
    private ObjectField addRootField;
    private Button exportButton;
    private ThreadlightEditorTooltipLayer tooltipLayer;
    private string errorMessage, installerPath;

    private readonly struct TreeItem
    {
        internal readonly ExportNode Node;
        internal readonly int Depth;
        internal TreeItem(ExportNode node, int depth) { Node = node; Depth = depth; }
    }

    private sealed class TreeRow
    {
        internal readonly VisualElement Root = new VisualElement();
        internal readonly Button Arrow = new Button(), Check = new Button();
        internal readonly Image Icon = new Image { scaleMode = ScaleMode.ScaleToFit };
        internal readonly Label Name = new Label(), Badge = new Label();
        internal ThreadlightComponentsAssetExportWindow Owner;
        internal ExportNode Node;
        internal Color RestingColor;
        internal Action RefreshInteraction;

        internal TreeRow()
        {
            Root.userData = this;
            Root.AddToClassList("wolfy-export-tree-row");
            Arrow.AddToClassList("wolfy-export-tree-arrow");
            Check.AddToClassList("wolfy-export-tree-check");
            Icon.AddToClassList("wolfy-export-tree-icon");
            ThreadlightEditorElements.StyleOpticalIcon(Icon, 16f, 14f);
            Name.AddToClassList("wolfy-export-tree-name");
            Badge.AddToClassList("wolfy-export-tree-badge");
            Root.Add(Arrow); Root.Add(Check); Root.Add(Icon); Root.Add(Name); Root.Add(Badge);
            Arrow.clicked += () => Owner?.ToggleExpanded(Node);
            Check.clicked += () => Owner?.ToggleSelected(Node);
            ThreadlightEditorElements.RegisterInteractionState(Root, (hovered, _) =>
                Root.style.backgroundColor = hovered
                    ? Color.Lerp(RestingColor, ThreadlightEditorTheme.WorkspaceExportAccent, .12f)
                    : RestingColor, out RefreshInteraction);
        }
    }

    [MenuItem("Tools/ThreadLight/Export Asset Package...")]
    public static void OpenFromMenu() => Open(
        ThreadlightComponentsAssetExportCatalog.SelectedProjectPaths(),
        ThreadlightComponentsBootstrapExportUtility.DefaultFolderPath, true);

    public static void Open(IEnumerable<string> suggestedProductRoots,
        string temporaryBootstrapFolderPath, bool shouldIncludeBootstrapper)
    {
        ThreadlightComponentsAssetExportWindow window = GetWindow<ThreadlightComponentsAssetExportWindow>();
        window.titleContent = new GUIContent("Export Asset Package");
        window.minSize = new Vector2(440f, 480f);
        window.productRoots = ThreadlightComponentsAssetExportCatalog.NormalizeRoots(suggestedProductRoots);
        foreach (string path in ThreadlightComponentsAssetExportCatalog.SelectedProjectPaths())
            if (!window.productRoots.Contains(path)) window.productRoots.Add(path);
        window.productRoots.Sort(StringComparer.OrdinalIgnoreCase);
        window.bootstrapFolderPath = string.IsNullOrWhiteSpace(temporaryBootstrapFolderPath)
            ? ThreadlightComponentsBootstrapExportUtility.DefaultFolderPath : temporaryBootstrapFolderPath;
        window.includeBootstrapper = shouldIncludeBootstrapper;
        window.selection.ClearExclusions();
        window.Show();
        window.BuildView(true);
    }

    public void CreateGUI() => BuildView(false);
    private void OnDisable() { tooltipLayer?.Dispose(); tooltipLayer = null; }

    private void BuildView(bool prepareBootstrapper)
    {
        VisualElement root = rootVisualElement;
        tooltipLayer?.Dispose();
        root.Clear();
        StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
        if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        root.AddToClassList("wolfy-export-window");
        root.style.backgroundColor = ThreadlightEditorTheme.Background;
        root.Add(ThreadlightEditorElements.CreateAuroraAtmosphere(root));
        tooltipLayer = new ThreadlightEditorTooltipLayer(root);
        VisualElement banner = ThreadlightEditorElements.CreateInspectorBanner(
            "Export Asset Package",
            "Choose the product content to package. The owned ThreadLight Components installer is included automatically.",
            ThreadlightEditorTheme.WorkspaceExportAccent);
        banner.AddToClassList("wolfy-export-banner");
        root.Add(banner);

        VisualElement content = new VisualElement();
        content.AddToClassList("wolfy-export-content");
        content.Add(CreateSources());
        errorHost = Element("wolfy-export-error");
        content.Add(errorHost);
        VisualElement header = Element("wolfy-export-contents-header");
        Label title = new Label("Package Contents");
        title.AddToClassList("wolfy-export-contents-title");
        title.style.color = ThreadlightEditorTheme.Text;
        contentsSummary = new Label();
        contentsSummary.AddToClassList("wolfy-export-contents-summary");
        contentsSummary.style.color = ThreadlightEditorTheme.TextMuted;
        header.Add(title); header.Add(contentsSummary); content.Add(header);
        tree = new ListView(visibleItems, 23f, MakeTreeRow, BindTreeRow)
        {
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.FixedHeight
        };
        tree.AddToClassList("wolfy-export-tree");
        tree.style.backgroundColor = ThreadlightEditorTheme.PanelInset;
        ThreadlightEditorElements.SetBorderColor(tree, ThreadlightEditorTheme.BorderSoft);
        content.Add(tree);
        root.Add(content);
        root.Add(CreateFooter());
        RefreshAssets(prepareBootstrapper);
    }

    private VisualElement CreateSources()
    {
        VisualElement section = ThreadlightEditorElements.CreatePageSection(
            "Product Source",
            "Add the product folders or files to package. The current Project selection is used when the field is empty.",
            ThreadlightEditorTheme.WorkspaceExportAccent, out VisualElement content);
        section.AddToClassList("wolfy-export-fixed");
        rootList = new ScrollView(ScrollViewMode.Vertical);
        rootList.AddToClassList("wolfy-export-roots");
        rootList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        ThreadlightEditorElements.StyleOverlayScrollbar(rootList, ThreadlightEditorTheme.WorkspaceExportAccent);
        content.Add(rootList);
        RebuildRoots();

        VisualElement chooser = Element("wolfy-export-chooser");
        addRootField = new ObjectField { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };
        addRootField.AddToClassList("wolfy-export-root-field");
        ThreadlightEditorElements.StyleField(addRootField,
            () => ThreadlightEditorTheme.WorkspaceExportAccent,
            () => ThreadlightEditorTheme.WorkspaceExportAccent);
        Button add = ThreadlightEditorElements.CreateCompactButton("Add", AddObjectOrSelection, false,
            ThreadlightEditorTheme.WorkspaceExportAccent);
        add.AddToClassList("wolfy-export-add-root");
        tooltipLayer.Register(addRootField, "Product Content",
            "Choose a project folder or asset to include as product content. Leave this empty to use the current Project selection.");
        tooltipLayer.Register(add, "Add Product Content",
            "Add the object above, or add the current Project selection when the field is empty.");
        chooser.Add(addRootField); chooser.Add(add); Responsive(chooser); content.Add(chooser);

        VisualElement options = Element("wolfy-export-options");
        options.AddToClassList("wolfy-export-fixed");
        Label label = new Label("Include Referenced Dependencies");
        label.AddToClassList("wolfy-export-options-label");
        label.style.color = ThreadlightEditorTheme.TextMuted;
        VisualElement controls = Element("wolfy-export-options-controls");
        Button refresh = ThreadlightEditorElements.CreateCompactButton("Refresh", () => RefreshAssets(true), false,
            ThreadlightEditorTheme.WorkspaceExportAccent);
        refresh.AddToClassList("wolfy-export-refresh");
        Button dependencies = ThreadlightEditorElements.CreateToggleControl(includeDependencies, value =>
        { includeDependencies = value; RefreshAssets(false); });
        dependencies.AddToClassList("wolfy-export-dependencies-toggle");
        tooltipLayer.Register(refresh, "Refresh Export Contents",
            "Re-scan product files, referenced dependencies, exclusions, and the guarded installer before exporting.");
        tooltipLayer.Register(dependencies, "Include Referenced Assets",
            "Include assets referenced by the selected product content.");
        controls.Add(refresh); controls.Add(dependencies); options.Add(label); options.Add(controls);
        Responsive(options); content.Add(options);
        return section;
    }

    private VisualElement CreateFooter()
    {
        VisualElement footer = Element("wolfy-export-footer");
        ThreadlightEditorElements.StyleFooterDockSurface(
            footer, ThreadlightEditorTheme.WorkspaceExportAccent);
        VisualElement copy = ThreadlightEditorElements.CreateFooterCopy("Export Unity Package", string.Empty, out summary);
        copy.AddToClassList("wolfy-export-footer-copy");
        exportButton = ThreadlightEditorElements.CreatePrimaryButton("Export Unity Package", ExportPackage,
            ThreadlightEditorTheme.WorkspaceExportAccent);
        exportButton.AddToClassList("wolfy-export-button");
        footer.Add(copy); footer.Add(exportButton); Responsive(footer);
        return footer;
    }

    private void AddObjectOrSelection()
    {
        if (addRootField.value == null)
            foreach (string path in ThreadlightComponentsAssetExportCatalog.SelectedProjectPaths()) AddRoot(path, false);
        else
        {
            AddRoot(AssetDatabase.GetAssetPath(addRootField.value), false);
            addRootField.SetValueWithoutNotify(null);
        }
        RebuildRoots(); RefreshAssets(false);
    }

    private void AddRoot(string path, bool refresh)
    {
        path = ThreadlightComponentsAssetExportCatalog.NormalizePath(path);
        if (!ThreadlightComponentsAssetExportCatalog.IsAssetsPath(path) || productRoots.Contains(path)) return;
        productRoots.Add(path); productRoots.Sort(StringComparer.OrdinalIgnoreCase);
        if (refresh) { RebuildRoots(); RefreshAssets(false); }
    }

    private void RebuildRoots()
    {
        if (rootList == null) return;
        rootList.Clear();
        if (productRoots.Count == 0)
        {
            Label empty = new Label("No product content selected.");
            empty.AddToClassList("wolfy-export-roots-empty");
            empty.style.color = ThreadlightEditorTheme.TextMuted;
            rootList.Add(empty); return;
        }
        foreach (string path in productRoots.ToArray())
        {
            VisualElement row = Element("wolfy-export-root-row");
            row.style.backgroundColor = ThreadlightEditorTheme.ItemBody;
            Label label = new Label(path); label.AddToClassList("wolfy-export-root-label");
            label.style.color = ThreadlightComponentsAssetExportCatalog.AssetPathExists(path)
                ? ThreadlightEditorTheme.Text : ThreadlightEditorTheme.Error;
            Button remove = ThreadlightEditorElements.CreateCompactButton("Remove", () =>
            { productRoots.Remove(path); RebuildRoots(); RefreshAssets(false); }, true);
            remove.AddToClassList("wolfy-export-remove-root");
            tooltipLayer.Register(remove, "Remove Product Source",
                "Remove this source from the export list. Project assets are not deleted.");
            row.Add(label); row.Add(remove); rootList.Add(row);
        }
    }

    private void RefreshAssets(bool prepareBootstrapper)
    {
        if (tree == null) return;
        selection.ClearAssets(); installerPath = string.Empty;
        List<string> roots = productRoots.Where(ThreadlightComponentsAssetExportCatalog.AssetPathExists).ToList();
        HashSet<string> products = ThreadlightComponentsAssetExportCatalog.CollectAssets(roots);
        foreach (string path in products) selection.Add(path, ExportKind.Product);
        if (includeDependencies && products.Count > 0)
            foreach (string path in AssetDatabase.GetDependencies(products.ToArray(), true))
                AddAsset(path, ExportKind.Dependency);

        string bootstrapError = string.Empty;
        if (includeBootstrapper)
        {
            bool resolved = prepareBootstrapper
                ? ThreadlightComponentsBootstrapExportUtility.TryCreateOrUpdate(bootstrapFolderPath, out installerPath, out bootstrapError)
                : ThreadlightComponentsBootstrapExportUtility.TryGetInstallerPath(bootstrapFolderPath, out installerPath, out bootstrapError);
            if (resolved && AssetDatabase.IsValidFolder(installerPath))
                foreach (string path in ThreadlightComponentsAssetExportCatalog.CollectAssets(new[] { installerPath }))
                    selection.Add(path, ExportKind.Bootstrapper);
            else if (string.IsNullOrWhiteSpace(bootstrapError))
                bootstrapError = "The ThreadLight Components installer has not been prepared yet. Use Refresh to prepare it.";
        }
        ShowError(bootstrapError); RebuildVisibleItems(); RefreshSummary();
    }

    private void AddAsset(string path, ExportKind kind)
    {
        path = ThreadlightComponentsAssetExportCatalog.NormalizePath(path);
        if (ThreadlightComponentsAssetExportCatalog.IsAssetsPath(path)) selection.Add(path, kind);
    }

    private void RebuildVisibleItems()
    {
        visibleItems.Clear();
        ExportNode root = selection.BuildTree(AssetDatabase.IsValidFolder);
        foreach (ExportNode child in root.Children) AddVisible(child, 0);
        tree.Rebuild();
    }

    private void AddVisible(ExportNode node, int depth)
    {
        bool hasChildren = node.Children.Count > 0;
        if (hasChildren && initializedTopLevelExpansion.Add(node.Path))
            selection.SetExpanded(node.Path, ThreadlightEditorPreferences.GetSessionState(
                "asset-export.folders", null, node.Path, depth == 0));
        visibleItems.Add(new TreeItem(node, depth));
        if (!hasChildren || !(selection.IsExpanded(node.Path) || node.ContainsBootstrapper)) return;
        foreach (ExportNode child in node.Children) AddVisible(child, depth + 1);
    }

    private VisualElement MakeTreeRow()
    {
        TreeRow row = new TreeRow { Owner = this };
        ThreadlightEditorElements.StyleIconButton(row.Arrow, Color.clear, Color.clear,
            ThreadlightEditorTheme.TreeArrow, ThreadlightEditorTheme.FieldHover);
        ThreadlightEditorElements.StyleIconButton(row.Check, () => TreeCheckBackground(row),
            () => TreeCheckBorder(row), ThreadlightEditorTheme.Text,
            () => Color.Lerp(TreeCheckBackground(row), TreeCheckBorder(row), .22f),
            () => Color.Lerp(TreeCheckBorder(row), Color.white, .22f));
        tooltipLayer.Register(row.Arrow, () => row.Node?.Children.Count > 0 && selection.IsExpanded(row.Node.Path)
                ? "Collapse Folder" : "Expand Folder",
            () => "Show or hide this folder without changing what will be exported.");
        tooltipLayer.Register(row.Check, () => row.Node?.ContainsOnlyBootstrapper == true
                ? "Required Installer Content" : "Include In Export",
            () => row.Node?.ContainsOnlyBootstrapper == true
                ? "This owned installer content is required while the temporary bootstrapper option is enabled."
                : "Include or exclude this item and its descendants from the exported Unity package.");
        tooltipLayer.Register(row.Name, () => row.Node?.Name, () => row.Node?.Path);
        return row.Root;
    }

    private void BindTreeRow(VisualElement element, int index)
    {
        TreeRow row = (TreeRow)element.userData;
        TreeItem item = visibleItems[index];
        ExportNode node = row.Node = item.Node;
        bool children = node.Children.Count > 0;
        bool forced = children && node.ContainsBootstrapper;
        bool expanded = children && (forced || selection.IsExpanded(node.Path));
        CheckState state = selection.GetCheckState(node);
        bool locked = node.ContainsOnlyBootstrapper;
        row.Root.style.paddingLeft = 4f + item.Depth * 16f;
        row.RestingColor = item.Depth % 2 == 0
            ? ThreadlightEditorTheme.TreeRowEven : ThreadlightEditorTheme.TreeRowOdd;
        row.RefreshInteraction?.Invoke();
        row.Arrow.text = children ? expanded ? "−" : "+" : string.Empty;
        ThreadlightEditorElements.SetButtonEnabled(row.Arrow, children && !forced, false);
        row.Check.text = state == CheckState.All ? "✓" : state == CheckState.Partial ? "−" : string.Empty;
        ThreadlightEditorElements.SetButtonEnabled(row.Check, !locked, false);
        row.Icon.image = AssetDatabase.GetCachedIcon(node.Path) ??
            EditorGUIUtility.IconContent(node.IsFolder ? "Folder Icon" : "DefaultAsset Icon")?.image;
        row.Name.text = node.Name; row.Name.style.color = ThreadlightEditorTheme.Text;
        ExportKind kind = node.HighestAssetKind;
        row.Badge.text = kind == ExportKind.Bootstrapper ? "REQUIRED INSTALLER" : "DEPENDENCY";
        row.Badge.style.display = kind == ExportKind.Product ? DisplayStyle.None : DisplayStyle.Flex;
        row.Badge.style.color = kind == ExportKind.Bootstrapper
            ? ThreadlightEditorTheme.WorkspaceExportAccent : ThreadlightEditorTheme.TextDim;
    }

    private Color TreeCheckBackground(TreeRow row)
    {
        if (row?.Node == null)
            return ThreadlightEditorTheme.ToggleOff;
        if (row.Node.ContainsOnlyBootstrapper)
            return Color.Lerp(ThreadlightEditorTheme.PanelInset, ThreadlightEditorTheme.TreeRequiredAccent, .40f);
        return selection.GetCheckState(row.Node) == CheckState.None ? ThreadlightEditorTheme.ToggleOff :
            Color.Lerp(ThreadlightEditorTheme.PanelInset, ThreadlightEditorTheme.WorkspaceExportAccent, .62f);
    }

    private Color TreeCheckBorder(TreeRow row)
    {
        if (row?.Node == null)
            return ThreadlightEditorTheme.FieldBorder;
        if (row.Node.ContainsOnlyBootstrapper)
            return ThreadlightEditorTheme.TreeRequiredAccent;
        return selection.GetCheckState(row.Node) == CheckState.None ? ThreadlightEditorTheme.FieldBorder :
            ThreadlightEditorTheme.WithAlpha(ThreadlightEditorTheme.WorkspaceExportAccent, .88f);
    }

    private void ToggleExpanded(ExportNode node)
    {
        if (node == null || node.Children.Count == 0 || node.ContainsBootstrapper) return;
        selection.ToggleExpanded(node.Path);
        ThreadlightEditorPreferences.SetSessionState(
            "asset-export.folders", null, node.Path, selection.IsExpanded(node.Path));
        RebuildVisibleItems();
    }

    private void ToggleSelected(ExportNode node)
    {
        if (node == null || node.ContainsOnlyBootstrapper) return;
        selection.ToggleSelected(node); tree.RefreshItems(); RefreshSummary();
    }

    private void RefreshSummary()
    {
        int products = selection.CountSelected(ExportKind.Product);
        int dependencies = selection.CountSelected(ExportKind.Dependency);
        int bootstrap = selection.CountSelected(ExportKind.Bootstrapper);
        contentsSummary.text = $"{products} product · {dependencies} dependencies" +
            (includeBootstrapper ? " · installer" : string.Empty) +
            (selection.ExcludedCount > 0 ? $" · {selection.ExcludedCount} excluded" : string.Empty);
        contentsSummary.style.color = selection.ExcludedCount > 0
            ? ThreadlightEditorTheme.Warning : ThreadlightEditorTheme.TextMuted;
        summary.text = products == 0 ? "Add product content to continue." :
            !string.IsNullOrWhiteSpace(errorMessage) ? "Resolve the issue above before exporting." :
            "Ready to create a Unity package.";
        summary.style.color = string.IsNullOrWhiteSpace(errorMessage)
            ? ThreadlightEditorTheme.TextMuted : ThreadlightEditorTheme.Warning;
        ThreadlightEditorElements.SetButtonEnabled(exportButton, products > 0 &&
            (!includeBootstrapper || bootstrap > 0) && string.IsNullOrWhiteSpace(errorMessage));
    }

    private void ExportPackage()
    {
        RefreshAssets(true);
        if (selection.CountSelected(ExportKind.Product) == 0 ||
            includeBootstrapper && selection.CountSelected(ExportKind.Bootstrapper) == 0)
        { ShowError("Choose product content and prepare the required installer before exporting."); RefreshSummary(); return; }
        string path = EditorUtility.SaveFilePanel("Export Asset Package", string.Empty, DefaultPackageName(), "unitypackage");
        if (string.IsNullOrWhiteSpace(path)) return;
        List<string> selected = selection.SelectedPaths();
        try
        {
            AssetDatabase.ExportPackage(selected.ToArray(), path, ExportPackageOptions.Interactive);
            ShowMessage("Export Started", $"Unity is exporting {selected.Count} selected asset" +
                (selected.Count == 1 ? "." : "s."), MessageType.Info);
        }
        catch (Exception exception)
        { ShowError("Unity could not export the asset package:\n" + exception.Message); Debug.LogException(exception); }
    }

    private string DefaultPackageName()
    {
        string path = productRoots.FirstOrDefault(ThreadlightComponentsAssetExportCatalog.AssetPathExists) ?? "Asset";
        string name = Path.GetFileName(path.TrimEnd('/'));
        if (string.IsNullOrWhiteSpace(name)) name = "Asset";
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '-');
        return name + ".unitypackage";
    }

    private void ShowError(string message)
    {
        errorMessage = message ?? string.Empty;
        if (string.IsNullOrWhiteSpace(errorMessage))
        { errorHost.Clear(); errorHost.style.display = DisplayStyle.None; return; }
        ShowMessage("Export Could Not Continue", errorMessage, MessageType.Error);
    }

    private void ShowMessage(string title, string text, MessageType type)
    {
        errorHost.Clear(); errorHost.style.display = DisplayStyle.Flex;
        errorHost.Add(ThreadlightEditorElements.CreateMessage(title, text, type));
        RefreshSummary();
    }

    private static VisualElement Element(string className)
    { VisualElement element = new VisualElement(); element.AddToClassList(className); return element; }

    private static void Responsive(VisualElement element) =>
        ThreadlightEditorElements.BindWidthClass(
            element, StackedClass, ThreadlightEditorTheme.CompactLayoutBreakpoint);
}
}
