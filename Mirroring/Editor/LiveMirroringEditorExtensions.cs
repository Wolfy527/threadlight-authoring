namespace Threadlight.Mirroring.Editor
{
using Threadlight.Mirroring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public interface ILiveMirroringInspectorElementContributor
{
    string ContributorId { get; }
    int Order { get; }
    VisualElement CreateElement(SerializedObject system);
}

public enum LiveMirroringValidationSeverity { Info, Warning, Error }

public sealed class LiveMirroringValidationMessage
{
    public LiveMirroringValidationSeverity Severity { get; }
    public string Title { get; }
    public string Message { get; }
    public string PropertyPath { get; }
    public LiveMirroringValidationMessage(LiveMirroringValidationSeverity severity, string title,
        string message = null, string propertyPath = null)
    {
        Severity = severity; Title = title; Message = message; PropertyPath = propertyPath;
    }
}

public interface ILiveMirroringValidationContributor
{
    string ContributorId { get; }
    int Order { get; }
    void Validate(SerializedObject system, List<LiveMirroringValidationMessage> messages);
}

public sealed class LiveMirroringTargetBuildContext
{
    public AuthoringLiveMirroringSystem System { get; }
    public AuthoringLiveMirroringSystem.MirrorPair Target { get; }
    public int TargetIndex { get; }
    public LiveMirroringTargetBuildContext(AuthoringLiveMirroringSystem system,
        AuthoringLiveMirroringSystem.MirrorPair target, int targetIndex)
    { System = system; Target = target; TargetIndex = targetIndex; }
}

public interface ILiveMirroringTargetBuildContributor
{
    string ContributorId { get; }
    int Order { get; }
    /// <summary>
    /// Applies one target-pair integration inside the caller's Undo
    /// transaction. Implementations must register every mutation with Undo;
    /// an exception aborts and rolls back the complete target build.
    /// </summary>
    int Apply(LiveMirroringTargetBuildContext context);
}

public sealed class LiveMirroringSetupOwnershipContext
{
    public GameObject PrefabRoot { get; }
    public AuthoringLiveMirroringSystem System { get; }
    public LiveMirroringSetupOwnershipContext(GameObject prefabRoot, AuthoringLiveMirroringSystem system)
    { PrefabRoot = prefabRoot; System = system; }
}

public interface ILiveMirroringSetupOwnershipContributor
{
    string ContributorId { get; }
    int Order { get; }
    string OwnerDisplayName { get; }
    bool Claims(LiveMirroringSetupOwnershipContext context);
}

public sealed class LiveMirroringPreviewContext
{
    public AuthoringLiveMirroringSystem System { get; }
    public Transform Target { get; }
    public GameObject PreviewInstance { get; }
    public LiveMirroringPreviewContext(AuthoringLiveMirroringSystem system, Transform target, GameObject previewInstance)
    { System = system; Target = target; PreviewInstance = previewInstance; }
}

public interface ILiveMirroringPreviewContributor
{
    string ContributorId { get; }
    int Order { get; }
    void OnPreviewCreated(LiveMirroringPreviewContext context);
    void UpdatePreview(LiveMirroringPreviewContext context);
}

public static class LiveMirroringEditorExtensionRegistry
{
    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object left, object right) => ReferenceEquals(left, right);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }

    private static ILiveMirroringInspectorElementContributor[] inspectors;
    private static ILiveMirroringValidationContributor[] validators;
    private static ILiveMirroringTargetBuildContributor[] builders;
    private static ILiveMirroringSetupOwnershipContributor[] owners;
    private static ILiveMirroringPreviewContributor[] previews;
    private static readonly Dictionary<object, string> ids = new Dictionary<object, string>(new ReferenceComparer());
    private static int generation;

    internal static int Generation => generation;

    public static IReadOnlyList<ILiveMirroringInspectorElementContributor> GetInspectorElements() =>
        inspectors ??= Load<ILiveMirroringInspectorElementContributor>(value => value.ContributorId, value => value.Order,
            LiveMirroringExtensionLoadPolicy.OptionalIsolated,
            LiveMirroringExtensionCapabilities.Inspector);
    public static IReadOnlyList<ILiveMirroringValidationContributor> GetValidators() =>
        validators ??= Load<ILiveMirroringValidationContributor>(value => value.ContributorId, value => value.Order,
            LiveMirroringExtensionLoadPolicy.OptionalIsolated,
            LiveMirroringExtensionCapabilities.Validation);
    public static IReadOnlyList<ILiveMirroringTargetBuildContributor> GetTargetBuildContributors() =>
        builders ??= Load<ILiveMirroringTargetBuildContributor>(value => value.ContributorId, value => value.Order,
            LiveMirroringExtensionLoadPolicy.MutationCriticalFailClosed,
            LiveMirroringExtensionCapabilities.TargetBuild);
    public static IReadOnlyList<ILiveMirroringSetupOwnershipContributor> GetSetupOwnershipContributors() =>
        owners ??= Load<ILiveMirroringSetupOwnershipContributor>(value => value.ContributorId, value => value.Order,
            LiveMirroringExtensionLoadPolicy.MutationCriticalFailClosed,
            LiveMirroringExtensionCapabilities.SetupOwnership);
    public static IReadOnlyList<ILiveMirroringPreviewContributor> GetPreviewContributors() =>
        previews ??= Load<ILiveMirroringPreviewContributor>(value => value.ContributorId, value => value.Order,
            LiveMirroringExtensionLoadPolicy.OptionalIsolated,
            LiveMirroringExtensionCapabilities.Preview);

    public static void Refresh()
    {
        inspectors = null; validators = null; builders = null; owners = null; previews = null; ids.Clear();
        unchecked
        {
            generation++;
            if (generation == 0) generation++;
        }
        LiveMirroringExtensionHealthJournal.Clear(
            LiveMirroringExtensionCapabilities.Inspector,
            LiveMirroringExtensionCapabilities.Validation,
            LiveMirroringExtensionCapabilities.TargetBuild,
            LiveMirroringExtensionCapabilities.SetupOwnership,
            LiveMirroringExtensionCapabilities.Preview);
    }

    internal static string GetContributorId(object contributor) => contributor != null && ids.TryGetValue(contributor,
        out string id) ? id : contributor?.GetType().FullName ?? "<unknown>";

    internal static void DispatchOptionalIsolated<T>(IReadOnlyList<T> contributors, HashSet<string> failed,
        string capability, UnityEngine.Object context, Action<T> action) where T : class
    {
        for (int i = 0; i < contributors.Count; i++)
        {
            T contributor = contributors[i];
            string id = GetContributorId(contributor);
            if (contributor == null || failed.Contains(id)) continue;
            try { action(contributor); }
            catch (Exception exception)
            {
                failed.Add(id);
                LiveMirroringExtensionHealthJournal.RecordIsolatedFailure(
                    capability, id, contributor.GetType(), "callback", exception);
                Debug.LogException(exception, context);
            }
        }
    }

    internal static void DispatchMutationCriticalFailClosed<T>(
        IReadOnlyList<T> contributors,
        Action<T> action) where T : class
    {
        if (contributors == null || action == null) return;
        for (int i = 0; i < contributors.Count; i++)
        {
            T contributor = contributors[i];
            string id = GetContributorId(contributor);
            if (contributor == null)
                throw new InvalidOperationException(
                    "Live Mirroring mutation contributor '" + id +
                    "' is unavailable; target generation was stopped before commit.");
            try { action(contributor); }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Live Mirroring mutation contributor '" + id +
                    "' failed; the complete target build will be rolled back.",
                    exception);
            }
        }
    }

    private static T[] Load<T>(Func<T, string> id, Func<T, int> order,
        LiveMirroringExtensionLoadPolicy policy, string capability) where T : class
    {
        LiveMirroringDiscovered<T>[] found = LiveMirroringDiscovery.Find(
            id, order, "editor extensions", policy, capability);
        for (int i = 0; i < found.Length; i++) ids[found[i].Instance] = found[i].Id;
        return found.Select(value => value.Instance).ToArray();
    }
}
}
