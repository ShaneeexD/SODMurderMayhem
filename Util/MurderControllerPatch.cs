using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MurderMayhem
{
    [HarmonyPatch(typeof(MurderController), "PickNewVictim")]
    public class PickNewVictimPatch
    {
        // This method will be called when a new victim is picked
        public static void Postfix()
        {
            try
            {
                // Get the current murder type
                string murderType = "";
                if (MurderController.Instance != null && MurderController.Instance.chosenMO != null)
                {
                    murderType = MurderController.Instance.chosenMO.name;
                    Plugin.Log?.LogInfo($"Murder MO detected: {murderType}");
                    
                    // Check if this is one of our custom MurderMOs
                    bool isCustomCase = IsCustomMurderMO(murderType);
                    if (isCustomCase)
                    {
                        Plugin.Log?.LogInfo($"CUSTOM CASE DETECTED: {murderType}");
                        var caseInfo = Plugin.CustomCases.FirstOrDefault(c => string.Equals(c.PresetName, murderType, StringComparison.OrdinalIgnoreCase));
                        if (caseInfo != null)
                        {
                            Plugin.Log?.LogInfo($"Custom Case Profile: {caseInfo.ProfileName}");
                            Plugin.Log?.LogInfo($"Location Keys Present: {caseInfo.GetLocationKeysInfo()}");
                            // Added: show file path and parsed flag booleans to diagnose selection & parsing
                            Plugin.Log?.LogInfo($"Custom Case FilePath: {caseInfo.FilePath}");
                            Plugin.Log?.LogInfo(
                                $"Flags => Anywhere-Mayhem={caseInfo.HasAllowAnywhereMayhem}, Alley-Mayhem={caseInfo.HasAllowAlleyMayhem}, Backstreets-Mayhem={caseInfo.HasAllowBackstreetsMayhem}, Work-Mayhem={caseInfo.HasAllowWorkMayhem}, Work={caseInfo.HasAllowWork}, Public={caseInfo.HasAllowPublic}, Streets={caseInfo.HasAllowStreets}, Home={caseInfo.HasAllowHome}, Anywhere={caseInfo.HasAllowAnywhere}, OccupancyLimit={(caseInfo.OccupancyLimit.HasValue ? caseInfo.OccupancyLimit.Value.ToString() : "null")}"
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in PickNewVictimPatch: {ex.Message}");
            }
        }
        
        private static bool IsCustomMurderMO(string murderType)
        {
            // First check if we have any custom cases loaded
            if (Plugin.CustomCases == null || Plugin.CustomCases.Count == 0)
            {
                return false;
            }
            
            // Check if the murder type matches any of our custom case preset names
            // Note: We're comparing case-insensitively since JSON parsing and game naming might differ
            foreach (var customCase in Plugin.CustomCases)
            {
                if (string.Equals(murderType, customCase.PresetName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
    }

    // Early interception patch for TryPickNewVictimSite to force park locations for allowPark-Mayhem
    [HarmonyPatch(typeof(MurderController.Murder), "TryPickNewVictimSite")]
    public class TryPickNewVictimSitePrefixPatch
    {
        public static bool Prefix(MurderController.Murder __instance, ref bool __result, ref NewGameLocation newTargetSite)
        {
            try
            {
                string moName = __instance?.mo?.name ?? "(null)";
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                
                // If allowPark-Mayhem is active, immediately set a park location
                if (caseInfo?.HasAllowParkMayhem == true)
                {
                    var chosen = MurderPatchHelpers.FindBestLocationByRule(__instance, caseInfo, MurderPatchHelpers.ParkRule);
                    if (chosen != null)
                    {
                        // Set the result directly and skip the original method
                        newTargetSite = chosen;
                        __result = true;
                        Plugin.Log?.LogInfo($"[Patch] TryPickNewVictimSitePrefix: INTERCEPTED and set Park/Path location '{chosen.name}' due to allowPark-Mayhem");
                        
                        // Skip the original method
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in TryPickNewVictimSitePrefixPatch: {ex.Message}");
            }
            
            // Continue with the original method
            return true;
        }
    }
    
    // Base postfix patch to handle custom location handling for custom MurderMOs
    [HarmonyPatch(typeof(MurderController.Murder), "TryPickNewVictimSite")]
    public class TryPickNewVictimSitePatch
    {
        public static void Postfix(MurderController.Murder __instance, ref bool __result, ref NewGameLocation newTargetSite)
        {
            try
            {
                string moName = __instance?.mo?.name ?? "(null)";
                string siteName = newTargetSite != null ? newTargetSite.name : "null";
                Plugin.Log?.LogInfo($"[Patch] TryPickNewVictimSite called. MO={moName}, result={__result}, site={siteName}");

                // If allowAnywhere-Mayhem is active, force the victim's current location as the site
                var caseInfoAnywhere = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                if (caseInfoAnywhere?.HasAllowAnywhereMayhem == true && __instance?.victim?.currentGameLocation != null)
                {
                    newTargetSite = __instance.victim.currentGameLocation;
                    __result = true;
                    Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Overriding to victim's CURRENT location due to allowAnywhere-Mayhem");
                    return;
                }

                // If base failed to choose a site, and our custom key is present, try the victim's workplace
                if (!__result && __instance != null && __instance.victim != null)
                {
                    var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                    if (caseInfo?.HasAllowWorkMayhem == true)
                    {
                        var workplace = __instance.victim.job?.employer?.placeOfBusiness;
                        if (workplace != null && MurderPatchHelpers.IsLocationUsable(__instance, workplace, caseInfo))
                        {
                            newTargetSite = workplace;
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Overriding to victim workplace due to allowWork-Mayhem");
                        }
                    }
                }
                
                // If allowPark-Mayhem is active, prefer Park/Path locations via dynamic rule
                {
                    var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                    if (caseInfo?.HasAllowParkMayhem == true)
                    {
                        var chosen = MurderPatchHelpers.FindBestLocationByRule(__instance, caseInfo, MurderPatchHelpers.ParkRule);
                        if (chosen != null)
                        {
                            newTargetSite = chosen;
                            __result = true;
                            Plugin.Log?.LogInfo($"[Patch] TryPickNewVictimSite: Overriding to Park/Path due to allowPark-Mayhem -> {chosen.name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in TryPickNewVictimSitePatch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(MurderController.Murder), "SetMurderLocation")]
    public class SetMurderLocationPatch
    {
        public static void Postfix(MurderController.Murder __instance, NewGameLocation newLoc)
        {
            try
            {
                string moName = __instance?.mo?.name ?? "(null)";
                string locName = newLoc != null ? newLoc.name : "null";
                Plugin.Log?.LogInfo($"[Patch] SetMurderLocation called. MO={moName}, newLoc={locName}");
                // No-op for now: allow original behavior
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in SetMurderLocationPatch: {ex.Message}");
            }
        }
    }

    // Prefix to force 'kill right here' behavior when allowAnywhere-Mayhem is active
    [HarmonyPatch(typeof(MurderController.Murder), "SetMurderLocation")]
    public class SetMurderLocationPrefixPatch
    {
        public static void Prefix(MurderController.Murder __instance, ref NewGameLocation newLoc)
        {
            try
            {
                string moName = __instance?.mo?.name ?? "(null)";
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);

                // If allowAnywhere-Mayhem is active, force the victim's current location (even if vanilla would reject it)
                if (caseInfo?.HasAllowAnywhereMayhem == true)
                {
                    newLoc = __instance.victim.currentGameLocation;
                    Plugin.Log?.LogInfo("[Patch] SetMurderLocation.Prefix: Overriding to victim CURRENT location due to allowAnywhere-Mayhem");
                    return;
                }
                
                // If allowPark-Mayhem is active, force a Park/Path location if the chosen location isn't park-like
                if (caseInfo?.HasAllowParkMayhem == true)
                {
                    // Check if current location is already park-like
                    var asAddr = newLoc?.thisAsAddress;
                    string name = newLoc?.name?.ToLower() ?? "";
                    bool isParkPreset = asAddr != null && (string.Equals(asAddr.addressPreset?.presetName, "Park", StringComparison.OrdinalIgnoreCase) || 
                                                         string.Equals(asAddr.addressPreset?.presetName, "Path", StringComparison.OrdinalIgnoreCase));
                    bool isParkName = (name.Contains("park") && !name.Contains("parking")) || name.Contains("path");
                    
                    if (!isParkPreset && !isParkName)
                    {
                        // Find a suitable park/path location
                        NewGameLocation chosen = null;
                        int chosenOcc = int.MaxValue;
                        
                        // First try addresses with Park/Path preset
                        foreach (var loc in CityData.Instance.gameLocationDirectory)
                        {
                            if (loc == null) continue;
                            
                            // Check if it's a park-like location by preset
                            var addr = loc.thisAsAddress;
                            if (addr != null)
                            {
                                var preset = addr.addressPreset?.presetName;
                                if (string.Equals(preset, "Park", StringComparison.OrdinalIgnoreCase) || 
                                    string.Equals(preset, "Path", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (MurderPatchHelpers.IsLocationUsable(__instance, loc, caseInfo))
                                    {
                                        int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                                        if (chosen == null || occ < chosenOcc)
                                        {
                                            chosen = loc;
                                            chosenOcc = occ;
                                        }
                                    }
                                }
                            }
                        }
                        
                        // If no preset match, try by name
                        if (chosen == null)
                        {
                            foreach (var loc in CityData.Instance.gameLocationDirectory)
                            {
                                if (loc == null) continue;
                                
                                string locName = loc.name?.ToLower() ?? "";
                                if (locName.Contains("park") || locName.Contains("path"))
                                {
                                    if (MurderPatchHelpers.IsLocationUsable(__instance, loc, caseInfo))
                                    {
                                        int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                                        if (chosen == null || occ < chosenOcc)
                                        {
                                            chosen = loc;
                                            chosenOcc = occ;
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (chosen != null)
                        {
                            Plugin.Log?.LogInfo($"[Patch] SetMurderLocation.Prefix: Redirecting non-park choice '{newLoc?.name ?? "(null)"}' to Park/Path '{chosen.name}' due to allowPark-Mayhem");
                            newLoc = chosen;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in SetMurderLocationPrefixPatch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(MurderController.Murder), "IsValidLocation")]
    public class IsValidLocationPatch
    {
        public static void Postfix(MurderController.Murder __instance, NewGameLocation newLoc, ref bool __result)
        {
            try
            {
                string moName = __instance?.mo?.name ?? "(null)";
                string locName = newLoc != null ? newLoc.name : "null";
                Plugin.Log?.LogInfo($"[Patch] IsValidLocation called. MO={moName}, loc={locName}, result={__result}");

                if (!__result && __instance != null && newLoc != null)
                {
                    var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                    // Anywhere-Mayhem: remove all restrictions, mirror and extend vanilla allowAnywhere
                    if (caseInfo?.HasAllowAnywhereMayhem == true)
                    {
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing ANY location due to allowAnywhere-Mayhem");
                    }
                    if (caseInfo?.HasAllowWorkMayhem == true)
                    {
                        // Treat victim's workplace as valid, even if base code rejected due to standard allowWork checks
                        var employer = __instance.victim?.job?.employer;
                        var workplace = employer?.placeOfBusiness;

                        // Diagnostics on equivalence
                        var wpAddr = workplace?.thisAsAddress;
                        var nlAddr = newLoc.thisAsAddress;
                        bool eq = workplace != null && workplace == newLoc;
                        bool addrEq = wpAddr != null && nlAddr != null && wpAddr == nlAddr;
                        bool companyMatch = nlAddr != null && employer != null && nlAddr.company == employer;
                        Plugin.Log?.LogInfo($"[Patch] IsValidLocation: allowWork-Mayhem true; eq={eq}, addrEq={addrEq}, companyMatch={companyMatch}");

                        if (MurderPatchHelpers.IsVictimWorkplace(__instance, newLoc) && MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                        {
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing victim workplace due to allowWork-Mayhem");
                        }
                    }

                    // Alley support: accept Alley preset locations if custom key is set
                    if (!__result && caseInfo?.HasAllowAlleyMayhem == true)
                    {
                        var asAddress = newLoc.thisAsAddress;
                        var preset = asAddress?.addressPreset?.presetName ?? "(null)";
                        bool isStreet = newLoc is StreetController;
                        bool streetIsAlley = isStreet && ((StreetController)newLoc).isAlley;
                        var typeName = newLoc.GetType().Name;
                        Plugin.Log?.LogInfo($"[Patch] IsValidLocation: allowAlley-Mayhem true; preset={preset}, type={typeName}, isStreet={isStreet}, streetIsAlley={streetIsAlley}");
                        if (string.Equals(preset, "Alley", StringComparison.OrdinalIgnoreCase) || streetIsAlley)
                        {
                            if (MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                            {
                                __result = true;
                                Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing Alley due to allowAlley-Mayhem");
                            }
                        }
                    }

                    // Backstreet support: accept Backstreet preset or streets that are backstreets when custom key is set
                    if (!__result && caseInfo?.HasAllowBackstreetsMayhem == true)
                    {
                        var asStreet = newLoc as StreetController;
                        var asAddress = newLoc.thisAsAddress;
                        var preset = asAddress?.addressPreset?.presetName ?? "(null)";
                        bool streetIsBackstreet = asStreet?.isBackstreet ?? false;
                        Plugin.Log?.LogInfo($"[Patch] IsValidLocation: allowBackstreets-Mayhem true; preset={preset}, isBackstreet={streetIsBackstreet}");
                        if (string.Equals(preset, "Backstreet", StringComparison.OrdinalIgnoreCase) || streetIsBackstreet)
                        {
                            if (MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                            {
                                __result = true;
                                Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing Backstreet due to allowBackstreets-Mayhem");
                            }
                        }
                    }
                    
                    // Park support: accept Park/Path presets or locations with park/path in name
                    if (!__result && caseInfo?.HasAllowParkMayhem == true)
                    {
                        var asAddress = newLoc.thisAsAddress;
                        var preset = asAddress?.addressPreset?.presetName ?? "(null)";
                        string name = newLoc.name?.ToLower() ?? "";
                        bool isParkPreset = string.Equals(preset, "Park", StringComparison.OrdinalIgnoreCase) || 
                                           string.Equals(preset, "Path", StringComparison.OrdinalIgnoreCase);
                        bool isParkName = (name.Contains("park") && !name.Contains("parking")) || name.Contains("path");
                        
                        Plugin.Log?.LogInfo($"[Patch] IsValidLocation: allowPark-Mayhem true; preset={preset}, isParkName={isParkName}");
                        
                        if (isParkPreset || isParkName)
                        {
                            if (MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                            {
                                __result = true;
                                Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing Park/Path due to allowPark-Mayhem");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in IsValidLocationPatch: {ex.Message}");
            }
        }
    }

    // Helpers shared by patches
    internal static class MurderPatchHelpers
    {
        internal static CustomCaseInfo GetCustomCaseInfoForMO(string moName)
        {
            if (string.IsNullOrWhiteSpace(moName) || Plugin.CustomCases == null || Plugin.CustomCases.Count == 0)
                return null;

            // First try exact (case-insensitive) match
            var exact = Plugin.CustomCases.FirstOrDefault(c => string.Equals(c.PresetName, moName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            // Fallback: normalize names by stripping non-alphanumerics and compare
            string norm = NormalizeName(moName);
            return Plugin.CustomCases.FirstOrDefault(c => NormalizeName(c.PresetName) == norm);
        }

        // Normalize a display name for relaxed comparisons (e.g., "The Scammer" vs "TheScammer")
        internal static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var filtered = s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
            return new string(filtered);
        }

        internal static bool IsLocationUsable(MurderController.Murder murder, NewGameLocation loc, CustomCaseInfo caseInfo)
        {
            if (murder == null || loc == null)
                return false;

            // Anywhere-Mayhem: bypass all subsequent occupancy and preset restrictions
            if (caseInfo?.HasAllowAnywhereMayhem == true)
            {
                return true;
            }

            // Occupancy: allow override when allowWork-Mayhem is active and occupancyLimit present
            int baseLimit = murder.preset.nonHomeMaximumOccupantsTrigger;
            int? overrideLimit = null;
            if (caseInfo?.HasAllowWorkMayhem == true && caseInfo.OccupancyLimit.HasValue)
            {
                overrideLimit = caseInfo.OccupancyLimit.Value;
            }

            int current = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
            if (overrideLimit.HasValue)
            {
                if (overrideLimit.Value >= 0)
                {
                    if (current > overrideLimit.Value)
                    {
                        Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Rejected due to occupancy {current} > override {overrideLimit.Value}");
                        return false;
                    }
                    Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Occupancy OK {current} <= override {overrideLimit.Value}");
                }
                else
                {
                    // -1 means infinite
                    Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Occupancy override is infinite (-1), bypassing occupancy check. Current={current}");
                }
            }
            else
            {
                // Mirror the base overcrowding check
                if (loc.currentOccupants != null && current > baseLimit)
                {
                    Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Rejected due to occupancy {current} > {baseLimit}");
                    return false;
                }
            }

            var asAddress = loc.thisAsAddress;
            if (asAddress != null && asAddress.addressPreset != null)
            {
                var name = asAddress.addressPreset.presetName;
                if (name == "Ballroom" || name == "CityHallLobby")
                {
                    Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Rejected banned address preset {name}");
                    return false;
                }
            }

            return true;
        }


        // Determine if the provided location represents the victim's workplace (allow address/company equivalence)
        internal static bool IsVictimWorkplace(MurderController.Murder murder, NewGameLocation newLoc)
        {
            var employer = murder?.victim?.job?.employer;
            var workplace = employer?.placeOfBusiness;
            if (employer == null || workplace == null || newLoc == null)
                return false;

            if (workplace == newLoc)
                return true;

            var wpAddr = workplace.thisAsAddress;
            var nlAddr = newLoc.thisAsAddress;

            // Same address object
            if (wpAddr != null && nlAddr != null && wpAddr == nlAddr)
                return true;

            // Address belongs to the same company
            if (nlAddr != null && nlAddr.company == employer)
                return true;

            return false;
        }
        
        // Location rule model and helpers (centralized table for future dynamic presets)
        internal class LocationRule
        {
            public string Key; // e.g., "allowPark-Mayhem"
            public string[] PresetNames; // Address presets to match (case-insensitive)
            public string[] NameContains; // Fallback substrings to match on location name (lowercase)
            public string[] NameExcludes; // Substrings that must NOT be present (lowercase), e.g., exclude "parking" when matching "park"
            // Future: add fields for street flags, etc.
        }

        // Rule: Park/Path
        internal static readonly LocationRule ParkRule = new LocationRule
        {
            Key = "allowPark-Mayhem",
            PresetNames = new[] { "Park", "Path" },
            NameContains = new[] { "park", "path" },
            NameExcludes = new[] { "" }
        };

        // Does a location match a rule by preset or name
        internal static bool LocationMatchesRule(NewGameLocation loc, LocationRule rule)
        {
            if (loc == null || rule == null) return false;

            var addrPreset = loc.thisAsAddress?.addressPreset?.presetName;
            if (!string.IsNullOrEmpty(addrPreset) && rule.PresetNames != null)
            {
                foreach (var p in rule.PresetNames)
                {
                    if (string.Equals(addrPreset, p, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (rule.NameContains != null && rule.NameContains.Length > 0)
            {
                string name = loc.name?.ToLower() ?? string.Empty;
                // If any exclude substring is present, treat as non-match by name
                if (rule.NameExcludes != null && rule.NameExcludes.Any(ex => !string.IsNullOrEmpty(ex) && name.Contains(ex)))
                    return false;

                foreach (var part in rule.NameContains)
                {
                    if (!string.IsNullOrEmpty(part) && name.Contains(part))
                        return true;
                }
            }

            return false;
        }

        // Find the best-matching location for a rule (prioritize preset matches, then name matches; choose lowest occupancy)
        internal static NewGameLocation FindBestLocationByRule(MurderController.Murder murder, CustomCaseInfo caseInfo, LocationRule rule)
        {
            if (murder == null || rule == null) return null;

            NewGameLocation chosen = null;
            int chosenOcc = int.MaxValue;

            // Phase 1: preset matches only
            foreach (var loc in CityData.Instance.gameLocationDirectory)
            {
                if (loc == null) continue;

                // Match only by preset
                var addrPreset = loc.thisAsAddress?.addressPreset?.presetName;
                bool presetMatch = !string.IsNullOrEmpty(addrPreset) && rule.PresetNames != null &&
                                   rule.PresetNames.Any(p => string.Equals(addrPreset, p, StringComparison.OrdinalIgnoreCase));
                if (!presetMatch)
                    continue;

                if (!IsLocationUsable(murder, loc, caseInfo))
                    continue;

                int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                if (chosen == null || occ < chosenOcc)
                {
                    chosen = loc;
                    chosenOcc = occ;
                }
            }

            if (chosen != null)
                return chosen;

            // Phase 2: fallback by name substrings
            foreach (var loc in CityData.Instance.gameLocationDirectory)
            {
                if (loc == null) continue;

                if (!LocationMatchesRule(loc, rule))
                    continue;

                if (!IsLocationUsable(murder, loc, caseInfo))
                    continue;

                int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                if (chosen == null || occ < chosenOcc)
                {
                    chosen = loc;
                    chosenOcc = occ;
                }
            }

            return chosen;
        }
    }

    // Patch to intercept AI goal creation for victims to ensure they go to the right location
    [HarmonyPatch(typeof(NewAIController), "CreateNewGoal")]
    public class CreateNewGoalPatch
    {
        public static bool Prefix(NewAIController __instance, ref AIGoalPreset newPreset, ref float newTrigerTime, ref float newDuration, ref NewNode newPassedNode, ref Interactable newPassedInteractable, ref NewGameLocation newPassedGameLocation, ref GroupsController.SocialGroup newPassedGroup, ref MurderController.Murder newMurderRef, ref int newPassedVar)
        {
            try
            {
                // Only intercept toGoGoal goals
                if (newPreset != RoutineControls.Instance.toGoGoal)
                    return true;
                    
                // Check if this citizen is a victim in an active murder
                var human = __instance?.human;
                if (human == null)
                    return true;
                    
                // Find if this citizen is a victim in a murder
                MurderController.Murder activeMurder = null;
                if (MurderController.Instance != null)
                {
                    var murder = MurderController.Instance.GetCurrentMurder();
                    if (murder != null && murder.victim == human)
                    {
                        activeMurder = murder;
                    }
                }
                
                // If not a victim, proceed normally
                if (activeMurder == null)
                    return true;
                    
                // Check if this is a custom case with allowPark-Mayhem
                string moName = activeMurder?.mo?.name ?? "(null)";
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                if (caseInfo?.HasAllowParkMayhem != true)
                    return true;
                    
                // Find a suitable park/path location via dynamic rule
                var chosen = MurderPatchHelpers.FindBestLocationByRule(activeMurder, caseInfo, MurderPatchHelpers.ParkRule);
                
                // If we found a park location, override the goal
                if (chosen != null)
                {
                    Plugin.Log?.LogInfo($"[Patch] CreateNewGoalPatch: Intercepted goal creation for victim {human.GetCitizenName()} and redirecting to park/path '{chosen.name}' due to allowPark-Mayhem");
                    
                    // Override the target location
                    newPassedGameLocation = chosen;
                    newPassedNode = human.FindSafeTeleport(chosen, false, true);
                    
                    // Let the original method continue with our modified parameters
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in CreateNewGoalPatch: {ex.Message}");
            }
            
            // Continue with original method
            return true;
        }
    }
    
    // Extend victim movement during waitForLocation to consider custom locations (e.g., alleys, workplace via custom flags)
    [HarmonyPatch(typeof(MurderController), "Update")]
    public class UpdatePatch
    {
        public static void Postfix(MurderController __instance)
        {
            try
            {
                var m = __instance?.GetCurrentMurder();
                if (m == null)
                    return;

                if (m.state != MurderController.MurderState.waitForLocation || !SessionData.Instance.play)
                    return;

                // Mirror base timing gate for creating GoTo goals while waiting
                if (SessionData.Instance.gameTime - m.waitingTimestamp <= 0.25f)
                    return;

                // If base already created a generic GoTo goal, don't duplicate
                bool hasToGo = false;
                if (m.victim != null && m.victim.ai != null && m.victim.ai.goals != null)
                {
                    foreach (var g in m.victim.ai.goals)
                    {
                        if (g != null && g.preset == RoutineControls.Instance.toGoGoal)
                        {
                            hasToGo = true;
                            break;
                        }
                    }
                }
                if (hasToGo)
                    return;

                string moName = m?.mo?.name ?? "(null)";
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                if (caseInfo == null)
                    return;

                // Anywhere-Mayhem: do not create or alter any movement; base will continue with current location
                if (caseInfo.HasAllowAnywhereMayhem)
                    return;

                // Custom: allowWork-Mayhem -> send to workplace even if vanilla allowWork is false
                if (caseInfo.HasAllowWorkMayhem)
                {
                    var workplace = m.victim?.job?.employer?.placeOfBusiness;
                    if (workplace != null && MurderPatchHelpers.IsLocationUsable(m, workplace, caseInfo))
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM work for victim {m.victim.GetCitizenName()} to: {workplace.name}", 2);
                        var ai = m.victim.ai;
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, m.victim.FindSafeTeleport(workplace, false, true), null, workplace, null, null, -2);
                        return;
                    }
                }

                // Custom: allowAlley-Mayhem -> choose a suitable alley street
                if (caseInfo.HasAllowAlleyMayhem)
                {
                    StreetController chosen = null;
                    foreach (var s in CityData.Instance.streetDirectory)
                    {
                        if (s.isAlley && MurderPatchHelpers.IsLocationUsable(m, s, caseInfo) && (chosen == null || s.currentOccupants.Count < chosen.currentOccupants.Count))
                        {
                            chosen = s;
                        }
                    }
                    if (chosen != null)
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM alley for victim {m.victim.GetCitizenName()} to: {chosen.name}", 2);
                        var ai = m.victim.ai;
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, m.victim.FindSafeTeleport(chosen, false, true), null, chosen, null, null, -2);
                        return;
                    }
                }

                // Custom: allowBackstreets-Mayhem -> choose a suitable backstreet
                if (caseInfo.HasAllowBackstreetsMayhem)
                {
                    StreetController chosen = null;
                    foreach (var s in CityData.Instance.streetDirectory)
                    {
                        if (s.isBackstreet && MurderPatchHelpers.IsLocationUsable(m, s, caseInfo) && (chosen == null || s.currentOccupants.Count < chosen.currentOccupants.Count))
                        {
                            chosen = s;
                        }
                    }
                    if (chosen != null)
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM backstreet for victim {m.victim.GetCitizenName()} to: {chosen.name}", 2);
                        var ai = m.victim.ai;
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, m.victim.FindSafeTeleport(chosen, false, true), null, chosen, null, null, -2);
                        return;
                    }
                }
                
                // Custom: allowPark-Mayhem -> choose a suitable Park/Path location via dynamic rule
                if (caseInfo.HasAllowParkMayhem)
                {
                    var chosen = MurderPatchHelpers.FindBestLocationByRule(m, caseInfo, MurderPatchHelpers.ParkRule);
                    if (chosen != null)
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM park/path for victim {m.victim.GetCitizenName()} to: {chosen.name}", 2);
                        var ai = m.victim.ai;
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, m.victim.FindSafeTeleport(chosen, false, true), null, chosen, null, null, -2);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in UpdatePatch.Postfix: {ex.Message}");
            }
        }
    }
}
