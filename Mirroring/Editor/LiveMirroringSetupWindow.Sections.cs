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
    private const string PairFilterSurfaceId = "live-mirroring.pair-filter";
    private Label pairSummary;
    private Button showAllPairsButton, showProblemPairsButton;
    private VisualElement pairFilterEmptyState;
    private bool showProblemPairsOnly;
    private void AddMirroringSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Live Mirroring",
            "Configures how mirrored targets follow their sources across the mirror plane.",
            ThreadlightEditorTone.Feature,
            "BEHAVIOR");
        RenderFields(section,
            Form.Toggle("Live Mirroring", "liveMirror"),
            Form.ObjectReference("Mirror Center", "mirrorCenter", typeof(Transform)),
            Form.Enum("Mirror Axis", "mirrorOptions.mirrorAxis"));
        LiveMirroringSetupCard channels = CreateWorkspaceSection(
            "Mirroring Transforms", "Controls mirrored position, rotation, and scale.", ThreadlightEditorTone.Feature, string.Empty, false);
        section.Add(channels);
        RenderFields(channels,
            Form.Toggle("Mirror Position", "mirrorOptions.mirrorPosition"),
            Form.Toggle("Mirror Rotation", "mirrorOptions.mirrorRotation"),
            Form.Toggle("Mirror Scale", "mirrorOptions.mirrorScale"));
        workspace.Add(section);
    }
    private void AddScaleSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Target Setup & Preview",
            "Configures target defaults, shared scaling, and prefab constraints.",
            ThreadlightEditorTone.Feature,
            "TARGETS");
        RenderFields(section,
            Form.Toggle("Add VRCFury Armature Links", "addVrcfuryArmatureLinks")
                .OnChanged(RebuildWorkspace),
            Form.Toggle("Synchronize Scale", "applyScaleReference"),
            Form.ObjectReference("Prefab Scale Object", "scaleReference",
                typeof(Transform)),
            Form.Toggle("Constrain Prefab to Targets",
                "addParentConstraintToPrefabContainer"));
        RegisterBoundNavigationTarget(section, "applyScaleReference");
        RegisterBoundNavigationTarget(section, "scaleReference");
        RegisterBoundNavigationTarget(section,
            "addParentConstraintToPrefabContainer");
        section.Add(CreateValidationSlot("scaleReference"));
        LiveMirroringSetupCard defaults = CreateWorkspaceSection(
            "Target Defaults", null, ThreadlightEditorTone.Feature, string.Empty, false);
        section.Add(defaults);
        RenderFields(defaults,
            Form.Vector3("Default Position", "targetLocalPosition"),
            Form.Vector3("Default Rotation", "targetLocalEulerRotation"),
            Form.Vector3("Default Scale", "targetLocalScale"),
            Form.Toggle("Apply Defaults to Existing Targets",
                "applyDefaultTransformToExistingTargets"));
        SerializedProperty handles = serializedSystem.FindProperty("scaleHandles");
        LiveMirroringSetupCard scaleHandles = CreateWorkspaceSection(
            "Additional Scale Handles", null, ThreadlightEditorTone.Feature, string.Empty, false);
        section.Add(scaleHandles);
        for (int i = 0; i < handles.arraySize; i++)
            scaleHandles.Add(CreateScaleHandleRow(i));
        scaleHandles.Add(CreateValidationSlot("scaleHandles"));
        VisualElement actions = CreateActionRow();
        actions.Add(AddTooltip(
            CreateButton("Add Scale Handle", AddScaleHandle, true, false,
                section.InteractionAccent),
            "Add Scale Handle",
            "Adds another object that drives or receives the targets' shared scale."));
        scaleHandles.Add(actions);
        workspace.Add(section);
    }
    private void AddPairs() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Target Organization",
            "Configures target names, source relationships, and mirrored counterparts.",
            ThreadlightEditorTone.Feature,
            "TARGETS");
        LiveMirroringSetupCard naming = CreateWorkspaceSection(
            "Target Naming", null, ThreadlightEditorTone.Feature, string.Empty, false);
        section.Add(naming);
        RenderFields(naming,
            Form.Text("Targets Folder Name", "constraintTargetsObjectName"),
            Form.Text("Generated Target Prefix", "targetNamePrefix"),
            Form.Text("Source Side Label", "sourceSideLabel"),
            Form.Text("Mirrored Side Label", "mirroredSideLabel"),
            Form.Text("Source Folder Name", "sourceFolderName"),
            Form.Text("Mirrored Folder Name", "mirroredFolderName"));
        RenderFields(section,
            Form.Toggle("Remove Unused Generated Targets",
                "removeUnusedGeneratedTargets"));
        LiveMirroringSetupCard generatedTargets = CreateWorkspaceSection(
            "Generated Targets", null, ThreadlightEditorTone.Feature, string.Empty);
        section.Add(generatedTargets);
        SerializedProperty pairs = serializedSystem.FindProperty("pairs");
        generatedTargets.Add(CreateValidationSlot("pairs"));
        showProblemPairsOnly = ThreadlightEditorPreferences.GetSessionState(
            PairFilterSurfaceId, currentSystem, "needs-attention", false);
        VisualElement pairTools = CreateActionRow();
        pairSummary = new Label { name = "threadlight-mirroring-pair-summary" };
        pairSummary.style.flexGrow = 1;
        pairTools.Add(pairSummary);
        showAllPairsButton = CreateButton("All Pairs", () => SetPairFilter(false),
            false, false, generatedTargets.InteractionAccent);
        showAllPairsButton.name = "threadlight-mirroring-filter-all";
        pairTools.Add(showAllPairsButton);
        showProblemPairsButton = CreateButton("Needs Attention", () => SetPairFilter(true),
            false, false, generatedTargets.InteractionAccent);
        showProblemPairsButton.name = "threadlight-mirroring-filter-problems";
        pairTools.Add(showProblemPairsButton);
        generatedTargets.Add(pairTools);
        pairFilterEmptyState = ThreadlightEditorElements.CreateMessage(
            "No Targets Need Attention",
            "All configured targets are ready. Select All Pairs to review them.");
        pairFilterEmptyState.name = "threadlight-mirroring-filter-empty";
        pairFilterEmptyState.style.display = DisplayStyle.None;
        generatedTargets.Add(pairFilterEmptyState);
        if (!string.IsNullOrWhiteSpace(pairActionError))
            generatedTargets.Add(ThreadlightEditorElements.CreateMessage(
                "Selected Objects Were Not Added",
                pairActionError,
                MessageType.Warning));
        if (pairs.arraySize == 0)
            generatedTargets.Add(ThreadlightEditorElements.CreateMessage(
                "No Targets",
                "Add a target, or select two scene objects and choose Add Selected Objects."));
        for (int i = 0; i < pairs.arraySize; i++)
            generatedTargets.Add(CreatePairCard(pairs, i));
        VisualElement actions = CreateActionRow();
        actions.Add(AddTooltip(
            CreateButton("Add Target", AddPair, true, false,
                section.InteractionAccent),
            "Add Target",
            "Adds a target. Leave its references empty for ThreadLight to create both targets during Build."));
        addSelectedObjectsButton = CreateButton("Add Selected Objects", AddPairFromSelection,
            false, false, section.InteractionAccent);
        addSelectedObjectsButton.name = "threadlight-mirroring-add-selected";
        RefreshAddSelectedObjectsState();
        AddTooltip(
            addSelectedObjectsButton,
            "Add Selected Objects",
            "Creates a pair from two selected scene objects. The active object becomes the mirrored target; the other becomes the source target.");
        actions.Add(addSelectedObjectsButton);
        generatedTargets.Add(actions);
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
        targetCards[basePath] = card;
        AddTooltip(card.RemoveButton, "Remove Target",
            "Removes this target from the setup. Assigned scene objects are preserved.");
        VisualElement createOpposite = RenderField<VisualElement>(card,
            Form.Toggle("Create Opposite Target",
                basePath + ".createOppositeTarget").OnChanged(RebuildWorkspace));
        propertyNavigationTargets[basePath + ".createOppositeTarget"] = createOpposite;
        TextField nameField = RenderField<TextField>(card,
            Form.Text("Target Name", basePath + ".pairName"));
        propertyNavigationTargets[basePath + ".pairName"] = nameField;
        nameField.RegisterValueChangedCallback(evt => {
            string value = string.IsNullOrWhiteSpace(evt.newValue)
                ? $"Target {index + 1}"
                : evt.newValue.Trim();
            card.SetTitle(value);
        });
        card.TrackPropertyValue(name, changed => {
            string value = string.IsNullOrWhiteSpace(changed.stringValue)
                ? $"Target {index + 1}"
                : changed.stringValue.Trim();
            card.SetTitle(value);
        });
        bool addLinks = GetBoolean("addVrcfuryArmatureLinks");
        bool mirrored = pair.FindPropertyRelative(
            "createOppositeTarget").boolValue;
        LiveMirroringSetupCard options = CreateWorkspaceSection(
            "Target Options", null, ThreadlightEditorTone.Feature, string.Empty, false,
            stateIdentity: basePath + ":options");
        RenderFields(options,
            Form.Vector3("Mirrored Rotation Offset",
                basePath + ".mirroredRotationOffset").When(() => mirrored),
            Form.Toggle("Use Global Side Labels",
                basePath + ".useGlobalSideLabels").OnChanged(RebuildWorkspace),
            Form.Text("Source Label", basePath + ".sourceSideLabel")
                .When(() => !pair.FindPropertyRelative(
                    "useGlobalSideLabels").boolValue),
            Form.Text("Mirrored Label", basePath + ".mirroredSideLabel")
                .When(() => mirrored && !pair.FindPropertyRelative(
                    "useGlobalSideLabels").boolValue));
        ObjectField sourceField = RenderField<ObjectField>(card,
            Form.ObjectReference("Source Target", basePath + ".sourceTarget",
                typeof(Transform)));
        propertyNavigationTargets[basePath + ".sourceTarget"] = sourceField;
        RenderField<VisualElement>(card,
            Form.Enum("Source Bone", basePath + ".sourceBone")
                .When(() => addLinks));
        ObjectField mirroredField = RenderField<ObjectField>(card,
            Form.ObjectReference("Mirrored Target", basePath + ".mirroredTarget",
                typeof(Transform)).When(() => mirrored));
        if (mirroredField != null)
            propertyNavigationTargets[basePath + ".mirroredTarget"] = mirroredField;
        RenderField<VisualElement>(card,
            Form.Enum("Mirrored Bone", basePath + ".mirroredBone")
                .When(() => mirrored && addLinks));
        card.Add(options);
        VisualElement actions = CreateActionRow();
        Button up = AddTooltip(
            CreateButton("Up", () => MovePair(index, index - 1), false, false,
                card.InteractionAccent),
            "Move Target Up",
            "Moves this target earlier in the update order.");
        ThreadlightEditorElements.SetButtonEnabled(up, index > 0, false);
        actions.Add(up);
        Button down = AddTooltip(
            CreateButton("Down", () => MovePair(index, index + 1), false, false,
                card.InteractionAccent),
            "Move Target Down",
            "Moves this target later in the update order.");
        ThreadlightEditorElements.SetButtonEnabled(down, index < pairs.arraySize - 1, false);
        actions.Add(down);
        actions.Add(AddTooltip(
            CreateButton("Swap", () => SwapPair(index), false, false,
                card.InteractionAccent),
            "Swap Source and Target",
            "Swaps the source and mirrored targets."));
        card.Add(actions);
        card.Add(CreateValidationSlot(basePath));
        return card;
    }
    private void AddPreviewSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Scene Preview",
            "Displays temporary, non-interactive prefab previews at configured targets. Previews are not added to the avatar.",
            ThreadlightEditorTone.Review,
            "PREVIEW");
        RenderFields(section,
            Form.Toggle("Show Scene Preview", "showScenePreview"),
            Form.ObjectReference("Preview Object", "previewSource",
                typeof(GameObject)));
        RegisterBoundNavigationTarget(section, "previewSource");
        section.Add(CreateValidationSlot("previewSource"));
        RenderFields(section, Form.ObjectReference("Ghost Material", "previewMaterial",
            typeof(Material)));
        workspace.Add(section);
    }
    private void AddDistributionSettings() {
        LiveMirroringSetupCard section = CreateWorkspaceSection(
            "Customer Installer",
            "When enabled, includes the Customer Installer. It runs only when ThreadLight Components is not already installed.",
            ThreadlightEditorTone.Export,
            "EXPORT", stateIdentity: "EXPORT:ThreadLight Components Bootstrapper");
        RenderFields(section, Form.Toggle("Include Customer Installer",
            "generateThreadlightComponentsBootstrapper")
                .OnChanged(RebuildWorkspace));
        SerializedProperty enabled = serializedSystem.FindProperty(
            "generateThreadlightComponentsBootstrapper");
        bool showFolder = enabled?.boolValue ?? false;
        RenderFields(section, Form.Text("Installer Folder",
            "threadlightComponentsBootstrapperFolderPath").When(() => showFolder));
        if (showFolder) {
            section.Add(ThreadlightEditorElements.CreateMessage(
                "Include the Installer Folder",
                "Include this folder in the Unity package. The installer runs only when ThreadLight Components is not already installed, then removes its own files.",
                MessageType.Info));
        }
        workspace.Add(section);
    }
    private VisualElement CreateScaleHandleRow(int index) {
        string path = $"scaleHandles.Array.data[{index}]";
        VisualElement row = new VisualElement();
        row.AddToClassList("threadlight-mirroring-scale-handle-row");
        ObjectField field = RenderField<ObjectField>(row,
            Form.ObjectReference($"Scale Handle {index + 1}", path,
                typeof(Transform)),
            ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Feature).Accent);
        propertyNavigationTargets[path] = field;
        field.style.minWidth = 0f;
        field.style.flexBasis = 0f;
        field.style.flexGrow = 1f;
        field.style.flexShrink = 1f;
        Button remove = CreateButton(
            "Remove Scale Handle", () => RemoveScaleHandle(index), false, true,
            ThreadlightEditorTheme.Palette(ThreadlightEditorTone.Feature).Accent);
        remove.AddToClassList("threadlight-mirroring-scale-handle-remove");
        row.Add(AddTooltip(
            remove,
            "Remove Scale Handle",
            "Removes this scale handle without deleting the object."));
        ThreadlightEditorElements.StyleFieldActionRow(row);
        VisualElement container = new VisualElement();
        container.Add(row);
        container.Add(CreateValidationSlot(path));
        return container;
    }
}
}
