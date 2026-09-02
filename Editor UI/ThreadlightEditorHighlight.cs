namespace Threadlight.EditorUI {
using System;
using System.Collections.Generic;
using UnityEngine;
public static class ThreadlightEditorHighlight {
    private sealed class HoverState {
        public int TargetId;
        public readonly HashSet<string> Paths = new HashSet<string>();
    }
    public readonly struct HighlightScope : IDisposable {
        private readonly int previousScopeId;
        internal HighlightScope(int previousScopeId) {
            this.previousScopeId = previousScopeId;
        }
        public void Dispose() {
            currentScopeId = previousScopeId;
        }
    }
    private const string ModulePrefix = "@module:";
    private const string FeaturePrefix = "@feature:";
    private const string ItemPrefix = "@item:";
    private static int currentScopeId;
    private static readonly HashSet<string> ComparisonPaths =
        new HashSet<string>();
    private static readonly Dictionary<int, HoverState> HoverByScope =
        new Dictionary<int, HoverState>();
    public static HighlightScope PushScope(UnityEngine.Object owner) {
        int previous = currentScopeId;
        currentScopeId = owner != null ? owner.GetInstanceID() : 0;
        return new HighlightScope(previous);
    }
    public static bool Hover(
        UnityEngine.Object target,
        IEnumerable<string> propertyPaths) {
        int targetId = target != null ? target.GetInstanceID() : 0;
        HoverState state = GetState();
        if (targetId == state.TargetId && Matches(state.Paths, propertyPaths))
            return false;
        state.TargetId = targetId;
        Replace(state.Paths, propertyPaths);
        return true;
    }
    public static bool IsPathHovered(
        UnityEngine.Object target,
        string propertyPath) {
        HoverState state = GetState();
        return target != null &&
               target.GetInstanceID() == state.TargetId &&
               !string.IsNullOrWhiteSpace(propertyPath) &&
               state.Paths.Contains(propertyPath);
    }
    public static string Module(string moduleId) =>
        Prefix(ModulePrefix, moduleId);
    public static string Feature(string generationModuleId) =>
        Prefix(FeaturePrefix, generationModuleId);
    public static string Item(string itemId) =>
        Prefix(ItemPrefix, itemId);
    private static HoverState GetState() {
        if (!HoverByScope.TryGetValue(currentScopeId, out HoverState state)) {
            state = new HoverState();
            HoverByScope[currentScopeId] = state;
        }
        return state;
    }
    private static string Prefix(string prefix, string value) =>
        string.IsNullOrWhiteSpace(value) ? null : prefix + value;
    private static void Replace(
        HashSet<string> destination,
        IEnumerable<string> values) {
        destination.Clear();
        if (values == null)
            return;
        foreach (string value in values) {
            if (!string.IsNullOrWhiteSpace(value))
                destination.Add(value);
        }
    }
    private static bool Matches(
        HashSet<string> current,
        IEnumerable<string> values) {
        if (values == null)
            return current.Count == 0;
        ComparisonPaths.Clear();
        foreach (string value in values) {
            if (!string.IsNullOrWhiteSpace(value))
                ComparisonPaths.Add(value);
        }
        return current.SetEquals(ComparisonPaths);
    }
}
}
