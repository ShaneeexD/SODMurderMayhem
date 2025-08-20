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
- **Dynamic Location Rules**: Centralized matching by presets and safe name substrings
- **Multiple Active Rules + Randomization**: When several rules are active, the mod selects among them in random order to avoid repetition
- **Optional Floor Name Filtering**: Rules may require specific floor names (e.g., hotel lobby vs. upper floors)
- **Automatic Scanning**: No manual configuration needed - just add your custom MurderMO files
- **Detailed Logging**: Comprehensive logs for debugging custom cases
- **Custom Case Objective Label**: When a new custom-case murder is reported, the crime scene objective text is tagged with a simple "[Custom]" suffix.

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
| `"allowHotelBathroom-Mayhem": true` | Allows murders in hotel bathrooms with floor filter for `hotel_basement` |
| `"allowDinerBathroom-Mayhem": true` | Allows murders in diner bathrooms with floor filter for `dinerfloorbeta` |
| `"allowFathomsYardBasement-Mayhem": true` | Allows murders in Fathoms Yard basement areas with floor filter for `shantytown_basement` |
| `"allowFathomsYardRooftop-Mayhem": true` | Allows murders on Fathoms Yard rooftops with floor filter for `shantytown` |
| `"allowHotelRooftopBar-Mayhem": true` | Allows murders in hotel rooftop bars with floor filter for `hotel_rooftopbar` |
| `"allowHotelRooftop-Mayhem": true` | Allows murders on hotel rooftops with floor filter for `hotel_rooftopbar` |
| `"allowMixedIndustrialRooftop-Mayhem": true` | Allows murders on mixed industrial rooftops with floor filter for `mixedindustrial` |
| `"occupancyLimit": 10` | Override the default occupancy limit (applies to all location types) |
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
- Optional floor name filters: include/exclude substrings applied to floor names of rooms belonging to a location.

Currently implemented location rules:

- Park/Path rule, activated by `"allowPark-Mayhem": true`.
  - Prefers addresses with presets `Park` or `Path`.
  - Falls back to name matches containing `park`/`path` while excluding `parking`.
  - Picks the candidate with the lowest occupancy that passes usability checks (e.g., occupancy limit overrides, etc.).
  - Note: The Park/Path rule does not require any floor names by default; floor constraints are supported by the system and can be used in future rules.

- Hotel Bathroom rule, activated by `"allowHotelBathroom-Mayhem": true`.
  - Prefers addresses with presets `BuildingBathroomMale` or `BuildingBathroomFemale`.
  - Falls back to location names containing `bathroom` or `public bathrooms`.
  - Applies floor-name filter includes for `hotel_basement` to narrow to hotel basement bathrooms.
  - Tokens are matched case-insensitively and normalized to lowercase internally.

- Diner Bathroom rule, activated by `"allowDinerBathroom-Mayhem": true`.
  - Prefers addresses with presets `BuildingBathroomMale` or `BuildingBathroomFemale`.
  - Falls back to location names containing `bathroom` or `public bathrooms`.
  - Applies floor-name filter includes for `dinerfloorbeta` to narrow to diner bathrooms.

- Fathoms Yard Basement rule, activated by `"allowFathomsYardBasement-Mayhem": true`.
  - Prefers addresses with preset `FathomsYard`.
  - Falls back to location names containing `Fathoms yard` or `Fathoms Yard`.
  - Applies floor-name filter includes for `shantytown_basement`.

- Fathoms Yard Rooftop rule, activated by `"allowFathomsYardRooftop-Mayhem": true`.
  - Prefers addresses with preset `Rooftop`.
  - Falls back to location names containing `rooftop` or `Rooftop`.
  - Applies floor-name filter includes for `shantytown`.

- Hotel Rooftop Bar rule, activated by `"allowHotelRooftopBar-Mayhem": true`.
  - Prefers addresses with preset `RooftopBar`.
  - Falls back to location names containing `rooftop bar` or `Rooftop Bar`.
  - Applies floor-name filter includes for `hotel_rooftopbar`.

- Hotel Rooftop rule, activated by `"allowHotelRooftop-Mayhem": true`.
  - Prefers addresses with preset `Rooftop`.
  - Falls back to location names containing `rooftop` or `Rooftop`.
  - Applies floor-name filter includes for `hotel_rooftopbar`.

- Mixed Industrial Rooftop rule, activated by `"allowMixedIndustrialRooftop-Mayhem": true`.
  - Prefers addresses with preset `Rooftop`.
  - Falls back to location names containing `rooftop` or `Rooftop`.
  - Applies floor-name filter includes for `mixedindustrial`.

Extending rules (for mod developers):

- New rules can be added following the same pattern using preset names and/or safe name substrings.
- **Important**: Floor name tokens must be lowercase to match properly with the game's floor names.
- Patches call into a dynamic rule registry that gathers all active rules for the current case and selects among them in random order.
- See `READ_TO_ADD_MORE_LOCATIONS.md` in the Util folder for quick reference on adding new locations.

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
