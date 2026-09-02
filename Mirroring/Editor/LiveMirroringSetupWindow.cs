namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow : EditorWindow {
    [SerializeField] private AuthoringLiveMirroringSystem currentSystem;
    [SerializeField] private GameObject candidateRoot;
    [SerializeField] private Transform candidateScaleReference;
    [SerializeField] private string creationError;
    [SerializeField] private Vector2 scrollPosition;
    private SerializedObject serializedSystem;
    private VisualElement workspace, footer;
    private ObjectField systemField, rootField;
    private ThreadlightEditorTooltipLayer tooltipLayer;
    private readonly Dictionary<string, VisualElement> validationSlots = new Dictionary<string, VisualElement>();
    private readonly LiveMirroringDiagnostics diagnostics = new LiveMirroringDiagnostics();
    private readonly Dictionary<LiveMirroringSetupCard, ValidationCounts> setupCardValidation =
        new Dictionary<LiveMirroringSetupCard, ValidationCounts>();
    private readonly Dictionary<string, bool> targetCardExpansion = new Dictionary<string, bool>();
    public static void Open() => ShowWindow(null);
    public static void OpenForSystem(AuthoringLiveMirroringSystem system) => ShowWindow(system);
    private static void ShowWindow(AuthoringLiveMirroringSystem system) {
        LiveMirroringSetupWindow window = GetWindow<LiveMirroringSetupWindow>();
        window.titleContent = new GUIContent("ThreadLight Mirroring");
        window.minSize = new Vector2(390, 420);
        if (system != null) window.SetSystem(system); else window.UseSelectionIfHelpful();
        window.Show();
        if (system != null) window.Focus();
    }
    private void OnEnable() {
        titleContent = new GUIContent("ThreadLight Mirroring");
        minSize = new Vector2(390, 420);
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        Selection.selectionChanged += OnSelectionChanged;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
    private void OnDisable() {
        tooltipLayer?.Dispose(); tooltipLayer = null;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.delayCall -= RestoreSystemAfterPlayMode;
    }
    public void CreateGUI() {
        VisualElement root = rootVisualElement;
        tooltipLayer?.Dispose();
        tooltipLayer = null;
        root.Unbind(); root.Clear();
        ThreadlightEditorElements.ApplySharedStyles(root);
        root.AddToClassList("threadlight-mirroring-window-root");
        root.style.backgroundColor = ThreadlightEditorTheme.Background;
        root.Add(ThreadlightEditorElements.CreateAuroraAtmosphere(root));
        root.Add(ThreadlightEditorElements.CreateInspectorBanner("ThreadLight Mirroring",
            "Build and assign lightweight constraint targets with live mirroring, shared scaling, and scene previews.",
            ThreadlightEditorTheme.WorkspacePrefabAccent));
        ScrollView scroll = new ScrollView(ScrollViewMode.Vertical) {
            scrollOffset = scrollPosition
        };
        scroll.AddToClassList("threadlight-mirroring-window-scroll");
        ThreadlightEditorElements.StyleOverlayScrollbar(scroll, ThreadlightEditorTheme.WorkspacePrefabAccent);
        scroll.verticalScroller.valueChanged += _ => scrollPosition = scroll.scrollOffset;
        root.Add(scroll);
        tooltipLayer = new ThreadlightEditorTooltipLayer(root);
        scroll.Add(CreateSetupSelector());
        workspace = new VisualElement();
        scroll.Add(workspace);
        VisualElement footerRow = new VisualElement();
        footerRow.AddToClassList("threadlight-mirroring-footer-row");
        footer = ThreadlightEditorElements.CreateFooterDock(ThreadlightEditorTheme.WorkspacePrefabAccent, 56);
        ThreadlightEditorElements.BindWidthClass(footer,
            "threadlight-mirroring-footer--stacked",
            ThreadlightEditorTheme.CompactLayoutBreakpoint);
        footerRow.Add(footer);
        root.Add(footerRow);
        RebuildWorkspace();
    }
}
}
