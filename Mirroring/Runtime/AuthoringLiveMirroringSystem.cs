namespace Threadlight.Mirroring
{
using Threadlight.Authoring;
using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
/// <summary>Authoring-only transform mirroring and preview configuration.</summary>
public class AuthoringLiveMirroringSystem : CreatorAuthoringComponent
{
    public const string DefaultConstraintTargetsObjectName = "SETUP - Constraint Locations";
    public const string StandaloneOwnerId = "threadlight.mirroring.manual";
    public const string StandaloneModuleId = "threadlight.mirroring.targets";
    public const string HolderStableId = "live-mirroring-holder";
    public const string TargetsRootStableId = "targets-root";
    public const string SourceFolderStableId = "source-folder";
    public const string MirroredFolderStableId = "mirrored-folder";
    public const string PrefabContainerStableId = "prefab-container";
    public override bool RemoveGameObjectWithComponent => true;
    public enum Axis { X, Y, Z }

    [Serializable]
    public class MirrorOptions
    {
        public bool mirrorPosition = true;
        public bool mirrorRotation = true;
        public bool mirrorScale = true;
        public Axis mirrorAxis = Axis.X;
    }

    [Serializable]
    public class MirrorPair
    {
        // Retained for serialized compatibility. Opposite targets now always
        // mirror while the setup's Live Mirroring switch is enabled.
        public bool mirrorEnabled = true;
        public bool createOppositeTarget = true;
        public string pairName = "Mirror Pair";
        public bool useGlobalSideLabels = true;
        public string sourceSideLabel = "R";
        public string mirroredSideLabel = "L";
        public HumanBodyBones sourceBone = HumanBodyBones.RightHand;
        public HumanBodyBones mirroredBone = HumanBodyBones.LeftHand;
        public Transform sourceTarget;
        public Transform mirroredTarget;
        public Vector3 mirroredRotationOffset;
    }

    [SerializeField] private int dataVersion;
    public int DataVersion => dataVersion;
    public bool liveMirror = true;
    public Transform mirrorCenter;
    [HideInInspector] public string constraintTargetsObjectName = DefaultConstraintTargetsObjectName;
    public string targetNamePrefix = string.Empty;
    public string sourceSideLabel = "R";
    public string mirroredSideLabel = "L";
    public string sourceFolderName = "SETUP - Move These";
    public string mirroredFolderName = "AUTO - Mirrored Targets - Do Not Touch";
    public bool removeUnusedGeneratedTargets = true;
    public Vector3 targetLocalPosition;
    public Vector3 targetLocalEulerRotation;
    public Vector3 targetLocalScale = Vector3.one;
    public bool applyDefaultTransformToExistingTargets;
    [HideInInspector] public bool addVrcfuryArmatureLinks = true;
    public bool applyScaleReference = true;
    public Transform scaleReference;
    public bool addParentConstraintToPrefabContainer = true;
    [HideInInspector] public Component prefabContainerParentConstraint;
    [SerializeField, HideInInspector]
    private bool prefabContainerParentConstraintCreatedByThreadlight;
    [HideInInspector] public Transform[] scaleHandles;

    public bool PrefabContainerParentConstraintCreatedByThreadlight =>
        prefabContainerParentConstraintCreatedByThreadlight;

    public void SetPrefabContainerParentConstraintOwnership(bool created)
    {
        prefabContainerParentConstraintCreatedByThreadlight = created;
    }

    [NonSerialized] internal bool scaleSynchronizationInitialized;
    [NonSerialized] internal Vector3 synchronizedWorldScale = Vector3.one;
    [NonSerialized] internal Transform synchronizedScaleReference;
    [NonSerialized] internal int synchronizedHandleSignature;
    [NonSerialized] internal bool preferScaleReferenceAfterUndo;
#if UNITY_EDITOR
    [NonSerialized] internal bool undoRefreshQueued;
    [NonSerialized] internal LiveMirroringEvaluationBuffers evaluationBuffers;
    [NonSerialized] internal HashSet<ILiveMirroringProcessor> failedMirroringProcessors;
    [NonSerialized] internal int mirroringProcessorFailureGeneration;
    [NonSerialized] internal bool migrationFailureBlocked;
    [NonSerialized] internal Hash128 migrationFailureStateHash;
#endif

    public MirrorPair[] pairs;
    public bool showScenePreview = true;
    public GameObject previewSource;
    public Material previewMaterial;
    [HideInInspector] public bool generateThreadlightComponentsBootstrapper = true;
    [HideInInspector] public string threadlightComponentsBootstrapperFolderPath = "Assets/Threadlight/Components/Temp";
    public MirrorOptions mirrorOptions = new MirrorOptions();

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!HasSupportedDataVersion) return;
        LiveMirroringMigrationService.QueueMigration(this);
#endif
    }

    protected override void OnEnable()
    {
        base.OnEnable();
#if UNITY_EDITOR
        scaleSynchronizationInitialized = false;
        failedMirroringProcessors?.Clear();
        UnityEditor.Undo.undoRedoPerformed -= QueueUndoRefresh;
        UnityEditor.Undo.undoRedoPerformed += QueueUndoRefresh;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        failedMirroringProcessors?.Clear();
        UnityEditor.Undo.undoRedoPerformed -= QueueUndoRefresh;
        UnityEditor.EditorApplication.delayCall -= ApplyUndoRefresh;
        undoRefreshQueued = false;
#endif
    }

    private void Update()
    {
        if (!Application.isPlaying && liveMirror) MirrorAll();
    }

    public void MirrorAll()
    {
#if UNITY_EDITOR
        if (!HasSupportedDataVersion) return;
        if (DataVersion < LiveMirroringMigrationService.CurrentDataVersion ||
            RequiresSerializedDefaults)
        {
            LiveMirroringMigrationService.QueueMigration(this);
            return;
        }
        LiveMirroringService.UpdateMirroring(this);
#else
        EnsureSerializedDefaults();
#endif
    }

    public void SetDataVersion(int version) => dataVersion = version;

    public bool ShouldCreateOppositeTarget(MirrorPair pair) =>
        pair != null && (dataVersion < 4
            ? pair.mirrorEnabled
            : pair.createOppositeTarget);

    public bool ShouldMirrorOppositeTarget(MirrorPair pair) =>
        ShouldCreateOppositeTarget(pair);

#if UNITY_EDITOR
    private void Reset()
    {
        dataVersion = LiveMirroringMigrationService.CurrentDataVersion;
        EnsureSerializedDefaults();
    }

    private bool HasSupportedDataVersion => dataVersion >= 0 &&
        dataVersion <= LiveMirroringMigrationService.CurrentDataVersion;

    private void QueueUndoRefresh()
    {
        if (undoRefreshQueued || Application.isPlaying) return;
        preferScaleReferenceAfterUndo = scaleSynchronizationInitialized && scaleReference != null &&
            (scaleReference.lossyScale - synchronizedWorldScale).sqrMagnitude > .00000001f;
        undoRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += ApplyUndoRefresh;
    }

    private void ApplyUndoRefresh()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyUndoRefresh;
        undoRefreshQueued = false;
        if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
        MirrorAll();
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    internal void EnsureSerializedDefaults()
    {
        mirrorOptions ??= new MirrorOptions();
        if (string.IsNullOrWhiteSpace(constraintTargetsObjectName))
            constraintTargetsObjectName = DefaultConstraintTargetsObjectName;
        if (string.IsNullOrWhiteSpace(sourceSideLabel)) sourceSideLabel = "R";
        if (string.IsNullOrWhiteSpace(mirroredSideLabel)) mirroredSideLabel = "L";
        if (string.IsNullOrWhiteSpace(sourceFolderName))
            sourceFolderName = "SETUP - Move These";
        if (string.IsNullOrWhiteSpace(mirroredFolderName))
            mirroredFolderName = "AUTO - Mirrored Targets - Do Not Touch";
        if (string.IsNullOrWhiteSpace(threadlightComponentsBootstrapperFolderPath))
            threadlightComponentsBootstrapperFolderPath = "Assets/Threadlight/Components/Temp";
    }

    internal bool RequiresSerializedDefaults =>
        mirrorOptions == null ||
        string.IsNullOrWhiteSpace(constraintTargetsObjectName) ||
        string.IsNullOrWhiteSpace(sourceSideLabel) ||
        string.IsNullOrWhiteSpace(mirroredSideLabel) ||
        string.IsNullOrWhiteSpace(sourceFolderName) ||
        string.IsNullOrWhiteSpace(mirroredFolderName) ||
        string.IsNullOrWhiteSpace(threadlightComponentsBootstrapperFolderPath);
}
}
