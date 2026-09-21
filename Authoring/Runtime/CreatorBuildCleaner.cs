namespace Threadlight.Authoring {
using System.Collections.Generic;
using UnityEngine;

public static class CreatorBuildCleaner {
    public struct CleanupReport {
        public int ComponentsRemoved;
        public int GameObjectsRemoved;
        public bool HasChanges => ComponentsRemoved > 0 || GameObjectsRemoved > 0;
    }

    public static CleanupReport StripAuthoringComponentsFrom(GameObject root) {
        if (root == null) return default;
        if (!CanStripAuthoringComponentsFrom(
                root, out string failureMessage, out Object failureContext)) {
            Debug.LogError(failureMessage, failureContext);
            return default;
        }
        StripComponents(root, out HashSet<GameObject> generatedObjects, out int componentCount);
        RemoveGeneratedObjects(generatedObjects);
        return new CleanupReport {
            ComponentsRemoved = componentCount,
            GameObjectsRemoved = generatedObjects.Count
        };
    }

    /// <summary>
    /// Checks whether upload cleanup can remove every authoring holder without
    /// deleting the avatar root or unrelated runtime components. This method is
    /// intentionally read-only so every preprocessing entry point can fail
    /// before cleanup changes the upload copy.
    /// </summary>
    public static bool CanStripAuthoringComponentsFrom(
        GameObject root,
        out string failureMessage,
        out Object failureContext) {
        failureMessage = null;
        failureContext = root;
        if (root == null) return true;
        if (!root.scene.IsValid() || IsPersistent(root)) {
            failureMessage =
                "ThreadLight Authoring cleanup can only run on the scene or upload copy, not directly on a prefab asset.";
            return false;
        }

        CreatorAuthoringComponent[] components =
            root.GetComponentsInChildren<CreatorAuthoringComponent>(true);
        for (int index = 0; index < components.Length; index++) {
            CreatorAuthoringComponent component = components[index];
            if (component == null || !component.RemoveGameObjectWithComponent)
                continue;
            if (!CanRemoveAuthoringHolder(
                    component, root, out failureMessage, out failureContext))
                return false;
        }
        failureContext = root;
        return true;
    }

    /// <summary>Read-only safety check for one holder removed as a whole.</summary>
    public static bool CanRemoveAuthoringHolder(
        CreatorAuthoringComponent component,
        GameObject cleanupRoot,
        out string failureMessage,
        out Object failureContext) {
        failureMessage = null;
        failureContext = component;
        if (component == null || !component.RemoveGameObjectWithComponent)
            return true;
        GameObject holder = component.gameObject;
        failureContext = holder;
        if (!holder.scene.IsValid() || IsPersistent(holder)) {
            failureMessage =
                "ThreadLight Authoring cleanup cannot remove a holder directly from a prefab asset. Use a scene or upload copy instead.";
            return false;
        }
        if (cleanupRoot == null || !cleanupRoot.scene.IsValid() ||
            IsPersistent(cleanupRoot)) {
            failureContext = cleanupRoot != null ? cleanupRoot : holder;
            failureMessage =
                "ThreadLight Authoring cleanup can only run on the scene or upload copy, not directly on a prefab asset.";
            return false;
        }
        if (holder == cleanupRoot) {
            failureMessage =
                "ThreadLight cannot remove this authoring holder because it is the avatar upload root. Move the authoring component to its dedicated EditorOnly child object, then upload again.";
            return false;
        }
        if (!holder.transform.IsChildOf(cleanupRoot.transform)) {
            failureMessage =
                "ThreadLight cannot remove this authoring holder because it is outside the cleanup root. Run cleanup from the setup's scene root.";
            return false;
        }
        Component[] holderComponents = holder.GetComponents<Component>();
        for (int index = 0; index < holderComponents.Length; index++) {
            Component holderComponent = holderComponents[index];
            if (holderComponent is Transform ||
                holderComponent is CreatorAuthoringComponent)
                continue;
            string componentName = holderComponent == null
                ? "a missing script"
                : holderComponent.GetType().Name;
            failureMessage =
                $"ThreadLight cannot remove the authoring holder '{holder.name}' because it also contains {componentName}. Move unrelated runtime components to another object, then upload again.";
            return false;
        }
        return true;
    }

    public static void StripAuthoringComponent(CreatorAuthoringComponent component) {
        if (component == null) return;
        GameObject cleanupRoot = component.transform.root.gameObject;
        if (!CanStripAuthoringComponentsFrom(
                cleanupRoot,
                out string failureMessage,
                out Object failureContext)) {
            Debug.LogError(failureMessage, failureContext);
            return;
        }
        if (component.RemoveGameObjectWithComponent)
            RemoveGeneratedObjectPreservingChildren(component.gameObject, null);
        else
            DestroyObject(component);
    }

    private static void StripComponents(GameObject root,
        out HashSet<GameObject> generatedObjects, out int componentCount) {
        generatedObjects = new HashSet<GameObject>();
        CreatorAuthoringComponent[] components =
            root.GetComponentsInChildren<CreatorAuthoringComponent>(true);
        componentCount = components.Length;
        foreach (CreatorAuthoringComponent component in components) {
            if (component == null) continue;
            if (component.RemoveGameObjectWithComponent) {
                generatedObjects.Add(component.gameObject);
                continue;
            }
            DestroyObject(component);
        }
    }

    private static void RemoveGeneratedObjects(HashSet<GameObject> generatedObjects) {
        List<GameObject> ordered = new List<GameObject>(generatedObjects);
        ordered.Sort((left, right) =>
            HierarchyDepth(right).CompareTo(HierarchyDepth(left)));
        foreach (GameObject generatedObject in ordered)
            if (generatedObject != null)
                RemoveGeneratedObjectPreservingChildren(generatedObject, generatedObjects);
    }

    private static int HierarchyDepth(GameObject item) {
        int depth = 0;
        for (Transform current = item != null ? item.transform.parent : null;
             current != null; current = current.parent)
            depth++;
        return depth;
    }

    // Lift creator content out before deleting editor-only holders. Processing
    // deepest-first also preserves creator grandchildren below nested helpers.
    private static void RemoveGeneratedObjectPreservingChildren(
        GameObject generatedObject, HashSet<GameObject> generatedObjects) {
        Transform generatedTransform = generatedObject.transform;
        Transform parent = generatedTransform.parent;
        int insertionIndex = generatedTransform.GetSiblingIndex();
        List<Transform> retainedChildren = new List<Transform>();
        for (int index = 0; index < generatedTransform.childCount; index++) {
            Transform child = generatedTransform.GetChild(index);
            if (generatedObjects == null || !generatedObjects.Contains(child.gameObject))
                retainedChildren.Add(child);
        }
        foreach (Transform child in retainedChildren) {
            child.SetParent(parent, true);
            child.SetSiblingIndex(insertionIndex++);
        }
        DestroyObject(generatedObject);
    }

    private static void DestroyObject(Object target) {
        if (target == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) {
            Object.DestroyImmediate(target);
            return;
        }
#endif
        Object.Destroy(target);
    }

    private static bool IsPersistent(Object target) {
#if UNITY_EDITOR
        return target != null && UnityEditor.EditorUtility.IsPersistent(target);
#else
        return false;
#endif
    }
}
}
