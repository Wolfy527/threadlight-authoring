#if UNITY_EDITOR
namespace Threadlight.Authoring
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared, SDK-optional boundary for the VRC constraints used by ThreadLight
/// authoring tools. Keeping serialized SDK layout knowledge here prevents the
/// full and lightweight builders from drifting apart.
/// </summary>
public static class VrcConstraintUtility
{
    private const int KeyableSourceLimit = 16;
    private const string ParentTypeName =
        "VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint";
    private const string LookAtTypeName =
        "VRC.SDK3.Dynamics.Constraint.Components.VRCLookAtConstraint";
    private static readonly Type ParentType = ResolveType(ParentTypeName, "VRC.SDK3A");
    private static readonly Type LookAtType = ResolveType(LookAtTypeName, "VRC.SDK3A");

    public static bool HasParentConstraint => ParentType != null;
    public static bool IsParentConstraint(Component component) =>
        component != null && ParentType?.IsInstanceOfType(component) == true;
    public static bool IsLookAtConstraint(Component component) =>
        component != null && LookAtType?.IsInstanceOfType(component) == true;

    public static bool IsParentConstraintConfigured(
        Component constraint,
        bool componentEnabled,
        bool isActive,
        float constraintWeight,
        bool freezeToWorld,
        bool rebakeOffsetsWhenUnfrozen)
    {
        if (!IsParentConstraint(constraint)) return false;
        SerializedObject serialized = new SerializedObject(constraint);
        return ReadBool(serialized, "m_Enabled", componentEnabled) == componentEnabled &&
               ReadBool(serialized, "IsActive", isActive) == isActive &&
               Mathf.Approximately(ReadFloat(serialized, "GlobalWeight", constraintWeight),
                   constraintWeight) &&
               ReadBool(serialized, "FreezeToWorld", freezeToWorld) == freezeToWorld &&
               ReadBool(serialized, "RebakeOffsetsWhenUnfrozen",
                   rebakeOffsetsWhenUnfrozen) == rebakeOffsetsWhenUnfrozen;
    }

    public static Component AddParentConstraint(
        GameObject target,
        string undoName = "Add VRC Parent Constraint",
        bool componentEnabled = true,
        bool isActive = false,
        float constraintWeight = 0f,
        UnityEngine.Object defaultSource = null,
        bool freezeToWorld = true,
        bool rebakeOffsetsWhenUnfrozen = false,
        bool reuseExisting = true)
    {
        Component constraint = reuseExisting
            ? AddOrFind(target, ParentType)
            : Add(target, ParentType);
        if (constraint == null) return null;
        Undo.RegisterCompleteObjectUndo(constraint, undoName);
        SerializedObject serialized = new SerializedObject(constraint);
        Set(serialized, "m_Enabled", componentEnabled);
        Set(serialized, "IsActive", isActive);
        Set(serialized, "GlobalWeight", constraintWeight);
        Set(serialized, "FreezeToWorld", freezeToWorld);
        Set(serialized, "RebakeOffsetsWhenUnfrozen", rebakeOffsetsWhenUnfrozen);
        serialized.ApplyModifiedProperties();
        Transform source = ResolveTransform(defaultSource);
        if (source != null) SetSources(constraint, new[] { source }, 1f, undoName);
        EditorUtility.SetDirty(constraint);
        return constraint;
    }

    public static Component AddLookAtConstraint(
        GameObject target,
        Transform source,
        float facingYaw,
        string undoName = "Add VRC Look At Constraint")
    {
        Component constraint = AddOrFind(target, LookAtType);
        if (constraint == null) return null;
        Undo.RegisterCompleteObjectUndo(constraint, undoName);
        SerializedObject serialized = new SerializedObject(constraint);
        Set(serialized, "m_Enabled", true);
        Set(serialized, "IsActive", false);
        Set(serialized, "Locked", true);
        Set(serialized, "RotationAtRest", new Vector3(0f, facingYaw, 0f));
        Set(serialized, "RotationOffset", new Vector3(0f, facingYaw, 0f));
        serialized.ApplyModifiedProperties();
        SetSources(constraint, new[] { source }, 1f, undoName);
        EditorUtility.SetDirty(constraint);
        return constraint;
    }

    public static void RemoveParentConstraint(
        GameObject target,
        string undoName = "Remove VRC Parent Constraint") =>
        RemoveParentConstraint(Find(target, ParentType), undoName);

    public static void RemoveParentConstraint(
        Component constraint,
        string undoName = "Remove VRC Parent Constraint")
    {
        if (IsParentConstraint(constraint)) Undo.DestroyObjectImmediate(constraint);
    }

    public static bool SetSources(
        Component constraint,
        IList<Transform> sources,
        float sourceWeight = 0f,
        string undoName = "Set VRC Constraint Sources")
    {
        if (!IsParentConstraint(constraint) && !IsLookAtConstraint(constraint))
            return false;
        List<Transform> valid = sources == null
            ? new List<Transform>()
            : sources.Where(source => source != null).Distinct().ToList();
        if (valid.Count > KeyableSourceLimit)
        {
            Debug.LogWarning(
                $"ThreadLight found {valid.Count} constraint targets, but VRChat " +
                $"supports {KeyableSourceLimit} keyable sources. Only the first " +
                $"{KeyableSourceLimit} were assigned.", constraint);
            valid.RemoveRange(KeyableSourceLimit, valid.Count - KeyableSourceLimit);
        }
        Undo.RegisterCompleteObjectUndo(constraint, undoName);
        SerializedObject serialized = new SerializedObject(constraint);
        SerializedProperty list = serialized.FindProperty("Sources");
        if (list == null)
        {
            Debug.LogWarning(
                "ThreadLight could not find the Sources field on this VRC constraint.",
                constraint);
            return false;
        }
        for (int i = 0; i < KeyableSourceLimit; i++)
        {
            SerializedProperty source = list.FindPropertyRelative($"source{i}");
            if (source == null) continue;
            Set(source, "SourceTransform", i < valid.Count ? valid[i] : null);
            Set(source, "Weight", i < valid.Count ? sourceWeight : 0f);
            Set(source, "ParentPositionOffset", Vector3.zero);
            Set(source, "ParentRotationOffset", Vector3.zero);
        }
        Set(list, "totalLength", valid.Count);
        SerializedProperty overflow = list.FindPropertyRelative("overflowList");
        if (overflow != null && overflow.isArray) overflow.arraySize = 0;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(constraint);
        return true;
    }

    public static bool TryGetSources(Component constraint, out List<Transform> sources)
    {
        sources = new List<Transform>();
        if (!IsParentConstraint(constraint) && !IsLookAtConstraint(constraint))
            return false;
        SerializedProperty list = new SerializedObject(constraint).FindProperty("Sources");
        if (list == null) return false;
        SerializedProperty length = list.FindPropertyRelative("totalLength");
        int count = length != null
            ? Mathf.Clamp(length.intValue, 0, KeyableSourceLimit)
            : KeyableSourceLimit;
        for (int i = 0; i < count; i++)
        {
            Transform source = list.FindPropertyRelative($"source{i}")?
                .FindPropertyRelative("SourceTransform")?.objectReferenceValue as Transform;
            if (source != null) sources.Add(source);
        }
        return true;
    }

    private static Component AddOrFind(GameObject target, Type type)
    {
        Component component = Find(target, type);
        return component ?? Add(target, type);
    }

    private static Component Add(GameObject target, Type type) =>
        target != null && type != null ? Undo.AddComponent(target, type) : null;

    private static Component Find(GameObject target, Type type)
    {
        if (target == null || type == null) return null;
        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && type.IsInstanceOfType(components[i]))
                return components[i];
        return null;
    }

    private static Type ResolveType(string fullName, params string[] assemblies)
    {
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = Type.GetType(fullName + ", " + assemblies[i], false);
            if (type != null) return type;
        }
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName, false);
            if (type != null) return type;
        }
        return null;
    }

    private static Transform ResolveTransform(UnityEngine.Object value) => value switch
    {
        Transform transform => transform,
        GameObject gameObject => gameObject.transform,
        Component component => component.transform,
        _ => null
    };

    private static void Set(SerializedObject target, string name, bool value) =>
        Set(target.FindProperty(name), value);
    private static void Set(SerializedObject target, string name, float value) =>
        Set(target.FindProperty(name), value);
    private static void Set(SerializedObject target, string name, Vector3 value) =>
        Set(target.FindProperty(name), value);
    private static void Set(SerializedProperty root, string name, UnityEngine.Object value)
    {
        SerializedProperty property = root?.FindPropertyRelative(name);
        if (property != null) property.objectReferenceValue = value;
    }
    private static void Set(SerializedProperty root, string name, float value)
    {
        SerializedProperty property = root?.FindPropertyRelative(name);
        if (property != null) property.floatValue = value;
    }
    private static void Set(SerializedProperty root, string name, int value)
    {
        SerializedProperty property = root?.FindPropertyRelative(name);
        if (property != null) property.intValue = value;
    }
    private static void Set(SerializedProperty root, string name, Vector3 value)
    {
        SerializedProperty property = root?.FindPropertyRelative(name);
        if (property != null) property.vector3Value = value;
    }
    private static void Set(SerializedProperty property, bool value)
    {
        if (property != null) property.boolValue = value;
    }
    private static void Set(SerializedProperty property, float value)
    {
        if (property != null) property.floatValue = value;
    }
    private static void Set(SerializedProperty property, Vector3 value)
    {
        if (property != null) property.vector3Value = value;
    }
    private static bool ReadBool(
        SerializedObject target, string name, bool fallback)
    {
        SerializedProperty property = target.FindProperty(name);
        return property != null ? property.boolValue : fallback;
    }
    private static float ReadFloat(
        SerializedObject target, string name, float fallback)
    {
        SerializedProperty property = target.FindProperty(name);
        return property != null ? property.floatValue : fallback;
    }
}
}
#endif
