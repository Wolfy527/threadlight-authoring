# ThreadLight Authoring

Shared creator-side infrastructure for ThreadLight authoring tools.

This dependency provides the common UI Toolkit theme, asset export workflow,
bootstrap generation, Live Mirroring authoring services, validation, preview,
and ownership-safe target generation used by ThreadLight Builder and
ThreadLight Mirroring.

It owns creator-side runtime behavior, editor tooling, and UI. It remains
independent of ThreadLight Components; export is the explicit handoff to that
lightweight customer package.

Creators normally install one of the ThreadLight tools instead of installing
this package directly. Finished prefab customers only need ThreadLight Components.

## Requirements

- Unity 2022.3
- VRChat SDK - Avatars 3.7 or newer within 3.x

## License

See [LICENSE.md](LICENSE.md).
