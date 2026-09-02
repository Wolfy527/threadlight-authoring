namespace Threadlight.Authoring.Editor
{
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Translates creator snapshots to and from the optional customer component
/// without creating an assembly dependency on ThreadLight Components.
/// </summary>
public static class CustomerSnapshotBridge
{
    private const string CustomerAssembly =
        "Threadlight.Components";
    private const string CustomerSnapshotType =
        "Threadlight.Authoring.PrefabId";
    private const string CustomerReferenceType =
        "Threadlight.Authoring.PrefabIdObjectReference";

    public static bool TryConvertToCustomer(
        CreatorPrefabSnapshot source,
        out Component customer,
        out string error)
    {
        customer = null;
        if (source == null)
            return Fail("There is no creator snapshot to convert.", out error);
        Type snapshotType = ResolveType(CustomerSnapshotType);
        Type referenceType = ResolveType(CustomerReferenceType);
        if (snapshotType == null || referenceType == null)
            return Fail("Customer support is not ready yet. Prepare the Prefab " +
                "Components bootstrapper and allow Unity to finish compiling before " +
                "finishing authoring.", out error);
        try
        {
            customer = Find(source.gameObject, snapshotType) ??
                Undo.AddComponent(source.gameObject, snapshotType) as Component;
            if (customer == null)
                return Fail("Unity could not create the customer Prefab ID.", out error);
            Undo.RegisterCompleteObjectUndo(customer, "Create Customer Prefab ID");
            object references = CreateReferenceList(referenceType, source.ObjectReferences);
            MethodInfo setter = snapshotType.GetMethod("SetSnapshot",
                BindingFlags.Instance | BindingFlags.Public);
            if (setter == null)
                return Fail("The installed ThreadLight Components package does not expose " +
                    "a compatible Prefab ID contract.", out error);
            setter.Invoke(customer, new[]
            {
                source.Id,
                (object)source.BuilderDataVersion,
                source.BuilderPackageVersion,
                source.BuilderState,
                references,
                new List<string>(source.BuilderOwnedPaths)
            });
            if (!Equals(Read(snapshotType, customer, "Id"), source.Id) ||
                !Equals(Read(snapshotType, customer, "BuilderState"), source.BuilderState))
                return Fail("The customer Prefab ID did not retain the complete " +
                    "authoring snapshot.", out error);
            EditorUtility.SetDirty(customer);
            Undo.DestroyObjectImmediate(source);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            if (customer != null && customer.gameObject != null &&
                string.IsNullOrEmpty(Read(snapshotType, customer, "Id") as string))
                Undo.DestroyObjectImmediate(customer);
            customer = null;
            return Fail("The creator snapshot could not be converted safely: " +
                Unwrap(exception).Message, out error);
        }
    }

    public static bool TryConvertFromCustomer(
        GameObject context,
        out CreatorPrefabSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        if (!TryFind(context, out Component customer))
            return Fail("No customer Prefab ID was found on this selection.", out error);
        Type type = customer.GetType();
        try
        {
            int schema = Convert.ToInt32(Read(type, customer, "PrefabSchema"));
            if (schema > CreatorPrefabSnapshot.CurrentSchemaVersion)
                return Fail("This prefab was saved by a newer ThreadLight Builder. Update " +
                    "the Builder before resuming it.", out error);
            snapshot = customer.GetComponent<CreatorPrefabSnapshot>() ??
                Undo.AddComponent<CreatorPrefabSnapshot>(customer.gameObject);
            Undo.RegisterCompleteObjectUndo(snapshot, "Resume Customer Prefab ID");
            snapshot.SetSnapshot(
                Read(type, customer, "Id") as string,
                Convert.ToInt32(Read(type, customer, "BuilderDataVersion")),
                Read(type, customer, "BuilderPackageVersion") as string,
                Read(type, customer, "BuilderState") as string,
                ReadReferences(type, customer),
                ReadStrings(Read(type, customer, "BuilderOwnedPaths")));
            EditorUtility.SetDirty(snapshot);
            Undo.DestroyObjectImmediate(customer);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            snapshot = null;
            return Fail("The customer Prefab ID could not be resumed safely: " +
                Unwrap(exception).Message, out error);
        }
    }

    public static bool TryFind(GameObject context, out Component customer)
    {
        customer = null;
        Type type = ResolveType(CustomerSnapshotType);
        if (context == null || type == null)
            return false;
        Transform current = context.transform;
        while (current != null)
        {
            customer = Find(current.gameObject, type);
            if (customer != null)
                return true;
            current = current.parent;
        }
        return false;
    }

    private static Component Find(GameObject target, Type type)
    {
        Component[] components = target.GetComponents<Component>();
        for (int index = 0; index < components.Length; index++)
            if (components[index] != null && components[index].GetType() == type)
                return components[index];
        return null;
    }

    private static object CreateReferenceList(
        Type referenceType,
        IReadOnlyList<CreatorPrefabObjectReference> source)
    {
        Type listType = typeof(List<>).MakeGenericType(referenceType);
        IList list = (IList)Activator.CreateInstance(listType);
        ConstructorInfo constructor = referenceType.GetConstructor(
            new[] { typeof(string), typeof(UnityEngine.Object) });
        if (constructor == null)
            throw new MissingMethodException(referenceType.FullName,
                ".ctor(string, UnityEngine.Object)");
        if (source != null)
            for (int index = 0; index < source.Count; index++)
            {
                CreatorPrefabObjectReference item = source[index];
                if (item != null)
                    list.Add(constructor.Invoke(new object[]
                        { item.PropertyPath, item.Value }));
            }
        return list;
    }

    private static List<CreatorPrefabObjectReference> ReadReferences(
        Type type,
        Component customer)
    {
        List<CreatorPrefabObjectReference> output =
            new List<CreatorPrefabObjectReference>();
        if (!(Read(type, customer, "ObjectReferences") is IEnumerable values))
            return output;
        foreach (object value in values)
        {
            if (value == null)
                continue;
            Type valueType = value.GetType();
            output.Add(new CreatorPrefabObjectReference(
                Read(valueType, value, "PropertyPath") as string,
                Read(valueType, value, "Value") as UnityEngine.Object));
        }
        return output;
    }

    private static List<string> ReadStrings(object value)
    {
        List<string> output = new List<string>();
        if (value is IEnumerable values)
            foreach (object item in values)
                if (item is string text)
                    output.Add(text);
        return output;
    }

    private static object Read(Type type, object target, string propertyName)
    {
        PropertyInfo property = type?.GetProperty(propertyName,
            BindingFlags.Instance | BindingFlags.Public);
        if (property == null)
            throw new MissingMemberException(type?.FullName, propertyName);
        return property.GetValue(target, null);
    }

    private static Type ResolveType(string fullName) =>
        Type.GetType(fullName + ", " + CustomerAssembly, false);

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException invocation &&
        invocation.InnerException != null ? invocation.InnerException : exception;

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
}
