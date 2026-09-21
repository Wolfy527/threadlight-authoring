namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private static bool SupportsInstalledData(AuthoringLiveMirroringSystem system) =>
        system == null ||
        (system.DataVersion >= 0 &&
         system.DataVersion <= LiveMirroringMigrationService.CurrentDataVersion);

    private void RebuildWorkspace() {
        if (workspace == null)
            return;
        CancelDiagnosticNavigationPresentation();
        workspace.Unbind();
        workspace.Clear();
        footer?.Clear();
        validationSlots.Clear();
        targetCards.Clear();
        propertyNavigationTargets.Clear();
        addSelectedObjectsButton = null;
        bool managed = IsCurrentSystemManagedExternally(out string managerName);
        AddFooterActions(managed, managerName);
        if (currentSystem == null) {
            AddCreationWorkspace();
            RememberWorkspaceComposition(managed, managerName);
            return;
        }
        if (!SupportsInstalledData(currentSystem)) {
            bool newerData = currentSystem.DataVersion >
                LiveMirroringMigrationService.CurrentDataVersion;
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Unsupported Mirroring Data",
                newerData
                    ? "This setup was saved by a newer ThreadLight Authoring version. Update the package or reopen it with the version that created the setup. No changes were made."
                    : "This setup has an invalid data version and cannot be edited safely. Reopen an unaffected copy or restore the component from source control. No changes were made.",
                MessageType.Error
            ));
            RememberWorkspaceComposition(managed, managerName);
            return;
        }
        if (managed) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Managed by Another Builder",
                $"{managerName} owns this setup. Edit and rebuild it there.",
                MessageType.Warning
            ));
            RememberWorkspaceComposition(managed, managerName);
            return;
        }
        serializedSystem = new SerializedObject(currentSystem);
        workspace.Add(CreateValidationSlot("@setup"));
        AddPairs();
        AddScaleSettings();
        AddMirroringSettings();
        AddPreviewSettings();
        AddDistributionSettings();
        ArrangeWorkspaceColumns();
        RememberWorkspaceComposition(managed, managerName);
        workspace.Bind(serializedSystem);
        workspace.TrackSerializedObjectValue(
            serializedSystem,
            _ => OnSerializedSystemChanged()
        );
        RebuildValidation();
    }
    private void AddCreationWorkspace() {
        if (candidateRoot == null) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Choose a Prefab Root",
                "Select the scene object or Prefab Mode root that will contain the mirrored targets."
            ));
            return;
        }
        if (EditorUtility.IsPersistent(candidateRoot)) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Open the Prefab First",
                "Open this prefab in Prefab Mode before creating its ThreadLight Mirroring setup.",
                MessageType.Warning
            ));
            return;
        }
        if (LiveMirroringSetupUtility.IsRootManagedByAnotherTool(
                candidateRoot,
                out string managerName)) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Managed by Another Builder",
                $"{managerName} manages this Prefab Root. Create or edit its ThreadLight Mirroring setup there.",
                MessageType.Warning
            ));
            return;
        }
        AuthoringLiveMirroringSystem[] existing =
            LiveMirroringSetupUtility.FindForRoot(candidateRoot);
        if (existing.Length > 0) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Existing Setup Found",
                existing.Length == 1
                    ? "This Prefab Root already contains one ThreadLight Mirroring setup."
                    : $"This Prefab Root contains {existing.Length} ThreadLight Mirroring setups. Choose one above.",
                MessageType.Warning
            ));
            if (existing.Length == 1) {
                workspace.Add(AddTooltip(
                    ThreadlightEditorElements.CreatePrimaryButton(
                        "Open Existing Setup",
                        () => SetSystem(existing[0]),
                        ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Core).Accent),
                    "Open Existing Setup",
                    "Opens the setup stored under this Prefab Root."));
            }
            return;
        }
        LiveMirroringSetupCard scaleReferenceCard = CreateWorkspaceSection(
            "Prefab Scale Object",
            "Select the object that scales with the targets, or leave it empty for Build to create a Prefab Container.",
            ThreadlightEditorTone.Core,
            "SETUP", stateIdentity: "SETUP:Prefab Scale Reference"
        );
        ObjectField scaleReferenceField = new ObjectField(
            "Prefab Scale Object") {
            objectType = typeof(Transform),
            allowSceneObjects = true
        };
        ThreadlightEditorElements.StyleField(scaleReferenceField,
            () => scaleReferenceCard.InteractionAccent,
            () => scaleReferenceCard.InteractionAccent);
        scaleReferenceField.SetValueWithoutNotify(candidateScaleReference);
        scaleReferenceField.RegisterValueChangedCallback(evt => {
            candidateScaleReference = evt.newValue as Transform;
            creationError = null;
            RebuildWorkspace();
        });
        AddTooltip(
            scaleReferenceField,
            "Prefab Scale Object",
            "Sets the prefab object or content container that scales. It cannot be the Prefab Root or part of the target hierarchy.");
        scaleReferenceCard.Add(scaleReferenceField);
        workspace.Add(scaleReferenceCard);
        if (candidateScaleReference != null &&
            !LiveMirroringSetupUtility.ValidateScaleReferenceForRoot(
                candidateRoot,
                candidateScaleReference,
                out string scaleReferenceError)) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Invalid Prefab Scale Object",
                scaleReferenceError,
                MessageType.Warning
            ));
        }
        workspace.Add(ThreadlightEditorElements.CreateMessage(
            "Ready to Create",
            candidateScaleReference == null
                ? "Build will create a ThreadLight-owned Prefab Container, EditorOnly setup holder, and targets."
                : "Build will create the EditorOnly setup holder and targets without replacing the Prefab Root or existing hierarchy."
        ));
        if (!string.IsNullOrWhiteSpace(creationError)) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Setup Could Not Be Created",
                creationError,
                MessageType.Error));
        }
    }
}
}
