namespace Threadlight.Mirroring.Editor {
using Threadlight.Authoring.Editor;
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private void AddFooterActions(bool managed, string managerName) {
        if (footer == null) return;
        bool creating = currentSystem == null;
        bool supportedData = SupportsInstalledData(currentSystem);
        VisualElement copy = ThreadlightEditorElements.CreateFooterCopy(
            managed ? "Managed by Another Builder" : !supportedData ? "Unsupported Live Mirroring Data" :
                creating ? "Create ThreadLight Mirroring" : "Build ThreadLight Mirroring",
            managed ? $"Build and export this setup from {managerName}." : creating
                ? candidateRoot == null ? "Choose a prefab root first." : "Creates only owned setup and target content."
                : !supportedData ? "Update ThreadLight Components before building or exporting this setup."
                : "Build again after changing settings.");
        copy.Add(CreateValidationSlot("@footer"));
        footer.Add(copy);
        VisualElement actions = new VisualElement();
        actions.AddToClassList("threadlight-mirroring-footer-actions");
        Button build = AddTooltip(CreateButton(
                creating ? "Create & Build" : "Build Setup", BuildSetup,
                true, false, ThreadlightEditorTheme.WorkspacePrefabAccent),
            creating ? "Create ThreadLight Mirroring" : "Build ThreadLight Mirroring",
            creating
                ? "Create the owned setup and target hierarchy for the selected prefab root."
                : "Apply the current settings to the owned target hierarchy.");
        ThreadlightEditorElements.SetButtonEnabled(build,
            !managed && supportedData && (!creating || candidateRoot != null));
        actions.Add(build);
        Button export = AddTooltip(CreateButton("Export Asset", OpenAssetExporter, false, false,
                ThreadlightEditorTheme.WorkspaceExportAccent), "Export Asset Package",
            "Choose product files and export a Unity package with the guarded ThreadLight Components installer.");
        ThreadlightEditorElements.SetButtonEnabled(export,
            currentSystem != null && !managed && supportedData, false);
        actions.Add(export);
        footer.Add(actions);
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
