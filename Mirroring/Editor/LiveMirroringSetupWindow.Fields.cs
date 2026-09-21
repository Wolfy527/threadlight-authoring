namespace Threadlight.Mirroring.Editor {
using Threadlight.EditorUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
public sealed partial class LiveMirroringSetupWindow {
    private const string SectionStateSurfaceId = "live-mirroring.sections";
    private LiveMirroringSetupCard CreateWorkspaceSection(
        string title,
        string description,
        ThreadlightEditorTone tone,
        string kind = "SECTION", bool defaultExpanded = true, string stateIdentity = null) {
        Object owner = currentSystem != null ? currentSystem : candidateRoot;
        string identity = stateIdentity ?? $"{kind}:{title}";
        bool expanded = ThreadlightEditorPreferences.GetSessionState(
            SectionStateSurfaceId, owner, identity, defaultExpanded);
        return LiveMirroringSetupElements.CreateSection(
            title, description, tone, kind, expanded,
            value => ThreadlightEditorPreferences.SetSessionState(
                SectionStateSurfaceId, owner, identity, value));
    }
    private ThreadlightSerializedForm CreateSerializedForm(VisualElement parent,
        Color? interactionAccent = null) {
        Color accent = interactionAccent ?? ResolveInteractionAccent(parent);
        return
        new ThreadlightSerializedForm(
            serializedSystem,
            ApplySerializedChange,
            GetBoolean,
            GetTooltip,
            (element, title, body) => AddTooltip(element, title, body),
            () => accent);
    }
    private static Color ResolveInteractionAccent(VisualElement parent) {
        for (VisualElement current = parent; current != null; current = current.parent)
            if (current is LiveMirroringSetupCard card)
                return card.InteractionAccent;
        return ThreadlightEditorTheme.WorkspacePrefabAccent;
    }
    private void RenderFields(
        VisualElement parent,
        params ThreadlightFormField[] fields) =>
        CreateSerializedForm(parent).AddFields(ResolveContentHost(parent), fields);
    private T RenderField<T>(
        VisualElement parent,
        ThreadlightFormField field)
        where T : VisualElement =>
        CreateSerializedForm(parent).AddField(ResolveContentHost(parent), field) as T;
    private T RenderField<T>(
        VisualElement parent,
        ThreadlightFormField field,
        Color interactionAccent)
        where T : VisualElement =>
        CreateSerializedForm(parent, interactionAccent).AddField(
            ResolveContentHost(parent), field) as T;
    private void RegisterBoundNavigationTarget(
        VisualElement parent,
        string propertyPath) {
        VisualElement target = null;
        parent.Query<BindableElement>().ForEach(element => {
            if (target == null && element.bindingPath == propertyPath)
                target = element;
        });
        if (target != null)
            propertyNavigationTargets[propertyPath] = target;
    }
    private static VisualElement ResolveContentHost(VisualElement parent) =>
        parent is ThreadlightDisclosureCard card ? card.Content : parent;
    private T AddTooltip<T>(T element, string title, string body)
        where T : VisualElement {
        return tooltipLayer != null
            ? tooltipLayer.Register(element, title, body)
            : element;
    }
    private bool IsCurrentSystemManagedExternally(out string managerName) {
        managerName = null;
        return currentSystem != null &&
            LiveMirroringSetupUtility.TryGetManagingTool(
                LiveMirroringSetupUtility
                    .ResolveAuthoringRoot(currentSystem)?.gameObject,
                currentSystem,
                out managerName);
    }
    private static string GetTooltip(string path) {
        if (path == "constraintTargetsObjectName") return "Contains generated source and mirrored targets.";
        if (path == "targetNamePrefix") return "Prefixes generated target names.";
        if (path == "sourceSideLabel") return "Identifies source-side targets.";
        if (path == "mirroredSideLabel") return "Identifies mirrored targets.";
        if (path == "sourceFolderName") return "Contains source-side targets.";
        if (path == "mirroredFolderName") return "Contains mirrored targets.";
        if (path == "removeUnusedGeneratedTargets") return "Removes only unedited targets created by ThreadLight that are no longer configured. Preserves objects you created or edited.";
        if (path == "addVrcfuryArmatureLinks") return "Adds VRCFury Armature Links that attach generated targets to selected avatar bones.";
        if (path == "liveMirror") return "Updates mirrored targets when their sources move in Edit Mode.";
        if (path == "mirrorCenter") return "Sets the mirror plane from this transform's position and orientation.";
        if (path == "mirrorOptions.mirrorAxis") return "Selects the Mirror Center's local separation axis.";
        if (path == "mirrorOptions.mirrorPosition") return "Mirrors source position.";
        if (path == "mirrorOptions.mirrorRotation") return "Mirrors source rotation.";
        if (path == "mirrorOptions.mirrorScale") return "Copies source scale to the mirrored target.";
        if (path == "applyScaleReference") return "Synchronizes scale across all targets and the Prefab Scale Object.";
        if (path == "scaleReference") return "Sets the prefab object or content container that scales with the targets. It must be outside the target hierarchy.";
        if (path == "addParentConstraintToPrefabContainer") return "Adds a VRC Parent Constraint so generated targets control the Prefab Scale Object's position and rotation.";
        if (path == "targetLocalPosition") return "Sets the starting local position for new targets.";
        if (path == "targetLocalEulerRotation") return "Sets the starting local rotation in degrees for new targets.";
        if (path == "targetLocalScale") return "Sets the starting local scale for new targets.";
        if (path == "applyDefaultTransformToExistingTargets") return "Resets ThreadLight-managed targets to these defaults during the next Build.";
        if (path.StartsWith("scaleHandles.Array.data[")) return "Adds an object that drives or receives the targets' shared scale.";
        if (path.EndsWith(".createOppositeTarget")) return "Creates a mirrored counterpart for this target.";
        if (path.EndsWith(".useGlobalSideLabels")) return "Uses the side labels from Target Organization.";
        if (path.EndsWith(".sourceSideLabel")) return "Overrides the source label for this target.";
        if (path.EndsWith(".mirroredSideLabel")) return "Overrides the mirrored label for this target.";
        if (path.EndsWith(".pairName")) return "Names this target in the setup.";
        if (path.EndsWith(".sourceTarget")) return "Sets the target that drives this relationship. ThreadLight creates it during Build when empty.";
        if (path.EndsWith(".sourceBone")) return "Sets the avatar bone followed by the source target's VRCFury Armature Link.";
        if (path.EndsWith(".mirroredTarget")) return "Sets the mirrored target. ThreadLight creates it during Build when empty.";
        if (path.EndsWith(".mirroredBone")) return "Sets the avatar bone followed by the mirrored target's VRCFury Armature Link.";
        if (path.EndsWith(".mirroredRotationOffset")) return "Applies an additional local rotation after mirroring.";
        if (path == "showScenePreview") return "Displays non-interactive prefab previews at each target.";
        if (path == "previewSource") return "Sets the prefab object shown in Scene Preview.";
        if (path == "previewMaterial") return "Sets the preview material. When empty, ThreadLight uses the included Ghost Material, then Unity's default material.";
        if (path == "generateThreadlightComponentsBootstrapper") return "When enabled, includes the Customer Installer. It runs only when ThreadLight Components is not already installed.";
        if (path == "threadlightComponentsBootstrapperFolderPath") return "Sets the Customer Installer folder inside Assets. The installer removes only its own files.";
        return string.Empty;
    }
    private bool GetBoolean(string path) {
        if (!PrepareSerializedSystem())
            return false;
        SerializedProperty property = serializedSystem.FindProperty(path);
        return property != null && property.boolValue;
    }
}
}
