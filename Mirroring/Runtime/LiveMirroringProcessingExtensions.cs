namespace Threadlight.Mirroring
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using Threadlight.Authoring;

public enum LiveMirroringProcessingStage { BeforeCore, AfterCore }

/// <summary>Public editor-time extension point around the built-in mirroring pass.</summary>
public interface ILiveMirroringProcessor
{
    string ProcessorId { get; }
    int Order { get; }
    LiveMirroringProcessingStage Stage { get; }
    void Process(AuthoringLiveMirroringSystem system);
}

#if UNITY_EDITOR
internal sealed class LiveMirroringDiscovered<T> where T : class
{
    public T Instance;
    public string Id;
    public int Order;
    public int Group;
    public string Capability;
}

internal enum LiveMirroringExtensionLoadPolicy
{
    OptionalIsolated,
    MutationCriticalFailClosed
}

/// <summary>One collision-safe TypeCache discovery path for every public Live Mirroring extension.</summary>
internal static class LiveMirroringDiscovery
{
    internal static LiveMirroringDiscovered<T>[] Find<T>(Func<T, string> id, Func<T, int> order,
        string category, LiveMirroringExtensionLoadPolicy policy,
        string capability, Func<T, int> group = null,
        Func<int, string> groupedCapability = null) where T : class
    {
        List<string> failures = new List<string>();
        List<ThreadlightExtensionHealthDescriptor> health =
            new List<ThreadlightExtensionHealthDescriptor>();
        List<LiveMirroringDiscovered<T>> discovered =
            new List<LiveMirroringDiscovered<T>>();
        foreach (Type type in UnityEditor.TypeCache.GetTypesDerivedFrom<T>()
                     .Where(type => type != null && !type.IsAbstract &&
                                    !type.IsInterface &&
                                    (type.IsPublic || type.IsNestedPublic)))
        {
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                health.Add(LiveMirroringExtensionHealthJournal.Descriptor(
                    type, null, null, capability,
                    ThreadlightExtensionDiscoveryStatus.DisabledMissingConstructor));
                if (policy ==
                    LiveMirroringExtensionLoadPolicy.MutationCriticalFailClosed)
                    Report(policy, failures, category,
                        $"could not create '{type.FullName}': a public " +
                        "parameterless constructor is required.");
                continue;
            }
            T value = Create<T>(type, category, policy, failures);
            if (value == null)
            {
                health.Add(LiveMirroringExtensionHealthJournal.Descriptor(
                    type, null, null, capability,
                    ThreadlightExtensionDiscoveryStatus.DisabledConstructionFailed));
                continue;
            }
            LiveMirroringDiscovered<T> metadata = Metadata(
                value, id, order, group, category, policy, failures);
            if (metadata == null)
            {
                health.Add(LiveMirroringExtensionHealthJournal.Descriptor(
                    type, null, null, capability,
                    ThreadlightExtensionDiscoveryStatus.DisabledMetadataInvalid));
                continue;
            }
            metadata.Capability = groupedCapability != null
                ? groupedCapability(metadata.Group)
                : capability;
            discovered.Add(metadata);
        }
        List<LiveMirroringDiscovered<T>> result =
            new List<LiveMirroringDiscovered<T>>();
        foreach (IGrouping<string, LiveMirroringDiscovered<T>> idGroup in
                 discovered.GroupBy(value => value.Id))
        {
            LiveMirroringDiscovered<T>[] values = idGroup.ToArray();
            LiveMirroringDiscovered<T> selected = Collision(
                idGroup, category, policy, failures);
            for (int index = 0; index < values.Length; index++)
            {
                bool active = ReferenceEquals(values[index], selected);
                health.Add(LiveMirroringExtensionHealthJournal.Descriptor(
                    values[index].Instance.GetType(),
                    values[index].Id,
                    values[index].Order,
                    values[index].Capability,
                    values.Length == 1
                        ? ThreadlightExtensionDiscoveryStatus.Active
                        : active
                            ? ThreadlightExtensionDiscoveryStatus.ActiveBuiltInPreferred
                            : ThreadlightExtensionDiscoveryStatus.DisabledIdCollision));
            }
            if (selected != null)
                result.Add(selected);
        }
        string[] ownedCapabilities = groupedCapability == null
            ? new[] { capability }
            : discovered.Select(value => value.Capability)
                .Append(capability).Distinct(StringComparer.Ordinal).ToArray();
        LiveMirroringExtensionHealthJournal.ReplaceDiscovery(
            ownedCapabilities, health);
        LiveMirroringDiscovered<T>[] ordered = result
            .OrderBy(value => value.Group)
            .ThenBy(value => value.Order)
            .ThenBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        if (policy == LiveMirroringExtensionLoadPolicy.MutationCriticalFailClosed &&
            failures.Count != 0)
            throw new InvalidOperationException(
                $"Live Mirroring {category} discovery failed closed:\n- " +
                string.Join("\n- ", failures.Distinct(StringComparer.Ordinal)));
        return ordered;
    }

    private static T Create<T>(Type type, string category,
        LiveMirroringExtensionLoadPolicy policy, ICollection<string> failures)
        where T : class
    {
        try { return (T)Activator.CreateInstance(type); }
        catch (Exception exception)
        {
            Report(policy, failures, category,
                $"could not create '{type.FullName}': {exception.GetBaseException().Message}");
            return null;
        }
    }

    private static LiveMirroringDiscovered<T> Metadata<T>(T value, Func<T, string> id, Func<T, int> order,
        Func<T, int> group, string category,
        LiveMirroringExtensionLoadPolicy policy, ICollection<string> failures)
        where T : class
    {
        try
        {
            string resolved = id(value);
            if (policy == LiveMirroringExtensionLoadPolicy.MutationCriticalFailClosed &&
                (string.IsNullOrWhiteSpace(resolved) || resolved != resolved.Trim()))
                throw new InvalidOperationException(
                    "Mutation contributor IDs must be non-blank and trimmed.");
            return new LiveMirroringDiscovered<T> { Instance = value,
                Id = string.IsNullOrWhiteSpace(resolved) ? value.GetType().FullName : resolved.Trim(),
                Order = order(value), Group = group?.Invoke(value) ?? 0 };
        }
        catch (Exception exception)
        {
            Report(policy, failures, category,
                $"could not read metadata from '{value.GetType().FullName}': " +
                exception.GetBaseException().Message);
            return null;
        }
    }

    private static LiveMirroringDiscovered<T> Collision<T>(IGrouping<string, LiveMirroringDiscovered<T>> group,
        string category, LiveMirroringExtensionLoadPolicy policy,
        ICollection<string> failures) where T : class
    {
        LiveMirroringDiscovered<T>[] values = group.ToArray();
        if (values.Length == 1) return values[0];
        LiveMirroringDiscovered<T>[] builtIn = values.Where(value =>
            value.Instance.GetType().Assembly == typeof(T).Assembly).ToArray();
        LiveMirroringDiscovered<T> selected =
            policy == LiveMirroringExtensionLoadPolicy.OptionalIsolated &&
            builtIn.Length == 1 ? builtIn[0] : null;
        Report(policy, failures, category,
            $"found {values.Length} extensions using ID '{group.Key}'. " +
            (selected != null ? $"The built-in '{selected.Instance.GetType().FullName}' was retained." :
                "All conflicting extensions were disabled.") + " Conflicting IDs must be renamed.");
        return selected;
    }

    private static void Report(LiveMirroringExtensionLoadPolicy policy,
        ICollection<string> failures, string category, string message)
    {
        string full = category + " " + message;
        if (policy == LiveMirroringExtensionLoadPolicy.MutationCriticalFailClosed)
            failures.Add(full);
        else
            Debug.LogWarning("Live Mirroring optional " + full);
    }
}

public static class LiveMirroringProcessorRegistry
{
    private sealed class ReferenceComparer : IEqualityComparer<ILiveMirroringProcessor>
    {
        public bool Equals(ILiveMirroringProcessor left, ILiveMirroringProcessor right) => ReferenceEquals(left, right);
        public int GetHashCode(ILiveMirroringProcessor value) => RuntimeHelpers.GetHashCode(value);
    }

    private static LiveMirroringDiscovered<ILiveMirroringProcessor>[] descriptors;
    private static ILiveMirroringProcessor[] processors;
    private static int failureGeneration = 1;

    public static IReadOnlyList<ILiveMirroringProcessor> GetProcessors() => processors ??=
        GetDescriptors().Select(value => value.Instance).ToArray();

    public static void Run(AuthoringLiveMirroringSystem system, LiveMirroringProcessingStage stage)
    {
        if (system == null) return;
        if (system.mirroringProcessorFailureGeneration != failureGeneration)
        {
            system.failedMirroringProcessors?.Clear();
            system.mirroringProcessorFailureGeneration = failureGeneration;
        }
        LiveMirroringDiscovered<ILiveMirroringProcessor>[] values = GetDescriptors();
        for (int i = 0; i < values.Length; i++)
        {
            ILiveMirroringProcessor processor = values[i].Instance;
            if (values[i].Group != (int)stage || system.failedMirroringProcessors?.Contains(processor) == true) continue;
            try { processor.Process(system); }
            catch (Exception exception) { Fail(system, values[i], exception); }
        }
    }

    public static void Refresh()
    {
        descriptors = null; processors = null;
        LiveMirroringExtensionHealthJournal.Clear(
            LiveMirroringExtensionCapabilities.Processor,
            LiveMirroringExtensionCapabilities.ProcessorBeforeCore,
            LiveMirroringExtensionCapabilities.ProcessorAfterCore);
        unchecked { if (++failureGeneration == 0) failureGeneration = 1; }
    }

    private static LiveMirroringDiscovered<ILiveMirroringProcessor>[] GetDescriptors() => descriptors ??=
        LiveMirroringDiscovery.Find<ILiveMirroringProcessor>(value => value.ProcessorId, value => value.Order,
            "processors", LiveMirroringExtensionLoadPolicy.OptionalIsolated,
            LiveMirroringExtensionCapabilities.Processor,
            value => (int)value.Stage,
            value => value == (int)LiveMirroringProcessingStage.BeforeCore
                ? LiveMirroringExtensionCapabilities.ProcessorBeforeCore
                : LiveMirroringExtensionCapabilities.ProcessorAfterCore);

    private static void Fail(AuthoringLiveMirroringSystem system,
        LiveMirroringDiscovered<ILiveMirroringProcessor> descriptor,
        Exception exception)
    {
        ILiveMirroringProcessor processor = descriptor.Instance;
        system.failedMirroringProcessors ??= new HashSet<ILiveMirroringProcessor>(new ReferenceComparer());
        system.failedMirroringProcessors.Add(processor);
        LiveMirroringExtensionHealthJournal.RecordIsolatedFailure(
            descriptor.Capability,
            descriptor.Id,
            processor.GetType(),
            "process",
            exception);
        Debug.LogException(exception, system);
    }
}
#endif
}
