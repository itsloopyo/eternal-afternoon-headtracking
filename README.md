# Eternal Afternoon Head Tracking

![Eternal Afternoon running with this mod](https://raw.githubusercontent.com/itsloopyo/eternal-afternoon-headtracking/main/assets/readme-clip.gif)

*Footage of [Eternal Afternoon](https://store.steampowered.com/app/3924170/Eternal_Afternoon/), copyright (c) Alex Klexber, recorded on a purchased copy with this mod running. Used to show what the mod does; no ownership is claimed and no licence to it is granted. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).*

An unofficial head tracking mod for Eternal Afternoon that moves the view with your head while your mouse or controller keeps control of look and interaction, driven by OpenTrack over UDP, with no VR headset required.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse/controller
- **6DOF positional tracking** - lean and peek with head position
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android

## Requirements

- [Eternal Afternoon](https://store.steampowered.com/app/3924170/Eternal_Afternoon/) (Steam)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11 (64-bit)

## Installation

### Lopari

Download [Lopari](https://lopari.app), choose **Eternal Afternoon**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/eternal-afternoon-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch the game

The installer automatically finds your game via Steam registry lookup and patches `Assembly-CSharp.dll`. No mod loader (BepInEx/MelonLoader) is required. If the installer can't find the game:

- Set the `ETERNALAFTERNOON_PATH` environment variable to your game folder, or
- Run from command prompt: `install.cmd "D:\Games\Eternal Afternoon"`

### Manual Installation

If you prefer to place files by hand, or you grabbed the `-nexus` package:

1. Run `install.cmd` at least once against any copy of the game so `Assembly-CSharp.dll` gets patched (the mod is loaded via IL injection into that assembly, not via a separate mod loader)
2. Extract the Nexus ZIP into your game folder - the DLLs will land in `Eternal Afternoon_Data/Managed/`:
   - `EternalAfternoonHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`
3. Configure your tracker to output UDP to `127.0.0.1:4242`
4. Launch the game

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Each action fires on any key in its list in `CameraUnlock.ini` (see [Configuration](#configuration)). The defaults are two equivalent binding sets - use whichever your keyboard has:

| Action               | Nav-cluster | Chord          |
|----------------------|-------------|----------------|
| Toggle head tracking | `End`       | `Ctrl+Shift+Y` |
| Cycle tracking mode  | `Page Up`   | `Ctrl+Shift+G` |
| Toggle yaw mode      | `Page Down` | `Ctrl+Shift+H` |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay (rotation + position)
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

The tracking mode and the yaw mode are saved to `CameraUnlock.ini` as soon as you change them, so the game starts in the mode you left it in. `End` turns head tracking on or off for the current session only; whether it is on when the game starts is `EnableOnStartup`.

The chord letters sit in a vertical strip in the center of the keyboard. `Ctrl+Shift+<letter>` is universally avoided by games, so the chord set works whether or not your keyboard has a nav cluster. The chords are ordinary entries in each key list, so you can rebind or remove them like any other key. A key bound on its own, with no Ctrl, Shift or Alt, does not fire while Ctrl and Shift are both held.

The game's crosshair follows your aim while head tracking moves the view. There is no key or setting to turn that off.

## Configuration

<!-- cameraunlock:config -->
The mod reads its settings from `Eternal Afternoon_Data\Managed\CameraUnlock.ini` in the game folder, and creates the file when it starts and finds none. Edit it with any text editor.

A setting set to `default` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.

`Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.

When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that. Edit it with any text editor.

Earlier versions of the mod kept these settings in `HeadTracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `HeadTracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `HeadTracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.

A setting that the defaults below set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it. `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.

Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:

- Reticle settings, and a key that toggled the reticle.
- A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
- The setting for a feature that earlier versions shipped switched off while it was untested. It now follows the mod's default.

An older version of the mod reads `HeadTracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `HeadTracking.cfg`.

Deleting only `CameraUnlock.ini` makes the next start read `HeadTracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults below. Every setting they set to `default` then follows `Defaults.ini`.

On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `HeadTracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.

The built-in value of each setting set to `default` below:

- `UdpPort=4242`
- `EnableOnStartup=true`
- `WorldSpaceYaw=true`
- `RotationEnabled=true`
- `LocalSmoothing=0.0`
- `RemoteSmoothing=0.15`
- `PositionEnabled=true`
- `ToggleKey=End, Ctrl+Shift+Y`
- `CycleTrackingModeKey=PageUp, Ctrl+Shift+G`
- `YawModeKey=PageDown, Ctrl+Shift+H`

With every setting at its default, the file reads:

```ini
; Eternal Afternoon head tracking settings.
; Comments start with ; and go on their own line. Text after a value is part of the value.
; Hotkeys are key names such as End, PageUp or Ctrl+Shift+Y. Separate several with commas; leave empty for none.
; A setting set to default takes its value from Defaults.ini, which every head tracking mod
; that keeps its settings in CameraUnlock.ini reads: %AppData%\CameraUnlock\Defaults.ini on
; Windows, $XDG_CONFIG_HOME/CameraUnlock/Defaults.ini (normally ~/.config/CameraUnlock) on
; Linux, under Wine and Proton too, and ~/Library/Application Support/CameraUnlock/Defaults.ini
; on macOS. The log names the file it read. Write a value instead of default to change that
; setting for this game only.

[CameraUnlock]
; Written by the mod. Leave this section in place.
ConfigFormat=1

[Network]
; UDP port the mod receives tracker data on (OpenTrack protocol).
UdpPort=default

[General]
; true: head tracking is on when the game starts. ToggleKey turns it on and off.
EnableOnStartup=default
; true: yaw turns around the world's up axis. false: around the camera's own up axis.
WorldSpaceYaw=default
; true: turning your head turns the view.
; Tracking mode at startup, with PositionEnabled. The mode hotkey changes both.
RotationEnabled=default

[Smoothing]
; Smoothing when the tracker runs on this PC. 0 is the least, 1 the most.
LocalSmoothing=default
; Smoothing when the tracker is another device on the network, such as a phone.
; 0 is the least, 1 the most.
RemoteSmoothing=default

[Position]
; true: moving your head moves the view.
; Tracking mode at startup, with RotationEnabled. The mode hotkey changes both.
PositionEnabled=default

[Hotkeys]
; Turns head tracking on and off.
ToggleKey=default
; Changes the tracking mode: rotation and position, rotation only, position only.
CycleTrackingModeKey=default
; Switches yaw between the world's up axis and the camera's own (WorldSpaceYaw).
YawModeKey=default
```
<!-- /cameraunlock:config -->

## Troubleshooting

**Mod not loading:**
- Check `HeadTracking_BOOT.log` in `Eternal Afternoon_Data/Managed/` for bootstrap messages
- Check `HeadTracking_BOOT_ERROR.log` in your temp folder (`%TEMP%`) for bootstrap errors
- Check `HeadTracking.log` in the Managed folder for mod errors. This is the
  main log: it records the port it listened on and an `OpenTrack connected` line
  the moment the first tracker packet arrives. Send this file when reporting a
  problem.
- Verify game files through Steam and re-run `install.cmd`

All three logs are rewritten from scratch on every game launch, so they only ever
contain the most recent session.

**No tracking response:**
- Verify OpenTrack is running and outputting data
- Check UDP port matches (default 4242)
- Press **End** to enable tracking
- Check firewall isn't blocking UDP port 4242

**View is off-centre:**
- Centre it in your tracker app: OpenTrack's Center hotkey, or the centre button in your phone app. The mod keeps no centre of its own, it applies whatever pose the tracker sends.

**Jittery / unstable tracking:**
- Increase `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `CameraUnlock.ini`
- Lower the sensitivity in your tracker app. The mod has no sensitivity settings of its own
- Improve lighting for webcam-based tracking

**Wrong rotation or lean axis:**
- Invert the offending axis in your tracker app, for example in OpenTrack's output mapping. The mod has no axis inversion settings of its own

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down`. World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

## Updating

Download the new release and run `install.cmd` again. Your settings are preserved. Updating from v0.2.0 or earlier imports them from `HeadTracking.cfg` into `CameraUnlock.ini` at the first start, as [Configuration](#configuration) describes.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs and restores `Assembly-CSharp.dll` from its `.original` backup. It leaves `CameraUnlock.ini` and `HeadTracking.cfg` in place, so your settings survive a reinstall. No mod loader to remove - this mod patches `Assembly-CSharp.dll` directly rather than shipping BepInEx or MelonLoader.

If something is stuck, use:

```
uninstall.cmd /force
```

to remove everything the installer touched regardless of the state file.

## Building from Source

### Prerequisites

- Windows 10/11
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- [pixi](https://pixi.sh) task runner
- Eternal Afternoon installed via Steam (for Unity reference DLLs)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/eternal-afternoon-headtracking.git
cd eternal-afternoon-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License - see [LICENSE](LICENSE) for details.

## Credits

- [Alex Klexber](https://store.steampowered.com/app/3924170/Eternal_Afternoon/) - Eternal Afternoon
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking protocol and software
- [Mono.Cecil](https://github.com/jbevain/cecil) - .NET assembly manipulation (used for install-time IL injection)

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by Klexber or the Eternal Afternoon development team. Use at your own risk.
