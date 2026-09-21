namespace Threadlight.Mirroring.Editor {
using Threadlight.Authoring.Editor;
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private void AddFooterActions(bool managed, string managerName) {
        if (footer == null) return;
        bool creating = currentSystem == null;
        bool supportedData = SupportsInstalledData(currentSystem);
        footer.AddToClassList("threadlight-checker-dock");
        AddBuildChecker(managed, supportedData, creating);
        VisualElement row = new VisualElement();
        row.AddToClassList("threadlight-checker-action-row");
        row.style.borderTopColor = Color.Lerp(ThreadlightEditorTheme.PanelInset,
            ThreadlightEditorTheme.WorkspaceReviewAccent, .42f);
        VisualElement copy = ThreadlightEditorElements.CreateFooterCopy(
            managed ? "Managed by Another Builder" : !supportedData ? "Unsupported Live Mirroring Data" :
                "Build This Setup",
            managed ? $"Build and export from {managerName}." : creating
                ? candidateRoot == null ? "Choose a Prefab Root first." : "Ready to create the setup and targets."
                : !supportedData ? "Update ThreadLight Authoring before building or exporting."
                : "Ready to build changes.");
        row.Add(copy);
        VisualElement actions = new VisualElement();
        actions.AddToClassList("threadlight-mirroring-footer-actions");
        actions.AddToClassList("threadlight-footer-actions");
        Button build = AddTooltip(CreateButton(
                creating ? "Create and Build" : "Build Setup", BuildSetup,
                true, false, ThreadlightEditorTheme.WorkspacePrefabAccent),
            creating ? "Create ThreadLight Mirroring" : "Build ThreadLight Mirroring",
            creating
                ? "Creates the setup and target hierarchy under the selected Prefab Root."
                : "Creates or updates generated targets using these settings.");
        ThreadlightEditorElements.SetButtonEnabled(build,
            !managed && supportedData && (!creating || candidateRoot != null));
        build.AddToClassList("threadlight-footer-button");
        actions.Add(build);
        Button export = AddTooltip(CreateButton("Export Asset", OpenAssetExporter, false, false,
                ThreadlightEditorTheme.WorkspaceExportAccent), "Export Asset Package",
            "Opens the asset exporter for product files and the optional Customer Installer.");
        ThreadlightEditorElements.SetButtonEnabled(export,
            currentSystem != null && !managed && supportedData, false);
        export.AddToClassList("threadlight-footer-button");
        actions.Add(export);
        row.Add(actions);
        footer.Add(row);
    }
    private void OpenAssetExporter() {
        if (currentSystem == null || !SupportsInstalledData(currentSystem) ||
            IsCurrentSystemManagedExternally(out _)) return;
        serializedSystem?.ApplyModifiedProperties();
        List<string> roots = new List<string>();
        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(currentSystem.gameObject);
        if (string.IsNullOrWhiteSpace(path)) {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && currentSystem.gameObject.scene == stage.scene) path = stage.assetPath;
        }
        if (!string.IsNullOrWhiteSpace(path)) roots.Add(path);
        ThreadlightComponentsAssetExportWindow.Open(roots, currentSystem.threadlightComponentsBootstrapperFolderPath,
            currentSystem.generateThreadlightComponentsBootstrapper);
    }
}
}
