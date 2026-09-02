namespace Threadlight.EditorUI {
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
public enum ThreadlightFormFieldKind
{ Toggle, Text, ObjectReference, Enum, Vector3 }
public sealed class ThreadlightFormField {
    internal ThreadlightFormFieldKind Kind { get; }
    internal string Label { get; }
    internal string Path { get; }
    internal Type ObjectType { get; }
    internal Action Changed { get; private set; }
    internal Func<bool> Visible { get; private set; }
    private ThreadlightFormField(
        ThreadlightFormFieldKind kind, string label, string path, Type type = null) {
        Kind = kind;
        Label = label;
        Path = path;
        ObjectType = type;
    }
    public static ThreadlightFormField Toggle(string label, string path) =>
        new ThreadlightFormField(ThreadlightFormFieldKind.Toggle, label, path);
    public static ThreadlightFormField Text(string label, string path) =>
        new ThreadlightFormField(ThreadlightFormFieldKind.Text, label, path);
    public static ThreadlightFormField Enum(string label, string path) =>
        new ThreadlightFormField(ThreadlightFormFieldKind.Enum, label, path);
    public static ThreadlightFormField Vector3(string label, string path) =>
        new ThreadlightFormField(ThreadlightFormFieldKind.Vector3, label, path);
    public static ThreadlightFormField ObjectReference(
        string label, string path, Type type) => new ThreadlightFormField(
            ThreadlightFormFieldKind.ObjectReference, label, path,
            type ?? throw new ArgumentNullException(nameof(type)));
    public ThreadlightFormField When(Func<bool> condition) {
        Visible = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }
    public ThreadlightFormField OnChanged(Action action) {
        Changed = action ?? throw new ArgumentNullException(nameof(action));
        return this;
    }
}
public delegate void ThreadlightSerializedChange(
    string path, string undoName, Action<SerializedProperty> change);
public sealed class ThreadlightSerializedForm {
    private readonly SerializedObject source;
    private readonly ThreadlightSerializedChange applyChange;
    private readonly Func<string, bool> readBoolean;
    private readonly Func<string, string> tooltipForPath;
    private readonly Action<VisualElement, string, string> decorateTooltip;
    private readonly Func<Color> interactionAccent;
    public ThreadlightSerializedForm(
        SerializedObject source,
        ThreadlightSerializedChange applyChange,
        Func<string, bool> readBoolean,
        Func<string, string> tooltipForPath = null,
        Action<VisualElement, string, string> decorateTooltip = null,
        Func<Color> interactionAccent = null) {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.applyChange = applyChange ?? throw new ArgumentNullException(nameof(applyChange));
        this.readBoolean = readBoolean ?? throw new ArgumentNullException(nameof(readBoolean));
        this.tooltipForPath = tooltipForPath;
        this.decorateTooltip = decorateTooltip;
        this.interactionAccent = interactionAccent;
    }
    public void AddFields(VisualElement parent, params ThreadlightFormField[] fields) {
        foreach (ThreadlightFormField field in fields)
            AddField(parent, field);
    }
    public VisualElement AddField(VisualElement parent, ThreadlightFormField field) {
        if (field.Visible != null && !field.Visible())
            return null;
        VisualElement element = Create(field);
        decorateTooltip?.Invoke(element, field.Label,
            tooltipForPath?.Invoke(field.Path) ?? string.Empty);
        parent.Add(element);
        return element;
    }
    private VisualElement Create(ThreadlightFormField field) {
        switch (field.Kind) {
            case ThreadlightFormFieldKind.Toggle:
                VisualElement row = ThreadlightEditorElements.CreateToggleRow(
                    field.Label, () => readBoolean(field.Path),
                    value => Change(field, property => property.boolValue = value),
                    interactionAccent);
                SerializedProperty toggleProperty = source.FindProperty(field.Path) ?? throw
                    new InvalidOperationException($"Property '{field.Path}' was not found.");
                Button toggle = row.Q<Button>(
                    className: ThreadlightEditorElements.ToggleControlClassName);
                row.TrackPropertyValue(toggleProperty,
                    _ => ThreadlightEditorElements.RefreshToggleControl(toggle));
                return row;
            case ThreadlightFormFieldKind.Text:
                return Style(new TextField(field.Label) { bindingPath = field.Path });
            case ThreadlightFormFieldKind.ObjectReference:
                return Style(new ObjectField(field.Label) {
                    bindingPath = field.Path,
                    objectType = field.ObjectType,
                    allowSceneObjects = true
                });
            case ThreadlightFormFieldKind.Enum:
                return CreateEnum(field);
            case ThreadlightFormFieldKind.Vector3:
                return CreateVector3(field);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    private VisualElement CreateEnum(ThreadlightFormField field) {
        SerializedProperty property = source.FindProperty(field.Path) ?? throw
            new InvalidOperationException($"Property '{field.Path}' was not found.");
        List<string> choices = new List<string>(property.enumDisplayNames);
        int selected = Mathf.Clamp(property.enumValueIndex, 0, choices.Count - 1);
        PopupField<string> control = new PopupField<string>(
            field.Label, choices, choices[selected]);
        Style(control);
        control.RegisterValueChangedCallback(evt => {
            int next = choices.IndexOf(evt.newValue);
            if (next >= 0)
                Change(field, target => target.enumValueIndex = next);
        });
        return control;
    }
    private VisualElement CreateVector3(ThreadlightFormField field) {
        Vector3Field control = new Vector3Field(field.Label)
            { bindingPath = field.Path };
        ThreadlightEditorElements.StyleVector3Field(
            control, interactionAccent, interactionAccent);
        return control;
    }
    private void Change(ThreadlightFormField field, Action<SerializedProperty> change) {
        applyChange(field.Path, $"Set {field.Label}", change);
        field.Changed?.Invoke();
    }
    private BaseField<T> Style<T>(BaseField<T> field) {
        ThreadlightEditorElements.StyleField(field, interactionAccent, interactionAccent);
        return field;
    }
}
}
