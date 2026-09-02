# Publishing

ThreadLight Authoring is the public creator-side dependency shared by
ThreadLight Builder and ThreadLight Mirroring. It must remain independent of
ThreadLight Components and the private Builder.

The public VPM listing is published through GitHub Pages at:

`https://wolfy527.github.io/threadlight-authoring/index.json`

Before publishing, validate the package, build an unpublished release archive,
and install it with ThreadLight Components absent. Then verify both public
Builder frontends against that exact archive. Do not publish a reused version
or replace an existing release artifact.
