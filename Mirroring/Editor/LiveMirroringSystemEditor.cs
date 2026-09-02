namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
[CustomEditor(typeof(AuthoringLiveMirroringSystem))]
public sealed class LiveMirroringSystemEditor : Editor {
    private readonly HashSet<string> failedElements = new HashSet<string>();
    private readonly LiveMirroringDiagnostics diagnostics = new LiveMirroringDiagnostics();
    private int extensionGeneration = -1;
    public override VisualElement CreateInspectorGUI() {
        VisualElement root = ThreadlightEditorElements.CreateInspectorRoot();
        root.Add(ThreadlightEditorElements.CreateInspectorBanner(
            "Live Mirroring System",
            "Stores the target mirroring and scene preview settings used by ThreadLight Mirroring.",
            ThreadlightEditorTheme.WorkspacePrefabAccent
        ));
        VisualElement validationHost = new VisualElement();
        root.Add(validationHost);
        VisualElement section = ThreadlightEditorElements.CreatePageSection(
            "System Overview",
            "This authoring system works automatically in the editor. Review its generated state or open the Builder to make changes.",
            ThreadlightEditorTheme.WorkspacePrefabAccent,
            out VisualElement status
        );
        root.Add(section);
        VisualElement contributorHost = new VisualElement();
        root.Add(contributorHost);
        RefreshInspector(validationHost, status, contributorHost);
        root.TrackSerializedObjectValue(
            serializedObject,
            _ => RefreshInspector(validationHost, status, contributorHost)
        );
        return root;
    }
    private void RefreshInspector(
        VisualElement validationHost,
        VisualElement overviewHost,
        VisualElement contributorHost) {
        if (contributorHost == null)
            return;
        bool supportsInstalledData = RebuildStatus(
            validationHost,
            overviewHost);
        contributorHost.style.display = supportsInstalledData
            ? DisplayStyle.Flex
            : DisplayStyle.None;
        int generation = LiveMirroringEditorExtensionRegistry.Generation;
        if (!supportsInstalledData ||
            contributorHost.userData is int renderedGeneration &&
            renderedGeneration == generation)
            return;
        contributorHost.Clear();
        contributorHost.userData = generation;
        AddExtensionElements(contributorHost);
    }
    private bool RebuildStatus(
        VisualElement validationHost,
        VisualElement overviewHost) {
        if (validationHost == null || overviewHost == null)
            return false;
        validationHost.Clear();
        overviewHost.Clear();
        serializedObject.UpdateIfRequiredOrScript();
        AuthoringLiveMirroringSystem system = target as AuthoringLiveMirroringSystem;
        if (system != null && system.DataVersion < 0) {
            validationHost.Add(ThreadlightEditorElements.CreateMessage(
                "Invalid Mirroring Data",
                $"This Live Mirroring System has an invalid data version ({system.DataVersion}). Restore the prefab from a valid copy before editing it.",
                MessageType.Error
            ));
            return false;
        }
        if (system != null &&
            system.DataVersion > LiveMirroringMigrationService.CurrentDataVersion) {
            validationHost.Add(ThreadlightEditorElements.CreateMessage(
                "Newer Mirroring Data",
                $"This Live Mirroring System uses data version {system.DataVersion}, but the installed scripts support up to version {LiveMirroringMigrationService.CurrentDataVersion}. Import the newer scripts before editing it.",
                MessageType.Error
            ));
            return false;
        }
        AddValidation(validationHost);
        AddReadOnlyStatus(overviewHost, system);
        if (system != null) {
            overviewHost.Add(ThreadlightEditorElements.CreatePrimaryButton(
                "Open ThreadLight Mirroring",
                () => LiveMirroringSetupWindow.OpenForSystem(system),
                ThreadlightEditorTheme.WorkspacePrefabAccent
            ));
        }
        return true;
    }
    private void AddExtensionElements(VisualElement container) {
        int generation = LiveMirroringEditorExtensionRegistry.Generation;
        if (extensionGeneration != generation) {
            failedElements.Clear();
            extensionGeneration = generation;
        }
        LiveMirroringEditorExtensionRegistry.DispatchOptionalIsolated(
            LiveMirroringEditorExtensionRegistry.GetInspectorElements(), failedElements,
            LiveMirroringExtensionCapabilities.Inspector, target,
            contributor => {
                VisualElement element =
                    contributor.CreateElement(serializedObject);
                if (element != null)
                    container.Add(element);
            });
    }
    private void AddReadOnlyStatus(
        VisualElement container,
        AuthoringLiveMirroringSystem system) {
        int pairCount = system?.pairs?.Length ?? 0;
        int targetCount = system == null ? 0 : LiveMirroringService.Evaluate(system).Targets.Count;
        container.Add(ThreadlightEditorElements.CreateReadOnlyValue(
            "Scalable Targets",
            targetCount.ToString()
        ));
        container.Add(ThreadlightEditorElements.CreateReadOnlyValue(
            "Mirrored Pairs",
            pairCount.ToString()
        ));
        container.Add(ThreadlightEditorElements.CreateReadOnlyValue(
            "Scene Preview",
            system != null && system.showScenePreview
                ? "Enabled"
                : "Disabled"
        ));
    }
    private void AddValidation(VisualElement container) {
        diagnostics.CollectReport(serializedObject);
        foreach (LiveMirroringValidationMessage message in diagnostics.Messages) {
            if (message == null)
                continue;
            container.Add(ThreadlightEditorElements.CreateMessage(
                message.Title,
                message.Message,
                LiveMirroringSetupElements.ToMessageType(message.Severity)
            ));
        }
    }
}
}
