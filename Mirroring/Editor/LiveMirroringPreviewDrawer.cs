namespace Threadlight.Mirroring.Editor
{
using Threadlight.Mirroring;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LiveMirroringPreviewDrawer
{
    private const int MaximumPreviewInstances = 128;
    private sealed class PreviewState
    {
        public int systemId;
        public AuthoringLiveMirroringSystem system;
        public GameObject source;
        public Material material;
        public readonly List<Transform> targets = new List<Transform>();
        public readonly List<GameObject> instances = new List<GameObject>();
        public readonly List<LiveMirroringPreviewContext> contexts = new List<LiveMirroringPreviewContext>();
    }

    private static readonly Dictionary<int, PreviewState> states = new Dictionary<int, PreviewState>();
    private static readonly Dictionary<ILiveMirroringPreviewContributor, HashSet<int>> failedContributors =
        new Dictionary<ILiveMirroringPreviewContributor, HashSet<int>>();
    private static readonly List<AuthoringLiveMirroringSystem> cachedSystems = new List<AuthoringLiveMirroringSystem>();
    private static readonly HashSet<int> activeIds = new HashSet<int>();
    private static readonly List<int> inactiveIds = new List<int>();
    private static readonly EditorApplication.CallbackFunction updateCallback =
        UpdatePreviews;
    private static bool systemsDirty = true, updateSubscribed;

    static LiveMirroringPreviewDrawer()
    {
        EnsureUpdateSubscribed();
        AssemblyReloadEvents.beforeAssemblyReload += DestroyAllPreviews;
        ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.hierarchyChanged += MarkSystemsDirty;
        Undo.undoRedoPerformed += MarkSystemsDirty;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
    }

    public static void RefreshPreview(AuthoringLiveMirroringSystem system)
    {
        if (system == null) return;
        int id = system.GetInstanceID();
        if (states.TryGetValue(id, out PreviewState state)) { DestroyStateInstances(state); states.Remove(id); }
        MarkSystemsDirty();
        SceneView.RepaintAll();
    }

    private static void UpdatePreviews()
    {
        if (Application.isPlaying)
        {
            DestroyAllPreviews();
            UnsubscribeFromUpdates();
            return;
        }
        if (systemsDirty) RefreshSystems();
        activeIds.Clear();
        for (int i = 0; i < cachedSystems.Count; i++) UpdateSystem(cachedSystems[i]);
        CleanupInactiveStates();
        if (!systemsDirty && activeIds.Count == 0) UnsubscribeFromUpdates();
    }

    private static void UpdateSystem(AuthoringLiveMirroringSystem system)
    {
        if (system == null || !system.isActiveAndEnabled || !system.showScenePreview || system.previewSource == null) return;
        LiveMirroringEvaluationBuffers evaluation = LiveMirroringService.Evaluate(system);
        IReadOnlyList<Transform> targets = evaluation.Targets;
        if (system.liveMirror) LiveMirroringService.ApplyScaleReference(system, evaluation);
        int count = Mathf.Min(targets.Count, MaximumPreviewInstances);
        if (count == 0) return;
        int id = system.GetInstanceID();
        activeIds.Add(id);
        if (!states.TryGetValue(id, out PreviewState state))
            states.Add(id, state = new PreviewState { systemId = id, system = system });
        Material material = system.previewMaterial ?? LiveMirroringSetupUtility.GetDefaultPreviewMaterial();
        if (state.source != system.previewSource || state.material != material || !TargetsMatch(state.targets, targets, count))
            RebuildState(state, system, targets, count, material);
        UpdateInstanceTransforms(state);
    }

    private static bool TargetsMatch(List<Transform> previous, IReadOnlyList<Transform> current, int count)
    {
        if (previous.Count != count) return false;
        for (int i = 0; i < count; i++) if (previous[i] != current[i]) return false;
        return true;
    }

    private static void RebuildState(PreviewState state, AuthoringLiveMirroringSystem system,
        IReadOnlyList<Transform> targets, int count, Material material)
    {
        DestroyStateInstances(state);
        state.system = system; state.source = system.previewSource; state.material = material;
        for (int i = 0; i < count; i++) state.targets.Add(targets[i]);
        for (int i = 0; i < count; i++)
        {
            GameObject container = new GameObject($"[Scene Preview] {system.name} {i + 1}")
                { hideFlags = HideFlags.HideAndDontSave };
            GameObject clone = Object.Instantiate(system.previewSource);
            clone.name = system.previewSource.name;
            clone.transform.SetParent(container.transform, false);
            ConfigureGhost(container, material);
            state.instances.Add(container);
            LiveMirroringPreviewContext context = new LiveMirroringPreviewContext(system, targets[i], container);
            state.contexts.Add(context);
            Notify(context, true);
        }
    }

    private static void UpdateInstanceTransforms(PreviewState state)
    {
        for (int i = 0; i < state.instances.Count; i++)
        {
            if (state.instances[i] == null || state.targets[i] == null) continue;
            Transform instance = state.instances[i].transform, target = state.targets[i];
            if ((instance.position - target.position).sqrMagnitude > .00000001f) instance.position = target.position;
            if (Quaternion.Angle(instance.rotation, target.rotation) > .001f) instance.rotation = target.rotation;
            if ((instance.localScale - target.lossyScale).sqrMagnitude > .00000001f) instance.localScale = target.lossyScale;
            Notify(state.contexts[i], false);
        }
    }

    private static void Notify(LiveMirroringPreviewContext context, bool created)
    {
        IReadOnlyList<ILiveMirroringPreviewContributor> contributors =
            LiveMirroringEditorExtensionRegistry.GetPreviewContributors();
        for (int i = 0; i < contributors.Count; i++)
        {
            ILiveMirroringPreviewContributor contributor = contributors[i];
            int id = context.System.GetInstanceID();
            if (failedContributors.TryGetValue(contributor, out HashSet<int> failures) && failures.Contains(id)) continue;
            try
            {
                if (created) contributor.OnPreviewCreated(context); else contributor.UpdatePreview(context);
            }
            catch (System.Exception exception)
            {
                if (failures == null) failedContributors.Add(contributor, failures = new HashSet<int>());
                failures.Add(id);
                LiveMirroringExtensionHealthJournal.RecordIsolatedFailure(
                    LiveMirroringExtensionCapabilities.Preview,
                    LiveMirroringEditorExtensionRegistry.GetContributorId(contributor),
                    contributor.GetType(),
                    "preview",
                    exception);
                Debug.LogException(exception, context.System);
            }
        }
    }

    private static void ConfigureGhost(GameObject root, Material material)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.HideAndDontSave;
        foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (AudioSource audio in root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (material == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static void RefreshSystems()
    {
        cachedSystems.Clear(); cachedSystems.AddRange(Object.FindObjectsOfType<AuthoringLiveMirroringSystem>(true));
        systemsDirty = false;
    }

    private static void MarkSystemsDirty() { systemsDirty = true; EnsureUpdateSubscribed(); }
    private static void OnObjectChangesPublished(ref ObjectChangeEventStream changes)
    {
        // Serialized preview settings can change without a hierarchy event.
        // A single wake pass is cheap and returns to idle when no preview is eligible.
        EnsureUpdateSubscribed();
    }
    private static void EnsureUpdateSubscribed()
    {
        if (updateSubscribed || Application.isPlaying) return;
        EditorApplication.update += updateCallback; updateSubscribed = true;
    }
    private static void UnsubscribeFromUpdates()
    {
        if (!updateSubscribed) return;
        EditorApplication.update -= updateCallback; updateSubscribed = false;
    }

    private static void CleanupInactiveStates()
    {
        inactiveIds.Clear();
        foreach (int id in states.Keys) if (!activeIds.Contains(id)) inactiveIds.Add(id);
        for (int i = 0; i < inactiveIds.Count; i++)
        { int id = inactiveIds[i]; DestroyStateInstances(states[id]); states.Remove(id); }
    }

    private static void DestroyStateInstances(PreviewState state)
    {
        for (int i = 0; i < state.instances.Count; i++)
            if (state.instances[i] != null) Object.DestroyImmediate(state.instances[i]);
        state.instances.Clear(); state.targets.Clear(); state.contexts.Clear();
        foreach (HashSet<int> failures in failedContributors.Values) failures.Remove(state.systemId);
    }

    private static void DestroyAllPreviews()
    {
        foreach (PreviewState state in states.Values) DestroyStateInstances(state);
        states.Clear();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) { DestroyAllPreviews(); UnsubscribeFromUpdates(); }
        systemsDirty = true;
        if (state == PlayModeStateChange.EnteredEditMode) EnsureUpdateSubscribed();
    }
    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => MarkSystemsDirty();
    private static void OnSceneClosed(Scene scene) => MarkSystemsDirty();
    private static void OnActiveSceneChanged(Scene previous, Scene current) => MarkSystemsDirty();
}
}
