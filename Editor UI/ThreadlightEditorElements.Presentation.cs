namespace Threadlight.EditorUI
{
using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

public static partial class ThreadlightEditorElements
{
    public const string ReducedMotionClassName = "threadlight-reduced-motion";

    private sealed class MotionPreferenceBinding
    {
        public Action Apply;
        public bool Subscribed;
    }

    private static readonly ConditionalWeakTable<VisualElement, MotionPreferenceBinding>
        MotionPreferenceBindings =
            new ConditionalWeakTable<VisualElement, MotionPreferenceBinding>();

    /// <summary>
    /// Keeps one visual tree synchronized with the shared motion preference.
    /// The preference event is held only while the tree is attached to a panel.
    /// </summary>
    public static void BindMotionPreference(VisualElement root)
    {
        if (root == null || MotionPreferenceBindings.TryGetValue(root, out _))
            return;

        MotionPreferenceBinding binding = new MotionPreferenceBinding();
        binding.Apply = () =>
        {
            bool reduced = ThreadlightEditorPreferences.ReducedMotion;
            root.EnableInClassList(ReducedMotionClassName, reduced);
            if (!reduced)
                return;
            VisualElement pointerLight = root.panel?.visualTree?.Q<VisualElement>(
                "wolfy-pointer-light");
            if (pointerLight != null)
                pointerLight.style.opacity = 0f;
        };
        MotionPreferenceBindings.Add(root, binding);
        root.RegisterCallback<AttachToPanelEvent>(evt =>
        {
            if (evt.target != root)
                return;
            SubscribeMotionPreference(binding);
            binding.Apply();
        });
        root.RegisterCallback<DetachFromPanelEvent>(evt =>
        {
            if (evt.target == root)
                UnsubscribeMotionPreference(binding);
        });
        binding.Apply();
        if (root.panel != null)
            SubscribeMotionPreference(binding);
    }

    /// <summary>
    /// Gives raster icons a consistent layout box while preserving optical
    /// breathing room around glyphs with different silhouettes.
    /// </summary>
    public static void StyleOpticalIcon(Image icon, float slotSize, float opticalSize)
    {
        if (icon == null)
            return;
        slotSize = Mathf.Max(1f, slotSize);
        opticalSize = Mathf.Clamp(opticalSize, 1f, slotSize);
        float inset = (slotSize - opticalSize) * .5f;
        icon.scaleMode = ScaleMode.ScaleToFit;
        icon.style.width = icon.style.minWidth = icon.style.maxWidth = slotSize;
        icon.style.height = icon.style.minHeight = icon.style.maxHeight = slotSize;
        icon.style.paddingLeft = icon.style.paddingRight = inset;
        icon.style.paddingTop = icon.style.paddingBottom = inset;
        icon.style.flexShrink = 0f;
    }

    /// <summary>Fits the shared horizontal brand mark without icon-style square padding.</summary>
    public static void StyleWordmark(Image wordmark, float width, float height)
    {
        if (wordmark == null)
            return;
        width = Mathf.Max(1f, width);
        height = Mathf.Max(1f, height);
        wordmark.scaleMode = ScaleMode.ScaleToFit;
        wordmark.style.width = wordmark.style.minWidth = wordmark.style.maxWidth = width;
        wordmark.style.height = wordmark.style.minHeight = wordmark.style.maxHeight = height;
        wordmark.style.alignSelf = Align.Center;
        wordmark.style.flexShrink = 0f;
    }

    private static void SubscribeMotionPreference(MotionPreferenceBinding binding)
    {
        if (binding.Subscribed)
            return;
        ThreadlightEditorPreferences.ReducedMotionChanged += binding.Apply;
        binding.Subscribed = true;
    }

    private static void UnsubscribeMotionPreference(MotionPreferenceBinding binding)
    {
        if (!binding.Subscribed)
            return;
        ThreadlightEditorPreferences.ReducedMotionChanged -= binding.Apply;
        binding.Subscribed = false;
    }
}

/// <summary>
/// A two-pane workspace whose child trees remain mounted for its lifetime.
/// Collapse, reveal, orientation, and size changes happen in place so fields
/// do not receive false panel detach/attach events and lose focus, hover state,
/// or in-progress UI Toolkit transitions.
/// </summary>
public sealed class ThreadlightRetainedSplitView : VisualElement
{
    private readonly TwoPaneSplitView splitView;
    private bool? secondaryVisible;

    public ThreadlightRetainedSplitView(
        VisualElement primary,
        VisualElement secondary,
        float secondaryDimension,
        TwoPaneSplitViewOrientation orientation)
    {
        if (primary == null)
            throw new ArgumentNullException(nameof(primary));
        if (secondary == null)
            throw new ArgumentNullException(nameof(secondary));
        style.flexGrow = 1f;
        style.minWidth = 0f;
        style.minHeight = 0f;
        splitView = new TwoPaneSplitView(
            1, secondaryDimension, orientation);
        splitView.AddToClassList("threadlight-retained-split-view__split");
        splitView.style.flexGrow = 1f;
        splitView.style.minWidth = 0f;
        splitView.style.minHeight = 0f;
        splitView.Add(primary);
        splitView.Add(secondary);
        hierarchy.Add(splitView);
        Orientation = orientation;
        RegisterCallback<AttachToPanelEvent>(evt =>
        {
            if (evt.target == this && secondaryVisible.HasValue)
                ApplySecondaryVisibility();
        });
    }

    public bool SecondaryVisible => secondaryVisible ?? true;
    public TwoPaneSplitViewOrientation Orientation { get; private set; }

    public void ConfigureSecondary(
        bool visible,
        float dimension,
        TwoPaneSplitViewOrientation nextOrientation)
    {
        splitView.orientation = nextOrientation;
        splitView.fixedPaneInitialDimension = Mathf.Max(0f, dimension);
        Orientation = nextOrientation;
        // The native splitter can reset its internal collapsed state during a
        // domain reload while this retained wrapper still remembers the same
        // logical value. Always enforce the requested native state; both
        // CollapseChild and UnCollapse are safe to repeat.
        secondaryVisible = visible;
        ApplySecondaryVisibility();
    }

    private void ApplySecondaryVisibility()
    {
        if (!secondaryVisible.HasValue)
            return;
        if (secondaryVisible.Value)
            splitView.UnCollapse();
        else
            splitView.CollapseChild(1);
    }
}
}
