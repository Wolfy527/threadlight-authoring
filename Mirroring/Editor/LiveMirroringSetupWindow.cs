namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using System;
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
    [NonSerialized] private string pairActionError;
    [SerializeField] private Vector2 scrollPosition;
    private SerializedObject serializedSystem;
    private VisualElement workspace, footer;
    private Button addSelectedObjectsButton;
    private ScrollView workspaceScroll;
    private ObjectField systemField, rootField;
    private ThreadlightEditorTooltipLayer tooltipLayer;
    private readonly Dictionary<string, VisualElement> validationSlots = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, LiveMirroringTargetCard> targetCards =
        new Dictionary<string, LiveMirroringTargetCard>();
    private readonly Dictionary<string, VisualElement> propertyNavigationTargets =
        new Dictionary<string, VisualElement>();
    private readonly LiveMirroringDiagnostics diagnostics = new LiveMirroringDiagnostics();
    private readonly Dictionary<LiveMirroringSetupCard, ValidationCounts> setupCardValidation =
        new Dictionary<LiveMirroringSetupCard, ValidationCounts>();
    private readonly Dictionary<string, bool> targetCardExpansion = new Dictionary<string, bool>();
    public static void Open() => ShowWindow(null);
    public static void OpenForSystem(AuthoringLiveMirroringSystem system) => ShowWindow(system);
    private static void ShowWindow(AuthoringLiveMirroringSystem system) {
        LiveMirroringSetupWindow window = GetWindow<LiveMirroringSetupWindow>();
        window.titleContent = new GUIContent("ThreadLight Mirroring");
        window.minSize = new Vector2(440, 420);
        if (system != null) window.SetSystem(system); else window.UseSelectionIfHelpful();
        window.Show();
        if (system != null) window.Focus();
    }
    private void OnEnable() {
        titleContent = new GUIContent("ThreadLight Mirroring");
        minSize = new Vector2(440, 420);
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
        CancelDiagnosticNavigationPresentation();
        ThreadlightContinuousEditGesture.CompleteActiveWithin(rootVisualElement);
        tooltipLayer?.Dispose(); tooltipLayer = null;
        Selection.selectionChanged -= OnSelectionChanged;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.delayCall -= RestoreSystemAfterPlayMode;
    }
    private void OnLostFocus() {
        ThreadlightContinuousEditGesture.CompleteActiveWithin(rootVisualElement);
    }
    public void CreateGUI() {
        CancelDiagnosticNavigationPresentation();
        VisualElement root = rootVisualElement;
        tooltipLayer?.Dispose();
        tooltipLayer = null;
        root.Unbind(); root.Clear();
        ThreadlightEditorElements.ApplySharedStyles(root);
        root.AddToClassList("threadlight-mirroring-window-root");
        root.style.backgroundColor = ThreadlightEditorTheme.Background;
        root.Add(ThreadlightEditorElements.CreateAuroraAtmosphere(root, workspaceScale: true));
        VisualElement studio = new VisualElement();
        studio.style.position = Position.Relative;
        studio.style.flexDirection = FlexDirection.Column;
        root.Add(ThreadlightEditorElements.CreateMinimumWidthViewport(studio, 440f));
        studio.Add(ThreadlightEditorElements.CreateInspectorBanner("ThreadLight Mirroring",
            "Create mirrored constraint targets that stay easy to position, scale, and preview.",
            ThreadlightEditorTheme.WorkspacePrefabAccent));
        ScrollView scroll = workspaceScroll = new ScrollView(ScrollViewMode.Vertical) {
            scrollOffset = scrollPosition
        };
        scroll.AddToClassList("threadlight-mirroring-window-scroll");
        ThreadlightEditorElements.StyleOverlayScrollbar(scroll, ThreadlightEditorTheme.WorkspacePrefabAccent);
        scroll.verticalScroller.valueChanged += _ => scrollPosition = scroll.scrollOffset;
        studio.Add(scroll);
        tooltipLayer = new ThreadlightEditorTooltipLayer(root);
        scroll.Add(CreateSetupSelector());
        workspace = new VisualElement();
        scroll.Add(workspace);
        VisualElement footerRow = new VisualElement();
        footerRow.AddToClassList("threadlight-mirroring-footer-row");
        footer = ThreadlightEditorElements.CreateFooterDock(ThreadlightEditorTheme.WorkspaceReviewAccent, 58);
        footerRow.Add(footer);
        studio.Add(footerRow);
        // Reserve space at the end of the scrollable content, not in its viewport.
        // Cards continue behind the floating dock while the final field can scroll above it.
        ThreadlightEditorElements.BindFooterInset(footerRow, scroll.contentContainer);
        RebuildWorkspace();
    }
}
}
