# ThreadLight Authoring

Shared creator-side infrastructure for ThreadLight authoring tools.

This dependency provides asset export, bootstrap generation, Live Mirroring
authoring services, validation, preview, and ownership-safe target generation
used by ThreadLight Builder and ThreadLight Mirroring. Its editors use the
neutral ThreadLight UI package for their shared theme and controls.

It owns creator-side runtime behavior and editor tooling. It remains
independent of ThreadLight Components; export is the explicit handoff to that
lightweight customer package.

Creators normally install one of the ThreadLight tools instead of installing
this package directly. Finished prefab customers only need ThreadLight Components.

## Customer export

Save the finished prefab, then open **Tools > ThreadLight > Export Asset Package...**.
Add its product content, review the selected dependencies, and keep the installer
included for customers who do not already have ThreadLight Components.

The export window runs the same conversion review used by the final export before
opening the save dialog. If the installer is excluded, the package remains
exportable but the window keeps the requirement visible: customers must install
ThreadLight Components through VPM before importing it.

Export converts supported creator components inside the customer package and
leaves the source prefab unchanged. Unity's standard export does not perform
this conversion.

If export stops, use the reported issue to prepare the prefab:

- For nested or variant creator state, make a separate export prefab and unpack
  it completely. Keep the original authoring prefab.
- For an unsupported schema, update the creator tools and rebuild the prefab.
- For a creator-only resource reference, finish authoring or copy the required
  product resource into Assets and assign that copy before exporting.

Live Mirroring pauses when more than one setup controls the same prefab root, when
saved data is invalid or newer than the installed Authoring package, or when a
mutation-critical extension cannot load. Upload cleanup also stops before changing
the upload copy if an authoring holder is the upload root or contains unrelated
runtime components.
The public cleanup methods and play-mode cleanup enforce the same rule, including
direct calls against prefab assets.

Creator and customer Live Mirroring implementations remain independent, but use
the same fail-closed topology policy for same-object, nested, persistent-asset,
cross-scene, duplicate-target, and cyclic pairs. Development conformance tests
keep those decisions aligned across the export boundary.

## Requirements

- Unity 2022.3
- VRChat SDK - Avatars 3.7 or newer within 3.x

## License

See [LICENSE.md](LICENSE.md).
