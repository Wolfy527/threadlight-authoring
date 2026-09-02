namespace Threadlight.EditorUI
{
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// User preferences and session-only presentation state shared by Threadlight.
/// This class never stores serialized authoring values or object bindings.
/// </summary>
public static class ThreadlightEditorPreferences
{
    private const string ReducedMotionKey = "Threadlight.EditorUI.ReducedMotion";
    private const string ReducedMotionMenu = "Tools/ThreadLight/Reduced Motion";
    private const string PresentationPrefix = "Threadlight.EditorUI.Presentation.";

    /// <summary>Raised after the user changes the shared motion preference.</summary>
    public static event Action ReducedMotionChanged;

    /// <summary>
    /// Disables ambient and transition motion. The default is false so existing
    /// installations retain their current presentation until the user opts in.
    /// </summary>
    public static bool ReducedMotion
    {
        get => EditorPrefs.GetBool(ReducedMotionKey, false);
        set
        {
            if (value == ReducedMotion)
                return;
            EditorPrefs.SetBool(ReducedMotionKey, value);
            Menu.SetChecked(ReducedMotionMenu, value);
            ReducedMotionChanged?.Invoke();
        }
    }

    [MenuItem(ReducedMotionMenu, priority = 1900)]
    private static void ToggleReducedMotion() => ReducedMotion = !ReducedMotion;

    [MenuItem(ReducedMotionMenu, true)]
    private static bool ValidateReducedMotion()
    {
        Menu.SetChecked(ReducedMotionMenu, ReducedMotion);
        return true;
    }

    /// <summary>
    /// Reads a foldout or similar presentation-only flag for the current Unity
    /// session. A target is used only when it has a reload-stable project ID.
    /// </summary>
    public static bool GetSessionState(
        string surfaceId,
        UnityEngine.Object target,
        string elementId,
        bool fallback)
    {
        return TryBuildPresentationKey(surfaceId, target, elementId, out string key)
            ? SessionState.GetBool(key, fallback)
            : fallback;
    }

    /// <summary>Stores a presentation-only flag until the Unity session ends.</summary>
    public static void SetSessionState(
        string surfaceId,
        UnityEngine.Object target,
        string elementId,
        bool value)
    {
        if (TryBuildPresentationKey(surfaceId, target, elementId, out string key))
            SessionState.SetBool(key, value);
    }

    /// <summary>Removes presentation-only state that no longer has an element.</summary>
    public static void ClearSessionState(
        string surfaceId,
        UnityEngine.Object target,
        string elementId)
    {
        if (TryBuildPresentationKey(surfaceId, target, elementId, out string key))
            SessionState.EraseBool(key);
    }

    private static bool TryBuildPresentationKey(
        string surfaceId,
        UnityEngine.Object target,
        string elementId,
        out string key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(surfaceId) || string.IsNullOrWhiteSpace(elementId))
            return false;

        string targetId = "window";
        if (target != null)
        {
            if (!HasStableProjectIdentity(target))
                return false;
            targetId = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
        }

        key = PresentationPrefix + surfaceId.Trim() + "." + targetId + "." + elementId.Trim();
        return true;
    }

    private static bool HasStableProjectIdentity(UnityEngine.Object target)
    {
        if (EditorUtility.IsPersistent(target))
            return true;
        if (target is Component component)
            return !string.IsNullOrWhiteSpace(component.gameObject.scene.path);
        if (target is GameObject gameObject)
            return !string.IsNullOrWhiteSpace(gameObject.scene.path);
        return false;
    }
}
}
