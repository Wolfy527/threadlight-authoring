# Extending Live Mirroring

`LiveMirroringSystem` is the stable serialized compatibility component. Package
maintainers must preserve its script GUID, class name, and existing serialized
fields. Add fields instead of changing field types, and use `FormerlySerializedAs`
or the adjacent package migration pipeline when stored data must move.

Normal component validation queues migration automatically after Unity finishes its
serialization callback. Explicit package-maintenance tools may call
`LiveMirroringMigrationService` on Unity's main thread, outside `OnValidate` and
other serialization callbacks. The service owns a complete Undo transaction and
rolls back before reporting migration failure. Extensions should keep their schema
on extension-owned components or assets instead of migrating this package-owned
component.

## ThreadLight Mirroring

`Tools > ThreadLight > Mirroring` is the lightweight creator
surface for the same serialized mirroring data used by ThreadLight Builder. It can
assign existing transforms or generate missing targets under an owned setup
folder. Its EditorOnly authoring holder lives inside that setup folder, matching
ThreadLight Builder; older sibling-holder layouts remain readable and are
normalized by the next successful Build. When no scale reference is assigned,
Build creates an owned `Prefab
Container`; the prefab root and target hierarchy are not valid references. It
never replaces creator-owned hierarchy content. Target naming, folders,
transform defaults, cleanup policy, and prefab-container Parent Constraint
configuration share the same serialized intent as ThreadLight Builder.

Hierarchy stable IDs identify roles within one resolved authoring root; they are
not global asset IDs and are intentionally shared by every setup. Generated target
instances use GUID identities. Reconciliation scopes both forms of identity to the
owning root, excludes nested setup scopes, and stops without mutation when duplicate
evidence is ambiguous inside one scope.

## Inspector elements

Create a public, non-abstract class with a public parameterless constructor that
implements `ILiveMirroringInspectorElementContributor`.

- Give it a permanent, unique `ContributorId`.
- Use `Order` to control its location.
- Return a UI Toolkit element from `CreateElement()`.

Elements are discovered automatically. Adding one does not require editing
`LiveMirroringSystemEditor` or a central list.

All editor contributor types must be public, non-abstract, and have a public
parameterless constructor. Lower `Order` values run first; ties use the
contributor ID. IDs are permanent and unique: a duplicate keeps exactly one
built-in contributor when available, otherwise the conflicting contributors are
disabled. Constructor and callback failures are logged and isolated by their
host rather than interrupting the core workflow.

Implement `ILiveMirroringTargetBuildContributor` for optional editor behavior
that runs after a lightweight target has been assigned or generated. Contributors
must preserve unrelated creator content and must not depend on ThreadLight Builder.
`Apply()` returns the non-negative number of owned changes it made. Target-build
contributors are mutation-critical: discovery and metadata are validated before
mutation, and a contributor exception stops and rolls back the complete target
build rather than committing partial output.

The lightweight builder reconciles only hierarchy proven by its stored owner,
module, and stable IDs. Existing pair references stay authoritative; moved or
creator-renamed targets remain in place, and ambiguous copied metadata is retained
unchanged rather than resolved by name or hierarchy order.

Implement `ILiveMirroringSetupOwnershipContributor` when another creator tool
owns and rebuilds a prefab root. Claimed roots remain read-only in the
lightweight ThreadLight Mirroring. Claims are checked in order, and the first
matching contributor is reported as the owner.

## Mirroring processing

Implement `ILiveMirroringProcessor` for behavior that runs before or after the
stable core scale/pair mirroring pass.

- Give it a permanent, unique `ProcessorId`.
- Choose `BeforeCore` only when the extension must prepare data consumed by the
  core pass. Prefer `AfterCore` for additive behavior.
- Keep processors stateless. Serialized configuration belongs on an
  extension-owned sibling component or asset, as demonstrated by the included
  extension sample. Only package maintainers add fields to `LiveMirroringSystem`.
- Processors run on every editor-time mirroring pass, ordered by stage, then
  `Order`, then ID. A processor that throws is disabled for that system until
  the processor registry is refreshed.

## Validation and scene previews

Implement `ILiveMirroringValidationContributor` to add messages above the normal
sections without modifying the main Inspector. Core messages run first; extension
messages then appear in both the Inspector and ThreadLight Mirroring window.
Extension errors are build-blocking. Editor automation should call
`LiveMirroringSetupValidation.CollectAll()` before presenting a custom workflow;
`LiveMirroringSetupUtility.BuildTargets()` repeats the complete preflight and
stops before mutation when any core or extension error remains.

Implement `ILiveMirroringPreviewContributor` when a feature must configure a newly
created ghost or update it after the core preview transform has been applied.
Preview contributors receive the system, target, and hidden preview instance.

## Extension health

Editor tooling can call `LiveMirroringExtensionHealth.GetSnapshot()` to inspect
the discovered processors and contributors without creating separate extension
instances. Each immutable entry reports its ID, type and assembly names, order,
capability, discovery status, and the latest isolated optional-callback failure.
The snapshot is deterministic and read-only. Health is kept only for the current
editor session: it is not serialized or sent anywhere, and registry refreshes
clear that registry's prior callback failures. Type and assembly names are
support information, not stable extension identity.

## Upload boundary

Live Mirroring is an authoring-only system and removes its generated editor-only
object during play mode and avatar upload. Features required on the uploaded avatar
must bake their runtime result into supported avatar components, animations, or
another upload-time integration before Live Mirroring is stripped.
