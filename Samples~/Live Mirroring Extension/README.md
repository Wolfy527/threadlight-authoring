# Live Mirroring extension sample

Import this sample from Package Manager, then add
`LiveMirroringExtensionExampleSettings` beside a `LiveMirroringSystem` to opt
into the examples. The sample demonstrates permanent IDs, read-only validation,
an isolated processor, preview customization, an Undo-aware target-build hook,
and extension-health inspection without referencing the private ThreadLight Builder.

The processor and target-build contributor deliberately make no creator-facing
changes. Replace their marked policy sections with your feature's behavior,
preserve unrelated content, and register every target-build mutation with Undo.
