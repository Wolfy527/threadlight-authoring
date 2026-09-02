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
        workspace.Unbind();
        workspace.Clear();
        footer?.Clear();
        validationSlots.Clear();
        bool managed = IsCurrentSystemManagedExternally(out string managerName);
        AddFooterActions(managed, managerName);
        if (currentSystem == null) {
            AddCreationWorkspace();
            return;
        }
        if (!SupportsInstalledData(currentSystem)) {
            bool newerData = currentSystem.DataVersion >
                LiveMirroringMigrationService.CurrentDataVersion;
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Unsupported Mirroring Data",
                newerData
                    ? "This setup was saved by a newer ThreadLight Components version. Update the package or reopen it with the version that created the setup. No changes were made."
                    : "This setup has an invalid data version and cannot be edited safely. Reopen an unaffected copy or restore the component from source control. No changes were made.",
                MessageType.Error
            ));
            return;
        }
        if (managed) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Managed by Another Builder",
                $"This ThreadLight Mirroring setup is owned by {managerName}. Edit and rebuild it there so its generated settings stay consistent.",
                MessageType.Warning
            ));
            return;
        }
        serializedSystem = new SerializedObject(currentSystem);
        workspace.Add(CreateValidationSlot("@setup"));
        AddPairs();
        AddScaleSettings();
        AddMirroringSettings();
        AddPreviewSettings();
        AddDistributionSettings();
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
                "Select a scene object or an object inside Prefab Mode, then use it as the prefab root."
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
                $"This prefab root is managed by {managerName}. Create or edit its ThreadLight Mirroring setup there.",
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
                    ? "This root already contains a ThreadLight Mirroring setup."
                    : $"This root contains {existing.Length} ThreadLight Mirroring setups. Choose the one you want to edit above.",
                MessageType.Warning
            ));
            if (existing.Length == 1) {
                workspace.Add(AddTooltip(
                    ThreadlightEditorElements.CreatePrimaryButton(
                        "Open Existing Setup",
                        () => SetSystem(existing[0]),
                        ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Core).Accent),
                    "Open Existing Setup",
                    "Edit the ThreadLight Mirroring setup already stored under this prefab root."));
            }
            return;
        }
        LiveMirroringSetupCard scaleReferenceCard = CreateWorkspaceSection(
            "Prefab Scale Reference",
            "Optionally choose a prefab object or content container. Build creates an owned Prefab Container when this is empty.",
            ThreadlightEditorTone.Core,
            "SETUP"
        );
        ObjectField scaleReferenceField = new ObjectField(
            "Prefab Scale Reference") {
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
            "Prefab Scale Reference",
            "Assign the prefab object or content container that should scale. It cannot be the prefab root or part of the target hierarchy.");
        scaleReferenceCard.Add(scaleReferenceField);
        workspace.Add(scaleReferenceCard);
        if (candidateScaleReference != null &&
            !LiveMirroringSetupUtility.ValidateScaleReferenceForRoot(
                candidateRoot,
                candidateScaleReference,
                out string scaleReferenceError)) {
            workspace.Add(ThreadlightEditorElements.CreateMessage(
                "Invalid Scale Reference",
                scaleReferenceError,
                MessageType.Warning
            ));
        }
        workspace.Add(ThreadlightEditorElements.CreateMessage(
            "Ready to Create",
            candidateScaleReference == null
                ? "Build creates an owned Prefab Container for shared scaling, then creates the EditorOnly setup holder and its targets."
                : "Build creates the EditorOnly setup holder and its targets. Your prefab root and existing hierarchy will not be replaced."
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
