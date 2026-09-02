namespace Threadlight.Mirroring.Editor
{
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class LiveMirroringArmatureLinkUtility
{
    private const string ApiType = "com.vrcfury.api.FuryComponents";
    private const string ComponentType = "VF.Model.VRCFury";
    private const string ModelType = "VF.Model.Feature.ArmatureLink";
    private const string LinkType = "VF.Model.Feature.ArmatureLink+LinkTo";
    private static readonly Dictionary<string, Type> types = new Dictionary<string, Type>(StringComparer.Ordinal);
    private static readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
    private static int typeResolutionCount, fieldResolutionCount;
    internal static int TypeResolutionCount => typeResolutionCount;
    internal static int FieldResolutionCount => fieldResolutionCount;
    internal static void RefreshReflectionCache() { types.Clear(); fields.Clear(); }

    public static bool AddOrUpdate(GameObject target, HumanBodyBones bone)
    {
        try { return Configure(target, bone); }
        catch (Exception) { return false; }
    }

    public static Component GetExistingComponent(GameObject target)
    {
        Type component = FindType(ComponentType), model = FindType(ModelType);
        return target != null && component != null && model != null ? FindExisting(target, component, model) : null;
    }

    private static bool Configure(GameObject target, HumanBodyBones bone)
    {
        if (target == null) return false;
        Type componentType = FindType(ComponentType), modelType = FindType(ModelType), linkType = FindType(LinkType);
        if (componentType == null || modelType == null || linkType == null) return false;
        Component component = FindExisting(target, componentType, modelType);
        if (IsConfigured(component, target, bone)) return false;
        component ??= CreateWithApi(target, componentType, modelType, bone);
        component ??= Undo.AddComponent(target, componentType);
        if (component == null) return false;
        Undo.RegisterCompleteObjectUndo(component, "Update VRCFury Armature Link");
        WriteModel(component, target, bone, modelType, linkType);
        EditorUtility.SetDirty(component);
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        return true;
    }

    private static Component CreateWithApi(GameObject target, Type componentType, Type modelType, HumanBodyBones bone)
    {
        MethodInfo create = FindType(ApiType)?.GetMethod("CreateArmatureLink", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(GameObject) }, null);
        if (create == null) return null;
        Component[] before = target.GetComponents<Component>();
        try
        {
            object wrapper = create.Invoke(null, new object[] { target });
            Invoke(wrapper, "LinkTo", new[] { typeof(HumanBodyBones), typeof(string) }, bone, string.Empty);
            Invoke(wrapper, "SetAlign", new[] { typeof(bool) }, false);
            Invoke(wrapper, "SetRecursive", new[] { typeof(bool) }, false);
        }
        catch (TargetInvocationException) { }
        catch (ArgumentException) { }
        Component created = FindExisting(target, componentType, modelType);
        if (created != null && Array.IndexOf(before, created) < 0)
            Undo.RegisterCreatedObjectUndo(created, "Add VRCFury Armature Link");
        return created;
    }

    private static void Invoke(object target, string method, Type[] signature, params object[] arguments) =>
        target?.GetType().GetMethod(method, signature)?.Invoke(target, arguments);

    private static void WriteModel(Component component, GameObject target, HumanBodyBones bone,
        Type modelType, Type linkType)
    {
        object model = Activator.CreateInstance(modelType), link = Activator.CreateInstance(linkType);
        Set(link, "useBone", true); Set(link, "bone", bone); Set(link, "useObj", false);
        Set(link, "obj", null); Set(link, "offset", string.Empty);
        IList links = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(linkType));
        links.Add(link);
        Set(model, "propBone", target); Set(model, "linkTo", links);
        Set(model, "alignPosition", false); Set(model, "alignRotation", false);
        Set(model, "alignScale", false); Set(model, "recursive", false);
        Set(component, "content", model);
    }

    private static bool IsConfigured(Component component, GameObject target, HumanBodyBones bone)
    {
        object model = Get(component, "content");
        if (model == null || Get(model, "propBone") as GameObject != target ||
            Bool(model, "alignPosition") || Bool(model, "alignRotation") || Bool(model, "alignScale") ||
            Bool(model, "recursive") || !(Get(model, "linkTo") is IList links) || links.Count != 1) return false;
        object link = links[0], configuredBone = Get(link, "bone");
        return Bool(link, "useBone") && !Bool(link, "useObj") && Get(link, "obj") == null &&
            string.IsNullOrEmpty(Get(link, "offset") as string) && configuredBone != null &&
            Convert.ToInt32(configuredBone) == (int)bone;
    }

    private static Component FindExisting(GameObject target, Type componentType, Type modelType)
    {
        Component[] components = target.GetComponents(componentType);
        for (int i = 0; i < components.Length; i++)
            if (Get(components[i], "content")?.GetType() == modelType) return components[i];
        return null;
    }

    private static Type FindType(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (types.TryGetValue(name, out Type cached)) return cached;
        typeResolutionCount++;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type value = assembly.GetType(name, false);
            if (value != null) return types[name] = value;
        }
        types[name] = null;
        return null;
    }

    private static FieldInfo FindField(Type type, string name)
    {
        if (type == null || string.IsNullOrEmpty(name)) return null;
        string key = type.AssemblyQualifiedName + "|" + name;
        if (fields.TryGetValue(key, out FieldInfo cached)) return cached;
        fieldResolutionCount++;
        return fields[key] = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static object Get(object target, string name) => FindField(target?.GetType(), name)?.GetValue(target);
    private static void Set(object target, string name, object value) => FindField(target?.GetType(), name)?.SetValue(target, value);
    private static bool Bool(object target, string name) => Get(target, name) is bool value && value;
}

public sealed class LiveMirroringArmatureLinkBuildContributor : ILiveMirroringTargetBuildContributor
{
    public string ContributorId => "threadlight.constraint-targets.vrcfury-armature-links";
    public int Order => 100;
    public int Apply(LiveMirroringTargetBuildContext context)
    {
        if (context?.System == null || context.Target == null || !context.System.addVrcfuryArmatureLinks) return 0;
        int changed = Add(context.Target.sourceTarget, context.Target.sourceBone);
        if (context.System.ShouldCreateOppositeTarget(context.Target))
            changed += Add(context.Target.mirroredTarget,
                context.Target.mirroredBone);
        return changed;
    }
    private static int Add(Transform target, HumanBodyBones bone) =>
        LiveMirroringSetupUtility.IsOwnedGeneratedTarget(target) &&
        LiveMirroringArmatureLinkUtility.AddOrUpdate(target.gameObject, bone) ? 1 : 0;
}
}
