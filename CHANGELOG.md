# Changelog

## [0.1.8] - 2026-08-18

### Changed

- Performance and stability improvements

## [0.1.7] - 2026-08-18

### Fixed

- match stub member kinds to the shipped Unity assemblies
- compile the uGUI stubs into UnityEngine.UI, not UnityEngine

## [0.1.6] - 2026-08-17

### Fixed

- follow core's per-connection smoothing split
- match stub member kinds to the shipped Unity assemblies

## [Unreleased]

### Fixed

- `pixi run install` no longer silently deploys a stale bootstrap patch. The
  Cecil deploy did not pass its patch marker to `Invoke-DevDeployCecil`, so the
  corrupt-backup guard was skipped: once an already-patched assembly had been
  captured as `Assembly-CSharp.dll.original`, every later deploy restored that
  patched backup, saw its own marker and reported "already patched - skipping",
  never applying the current injection. `pixi run uninstall` would also have
  restored a patched assembly instead of a vanilla one. `BootstrapPatcher` gained
  `UnpatchAssembly`, wired in as the `-Unpatcher`, so a corrupt backup is
  repaired in place rather than enshrined.

### Changed

- Smoothing is now two `HeadTracking.cfg` keys instead of one: `LocalSmoothing`
  (default 0.0) applies when the tracker runs on this machine, `RemoteSmoothing`
  (default 0.15) applies when the tracker is a remote device on the network. The
  value is selected per connection from the packet source address and
  re-evaluated whenever the connection changes.
- Removed the `Smoothing` key. Both new keys cover rotation and position, and
  the hardcoded 0.15 position smoothing is gone with them.
- Removed the hidden 0.15 baseline smoothing floor. Local users now get
  zero-latency tracking by default instead of a silently enforced minimum.
- Sample-rate-to-frame-rate interpolation is no longer gated on the smoothing
  value, so local users at smoothing 0.0 keep smooth motion on high-refresh
  displays.

## [0.1.5] - 2026-08-03

### Fixed

- show full control set in pixi install via shared -Controls
- recenter via tracker app request instead of on reconnection

## [0.1.4] - 2026-06-07

### Other

- powershell: stop redirecting git stderr in Invoke-VersionCommit

## [0.1.3] - 2026-06-07

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- manifest-mode delivery and pixi-driven CI
- add R.E.P.O. detection data

### Fixed

- fail fast in ASI dev-deploy when the game is running
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch

### Other

- Add Ubisoft Connect detection and VendorZip BepInEx install
- Add PluginSubfolder param to Invoke-DevDeployBepInEx
- Add Xbox install path for Easy Delivery Co
- Add GOG IDs for Cyberpunk 2077
- Add PLUGIN_SUBFOLDER support to BepInEx install/uninstall bodies
- scripts: drop the two-phase loader-init prompt from install bodies
- data: add Black & White (Lionhead) to games registry
- scripts: detect BepInEx 6 IL2CPP via BepInEx.Core.dll marker
- powershell: skip cameraunlock-core remote refresh in CI
- scripts: add UE4SS install template, fix delayed expansion in ASI body, expand games registry
- protocol: reject finite-but-out-of-float-range packet values
- data: add Subnautica 2 to games registry
- detection: add installer-registry game path lookup (Black & White GameDir)
- protocol: reorder tracking data member in udp_receiver
- data: fix Subnautica 2 Steam app id (3367150 -> 1962700)
- data: add Ni no Kuni Remastered and Yakuza 0; switch find-game output to UTF-8
- detection: add Xbox/GDK build support for Subnautica 2 (and any future GDK title)
- find-game: escape `&` in GAME_DISPLAY_NAME so echo doesn't split
- templates: add uninstall.ps1; data: add Deus Ex Mankind Divided
- powershell: add NightlyRelease module for Patreon-gated nightly builds
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics; powershell: write nightly manifest.json without UTF-8 BOM; data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: publish dev builds as GitHub pre-releases
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics
- data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: run gh under Continue so its stderr doesn't abort the dev-release publish
- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it

## [0.1.2] - 2026-05-03

### Other

- Verify existing BepInEx loader arch and replace on mismatch
- Fall back to dev-tree vendor path in BepInEx install body

## [0.1.1] - 2026-05-03

### Other

- Add automatic port retry to C++ UdpReceiver
- Take BuildOutputPath in dev-deploy and add loader/config auto-install

## [1.0.0] - 2026-04-22

### Added

- Added decoupled head tracking with OpenTrack/phone app UDP support.
- Added 6DOF positional tracking with configurable per-axis limits.
- Added automatic game state detection (menus, loading, pause).
- Added auto-recenter on first connection.
- Added configurable sensitivity, smoothing, and deadzone.
