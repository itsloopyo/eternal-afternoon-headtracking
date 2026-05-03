# Third-Party Notices

This project bundles and depends on the following third-party software.

---

## Mono.Cecil

- **Version:** 0.11.5
- **License:** MIT
- **Upstream:** https://github.com/jbevain/cecil
- **Usage:** Used at install time to inject a bootstrap call into the game's `Assembly-CSharp.dll` so our mod loads on game start. Also shipped alongside the patched assembly so the injected bootstrap can resolve its dependency at runtime.
- **Bundled:** yes (shipped in the release ZIP).

Copyright (c) 2008 - 2015 Jb Evain. Licensed under the MIT license.

---

## OpenTrack

- **Version:** N/A (UDP protocol only)
- **License:** ISC
- **Upstream:** https://github.com/opentrack/opentrack
- **Usage:** Head tracking data is received via the OpenTrack UDP protocol on port 4242. No OpenTrack code is bundled.
- **Bundled:** no (protocol only, runtime external).

---

## CameraUnlock Core Library

- **Version:** 493b736 (submodule commit)
- **License:** MIT
- **Upstream:** https://github.com/itsloopyo/cameraunlock-core
- **Usage:** Shared C# library providing the UDP receiver, tracking processing pipeline, smoothing, interpolation, Unity view-matrix modification, and math utilities. Compiled into the release ZIP as `CameraUnlock.Core.dll` and `CameraUnlock.Core.Unity.dll`.
- **Bundled:** yes (shipped in the release ZIP).

Copyright (c) itsloopyo. Licensed under the MIT license.

---

## Eternal Afternoon

Eternal Afternoon is the property of its developers and publisher. This mod is a fan project and is not affiliated with or endorsed by them.
