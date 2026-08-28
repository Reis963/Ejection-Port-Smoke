# Ejection Port Smoke

A BepInEx plugin for SPT *(Single Player Tarkov)* that adds a short, configurable
jet of smoke when a weapon physically ejects a cartridge or shell.

The smoke source follows the moving ejection port during the emission and uses
Tarkov's native muzzle-fume particles. It does not replace muzzle flashes or
muzzle smoke.

## Installation

1. Download the latest version of the plugin from the releases page.
2. Extract the zip file to the root of your SPT installation.

## Configuration

The plugin creates `BepInEx\config\com.diego.ejectionportsmoke.cfg` after it is
loaded for the first time.

* `General.Enabled` (default: `true`) enables or disables all smoke emitted by
  the plugin.
* `General.IncludeUnderbarrel` (default: `true`) enables smoke for underbarrel
  weapon cartridge ejections.
* `General.DebugLogging` (default: `false`) logs scheduled and completed smoke
  emissions for troubleshooting.
* `Smoke.ParticleCount` (default: `12`, range: `2` to `32`) controls how many
  particles are emitted.
* `Smoke.EmissionDuration` (default: `0.18`, range: `0.03` to `0.6`) controls
  how long the smoke source follows the weapon's ejection port, in seconds.
* `Smoke.Speed` (default: `1.5`, range: `0` to `3`) controls the outward speed
  of the smoke.
* `Smoke.ConeAngle` (default: `18`, range: `0` to `60`) controls the half-angle
  of the emission cone, in degrees.
* `Smoke.Size` (default: `0.12`, range: `0.01` to `1`) controls the base particle
  size.
* `Smoke.Lifetime` (default: `0.25`, range: `0.1` to `3`) controls how long each
  particle remains visible, in seconds.
* `Smoke.PositionSpread` (default: `0.015`, range: `0` to `0.5`) controls the
  random starting-position radius around the ejection port.
* `Smoke.SpreadSpeed` (default: `0.12`, range: `0` to `2`) controls the random
  expansion speed of the smoke.
* `Smoke.RiseSpeed` (default: `0.16`, range: `0` to `2`) controls the upward
  velocity applied to the smoke.
* `Smoke.EjectionVelocityInheritance` (default: `0.06`, range: `0` to `1`)
  controls how much of the cartridge or shell velocity is inherited by the
  smoke.
* `Smoke.Opacity` (default: `0.42`, range: `0` to `1`) controls the starting
  opacity of the smoke.

## Known Issues

* Weapons that do not create a physical cartridge or shell through Tarkov's
  extraction pipeline do not trigger the effect.
* The plugin uses Tarkov's shared native fume effect, so mods that replace that
  effect can change the appearance of the ejection-port smoke.

## License

The plugin is licensed under the MIT License. See the LICENSE file for more
information.
