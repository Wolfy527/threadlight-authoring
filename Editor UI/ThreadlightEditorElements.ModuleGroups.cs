namespace Threadlight.EditorUI {
using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
public static partial class ThreadlightEditorElements {
    private const double DisclosureAnimationSeconds = 0.18d;
    private const int DisclosureMeasureAttempts = 3;
    private sealed class ExpansionAnimationState {
        public EventCallback<GeometryChangedEvent> PendingMeasure;
        public EditorApplication.CallbackFunction PendingProbe;
        public EditorApplication.CallbackFunction PendingStart;
        public EditorApplication.CallbackFunction PendingFinalize;
        public double FinalizeAt;
        public bool Expanded;
    }
    private static readonly ConditionalWeakTable<VisualElement, ExpansionAnimationState> ExpansionAnimations =
        new ConditionalWeakTable<VisualElement, ExpansionAnimationState>();
    public static void StyleModuleGroupSurface(VisualElement group, Color accent, Color fill) {
        if (group == null) return;
        ApplySharedStyles(group);
        group.AddToClassList("threadlight-module-group");
        group.AddToClassList("threadlight-level--feature");
        group.style.backgroundColor = fill;
        SetBorderColor(group, GroupColor(accent, .32f));
        RegisterPointerLight(group, accent, 122f, .38f);
    }
    public static VisualElement BuildModuleGroupHeader(VisualElement header, string title, string kind,
        Color accent, bool expanded, out Label expansionIndicator, out Label titleLabel) {
        if (header == null) throw new ArgumentNullException(nameof(header));
        header.Clear();
        ApplySharedStyles(header);
        header.AddToClassList("threadlight-module-header");
        expansionIndicator = Label(expanded ? "−" : "+", "threadlight-module-indicator", accent);
        header.Add(expansionIndicator);
        titleLabel = Label(title, "threadlight-module-title", accent);
        header.Add(titleLabel);
        Label kindLabel = Label(string.IsNullOrWhiteSpace(kind) ? "SECTION" : kind.Trim().ToUpperInvariant(),
            "threadlight-module-kind", GroupColor(accent, .88f));
        kindLabel.style.backgroundColor = GroupColor(accent, .10f);
        header.Add(kindLabel);
        VisualElement accessories = new VisualElement();
        accessories.AddToClassList("threadlight-module-accessories");
        header.Add(accessories);
        SetModuleGroupExpansion(header, expansionIndicator, accent, expanded);
        return accessories;
    }
    private static Label Label(string text, string className, Color color) {
        Label label = new Label(text ?? string.Empty);
        label.AddToClassList(className);
        label.style.color = color;
        return label;
    }
    public static void SetModuleGroupExpansion(VisualElement header, Label indicator, Color accent, bool expanded) {
        if (indicator != null) indicator.text = expanded ? "−" : "+";
        if (header == null) return;
        header.style.marginBottom = expanded ? 5f : 0f;
        header.style.borderBottomWidth = expanded ? 1f : 0f;
        header.style.borderBottomColor = GroupColor(accent, expanded ? .28f : 0f);
    }
    public static void SetModuleGroupInteraction(VisualElement group, VisualElement header, Color accent,
        bool hovered, bool focused) {
        if (group != null) SetBorderColor(group, GroupColor(accent, hovered || focused ? .72f : .32f));
        if (header != null) header.style.backgroundColor = Color.clear;
    }
    public static void RegisterModuleGroupHeaderInteraction(VisualElement group, VisualElement header,
        Color accent, Action toggled) {
        if (header == null) return;
        header.style.backgroundColor = Color.clear;
        RegisterInteractionState(header, (hovered, focused) =>
            SetModuleGroupInteraction(group, header, accent, hovered, focused));
        header.RegisterCallback<KeyDownEvent>(evt => {
            if (evt.target != header || evt.keyCode != KeyCode.Return &&
                evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space) return;
            toggled?.Invoke(); evt.StopPropagation(); evt.PreventDefault();
        });
    }
    public static void StyleModuleGroupBody(VisualElement body) {
        if (body == null) return;
        StyleExpandableBody(body);
        body.AddToClassList("threadlight-module-body");
        body.style.backgroundColor = Color.clear;
    }
    public static void SetModuleGroupBodyExpansion(VisualElement body, bool expanded, bool animate) {
        if (body == null) return;
        StyleExpandableBody(body);
        ExpansionAnimationState state = ExpansionAnimations.GetValue(body, element => {
            ExpansionAnimationState created = new ExpansionAnimationState();
            element.RegisterCallback<DetachFromPanelEvent>(evt => {
                if (evt.target == element)
                    CompleteExpansion(element, created);
            });
            return created;
        });
        CancelPendingExpansion(body, state);
        state.Expanded = expanded;
        if (!animate || ThreadlightEditorPreferences.ReducedMotion || body.panel == null) {
            ApplyExpansionState(body, expanded);
            return;
        }
        if (expanded) BeginOpening(body, state);
        else BeginClosing(body, state);
    }
    private static void StyleExpandableBody(VisualElement body) {
        ApplySharedStyles(body);
        body.AddToClassList("threadlight-expandable-body");
    }
    private static void BeginOpening(VisualElement body, ExpansionAnimationState state) {
        body.style.display = DisplayStyle.Flex;
        body.style.maxHeight = StyleKeyword.Null;
        body.style.overflow = Overflow.Hidden;
        body.style.opacity = 0f;
        body.style.translate = new Translate(0, -6, 0);
        EventCallback<GeometryChangedEvent> measure = null;
        measure = evt => {
            if (state.PendingMeasure != measure || !state.Expanded) return;
            float height = evt.newRect.height;
            if (UsableExpansionHeight(height)) StartOpening(body, state, height);
        };
        state.PendingMeasure = measure;
        body.RegisterCallback(measure);
        int attempts = 0;
        EditorApplication.CallbackFunction probe = null;
        probe = () => {
            if (state.PendingProbe != probe || !state.Expanded) return;
            attempts++;
            float height = body.resolvedStyle.height;
            if (UsableExpansionHeight(height)) StartOpening(body, state, height);
            else if (attempts >= DisclosureMeasureAttempts) CompleteExpansion(body, state);
        };
        state.PendingProbe = probe;
        EditorApplication.update += probe;
    }
    private static void StartOpening(VisualElement body, ExpansionAnimationState state, float targetHeight) {
        CancelPendingMeasurement(body, state);
        body.style.maxHeight = 0f;
        QueueExpansionStart(body, state, () => {
            body.style.maxHeight = targetHeight;
            body.style.opacity = 1f;
            body.style.translate = new Translate(0, 0, 0);
        });
    }
    private static void BeginClosing(VisualElement body, ExpansionAnimationState state) {
        float height = body.resolvedStyle.height;
        if (!UsableExpansionHeight(height)) {
            ApplyExpansionState(body, false);
            return;
        }
        body.style.display = DisplayStyle.Flex;
        body.style.maxHeight = height;
        body.style.overflow = Overflow.Hidden;
        body.style.opacity = 1f;
        body.style.translate = new Translate(0, 0, 0);
        QueueExpansionStart(body, state, () => {
            body.style.maxHeight = 0f;
            body.style.opacity = 0f;
            body.style.translate = new Translate(0, -6, 0);
        });
    }
    private static void QueueExpansionStart(VisualElement body, ExpansionAnimationState state, Action start) {
        EditorApplication.CallbackFunction pending = null;
        pending = () => {
            if (state.PendingStart != pending) return;
            state.PendingStart = null;
            if (body == null || body.panel == null) {
                CompleteExpansion(body, state);
                return;
            }
            start?.Invoke();
            ScheduleExpansionFinalize(body, state);
        };
        state.PendingStart = pending;
        EditorApplication.delayCall += pending;
    }
    private static void ScheduleExpansionFinalize(VisualElement body, ExpansionAnimationState state) {
        state.FinalizeAt = EditorApplication.timeSinceStartup + DisclosureAnimationSeconds;
        EditorApplication.CallbackFunction finalize = null;
        finalize = () => {
            if (state.PendingFinalize != finalize) return;
            if (body == null || body.panel == null ||
                EditorApplication.timeSinceStartup >= state.FinalizeAt)
                CompleteExpansion(body, state);
        };
        state.PendingFinalize = finalize;
        EditorApplication.update += finalize;
    }
    private static void CompleteExpansion(VisualElement body, ExpansionAnimationState state) {
        if (body == null || state == null) return;
        CancelPendingExpansion(body, state);
        ApplyExpansionState(body, state.Expanded);
    }
    private static void CancelPendingExpansion(VisualElement body, ExpansionAnimationState state) {
        CancelPendingMeasurement(body, state);
        if (state.PendingStart != null) {
            EditorApplication.delayCall -= state.PendingStart;
            state.PendingStart = null;
        }
        if (state.PendingFinalize != null) {
            EditorApplication.update -= state.PendingFinalize;
            state.PendingFinalize = null;
        }
    }
    private static void CancelPendingMeasurement(VisualElement body, ExpansionAnimationState state) {
        if (state.PendingMeasure != null) {
            body?.UnregisterCallback(state.PendingMeasure);
            state.PendingMeasure = null;
        }
        if (state.PendingProbe != null) {
            EditorApplication.update -= state.PendingProbe;
            state.PendingProbe = null;
        }
    }
    private static void ApplyExpansionState(VisualElement body, bool expanded) {
        body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        body.style.maxHeight = expanded ? StyleKeyword.Null : 0f;
        body.style.overflow = expanded ? Overflow.Visible : Overflow.Hidden;
        body.style.opacity = expanded ? 1f : 0f;
        body.style.translate = expanded ? new Translate(0, 0, 0) : new Translate(0, -6, 0);
    }
    private static bool UsableExpansionHeight(float height) =>
        !float.IsNaN(height) && !float.IsInfinity(height) && height > 0f;
    private static Color GroupColor(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
}

/// <summary>
/// Shared expandable card shell for editor tools. Feature surfaces supply
/// their own validation, actions, colors, and state persistence while focus,
/// keyboard input, pointer feedback, and expansion remain consistent.
/// </summary>
public class ThreadlightDisclosureCard : VisualElement {
    protected readonly VisualElement Body;
    protected readonly VisualElement Header;
    protected readonly VisualElement Accessories;
    protected readonly Label Arrow;
    protected readonly Label Heading;
    protected readonly Color Accent;
    protected bool Expanded;
    private readonly Action<bool> expansionChanged;

    /// <summary>The collapsible content host for fields and child controls.</summary>
    public VisualElement Content => Body;

    public ThreadlightDisclosureCard(
        string title,
        string description,
        Color accent,
        Color fill,
        bool expanded = true,
        string kind = "SECTION",
        string descriptionClassName = null,
        Action<bool> expansionChanged = null) {
        Accent = accent;
        Expanded = expanded;
        this.expansionChanged = expansionChanged;
        ThreadlightEditorElements.StyleModuleGroupSurface(this, accent, fill);

        Header = new VisualElement {
            focusable = true,
            tabIndex = 0
        };
        Accessories = ThreadlightEditorElements.BuildModuleGroupHeader(
            Header,
            title,
            kind,
            accent,
            expanded,
            out Label arrow,
            out Label heading);
        Arrow = arrow;
        Heading = heading;
        Header.RegisterCallback<PointerUpEvent>(evt => {
            if (evt.button != 0 || IsInteractiveHeaderChild(
                    evt.target as VisualElement, Header))
                return;
            Toggle();
            evt.StopPropagation();
        });
        hierarchy.Add(Header);

        Body = new VisualElement();
        ThreadlightEditorElements.StyleModuleGroupBody(Body);
        hierarchy.Add(Body);
        ThreadlightEditorElements.SetModuleGroupBodyExpansion(
            Body,
            expanded,
            false);

        if (!string.IsNullOrWhiteSpace(description)) {
            Label copy = new Label(description.Trim());
            if (!string.IsNullOrWhiteSpace(descriptionClassName))
                copy.AddToClassList(descriptionClassName);
            copy.style.color = ThreadlightEditorTheme.TextMuted;
            Body.Add(copy);
        }

        ThreadlightEditorElements.RegisterModuleGroupHeaderInteraction(
            this,
            Header,
            accent,
            Toggle);
    }

    public new void Add(VisualElement child) => Body.Add(child);

    protected virtual void Toggle() {
        SetExpanded(!Expanded);
        expansionChanged?.Invoke(Expanded);
    }

    private static bool IsInteractiveHeaderChild(
        VisualElement target,
        VisualElement header) {
        for (VisualElement current = target;
             current != null && current != header;
             current = current.parent)
            if (current is Button)
                return true;
        return false;
    }

    protected void SetExpanded(bool value) {
        Expanded = value;
        ThreadlightEditorElements.SetModuleGroupExpansion(
            Header,
            Arrow,
            Accent,
            value);
        ThreadlightEditorElements.SetModuleGroupBodyExpansion(
            Body,
            value,
            true);
    }
}
}
