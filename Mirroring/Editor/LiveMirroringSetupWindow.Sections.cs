namespace Threadlight.Mirroring.Editor {
using Threadlight.EditorUI;
using static Threadlight.Mirroring.Editor.LiveMirroringSetupElements;
using Form = Threadlight.EditorUI.ThreadlightFormField;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private const string TargetCardSurfaceId = "live-mirroring.target-cards";
    private void AddMirroringSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Live Mirroring",
            "Choose the reference root, mirrored axis, and transform properties that should follow the source.",
            ThreadlightEditorTone.Feature,
            "BEHAVIOR");
        RenderFields(section,
            Form.Toggle("Live Mirroring", "liveMirror"),
            Form.ObjectReference("Mirror Center", "mirrorCenter", typeof(Transform)),
            Form.Enum("Mirror Axis", "mirrorOptions.mirrorAxis"),
            Form.Toggle("Mirror Position", "mirrorOptions.mirrorPosition"),
            Form.Toggle("Mirror Rotation", "mirrorOptions.mirrorRotation"),
            Form.Toggle("Mirror Scale", "mirrorOptions.mirrorScale"));
        workspace.Add(section);
    }
    private void AddScaleSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Target Setup & Preview",
            "Configure the shared scale behavior used by generated and assigned constraint targets.",
            ThreadlightEditorTone.Feature,
            "TARGETS");
        RenderFields(section,
            Form.Toggle("Add VRCFury Armature Links", "addVrcfuryArmatureLinks")
                .OnChanged(RebuildWorkspace),
            Form.Toggle("Synchronize Scale", "applyScaleReference"),
            Form.ObjectReference("Prefab Scale Reference", "scaleReference",
                typeof(Transform)),
            Form.Toggle("Add Parent Constraint To Prefab Container",
                "addParentConstraintToPrefabContainer"));
        section.Add(CreateValidationSlot("scaleReference"));
        section.Add(CreateNativeLikeSectionLabel("Target Defaults",
            section.InteractionAccent));
        RenderFields(section,
            Form.Vector3("Default Position", "targetLocalPosition"),
            Form.Vector3("Default Rotation", "targetLocalEulerRotation"),
            Form.Vector3("Default Scale", "targetLocalScale"),
            Form.Toggle("Apply Defaults To Existing Targets",
                "applyDefaultTransformToExistingTargets"));
        SerializedProperty handles = serializedSystem.FindProperty("scaleHandles");
        section.Add(CreateNativeLikeSectionLabel("Additional Scale Handles",
            section.InteractionAccent));
        for (int i = 0; i < handles.arraySize; i++)
            section.Add(CreateScaleHandleRow(i));
        section.Add(CreateValidationSlot("scaleHandles"));
        VisualElement actions = CreateActionRow();
        actions.Add(AddTooltip(
            CreateButton("Add Scale Handle", AddScaleHandle, true, false,
                section.InteractionAccent),
            "Add Scale Handle",
            "Add another object that can drive the same shared scale as the mirrored targets."));
        section.Add(actions);
        workspace.Add(section);
    }
    private void AddPairs() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Target Organization",
            "Add constraint targets, assign existing objects, and choose whether each target receives a mirrored side.",
            ThreadlightEditorTone.Feature,
            "TARGETS");
        RenderFields(section,
            Form.Text("Targets Folder Name", "constraintTargetsObjectName"),
            Form.Text("Generated Target Prefix", "targetNamePrefix"),
            Form.Text("Source Side Label", "sourceSideLabel"),
            Form.Text("Mirrored Side Label", "mirroredSideLabel"),
            Form.Text("Source Folder Name", "sourceFolderName"),
            Form.Text("Mirrored Folder Name", "mirroredFolderName"),
            Form.Toggle("Remove Unused Generated Targets",
                "removeUnusedGeneratedTargets"));
        section.Add(CreateNativeLikeSectionLabel("Generated Targets"));
        SerializedProperty pairs = serializedSystem.FindProperty("pairs");
        section.Add(CreateValidationSlot("pairs"));
        for (int i = 0; i < pairs.arraySize; i++)
            section.Add(CreatePairCard(pairs, i));
        VisualElement actions = CreateActionRow();
        actions.Add(AddTooltip(
            CreateButton("+ Add Target", AddPair, true, false,
                section.InteractionAccent),
            "Add Target",
            "Add a constraint target. Missing references receive generated empty target objects automatically."));
        Button selectionButton = CreateButton("Add Selected Objects", AddPairFromSelection,
            false, false, section.InteractionAccent);
        ThreadlightEditorElements.SetButtonEnabled(selectionButton,
            Selection.gameObjects.Length == 2, false);
        AddTooltip(
            selectionButton,
            "Add Selected Objects",
            "Create a pair from exactly two selected objects. The first is the source and the second is the mirrored target.");
        actions.Add(selectionButton);
        section.Add(actions);
        workspace.Add(section);
    }
    private VisualElement CreatePairCard(SerializedProperty pairs, int index) {
        SerializedProperty pair = pairs.GetArrayElementAtIndex(index);
        SerializedProperty name = pair.FindPropertyRelative("pairName");
        string displayName = string.IsNullOrWhiteSpace(name.stringValue)
            ? $"Target {index + 1}"
            : name.stringValue;
        string basePath = TargetCardPath(index);
        bool expanded = GetTargetCardExpansion(index);
        LiveMirroringTargetCard card = new LiveMirroringTargetCard(
            displayName, expanded,
            value => {
                targetCardExpansion[basePath] = value;
                ThreadlightEditorPreferences.SetSessionState(
                    TargetCardSurfaceId, currentSystem, basePath, value);
            },
            () => RemovePair(index));
        AddTooltip(card.RemoveButton, "Remove Target",
            "Removes this target from the Live Mirroring configuration. Assigned scene objects are preserved.");
        RenderFields(card, Form.Toggle("Create Opposite Target",
            basePath + ".createOppositeTarget").OnChanged(RebuildWorkspace));
        TextField nameField = RenderField<TextField>(card,
            Form.Text("Target Name", basePath + ".pairName"));
        nameField.RegisterValueChangedCallback(evt => {
            string value = string.IsNullOrWhiteSpace(evt.newValue)
                ? $"Target {index + 1}"
                : evt.newValue.Trim();
            card.SetTitle(value);
        });
        bool addLinks = GetBoolean("addVrcfuryArmatureLinks");
        bool mirrored = pair.FindPropertyRelative(
            "createOppositeTarget").boolValue;
        RenderFields(card,
            Form.Toggle("Use Global Side Labels",
                basePath + ".useGlobalSideLabels").OnChanged(RebuildWorkspace),
            Form.Text("Source Label", basePath + ".sourceSideLabel")
                .When(() => !pair.FindPropertyRelative(
                    "useGlobalSideLabels").boolValue),
            Form.Text("Mirrored Label", basePath + ".mirroredSideLabel")
                .When(() => mirrored && !pair.FindPropertyRelative(
                    "useGlobalSideLabels").boolValue),
            Form.ObjectReference("Source", basePath + ".sourceTarget",
                typeof(Transform)),
            Form.Enum("Source Bone", basePath + ".sourceBone")
                .When(() => addLinks),
            Form.ObjectReference("Mirrored Target", basePath + ".mirroredTarget",
                typeof(Transform)).When(() => mirrored),
            Form.Enum("Mirrored Bone", basePath + ".mirroredBone")
                .When(() => mirrored && addLinks),
            Form.Vector3("Rotation Offset",
                basePath + ".mirroredRotationOffset").When(() => mirrored));
        VisualElement actions = CreateActionRow();
        Button up = AddTooltip(
            CreateButton("Up", () => MovePair(index, index - 1), false, false,
                card.InteractionAccent),
            "Move Pair Up",
            "Move this pair earlier in the evaluation order.");
        ThreadlightEditorElements.SetButtonEnabled(up, index > 0, false);
        actions.Add(up);
        Button down = AddTooltip(
            CreateButton("Down", () => MovePair(index, index + 1), false, false,
                card.InteractionAccent),
            "Move Pair Down",
            "Move this pair later in the evaluation order.");
        ThreadlightEditorElements.SetButtonEnabled(down, index < pairs.arraySize - 1, false);
        actions.Add(down);
        actions.Add(AddTooltip(
            CreateButton("Swap", () => SwapPair(index), false, false,
                card.InteractionAccent),
            "Swap Source and Target",
            "Exchange the two assigned objects so the current mirrored target becomes the source and the current source becomes the mirrored target."));
        card.Add(actions);
        card.Add(CreateValidationSlot(basePath));
        return card;
    }
    private void AddPreviewSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Scene Preview",
            "Display non-interactive scene ghosts at every configured target.",
            ThreadlightEditorTone.Review,
            "PREVIEW");
        RenderFields(section,
            Form.Toggle("Show Scene Preview", "showScenePreview"),
            Form.ObjectReference("Preview Source", "previewSource",
                typeof(GameObject)));
        section.Add(CreateValidationSlot("previewSource"));
        RenderFields(section, Form.ObjectReference("Preview Material", "previewMaterial",
            typeof(Material)));
        workspace.Add(section);
    }
    private void AddDistributionSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "ThreadLight Components Bootstrapper",
            "Create a temporary installer folder that creators can include with their exported asset.",
            ThreadlightEditorTone.Export,
            "EXPORT");
        RenderFields(section, Form.Toggle("Create Temporary Bootstrapper",
            "generateThreadlightComponentsBootstrapper"));
        SerializedProperty enabled = serializedSystem.FindProperty(
            "generateThreadlightComponentsBootstrapper");
        bool showFolder = enabled?.boolValue ?? false;
        RenderFields(section, Form.Text("Temporary Folder",
            "threadlightComponentsBootstrapperFolderPath").When(() => showFolder));
        if (showFolder) {
            section.Add(ThreadlightEditorElements.CreateMessage(
                "Export With Your Asset",
                "Include this temporary folder in your Unity export. It installs ThreadLight Components only when VCC has not already supplied it, then removes itself.",
                MessageType.Info));
        }
        workspace.Add(section);
    }
    private VisualElement CreateScaleHandleRow(int index) {
        string path = $"scaleHandles.Array.data[{index}]";
        VisualElement row = new VisualElement();
        row.AddToClassList("threadlight-mirroring-scale-handle-row");
        ThreadlightEditorElements.StyleFieldActionRow(row);
        ObjectField field = RenderField<ObjectField>(row,
            Form.ObjectReference($"Scale Handle {index + 1}", path,
                typeof(Transform)),
            ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Feature).Accent);
        field.style.minWidth = 0f;
        field.style.flexBasis = 0f;
        field.style.flexGrow = 1f;
        field.style.flexShrink = 1f;
        Button remove = CreateButton(
            "Remove", () => RemoveScaleHandle(index), false, true,
            ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Feature).Accent);
        remove.AddToClassList("threadlight-mirroring-scale-handle-remove");
        row.Add(AddTooltip(
            remove,
            "Remove Scale Handle",
            "Stop using this object as an additional shared-scale handle. The object itself is not deleted."));
        VisualElement container = new VisualElement();
        container.Add(row);
        container.Add(CreateValidationSlot(path));
        return container;
    }
}
}
