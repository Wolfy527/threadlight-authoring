namespace Threadlight.Mirroring.Editor {
using Threadlight.Mirroring;
using Threadlight.EditorUI;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public sealed partial class LiveMirroringSetupWindow {
    private void OnSerializedSystemChanged() {
        if (WorkspaceCompositionChanged()) {
            RebuildWorkspace();
            return;
        }
        RefreshRetainedWorkspace();
    }
    private void ApplySerializedChange(
        string path,
        string undoName,
        System.Action<SerializedProperty> change) {
        if (!PrepareSerializedSystem())
            return;
        SerializedProperty property = serializedSystem.FindProperty(path);
        if (property == null)
            return;
        Undo.RecordObject(currentSystem, undoName);
        change?.Invoke(property);
        CommitSerializedMutation(false, true);
    }
    private void AddScaleHandle() {
        MutateArray("scaleHandles", handles => {
            int index = handles.arraySize++;
            handles.GetArrayElementAtIndex(index).objectReferenceValue = null;
            return true;
        });
    }
    private void RemoveScaleHandle(int index) {
        MutateArray("scaleHandles", handles => {
            if (index < 0 || index >= handles.arraySize) return false;
            for (int current = index; current < handles.arraySize - 1; current++)
                handles.GetArrayElementAtIndex(current).objectReferenceValue =
                    handles.GetArrayElementAtIndex(current + 1).objectReferenceValue;
            handles.arraySize--;
            return true;
        });
    }
    private void CreateSetup() {
        if (!LiveMirroringSetupUtility.TryCreate(
                candidateRoot,
                candidateScaleReference,
                out AuthoringLiveMirroringSystem system,
                out string error)) {
            if (!string.IsNullOrWhiteSpace(error)) {
                creationError = error;
                Debug.LogWarning(
                    $"ThreadLight Mirroring setup was not created: {error}",
                    candidateRoot);
            }
            RebuildWorkspace();
            return;
        }
        creationError = null;
        SetSystem(system);
        Selection.activeGameObject = system.gameObject;
    }
    private void AddPair() {
        AddPairInternal(null, null);
    }
    private void AddPairFromSelection() {
        GameObject[] selected = Selection.gameObjects;
        if (!TryResolveSelectedPairRoles(
                selected,
                Selection.activeGameObject,
                out Transform source,
                out Transform target)) {
            pairActionError =
                "Select two different scene objects. The active object becomes the mirrored target; the other becomes the source target.";
            RebuildWorkspace();
            return;
        }
        var candidate = new AuthoringLiveMirroringSystem.MirrorPair {
            mirrorEnabled = true,
            createOppositeTarget = true,
            sourceTarget = source,
            mirroredTarget = target
        };
        LiveMirroringPairStatus status = LiveMirroringService.AnalyzeCandidatePair(
            currentSystem, candidate);
        if (status != LiveMirroringPairStatus.Accepted) {
            pairActionError = LiveMirroringSetupValidation.PairIssue(status);
            RebuildWorkspace();
            return;
        }
        pairActionError = null;
        AddPairInternal(source, target);
    }
    internal static bool TryResolveSelectedPairRoles(
        IReadOnlyList<GameObject> selected,
        GameObject active,
        out Transform source,
        out Transform target) {
        source = null;
        target = null;
        if (selected == null || selected.Count != 2 || active == null ||
            selected[0] == null || selected[1] == null ||
            selected[0] == selected[1])
            return false;
        bool firstIsActive = selected[0] == active;
        bool secondIsActive = selected[1] == active;
        if (firstIsActive == secondIsActive)
            return false;
        target = active.transform;
        source = (firstIsActive ? selected[1] : selected[0]).transform;
        return source != null && target != null && source != target;
    }
    private bool CanAddPairFromSelection() {
        if (currentSystem == null ||
            !TryResolveSelectedPairRoles(
                Selection.gameObjects,
                Selection.activeGameObject,
                out Transform source,
                out Transform target))
            return false;
        var candidate = new AuthoringLiveMirroringSystem.MirrorPair {
            mirrorEnabled = true,
            createOppositeTarget = true,
            sourceTarget = source,
            mirroredTarget = target
        };
        return LiveMirroringService.AnalyzeCandidatePair(currentSystem, candidate) ==
            LiveMirroringPairStatus.Accepted;
    }
    private void RefreshAddSelectedObjectsState() {
        if (addSelectedObjectsButton == null)
            return;
        ThreadlightEditorElements.SetButtonEnabled(
            addSelectedObjectsButton,
            CanAddPairFromSelection(),
            false);
    }
    private void AddPairInternal(Transform source, Transform mirrored) {
        MutateArray("pairs", pairs => {
            int index = pairs.arraySize++;
            SerializedProperty pair = pairs.GetArrayElementAtIndex(index);
            pair.FindPropertyRelative("mirrorEnabled").boolValue = true;
            pair.FindPropertyRelative("createOppositeTarget").boolValue = true;
            pair.FindPropertyRelative("pairName").stringValue = FindAvailableTargetName(pairs);
            pair.FindPropertyRelative("useGlobalSideLabels").boolValue = true;
            pair.FindPropertyRelative("sourceSideLabel").stringValue = "R";
            pair.FindPropertyRelative("mirroredSideLabel").stringValue = "L";
            pair.FindPropertyRelative("sourceBone").enumValueIndex =
                (int)HumanBodyBones.RightHand;
            pair.FindPropertyRelative("mirroredBone").enumValueIndex =
                (int)HumanBodyBones.LeftHand;
            pair.FindPropertyRelative("sourceTarget").objectReferenceValue = source;
            pair.FindPropertyRelative("mirroredTarget").objectReferenceValue = mirrored;
            pair.FindPropertyRelative("mirroredRotationOffset").vector3Value = Vector3.zero;
            return true;
        });
    }
    private static string FindAvailableTargetName(SerializedProperty pairs) {
        HashSet<string> used = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < pairs.arraySize; index++) {
            string name = pairs.GetArrayElementAtIndex(index)
                .FindPropertyRelative("pairName")?.stringValue;
            if (!string.IsNullOrWhiteSpace(name))
                used.Add(name.Trim());
        }
        for (int number = 1; ; number++) {
            string candidate = $"Target {number}";
            if (!used.Contains(candidate))
                return candidate;
        }
    }
    private void RemovePair(int index) {
        MutateArray("pairs", pairs => {
            if (index < 0 || index >= pairs.arraySize) return false;
            List<bool> expansion = CaptureTargetCardExpansion(pairs.arraySize);
            pairs.DeleteArrayElementAtIndex(index);
            expansion.RemoveAt(index);
            StoreTargetCardExpansion(expansion, pairs.arraySize + 1);
            return true;
        });
    }
    private void MovePair(int from, int to) {
        MutateArray("pairs", pairs => {
            if (from < 0 || from >= pairs.arraySize || to < 0 || to >= pairs.arraySize) return false;
            List<bool> expansion = CaptureTargetCardExpansion(pairs.arraySize);
            pairs.MoveArrayElement(from, to);
            bool movedExpansion = expansion[from];
            expansion.RemoveAt(from);
            expansion.Insert(to, movedExpansion);
            StoreTargetCardExpansion(expansion, pairs.arraySize);
            return true;
        });
    }
    private static string TargetCardPath(int index) =>
        $"pairs.Array.data[{index}]";
    private bool GetTargetCardExpansion(int index) {
        string path = TargetCardPath(index);
        if (targetCardExpansion.TryGetValue(path, out bool expanded))
            return expanded;
        expanded = ThreadlightEditorPreferences.GetSessionState(
            TargetCardSurfaceId, currentSystem, path, index == 0);
        targetCardExpansion[path] = expanded;
        return expanded;
    }
    private List<bool> CaptureTargetCardExpansion(int count) {
        List<bool> expansion = new List<bool>(count);
        for (int index = 0; index < count; index++)
            expansion.Add(GetTargetCardExpansion(index));
        return expansion;
    }
    private void StoreTargetCardExpansion(
        IReadOnlyList<bool> expansion,
        int previousCount) {
        targetCardExpansion.Clear();
        for (int index = 0; index < expansion.Count; index++) {
            string path = TargetCardPath(index);
            targetCardExpansion[path] = expansion[index];
            ThreadlightEditorPreferences.SetSessionState(
                TargetCardSurfaceId, currentSystem, path, expansion[index]);
        }
        for (int index = expansion.Count; index < previousCount; index++)
            ThreadlightEditorPreferences.ClearSessionState(
                TargetCardSurfaceId, currentSystem, TargetCardPath(index));
    }
    private void ResetTargetCardExpansionForExternalTopologyChange(int count) {
        targetCardExpansion.Clear();
        for (int index = 0; index < count; index++)
            ThreadlightEditorPreferences.ClearSessionState(
                TargetCardSurfaceId, currentSystem, TargetCardPath(index));
    }
    private void SwapPair(int index) {
        MutateArray("pairs", pairs => {
            if (index < 0 || index >= pairs.arraySize) return false;
            SerializedProperty pair = pairs.GetArrayElementAtIndex(index);
            SerializedProperty source = pair.FindPropertyRelative("sourceTarget");
            SerializedProperty mirrored = pair.FindPropertyRelative("mirroredTarget");
            UnityEngine.Object previous = source.objectReferenceValue;
            source.objectReferenceValue = mirrored.objectReferenceValue;
            mirrored.objectReferenceValue = previous;
            return true;
        });
    }
    private void BuildSetup() {
        if (!SupportsInstalledData(currentSystem)) {
            Debug.LogWarning(
                "ThreadLight Mirroring did not build this setup because its data version is not supported by the installed ThreadLight Authoring package.",
                currentSystem);
            RebuildWorkspace();
            return;
        }
        if (IsCurrentSystemManagedExternally(out string managerName)) {
            Debug.LogWarning(
                $"ThreadLight Mirroring did not build this setup because it is managed by {managerName}.",
                currentSystem);
            RebuildWorkspace();
            return;
        }
        if (currentSystem == null) {
            CreateSetup();
            if (currentSystem == null)
                return;
        }
        serializedSystem?.ApplyModifiedProperties();
        try {
            // Resolve every mutation-critical extension before assigning a
            // container or preview material. Discovery failures must leave an
            // existing setup byte-for-byte unchanged.
            LiveMirroringEditorExtensionRegistry
                .GetTargetBuildContributors();
        }
        catch (InvalidOperationException exception) {
            Debug.LogWarning(
                "ThreadLight Mirroring setup could not build: " +
                exception.Message,
                currentSystem);
            RebuildValidation();
            return;
        }

        Undo.IncrementCurrentGroup();
        int buildGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Live Mirroring Setup");
        try {
            if (!LiveMirroringSetupUtility.EnsureScaleReference(
                    currentSystem,
                    out bool scaleReferenceChanged,
                    out string scaleReferenceError)) {
                Undo.RevertAllDownToGroup(buildGroup);
                RebuildWorkspace();
                Debug.LogWarning(
                    $"ThreadLight Mirroring setup could not build: {scaleReferenceError}",
                    currentSystem);
                return;
            }
            if (LiveMirroringSetupValidation.HasBlockingErrors(currentSystem)) {
                Undo.RevertAllDownToGroup(buildGroup);
                RebuildValidation();
                return;
            }
            EnsureDefaultPreviewMaterial(currentSystem);
            LiveMirroringSetupUtility.BuildResult result =
                LiveMirroringSetupUtility.BuildTargets(currentSystem);
            currentSystem.MirrorAll();
            LiveMirroringPreviewDrawer.RefreshPreview(currentSystem);
            EditorUtility.SetDirty(currentSystem);
            MarkSceneDirty(currentSystem.gameObject);
            SceneView.RepaintAll();
            Undo.CollapseUndoOperations(buildGroup);
            if (result.Changed || scaleReferenceChanged)
                RebuildWorkspace();
        }
        catch (Exception exception) {
            Undo.RevertAllDownToGroup(buildGroup);
            Debug.LogWarning(
                "ThreadLight Mirroring setup could not build and restored " +
                "its previous state: " + exception.GetBaseException().Message,
                currentSystem);
            RebuildWorkspace();
        }
    }
    private void ApplyAndRebuild() {
        CommitSerializedMutation(true, false);
    }
    private void CommitSerializedMutation(bool rebuildWorkspace, bool repaint) {
        serializedSystem.ApplyModifiedProperties();
        EditorUtility.SetDirty(currentSystem);
        PrefabUtility.RecordPrefabInstancePropertyModifications(currentSystem);
        MarkSceneDirty(currentSystem.gameObject);
        if (rebuildWorkspace) RebuildWorkspace(); else RebuildValidation();
        if (repaint) SceneView.RepaintAll();
    }
    private void MutateArray(string path, System.Func<SerializedProperty, bool> change) {
        if (!PrepareSerializedSystem()) return;
        SerializedProperty property = serializedSystem.FindProperty(path);
        if (property != null && change(property)) ApplyAndRebuild();
    }
    private bool PrepareSerializedSystem() {
        if (currentSystem == null)
            return false;
        if (serializedSystem == null ||
            serializedSystem.targetObject != currentSystem) {
            serializedSystem = new SerializedObject(currentSystem);
        }
        serializedSystem.Update();
        return true;
    }
    private void SetSystem(AuthoringLiveMirroringSystem system) {
        ThreadlightContinuousEditGesture.CompleteActiveWithin(rootVisualElement);
        targetCardExpansion.Clear();
        currentSystem = system;
        creationError = null;
        serializedSystem = system != null
            ? new SerializedObject(system)
            : null;
        if (system != null) {
            candidateRoot = LiveMirroringSetupUtility
                .ResolveAuthoringRoot(system)?.gameObject;
            candidateScaleReference = system.scaleReference;
        }
        systemField?.SetValueWithoutNotify(system);
        rootField?.SetValueWithoutNotify(candidateRoot);
        RebuildWorkspace();
    }
    private void UseSelection() {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return;
        AuthoringLiveMirroringSystem selectedSystem = FindSystem(selected);
        if (selectedSystem == null &&
            LegacyLiveMirroringBridge.CanConvert(selected, out _) &&
            !LegacyLiveMirroringBridge.TryConvert(
                selected, out selectedSystem, out creationError))
        {
            candidateRoot = selected;
            RebuildWorkspace();
            return;
        }
        if (selectedSystem != null) {
            SetSystem(selectedSystem);
            return;
        }
        ClearSystemReference();
        candidateRoot = selected;
        candidateScaleReference = null;
        creationError = null;
        rootField?.SetValueWithoutNotify(candidateRoot);
        RebuildWorkspace();
    }
    private void UseSelectionIfHelpful() {
        if (currentSystem == null && Selection.activeGameObject != null)
            UseSelection();
    }
    private void OnSelectionChanged() {
        RefreshAddSelectedObjectsState();
        if (rootVisualElement.panel == null)
            return;
        if (currentSystem == null && candidateRoot == null)
            UseSelectionIfHelpful();
    }
    private void OnUndoRedo() {
        ThreadlightContinuousEditGesture.AbandonActiveWithin(
            rootVisualElement);
        if (currentSystem == null) {
            ClearSystemReference();
            if (TryRestoreCandidateSystem()) return;
            RebuildWorkspace();
            return;
        }
        if (WorkspaceCompositionChanged()) {
            RebuildWorkspace();
            return;
        }
        RefreshRetainedWorkspace();
    }
    private void OnHierarchyChanged() {
        if (currentSystem != null) {
            if (WorkspaceCompositionChanged()) {
                RebuildWorkspace();
                return;
            }
            RefreshRetainedWorkspace();
            return;
        }
        if (serializedSystem == null)
            return;
        ClearSystemReference();
        RebuildWorkspace();
    }
    private void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;
        EditorApplication.delayCall -= RestoreSystemAfterPlayMode;
        EditorApplication.delayCall += RestoreSystemAfterPlayMode;
    }
    private void RestoreSystemAfterPlayMode() {
        EditorApplication.delayCall -= RestoreSystemAfterPlayMode;
        if (currentSystem != null || candidateRoot == null) {
            return;
        }
        if (TryRestoreCandidateSystem()) return;
        ClearSystemReference();
        RebuildWorkspace();
    }
    private bool TryRestoreCandidateSystem() {
        AuthoringLiveMirroringSystem system = FindSystem(candidateRoot);
        if (system == null) return false;
        SetSystem(system);
        return true;
    }
    private void ClearSystemReference() {
        ThreadlightContinuousEditGesture.CompleteActiveWithin(rootVisualElement);
        targetCardExpansion.Clear();
        currentSystem = null;
        serializedSystem = null;
        systemField?.SetValueWithoutNotify(null);
    }
    private static AuthoringLiveMirroringSystem FindSystem(GameObject root) {
        if (root == null) return null;
        AuthoringLiveMirroringSystem direct = root.GetComponent<AuthoringLiveMirroringSystem>();
        if (direct != null) return direct;
        AuthoringLiveMirroringSystem[] systems = LiveMirroringSetupUtility.FindForRoot(root);
        return systems.Length == 1 ? systems[0] : null;
    }
    private void SelectSetupObject() {
        if (currentSystem != null)
            Selection.activeGameObject = currentSystem.gameObject;
    }
    private static void MarkSceneDirty(GameObject target) {
        if (target != null && target.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(target.scene);
    }
    private static void EnsureDefaultPreviewMaterial(
        AuthoringLiveMirroringSystem system) {
        if (system == null || system.previewMaterial != null)
            return;
        Material fallback =
            LiveMirroringSetupUtility.GetDefaultPreviewMaterial();
        if (fallback == null)
            return;
        Undo.RecordObject(system, "Assign Default Preview Material");
        system.previewMaterial = fallback;
        EditorUtility.SetDirty(system);
        PrefabUtility.RecordPrefabInstancePropertyModifications(system);
        MarkSceneDirty(system.gameObject);
    }
}
}
