namespace Threadlight.EditorUI
{
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static partial class ThreadlightEditorElements
{
    private sealed class AuroraMotion
    {
        public VisualElement Owner;
        public VisualElement LeftBoundary;
        public VisualElement[] Layers;
        public VisualElement[] Particles;
        public Vector2[] ParticleOffset;
        public Vector2[] ParticleVelocity;
        public Vector2 Pointer;
        public Vector2 PointerLocal;
        public Vector2 Current;
        public bool PointerInside;
        public float Phase;
        public double LastFrame;
        public double LastPointerMove;
        public double NextFrame;
    }

    private const double MovingAuroraFrameInterval = 1d / 30d;
    private const double IdleAuroraFrameInterval = .08d;
    private const double HiddenAuroraCheckInterval = .25d;
    private const double PointerActivityDuration = .22d;
    private const float AmbientAuroraScalePulse = .05f;
    private const int AmbientParticleCount = 20;
    private const float AmbientParticleBoundaryOverlap = 2f;

    private static readonly List<AuroraMotion> auroraMotions =
        new List<AuroraMotion>();
    private static readonly ConditionalWeakTable<VisualElement, AuroraMotion>
        auroraByOwner =
            new ConditionalWeakTable<VisualElement, AuroraMotion>();
    private static double nextAuroraFrame;
    private static bool motionPreferenceHooked;

    public static void BindAuroraMotion(
        VisualElement owner,
        params VisualElement[] layers)
    {
        if (owner == null || layers == null || layers.Length == 0)
            return;

        EnsureMotionPreferenceHook();
        BindMotionPreference(owner);
        if (!auroraByOwner.TryGetValue(owner, out AuroraMotion motion))
        {
            motion = new AuroraMotion
            {
                Owner = owner,
                Phase = auroraMotions.Count * 1.37f
            };
            auroraByOwner.Add(owner, motion);

            owner.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (evt.target == owner)
                    ActivateAurora(motion);
            });
            owner.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target == owner)
                    DeactivateAurora(motion);
            });
            owner.RegisterCallback<PointerMoveEvent>(evt =>
            {
                Vector2 local = owner.WorldToLocal(evt.position);
                motion.PointerLocal = local;
                motion.PointerInside = true;
                motion.Pointer = new Vector2(
                    Mathf.InverseLerp(0, owner.contentRect.width, local.x) * 2 - 1,
                    Mathf.InverseLerp(0, owner.contentRect.height, local.y) * 2 - 1
                );
                motion.LastPointerMove = EditorApplication.timeSinceStartup;
                WakeAurora(motion);
            });
            owner.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                motion.PointerInside = false;
                motion.Pointer = Vector2.zero;
                WakeAurora(motion);
            });
        }

        motion.Layers = layers;
        motion.Particles = EnsureAuroraParticles(layers);
        if (motion.ParticleOffset == null || motion.ParticleVelocity == null ||
            motion.ParticleOffset.Length != motion.Particles.Length ||
            motion.ParticleVelocity.Length != motion.Particles.Length)
        {
            motion.ParticleOffset = new Vector2[motion.Particles.Length];
            motion.ParticleVelocity = new Vector2[motion.Particles.Length];
        }
        if (owner.panel != null)
            ActivateAurora(motion);
    }

    public static void SetAuroraLeftBoundary(
        VisualElement owner,
        VisualElement boundary)
    {
        if (owner == null || !auroraByOwner.TryGetValue(
                owner, out AuroraMotion motion))
            return;
        motion.LeftBoundary = boundary;
        WakeAurora(motion);
    }

    private static void ActivateAurora(AuroraMotion motion)
    {
        WakeAurora(motion);
        if (!auroraMotions.Contains(motion))
            auroraMotions.Add(motion);
        if (ThreadlightEditorPreferences.ReducedMotion)
        {
            ResetAuroraMotionState(motion);
            EditorApplication.update -= UpdateAuroraMotion;
        }
        else
        {
            EditorApplication.update -= UpdateAuroraMotion;
            EditorApplication.update += UpdateAuroraMotion;
        }
    }

    private static void DeactivateAurora(AuroraMotion motion)
    {
        if (motion == null)
            return;
        ResetAuroraMotionState(motion);
        auroraMotions.Remove(motion);
        if (auroraMotions.Count == 0)
            EditorApplication.update -= UpdateAuroraMotion;
    }

    private static void WakeAurora(AuroraMotion motion)
    {
        if (motion != null)
            motion.NextFrame = 0d;
        nextAuroraFrame = 0d;
    }

    private static void UpdateAuroraMotion()
    {
        if (ThreadlightEditorPreferences.ReducedMotion)
        {
            ApplyReducedMotionPreference();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        if (now < nextAuroraFrame)
            return;

        double nextFrame = double.MaxValue;
        for (int i = auroraMotions.Count - 1; i >= 0; i--)
        {
            AuroraMotion motion = auroraMotions[i];
            if (motion.Owner?.panel == null)
            {
                ResetAuroraMotionState(motion);
                auroraMotions.RemoveAt(i);
                continue;
            }

            if (!IsAuroraRenderable(motion.Owner))
            {
                motion.NextFrame = now + HiddenAuroraCheckInterval;
                nextFrame = Math.Min(nextFrame, motion.NextFrame);
                continue;
            }

            if (now < motion.NextFrame)
            {
                nextFrame = Math.Min(nextFrame, motion.NextFrame);
                continue;
            }

            double elapsed = motion.LastFrame > 0d ? Math.Min(now - motion.LastFrame, .25d) : .05d;
            float interpolation = 1f - Mathf.Exp(-2.56f * (float)elapsed);
            motion.Current = Vector2.Lerp(motion.Current, motion.Pointer, interpolation);
            Rect ownerRect = motion.Owner.contentRect;
            float horizontalTravel = Mathf.Clamp(
                ownerRect.width * .55f, 120f, 340f);
            float verticalTravel = Mathf.Clamp(
                ownerRect.height * .16f, 70f, 160f);
            for (int j = 0; j < motion.Layers.Length; j++)
            {
                float direction = (j & 1) == 0 ? 1f : -1f;
                float phase = (float)now * (.055f + j * .012f) * direction +
                    motion.Phase + j * 2.1f;
                float depth = 6f + j * 4f;
                VisualElement layer = motion.Layers[j];
                if (layer == null)
                    continue;
                layer.transform.position = new Vector3(
                    Mathf.Sin(phase) * horizontalTravel * (1f - j * .08f) +
                    motion.Current.x * depth,
                    Mathf.Cos(phase * (.68f + j * .07f)) *
                    verticalTravel * (.78f + j * .12f) +
                    motion.Current.y * depth,
                    0
                );
                layer.transform.scale = Vector3.one *
                    (1f + Mathf.Sin(phase * .61f) * AmbientAuroraScalePulse);
            }
            bool particleSettling = UpdateAuroraParticles(
                motion, now, ownerRect, elapsed);
            motion.LastFrame = now;
            bool pointerMoving = now - motion.LastPointerMove <= PointerActivityDuration;
            bool settling = (motion.Current - motion.Pointer).sqrMagnitude > .0001f;
            motion.NextFrame = now + (pointerMoving || settling || particleSettling
                ? MovingAuroraFrameInterval : IdleAuroraFrameInterval);
            nextFrame = Math.Min(nextFrame, motion.NextFrame);
        }

        if (auroraMotions.Count == 0)
        {
            EditorApplication.update -= UpdateAuroraMotion;
            nextAuroraFrame = 0d;
            return;
        }
        nextAuroraFrame = nextFrame < double.MaxValue ? nextFrame : now + HiddenAuroraCheckInterval;
    }

    private static bool IsAuroraRenderable(VisualElement owner)
    {
        if (owner?.panel == null || owner.resolvedStyle.display == DisplayStyle.None ||
            owner.resolvedStyle.visibility == Visibility.Hidden)
            return false;
        Rect content = owner.contentRect;
        return content.width > 1f && content.height > 1f &&
            !float.IsNaN(content.width) && !float.IsNaN(content.height);
    }

    private static void EnsureMotionPreferenceHook()
    {
        if (motionPreferenceHooked)
            return;
        motionPreferenceHooked = true;
        ThreadlightEditorPreferences.ReducedMotionChanged += ApplyReducedMotionPreference;
    }

    private static void ApplyReducedMotionPreference()
    {
        EditorApplication.update -= UpdateAuroraMotion;
        nextAuroraFrame = 0d;
        if (ThreadlightEditorPreferences.ReducedMotion)
        {
            for (int i = 0; i < auroraMotions.Count; i++)
                ResetAuroraMotionState(auroraMotions[i]);
            return;
        }
        for (int i = 0; i < auroraMotions.Count; i++)
            WakeAurora(auroraMotions[i]);
        if (auroraMotions.Count > 0)
            EditorApplication.update += UpdateAuroraMotion;
    }

    private static void ResetAuroraMotionState(AuroraMotion motion)
    {
        if (motion == null)
            return;
        motion.Pointer = motion.Current = Vector2.zero;
        motion.PointerLocal = Vector2.zero;
        motion.PointerInside = false;
        motion.LastFrame = motion.LastPointerMove = motion.NextFrame = 0d;
        for (int i = 0; i < (motion.Layers?.Length ?? 0); i++)
        {
            VisualElement layer = motion.Layers[i];
            if (layer == null)
                continue;
            layer.transform.position = Vector3.zero;
            layer.transform.scale = Vector3.one;
        }
        if (motion.Particles == null)
            return;
        for (int i = 0; i < motion.Particles.Length; i++)
        {
            VisualElement particle = motion.Particles[i];
            if (particle == null)
                continue;
            particle.transform.position = Vector3.zero;
            particle.transform.scale = Vector3.one;
            if (motion.ParticleOffset != null && motion.ParticleVelocity != null &&
                i < motion.ParticleOffset.Length && i < motion.ParticleVelocity.Length)
            {
                motion.ParticleOffset[i] = Vector2.zero;
                motion.ParticleVelocity[i] = Vector2.zero;
            }
        }
    }

    private static VisualElement[] EnsureAuroraParticles(
        VisualElement[] layers)
    {
        VisualElement atmosphere = layers.Length > 0 ? layers[0]?.parent : null;
        if (atmosphere == null)
            return Array.Empty<VisualElement>();

        const string fieldName = "threadlight-ambient-particles";
        VisualElement field = atmosphere.Q<VisualElement>(fieldName);
        if (field == null)
        {
            field = new VisualElement
            {
                name = fieldName,
                pickingMode = PickingMode.Ignore
            };
            field.style.position = Position.Absolute;
            field.style.left = field.style.right = 0;
            field.style.top = field.style.bottom = 0;
            field.style.overflow = Overflow.Hidden;
            atmosphere.Add(field);
        }

        while (field.childCount < AmbientParticleCount)
            field.Add(CreateAuroraParticle(field.childCount));
        VisualElement[] particles = new VisualElement[AmbientParticleCount];
        for (int i = 0; i < particles.Length; i++)
            particles[i] = field[i];
        return particles;
    }

    private static VisualElement CreateAuroraParticle(int index)
    {
        float size = ParticleSize(index);
        VisualElement particle = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };
        particle.style.position = Position.Absolute;
        particle.style.left = Length.Percent(ParticleLeftPercent(index));
        particle.style.top = Length.Percent(ParticleTopPercent(index));
        particle.style.width = particle.style.height = size;
        particle.style.backgroundImage = new StyleBackground(AuroraTexture());
        particle.style.unityBackgroundImageTintColor = ParticleTint(index);
        particle.style.opacity = .20f + index % 3 * .05f;
        return particle;
    }

    private static float ParticleSize(int index) =>
        6f + index % 4 * 1.6f;

    private static float ParticleLeftPercent(int index) =>
        (index * 37 + 11) % 94;

    private static float ParticleTopPercent(int index) =>
        (index * 53 + 17) % 90;

    private static Color ParticleTint(int index)
    {
        switch (index % 3)
        {
            case 1: return ThreadlightEditorTheme.AuroraCyan;
            case 2: return ThreadlightEditorTheme.AuroraMagenta;
            default: return ThreadlightEditorTheme.AuroraViolet;
        }
    }

    private static bool UpdateAuroraParticles(
        AuroraMotion motion,
        double now,
        Rect ownerRect,
        double elapsed)
    {
        if (motion.Particles == null)
            return false;
        float horizontalTravel = Mathf.Clamp(ownerRect.width * .09f, 24f, 72f);
        float verticalTravel = Mathf.Clamp(ownerRect.height * .065f, 18f, 54f);
        float repelRadius = Mathf.Clamp(
            Mathf.Min(ownerRect.width, ownerRect.height) * .22f, 90f, 150f);
        float deltaTime = (float)elapsed;
        float velocityDamping = Mathf.Exp(-1.25f * deltaTime);
        float leftBoundary = ResolveAuroraLeftBoundary(motion, ownerRect);
        bool settling = false;
        for (int i = 0; i < motion.Particles.Length; i++)
        {
            VisualElement particle = motion.Particles[i];
            if (particle == null)
                continue;
            float direction = (i & 1) == 0 ? 1f : -1f;
            float phase = (float)now * (.07f + i * .009f) * direction +
                motion.Phase + i * 1.73f;
            Vector2 orbit = new Vector2(
                Mathf.Sin(phase) * horizontalTravel * (.62f + i % 3 * .16f),
                Mathf.Cos(phase * (.71f + i % 4 * .06f)) * verticalTravel);
            Vector2 offset = motion.ParticleOffset[i];
            Vector2 velocity = motion.ParticleVelocity[i];
            if (motion.PointerInside)
            {
                Vector2 seededPosition = new Vector2(
                    ownerRect.width * ParticleLeftPercent(i) * .01f,
                    ownerRect.height * ParticleTopPercent(i) * .01f) +
                    orbit + offset;
                Vector2 away = seededPosition - motion.PointerLocal;
                float distance = away.magnitude;
                if (distance < repelRadius)
                {
                    float strength = 1f - distance / repelRadius;
                    Vector2 directionAway = distance > .01f
                        ? away / distance
                        : Vector2.right;
                    velocity += directionAway *
                        (strength * strength * 220f * deltaTime);
                }
            }
            if (velocity.sqrMagnitude > 12100f)
                velocity = velocity.normalized * 110f;
            velocity *= velocityDamping;
            offset += velocity * deltaTime;
            ConstrainAuroraParticle(
                ownerRect, leftBoundary, i, orbit, ref offset, ref velocity);
            motion.ParticleOffset[i] = offset;
            motion.ParticleVelocity[i] = velocity;
            settling |= velocity.sqrMagnitude > .25f;
            particle.transform.position = new Vector3(
                orbit.x + offset.x,
                orbit.y + offset.y,
                0f);
            particle.transform.scale = Vector3.one *
                (1f + Mathf.Sin(phase * .83f) * .16f);
        }
        return settling;
    }

    private static float ResolveAuroraLeftBoundary(
        AuroraMotion motion,
        Rect ownerRect)
    {
        VisualElement boundary = motion.LeftBoundary;
        if (boundary?.panel == null ||
            boundary.resolvedStyle.display == DisplayStyle.None)
            return 0f;
        float edge = motion.Owner.WorldToLocal(new Vector2(
            boundary.worldBound.xMax,
            boundary.worldBound.center.y)).x;
        return float.IsNaN(edge) ? 0f : Mathf.Clamp(edge, 0f, ownerRect.width);
    }

    private static void ConstrainAuroraParticle(
        Rect ownerRect,
        float leftBoundary,
        int index,
        Vector2 orbit,
        ref Vector2 offset,
        ref Vector2 velocity)
    {
        float size = ParticleSize(index);
        float minX = Mathf.Min(
            Mathf.Max(0f, leftBoundary - AmbientParticleBoundaryOverlap),
            ownerRect.width - size);
        float maxX = Mathf.Max(minX, ownerRect.width - size);
        float minY = 0f;
        float maxY = Mathf.Max(minY, ownerRect.height - size);
        Vector2 seededPosition = new Vector2(
            ownerRect.width * ParticleLeftPercent(index) * .01f,
            ownerRect.height * ParticleTopPercent(index) * .01f) + orbit;
        Vector2 position = seededPosition + offset;
        if (position.x < minX)
        {
            offset.x += minX - position.x;
            velocity.x = Mathf.Max(Mathf.Abs(velocity.x) * .72f, 8f);
        }
        else if (position.x > maxX)
        {
            offset.x -= position.x - maxX;
            velocity.x = -Mathf.Max(Mathf.Abs(velocity.x) * .72f, 8f);
        }
        if (position.y < minY)
        {
            offset.y += minY - position.y;
            velocity.y = Mathf.Max(Mathf.Abs(velocity.y) * .72f, 7f);
        }
        else if (position.y > maxY)
        {
            offset.y -= position.y - maxY;
            velocity.y = -Mathf.Max(Mathf.Abs(velocity.y) * .72f, 7f);
        }
    }
}
}
