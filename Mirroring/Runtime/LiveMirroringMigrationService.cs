#if UNITY_EDITOR
namespace Threadlight.Mirroring
{
using Threadlight.Authoring;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LiveMirroringMigrationService
{
    private const string UndoName = "Upgrade Live Mirroring Data";
    public const int CurrentDataVersion = 5;

    private static readonly Dictionary<int, AuthoringLiveMirroringSystem> Pending =
        new Dictionary<int, AuthoringLiveMirroringSystem>();
    private static bool drainQueued;
    private static bool migrationInProgress;

    private static readonly VersionedDataMigrationPipeline<AuthoringLiveMirroringSystem>
        Pipeline = new VersionedDataMigrationPipeline<AuthoringLiveMirroringSystem>(
            CurrentDataVersion,
            new LiveMirroringMigrationV0ToV1(),
            new LiveMirroringMigrationV1ToV2(),
            new LiveMirroringMigrationV2ToV3(),
            new LiveMirroringMigrationV3ToV4(),
            new LiveMirroringMigrationV4ToV5()
        );

    /// <summary>
    /// Runs an explicit package-maintenance migration on Unity's main thread.
    /// Do not call this from OnValidate or another serialization callback;
    /// normal component validation already queues a deferred migration. The
    /// service records one complete Undo step and throws after rolling back a
    /// failed migration.
    /// </summary>
    public static void MigrateIfNeeded(AuthoringLiveMirroringSystem system)
    {
        if (system == null)
            return;

        TryMigrate(system);
    }

    /// <summary>
    /// Defers automatic upgrades until Unity has finished its validation pass.
    /// Repeated validation callbacks for the same component collapse to one job.
    /// </summary>
    internal static void QueueMigration(AuthoringLiveMirroringSystem system)
    {
        if (system == null || migrationInProgress)
            return;
        if (system.DataVersion < 0 || system.DataVersion > CurrentDataVersion)
            return;
        if (system.migrationFailureBlocked &&
            system.migrationFailureStateHash == ComputeStateHash(system))
            return;
        if (system.DataVersion == CurrentDataVersion &&
            !system.RequiresSerializedDefaults)
            return;

        Pending[system.GetInstanceID()] = system;
        QueueDrain();
    }

    /// <summary>
    /// Runs an explicit package-maintenance migration and returns its version
    /// result. Call only on Unity's main thread and outside serialization
    /// callbacks. A migration exception is rethrown after rollback; this method
    /// does not convert transaction failure into a result value.
    /// </summary>
    public static VersionedDataMigrationResult TryMigrate(
        AuthoringLiveMirroringSystem system)
    {
        if (system == null)
            return VersionedDataMigrationResult.InvalidVersion;

        Pending.Remove(system.GetInstanceID());

        if (system.DataVersion < 0)
            return VersionedDataMigrationResult.InvalidVersion;
        if (system.DataVersion > CurrentDataVersion)
            return VersionedDataMigrationResult.NewerThanSupported;
        if (system.DataVersion == CurrentDataVersion &&
            !system.RequiresSerializedDefaults)
            return VersionedDataMigrationResult.UpToDate;

        return RunTransaction(system, () =>
        {
            system.EnsureSerializedDefaults();
            return MigrateCore(system);
        });
    }

    private static VersionedDataMigrationResult RunTransaction(
        AuthoringLiveMirroringSystem system,
        Func<VersionedDataMigrationResult> migrate)
    {
        if (system == null || migrate == null)
            return VersionedDataMigrationResult.InvalidVersion;

        // Keep migration changes isolated from edits recorded by the UI event
        // that requested the migration. This also preserves complete Redo data.
        Undo.FlushUndoRecordObjects();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        Undo.RegisterCompleteObjectUndo(system, UndoName);

        migrationInProgress = true;
        try
        {
            VersionedDataMigrationResult result = migrate();
            system.scaleSynchronizationInitialized = false;
            system.migrationFailureBlocked = false;
            EditorUtility.SetDirty(system);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            return result;
        }
        catch (Exception exception)
        {
            Exception failure;
            try
            {
                Undo.RevertAllDownToGroup(undoGroup);
                failure = new InvalidOperationException(
                    $"Live Mirroring data on '{system.name}' could not be upgraded safely.",
                    exception);
            }
            catch (Exception rollbackException)
            {
                failure = new AggregateException(
                    $"Live Mirroring data on '{system.name}' failed to upgrade and Unity could not fully roll it back.",
                    exception,
                    rollbackException);
            }
            system.migrationFailureStateHash = ComputeStateHash(system);
            system.migrationFailureBlocked = true;
            throw failure;
        }
        finally
        {
            migrationInProgress = false;
        }
    }

    private static VersionedDataMigrationResult MigrateCore(
        AuthoringLiveMirroringSystem system)
    {

        return Pipeline.Migrate(
            system,
            system.DataVersion,
            system.SetDataVersion
        );
    }

    private static void QueueDrain()
    {
        EditorApplication.update -= DrainWhenReady;
        if (drainQueued)
            return;
        drainQueued = true;
        EditorApplication.delayCall += DrainPending;
    }

    private static void DrainPending()
    {
        EditorApplication.delayCall -= DrainPending;
        drainQueued = false;

        if (Pending.Count == 0)
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.update -= DrainWhenReady;
            EditorApplication.update += DrainWhenReady;
            return;
        }

        AuthoringLiveMirroringSystem[] systems =
            new List<AuthoringLiveMirroringSystem>(Pending.Values).ToArray();
        Pending.Clear();
        for (int i = 0; i < systems.Length; i++)
        {
            AuthoringLiveMirroringSystem system = systems[i];
            if (system == null)
                continue;
            try
            {
                TryMigrate(system);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, system);
            }
        }
    }

    private static void DrainWhenReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.update -= DrainWhenReady;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        EditorApplication.update -= DrainWhenReady;
        QueueDrain();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        QueueDrain();
    }

    private static Hash128 ComputeStateHash(AuthoringLiveMirroringSystem system)
    {
        return system == null
            ? default
            : Hash128.Compute(EditorJsonUtility.ToJson(system, false));
    }
}
}
#endif
