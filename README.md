# Murder Mayhem

A Shadows of Doubt mod that enhances murder cases with custom location support.

![Murder Mayhem](MurderMayhem/icon.png)

## Overview

Murder Mayhem expands the murder case system in Shadows of Doubt by allowing custom murder cases to occur in locations that would normally be rejected by the base game. This mod is perfect for:

- Mod creators who want to design more varied and interesting murder scenarios
- Players who want more unpredictable and diverse murder locations
- Anyone who wants to experience murders in previously restricted areas like crowded workplaces or dark alleys

The mod works by scanning all installed profiles for custom MurderMO JSON files and applying special location handling based on custom flags.

## Features

- **Custom Location Support**: Override the game's default location restrictions
- **Workplace Murders**: Allow murders at victim workplaces even when crowded
- **Street Murders**: Enable murders in alleys and backstreets
- **Occupancy Control**: Set custom occupancy limits or disable them entirely
- **Automatic Scanning**: No manual configuration needed - just add your custom MurderMO files
- **Detailed Logging**: Comprehensive logs for debugging custom cases

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) if you haven't already
2. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/) (recommended)
3. Install Murder Mayhem via r2modman or manually by extracting to your BepInEx/plugins folder
4. Start the game and the mod will automatically scan for custom murder cases

## Creating Custom Murder Cases

### Basic Structure

Custom murder cases are defined in JSON files with the `fileType` set to `"MurderMO"`. Place these files in your mod's folder structure, and Murder Mayhem will automatically detect them.

### Special Location Flags

Add these flags to your MurderMO JSON files to enable special location handling:

| Flag | Description |
|------|-------------|
| `"allowAnywhere-Mayhem": true` | Removes all location restrictions (superset of vanilla `allowAnywhere`) |
| `"allowWork-Mayhem": true` | Allows murders at the victim's workplace, even when crowded (use occupancyLimit to override) |
| `"allowAlley-Mayhem": true` | Allows murders in alleys |
| `"allowBackstreets-Mayhem": true` | Allows murders in backstreets |
| `"allowPark-Mayhem": true` | Allows murders in parks and paths |
| `"occupancyLimit": 10` | Override the default occupancy limit (use with allowWork-Mayhem) |
| `"occupancyLimit": -1` | Disable occupancy checks entirely (infinite limit) |

These flags work alongside the standard location flags from the base game:

| Standard Flag | Description |
|---------------|--------------|
| `"allowHome": true` | Allows murders at the victim's home |
| `"allowWork": true` | Allows murders at the victim's workplace (with standard occupancy limits) |
| `"allowPublic": true` | Allows murders in public locations |
| `"allowStreets": true` | Allows murders on streets |
| `"allowAnywhere": true` | Allows murders in any location |

## How It Works

Murder Mayhem uses Harmony patches to modify the game's murder location selection and victim movement logic:

1. When a murder is initiated, the mod checks if the selected MurderMO has any of our custom flags
2. If custom flags are present, the mod overrides the game's location validation to accept our special locations
3. If the victim is taking too long to reach a location, the mod can redirect them to appropriate custom locations
4. Detailed logs are generated to help with debugging

### Dynamic Location Rules

To avoid hard-coding location names, the mod uses a small, reusable "LocationRule" model to match and select locations by:

- Preset names (e.g., Address preset "Park", "Path").
- Fallback name substrings (e.g., "park", "path").
- Optional name exclusions (e.g., exclude "parking" when matching "park").

Currently implemented rule(s):

- Park/Path rule, activated by `"allowPark-Mayhem": true`.
  - Prefers addresses with presets `Park` or `Path`.
  - Falls back to name matches containing `park`/`path` while excluding `parking`.
  - Picks the candidate with the lowest occupancy that passes usability checks (e.g., occupancy limit overrides, etc.).

Extending rules (for mod developers):

- New rules can be added following the same pattern (e.g., Alleys, Backstreets) using preset names and/or safe name substrings.
- Patches call a single helper that finds the best-matching location, which reduces duplication and makes future additions easy.

## Compatibility

- Works with Shadows of Doubt v1.x
- Compatible with other mods that don't modify the same murder controller methods
- Safe to add or remove mid-game

## Troubleshooting

If your custom murder cases aren't working as expected:

1. Check the BepInEx logs for entries starting with `[MurderMayhem]`
2. Verify your JSON files have the correct flags and are properly formatted
3. Make sure your MurderMO files are in a location that will be scanned by the mod
4. Look for specific log entries about location validation:
   - `[Patch] IsValidLocation: allowAlley-Mayhem true; ...`
   - `[Patch] IsValidLocation: allowBackstreets-Mayhem true; ...`
   - `[Patch] IsValidLocation: allowPark-Mayhem true; ...`
   - `[Patch] IsValidLocation: allowWork-Mayhem true; ...`
   - `[Patch] IsValidLocation: Allowing ANY location due to allowAnywhere-Mayhem`

## Known Issues

- Some locations might still be rejected if they have other restrictions not handled by this mod

## Future Plans

- Support for more location types
- Custom murder timing and conditions
- Integration with other murder-related mods

## Credits

- Created by ShaneeexD

## Changelog

### v1.0.0
- Initial release
- Support for workplace, alley, and backstreet murders
- Custom occupancy limits
