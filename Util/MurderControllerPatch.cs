using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
                                $"Flags => Anywhere-Mayhem={caseInfo.HasAllowAnywhereMayhem}, Alley-Mayhem={caseInfo.HasAllowAlleyMayhem}, Backstreets-Mayhem={caseInfo.HasAllowBackstreetsMayhem}, Work-Mayhem={caseInfo.HasAllowWorkMayhem}, VictimHomeRooftop-Mayhem={caseInfo.HasAllowVictimHomeRooftopMayhem}, VictimWorkRooftop-Mayhem={caseInfo.HasAllowVictimWorkRooftopMayhem}, Work={caseInfo.HasAllowWork}, Public={caseInfo.HasAllowPublic}, Streets={caseInfo.HasAllowStreets}, Home={caseInfo.HasAllowHome}, Anywhere={caseInfo.HasAllowAnywhere}, OccupancyLimit={(caseInfo.OccupancyLimit.HasValue ? caseInfo.OccupancyLimit.Value.ToString() : "null")}"
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
                // Hardcoded: allowVictimHomeRooftop-Mayhem
                if (caseInfo?.HasAllowVictimHomeRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindVictimHomeRooftop(__instance, caseInfo, out var victimHome);
                    if (rooftop != null)
                    {
                        if (MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                        {
                            newTargetSite = rooftop;
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: Overriding to victim HOME ROOFTOP due to allowVictimHomeRooftop-Mayhem");
                            // Ensure murderer state progresses by explicitly setting the murder location
                            try { __instance.SetMurderLocation(newTargetSite); } catch (Exception ex) { Plugin.Log?.LogWarning($"[Patch] TryPickNewVictimSitePrefix: SetMurderLocation failed: {ex.Message}"); }
                            return false; // skip vanilla
                        }
                    }
                    // No rooftop found: if home exists and usable, prefer letting vanilla pick it; do not force here
                    if (victimHome != null)
                    {
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: No rooftop, falling back to vanilla (home should be valid)");
                    }
                }

                // Hardcoded: allowVictimWorkRooftop-Mayhem
                if (caseInfo?.HasAllowVictimWorkRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindVictimWorkRooftop(__instance, caseInfo, out var victimWork);
                    if (rooftop != null)
                    {
                        if (MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                        {
                            newTargetSite = rooftop;
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: Overriding to victim WORK ROOFTOP due to allowVictimWorkRooftop-Mayhem");
                            // Ensure murderer state progresses by explicitly setting the murder location
                            try { __instance.SetMurderLocation(newTargetSite); } catch (Exception ex) { Plugin.Log?.LogWarning($"[Patch] TryPickNewVictimSitePrefix: SetMurderLocation failed: {ex.Message}"); }
                            return false; // skip vanilla
                        }
                    }
                    // No rooftop found: if work exists and usable, prefer letting vanilla pick it; do not force here
                    if (victimWork != null)
                    {
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: No work rooftop found, allowing vanilla to proceed");
                    }
                }

                // New: allowMurdererHomeRooftop-Mayhem
                if (caseInfo?.HasAllowMurdererHomeRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindMurdererHomeRooftop(__instance, caseInfo, out var murdererHome);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                    {
                        newTargetSite = rooftop;
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: Overriding to MURDERER HOME ROOFTOP due to allowMurdererHomeRooftop-Mayhem");
                        try { __instance.SetMurderLocation(newTargetSite); } catch (Exception ex) { Plugin.Log?.LogWarning($"[Patch] TryPickNewVictimSitePrefix: SetMurderLocation failed: {ex.Message}"); }
                        return false;
                    }
                    if (murdererHome != null)
                    {
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: No murderer home rooftop found, letting vanilla continue");
                    }
                }

                // New: allowMurdererWorkRooftop-Mayhem
                if (caseInfo?.HasAllowMurdererWorkRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindMurdererWorkRooftop(__instance, caseInfo, out var murdererWork);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                    {
                        newTargetSite = rooftop;
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: Overriding to MURDERER WORK ROOFTOP due to allowMurdererWorkRooftop-Mayhem");
                        try { __instance.SetMurderLocation(newTargetSite); } catch (Exception ex) { Plugin.Log?.LogWarning($"[Patch] TryPickNewVictimSitePrefix: SetMurderLocation failed: {ex.Message}"); }
                        return false;
                    }
                    if (murdererWork != null)
                    {
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSitePrefix: No murderer work rooftop found, allowing vanilla to proceed");
                    }
                }

                var rules = MurderPatchHelpers.GetActiveRules(caseInfo);
                if (rules.Count > 0)
                {
                    var chosen = MurderPatchHelpers.FindBestLocationByRulesRandom(__instance, caseInfo, rules);
                    if (chosen != null)
                    {
                        // Set the result directly and skip the original method
                        newTargetSite = chosen;
                        __result = true;
                        var matched = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules)?.Key ?? "dynamic-rule";
                        Plugin.Log?.LogInfo($"[Patch] TryPickNewVictimSitePrefix: INTERCEPTED and set '{chosen.name}' due to {matched}");
                        // Ensure murderer state progresses by explicitly setting the murder location
                        try { __instance.SetMurderLocation(newTargetSite); } catch (Exception ex) { Plugin.Log?.LogWarning($"[Patch] TryPickNewVictimSitePrefix: SetMurderLocation failed: {ex.Message}"); }
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

                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                // If allowAnywhere-Mayhem is active, force the victim's current location as the site
                if (caseInfo?.HasAllowAnywhereMayhem == true && __instance?.victim?.currentGameLocation != null)
                {
                    newTargetSite = __instance.victim.currentGameLocation;
                    __result = true;
                    Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Overriding to victim's CURRENT location due to allowAnywhere-Mayhem");
                    return;
                }

                // If the new rooftop flag is set and we haven't got a valid site, try again here as a fallback
                if ((!__result || newTargetSite == null) && caseInfo?.HasAllowVictimHomeRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindVictimHomeRooftop(__instance, caseInfo, out _);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                    {
                        newTargetSite = rooftop;
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Postfix override to victim HOME ROOFTOP due to allowVictimHomeRooftop-Mayhem");
                        return;
                    }
                }

                // If the new rooftop flag is set and we haven't got a valid site, try again here as a fallback
                if ((!__result || newTargetSite == null) && caseInfo?.HasAllowVictimWorkRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindVictimWorkRooftop(__instance, caseInfo, out _);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                    {
                        newTargetSite = rooftop;
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Postfix override to victim WORK ROOFTOP due to allowVictimWorkRooftop-Mayhem");
                        return;
                    }
                }

                // New: murderer home rooftop fallback
                if ((!__result || newTargetSite == null) && caseInfo?.HasAllowMurdererHomeRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindMurdererHomeRooftop(__instance, caseInfo, out _);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                    {
                        newTargetSite = rooftop;
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Postfix override to MURDERER HOME ROOFTOP due to allowMurdererHomeRooftop-Mayhem");
                        return;
                    }
                }

                // New: murderer work rooftop fallback
                if ((!__result || newTargetSite == null) && caseInfo?.HasAllowMurdererWorkRooftopMayhem == true)
                {
                    var rooftop = MurderPatchHelpers.TryFindMurdererWorkRooftop(__instance, caseInfo, out _);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(__instance, rooftop, caseInfo))
                    {
                        newTargetSite = rooftop;
                        __result = true;
                        Plugin.Log?.LogInfo("[Patch] TryPickNewVictimSite: Postfix override to MURDERER WORK ROOFTOP due to allowMurdererWorkRooftop-Mayhem");
                        return;
                    }
                }

                // If base failed to choose a site, and our custom key is present, try the victim's workplace
                if (!__result && __instance != null && __instance.victim != null)
                {
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

                // Dynamic rule selection: if any active rules, prefer a matching location (random rule order)
                var rules = MurderPatchHelpers.GetActiveRules(caseInfo);
                if (rules.Count > 0)
                {
                    if (!MurderPatchHelpers.LocationMatchesAnyRule(newTargetSite, rules))
                    {
                        var chosen = MurderPatchHelpers.FindBestLocationByRulesRandom(__instance, caseInfo, rules);
                        if (chosen != null)
                        {
                            newTargetSite = chosen;
                            __result = true;
                            var matched = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules)?.Key ?? "dynamic-rule";
                            Plugin.Log?.LogInfo($"[Patch] TryPickNewVictimSite: Overriding to '{chosen.name}' due to {matched}");
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
                
                // If any active rules exist and current location doesn't match, redirect dynamically
                var rules = MurderPatchHelpers.GetActiveRules(caseInfo);
                if (rules.Count > 0)
                {
                    if (!MurderPatchHelpers.LocationMatchesAnyRule(newLoc, rules))
                    {
                        var chosen = MurderPatchHelpers.FindBestLocationByRulesRandom(__instance, caseInfo, rules);
                        if (chosen != null)
                        {
                            var matched = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules)?.Key ?? "dynamic-rule";
                            Plugin.Log?.LogInfo($"[Patch] SetMurderLocation.Prefix: Redirecting '{newLoc?.name ?? "(null)"}' to '{chosen.name}' due to {matched}");
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
                    // Allow victim home or the victim's building rooftop if the rooftop rule is active
                    if (!__result && caseInfo?.HasAllowVictimHomeRooftopMayhem == true)
                    {
                        var homeAddr = __instance.victim?.home;
                        var victimBuilding = homeAddr?.building;
                        var asAddr = newLoc.thisAsAddress;

                        bool allow = false;
                        if (homeAddr != null && newLoc == (NewGameLocation)homeAddr)
                        {
                            allow = true; // exact home match
                        }
                        else if (asAddr != null)
                        {
                            // Same building and contains a Rooftop sub-room
                            if (victimBuilding != null && asAddr.building == victimBuilding)
                            {
                                var rooms = asAddr.rooms;
                                if (rooms != null)
                                {
                                    foreach (var r in rooms)
                                    {
                                        var rp = r?.preset?.name;
                                        if (!string.IsNullOrEmpty(rp) && string.Equals(rp, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                        { allow = true; break; }
                                        var rn = r?.name;
                                        if (!allow && !string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                        { allow = true; break; }
                                    }
                                }
                            }
                        }

                        if (allow && MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                        {
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing victim BUILDING ROOFTOP due to allowVictimHomeRooftop-Mayhem");
                        }
                    }

                    // Allow victim work rooftop if the rooftop rule is active (but NOT the exact workplace itself)
                    if (!__result && caseInfo?.HasAllowVictimWorkRooftopMayhem == true)
                    {
                        var workAddr = __instance.victim?.job?.employer?.placeOfBusiness?.thisAsAddress;
                        var victimBuilding = workAddr?.building;
                        var asAddr = newLoc.thisAsAddress;
                        bool allow = false;
                        if (asAddr != null)
                        {
                            // Same building and contains a Rooftop sub-room
                            if (victimBuilding != null && asAddr.building == victimBuilding)
                            {
                                var rooms = asAddr.rooms;
                                if (rooms != null)
                                {
                                    foreach (var r in rooms)
                                    {
                                        var rp = r?.preset?.name;
                                        if (!string.IsNullOrEmpty(rp) && string.Equals(rp, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                        { allow = true; break; }
                                        var rn = r?.name;
                                        if (!allow && !string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                        { allow = true; break; }
                                    }
                                }
                            }
                        }
                        if (allow && MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                        {
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing victim BUILDING WORK ROOFTOP due to allowVictimWorkRooftop-Mayhem");
                        }
                    }

                    // Allow murderer home rooftop if the rooftop rule is active
                    if (!__result && caseInfo?.HasAllowMurdererHomeRooftopMayhem == true)
                    {
                        var homeAddr = __instance.murderer?.home; 
                        var murdererBuilding = homeAddr?.building;
                        var asAddr = newLoc.thisAsAddress;
                        bool allow = false;
                        // Exact home match
                        if (homeAddr != null && newLoc == (NewGameLocation)homeAddr)
                        {
                            allow = true;
                        }
                        else if (asAddr != null && murdererBuilding != null && asAddr.building == murdererBuilding)
                        {
                            var rooms = asAddr.rooms;
                            if (rooms != null)
                            {
                                foreach (var r in rooms)
                                {
                                    var rp = r?.preset?.name;
                                    if (!string.IsNullOrEmpty(rp) && string.Equals(rp, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                    { allow = true; break; }
                                    var rn = r?.name;
                                    if (!allow && !string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    { allow = true; break; }
                                }
                            }
                        }
                        if (allow && MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                        {
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing MURDERER BUILDING ROOFTOP due to allowMurdererHomeRooftop-Mayhem");
                        }
                    }

                    // Allow murderer work rooftop if the rooftop rule is active
                    if (!__result && caseInfo?.HasAllowMurdererWorkRooftopMayhem == true)
                    {
                        var workAddr = __instance.murderer?.job?.employer?.placeOfBusiness?.thisAsAddress;
                        var murdererBuilding = workAddr?.building;
                        var asAddr = newLoc.thisAsAddress;
                        bool allow = false;
                        if (asAddr != null && murdererBuilding != null && asAddr.building == murdererBuilding)
                        {
                            var rooms = asAddr.rooms;
                            if (rooms != null)
                            {
                                foreach (var r in rooms)
                                {
                                    var rp = r?.preset?.name;
                                    if (!string.IsNullOrEmpty(rp) && string.Equals(rp, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                    { allow = true; break; }
                                    var rn = r?.name;
                                    if (!allow && !string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    { allow = true; break; }
                                }
                            }
                        }
                        if (allow && MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                        {
                            __result = true;
                            Plugin.Log?.LogInfo("[Patch] IsValidLocation: Allowing MURDERER BUILDING WORK ROOFTOP due to allowMurdererWorkRooftop-Mayhem");
                        }
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
                    
                    // Dynamic rules: accept any location that matches an active rule and passes usability checks
                    if (!__result)
                    {
                        var rules = MurderPatchHelpers.GetActiveRules(caseInfo);
                        if (rules.Count > 0 && MurderPatchHelpers.LocationMatchesAnyRule(newLoc, rules))
                        {
                            if (MurderPatchHelpers.IsLocationUsable(__instance, newLoc, caseInfo))
                            {
                                var matched = MurderPatchHelpers.GetMatchingRuleForLocation(newLoc, rules)?.Key ?? "dynamic-rule";
                                __result = true;
                                Plugin.Log?.LogInfo($"[Patch] IsValidLocation: Allowing location due to {matched}");
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
        private static readonly System.Random Rng = new System.Random();
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
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
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

            // Occupancy: apply limit universally if present, regardless of location type
            int baseLimit = murder.preset.nonHomeMaximumOccupantsTrigger;
            int? overrideLimit = null;
            if (caseInfo?.OccupancyLimit.HasValue == true)
            {
                overrideLimit = caseInfo.OccupancyLimit.Value;
                Plugin.Log?.LogInfo($"[Patch] IsLocationUsable: Using universal occupancy limit: {overrideLimit.Value}");
            }

            int current = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
            if (overrideLimit.HasValue)
            {
                if (overrideLimit.Value >= 0)
                {
                    if (current > overrideLimit.Value)
                    {
                        Plugin.Log?.LogInfo($"[Patch] IsLocationUsable: Rejected due to occupancy {current} > limit {overrideLimit.Value}");
                        return false;
                    }
                    Plugin.Log?.LogInfo($"[Patch] IsLocationUsable: Occupancy OK {current} <= limit {overrideLimit.Value}");
                }
                else
                {
                    // -1 means infinite
                    Plugin.Log?.LogInfo($"[Patch] IsLocationUsable: Occupancy limit is infinite (-1), bypassing occupancy check. Current={current}");
                }
            }
            else
            {
                // Mirror the base overcrowding check
                if (loc.currentOccupants != null && current > baseLimit)
                {
                    Plugin.Log?.LogInfo($"[Patch] IsLocationUsable: Rejected due to occupancy {current} > base limit {baseLimit}");
                    return false;
                }
            }

            var asAddress = loc.thisAsAddress;
            if (asAddress != null && asAddress.addressPreset != null)
            {
                var name = asAddress.addressPreset.presetName;
                if (name == "Ballroom" || name == "CityHallLobby")
                {
                    if (caseInfo?.HasAllowAnywhereMayhem == true)
                    {
                        Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Allowed banned address preset {name}");
                        return true;
                    }
                    else
                    {
                        Plugin.Log?.LogInfo($"[Patch] IsWorkLocationUsable: Rejected banned address preset {name}");
                        return false;
                    }
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
            public string[] FloorNameContains; // Optional substrings to match on floor name(s) within the address (lowercase)
            public string[] FloorNameExcludes; // Optional substrings to exclude on floor name(s) (lowercase)
            // Future: add fields for street flags, etc.

            // Sub-room level filters (optional)
            public string SubRoomName; // single name contains
            public string SubRoomPreset; // single preset contains
            public string[] SubRoomNames; // list of name contains
            public string[] SubRoomPresets; // list of preset contains
        }

        // Rule: Park/Path
        internal static readonly LocationRule ParkRule = new LocationRule
        {
            Key = "allowPark-Mayhem",
            PresetNames = new[] { "Park", "Path" },
            NameContains = new[] { "park", "path" },
            NameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        internal static readonly LocationRule HotelBathroomRule = new LocationRule
        {
            Key = "allowHotelBathroom-Mayhem",
            PresetNames = new[] { "BuildingBathroomMale", "BuildingBathroomFemale" },
            NameContains = new[] { "public bathrooms", "bathroom" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "hotel_basement" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        internal static readonly LocationRule DinerBathroomRule = new LocationRule
        {
            Key = "allowDinerBathroom-Mayhem",
            PresetNames = new[] { "BuildingBathroomMale", "BuildingBathroomFemale" },
            NameContains = new[] { "public bathrooms", "bathroom" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "dinerfloorbeta" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        internal static readonly LocationRule FathomsYardBasementRule = new LocationRule
        {
            Key = "allowFathomsYardBasement-Mayhem",
            PresetNames = new[] { "FathomsYard" },
            NameContains = new[] { "Fathoms yard", "Fathoms Yard" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "shantytown_basement" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        internal static readonly LocationRule FathomsYardRooftopRule = new LocationRule
        {
            Key = "allowFathomsYardRooftop-Mayhem",
            PresetNames = new[] { "Rooftop" },
            NameContains = new[] { "rooftop", "Rooftop" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "shantytown" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        internal static readonly LocationRule HotelRooftopBarRule = new LocationRule
        {
            Key = "allowHotelRooftopBar-Mayhem",
            PresetNames = new[] { "RooftopBar" },
            NameContains = new[] { "rooftop bar", "Rooftop Bar" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "hotel_rooftopbar" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        
        internal static readonly LocationRule HotelRooftopRule = new LocationRule
        {
            Key = "allowHotelRooftop-Mayhem",
            PresetNames = new[] { "PowerRoom" },
            NameContains = new[] { "Power room", "Power Room" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "hotel_rooftopbar" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = new[] { "Rooftop Rooftop", "Rooftop" },
            SubRoomPresets = new[] { "Rooftop" }
        };

        internal static readonly LocationRule MixedIndustrialRooftopRule = new LocationRule
        {
            Key = "allowMixedIndustrialRooftop-Mayhem",
            PresetNames = new[] { "Rooftop" },
            NameContains = new[] { "rooftop", "Rooftop" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "mixedindustrial" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        internal static readonly LocationRule TestRule = new LocationRule
        {
            Key = "allowTest-Mayhem",
            PresetNames = new[] { "AmericanDiner" },
            NameContains = new[] { "Diner", "diner" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "dinerfloorbeta" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = new[] { "Backroom", "backroom" },
            SubRoomPresets = new[] { "BusinessBackroom" }
        };

        internal static readonly LocationRule FathomsPowerRoomRule = new LocationRule
        {
            Key = "allowFathomsPowerRoom-Mayhem",
            PresetNames = new[] { "PowerRoom" },
            NameContains = new[] { "Power Room", "Power room" },
            NameExcludes = Array.Empty<string>(),
            FloorNameContains = new[] { "shantytown_basement01" },
            FloorNameExcludes = Array.Empty<string>(),
            SubRoomNames = Array.Empty<string>(),
            SubRoomPresets = Array.Empty<string>()
        };

        // Helper: does any floor name on this location's address match the rule's floor constraints
        internal static bool LocationFloorsMatch(NewGameLocation loc, LocationRule rule)
        {
            if (rule == null)
                return false;

            // No floor constraints specified => treat as match
            if (rule.FloorNameContains == null || rule.FloorNameContains.Length == 0)
                return true;

            var addr = loc?.thisAsAddress;
            var rooms = addr?.rooms;
            if (rooms == null || rooms.Count == 0)
                return false;

            foreach (var room in rooms)
            {
                var floor = room?.floor;
                if (floor == null)
                    continue;

                // Prefer NewFloor.floorName (e.g., "Eden_Rooftop"), fallback to transform name (e.g., "Eden_Rooftop (Floor 19)")
                string floorName = floor.floorName;
                if (string.IsNullOrEmpty(floorName))
                    floorName = floor.transform != null ? floor.transform.name : null;
                if (string.IsNullOrEmpty(floorName))
                    continue;

                string floorLower = floorName.ToLowerInvariant();

                // Exclusions: if any exclude term is contained, skip this floor name
                if (rule.FloorNameExcludes != null && rule.FloorNameExcludes.Any(ex => !string.IsNullOrEmpty(ex) && floorLower.Contains(ex)))
                    continue;

                // Inclusions: if any include term is contained, we match
                foreach (var part in rule.FloorNameContains)
                {
                    if (!string.IsNullOrEmpty(part) && floorLower.Contains(part))
                        return true;
                }
            }

            return false;
        }

        // Does a location match a rule by preset or name (with optional floor constraints)
        internal static bool LocationMatchesRule(NewGameLocation loc, LocationRule rule)
        {
            if (loc == null || rule == null) return false;

            var addrPreset = loc.thisAsAddress?.addressPreset?.presetName;
            if (!string.IsNullOrEmpty(addrPreset) && rule.PresetNames != null)
            {
                foreach (var p in rule.PresetNames)
                {
                    if (string.Equals(addrPreset, p, StringComparison.OrdinalIgnoreCase))
                    {
                        // If floor constraints are provided, enforce them
                        if (rule.FloorNameContains != null && rule.FloorNameContains.Length > 0)
                        {
                            if (!LocationFloorsMatch(loc, rule))
                                continue;
                        }

                        return true;
                    }
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
                    {
                        // If floor constraints are provided, enforce them
                        return LocationFloorsMatch(loc, rule);
                    }
                }
            }

            // Sub-room preset/name matching: if specified, require at least one room match
            bool wantsSubRooms = (rule.SubRoomPresets != null && rule.SubRoomPresets.Length > 0) || (rule.SubRoomNames != null && rule.SubRoomNames.Length > 0);
            if (wantsSubRooms)
            {
                var addr = loc.thisAsAddress;
                var rooms = addr?.rooms;
                if (rooms == null || rooms.Count == 0) return false;

                foreach (var room in rooms)
                {
                    // Check floor constraint per room if provided
                    if (rule.FloorNameContains != null && rule.FloorNameContains.Length > 0)
                    {
                        var f = room?.floor;
                        string fname = f?.floorName;
                        if (string.IsNullOrEmpty(fname) && f?.transform != null) fname = f.transform.name;
                        if (string.IsNullOrEmpty(fname)) continue;
                        var fl = fname.ToLowerInvariant();
                        if (rule.FloorNameExcludes != null && rule.FloorNameExcludes.Any(ex => !string.IsNullOrEmpty(ex) && fl.Contains(ex)))
                            continue;
                        if (!rule.FloorNameContains.Any(inc => !string.IsNullOrEmpty(inc) && fl.Contains(inc)))
                            continue;
                    }

                    // Room preset name (use NewRoom.preset.name)
                    var roomPreset = room?.preset?.name;
                    if (!string.IsNullOrEmpty(roomPreset) && rule.SubRoomPresets != null && rule.SubRoomPresets.Any(rp => string.Equals(rp, roomPreset, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Optional: also enforce SubRoomNames if provided
                        if (rule.SubRoomNames != null && rule.SubRoomNames.Length > 0)
                        {
                            var rn = room?.name ?? string.Empty;
                            if (!rule.SubRoomNames.Any(n => !string.IsNullOrEmpty(n) && rn.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                                continue;
                        }
                        return true;
                    }

                    // If no preset constraint, allow name-only matching
                    if ((rule.SubRoomPresets == null || rule.SubRoomPresets.Length == 0) && rule.SubRoomNames != null && rule.SubRoomNames.Length > 0)
                    {
                        var rn = room?.name ?? string.Empty;
                        if (rule.SubRoomNames.Any(n => !string.IsNullOrEmpty(n) && rn.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                            return true;
                    }
                }
                return false;
            }

            return false;
        }

        // Return active rules for a given custom case info
        internal static List<LocationRule> GetActiveRules(CustomCaseInfo caseInfo)
        {
            var rules = new List<LocationRule>();
            if (caseInfo == null) return rules;
            if (caseInfo.HasAllowParkMayhem) rules.Add(ParkRule);
            if (caseInfo.HasAllowHotelBathroomMayhem) rules.Add(HotelBathroomRule);
            if (caseInfo.HasAllowDinerBathroomMayhem) rules.Add(DinerBathroomRule);
            if (caseInfo.HasAllowFathomsYardBasementMayhem) rules.Add(FathomsYardBasementRule);
            if (caseInfo.HasAllowFathomsYardRooftopMayhem) rules.Add(FathomsYardRooftopRule);
            if (caseInfo.HasAllowHotelRooftopBarMayhem) rules.Add(HotelRooftopBarRule);
            if (caseInfo.HasAllowHotelRooftopMayhem) rules.Add(HotelRooftopRule);
            if (caseInfo.HasAllowMixedIndustrialRooftopMayhem) rules.Add(MixedIndustrialRooftopRule);
            if (caseInfo.HasAllowTestMayhem) rules.Add(TestRule);
            if (caseInfo.HasAllowFathomsPowerRoomMayhem) rules.Add(FathomsPowerRoomRule);
            return rules;
        }

        // Whether a location matches any of the provided rules
        internal static bool LocationMatchesAnyRule(NewGameLocation loc, IEnumerable<LocationRule> rules)
        {
            if (loc == null || rules == null) return false;
            foreach (var r in rules)
            {
                if (LocationMatchesRule(loc, r)) return true;
            }
            return false;
        }

        // Try to identify which rule matched a given location
        internal static LocationRule GetMatchingRuleForLocation(NewGameLocation loc, IEnumerable<LocationRule> rules)
        {
            if (loc == null || rules == null) return null;
            foreach (var r in rules)
            {
                if (LocationMatchesRule(loc, r)) return r;
            }
            return null;
        }

        // --- Victim home rooftop helpers ---
        internal static NewGameLocation TryFindVictimHomeRooftop(MurderController.Murder murder, CustomCaseInfo caseInfo, out NewGameLocation victimHome)
        {
            victimHome = null;
            try
            {
                var homeAddr = murder?.victim?.home; // NewAddress in most cases
                if (homeAddr == null)
                    return null;
                victimHome = homeAddr as NewGameLocation;
                // Derive building name as prefix for rooftop sub-room matching, and prefer same-building locations
                var building = homeAddr.building;
                string buildingName = building?.name;
                if (string.IsNullOrEmpty(buildingName)) buildingName = building?.preset?.name;
                buildingName = (buildingName ?? string.Empty).Trim();
                var buildingTokens = new List<string>();
                if (!string.IsNullOrEmpty(buildingName))
                {
                    foreach (var t in buildingName.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var tl = t.Trim();
                        if (tl.Length >= 2) buildingTokens.Add(tl);
                    }
                }

                // First, try a strict same-building scan with rooftop rooms
                try
                {
                    NewGameLocation bestSameBuilding = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        if (addr.building != building) continue; // enforce same building

                        var rooms = addr.rooms;
                        bool hasRooftop = false;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                // preset match
                                var rp = r?.preset?.name;
                                if (!string.IsNullOrEmpty(rp) && string.Equals(rp, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasRooftop = true;
                                    break;
                                }
                                // name-based fallback (some maps)
                                var rn = r?.name;
                                if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    // optionally require building token containment if we have any
                                    if (buildingTokens.Count == 0)
                                    {
                                        hasRooftop = true; break;
                                    }
                                    foreach (var bt in buildingTokens)
                                    {
                                        if (!string.IsNullOrEmpty(bt) && rn.IndexOf(bt, StringComparison.OrdinalIgnoreCase) >= 0)
                                        { hasRooftop = true; break; }
                                    }
                                    if (hasRooftop) break;
                                }
                            }
                        }
                        if (!hasRooftop) continue;

                        considered++;
                        if (!IsLocationUsable(murder, loc, caseInfo))
                        {
                            Plugin.Log?.LogInfo($"[Patch] Rooftop same-building rejected (not usable): '{loc.name}' building='{buildingName}'");
                            continue;
                        }
                        usable++;
                        int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                        if (bestSameBuilding == null || occ < bestOcc)
                        {
                            bestSameBuilding = loc;
                            bestOcc = occ;
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Rooftop same-building scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{bestSameBuilding?.name ?? "null"}'");
                    if (bestSameBuilding != null)
                        return bestSameBuilding;
                }
                catch (Exception exSB)
                {
                    Plugin.Log?.LogWarning($"[Patch] Rooftop same-building scan error: {exSB.Message}");
                }

                // Build a dynamic rule: target rooftop sub-rooms by building name prefix (no generic name/floor filters)
                var victimHomeRooftopRule = new LocationRule
                {
                    Key = "allowVictimHomeRooftop-Mayhem",
                    PresetNames = null,
                    NameContains = Array.Empty<string>(),
                    NameExcludes = Array.Empty<string>(),
                    FloorNameContains = Array.Empty<string>(),
                    FloorNameExcludes = Array.Empty<string>(),
                    // Require Rooftop preset and also sub-room name containing building tokens
                    SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                    SubRoomPresets = new[] { "Rooftop" }
                };

                Plugin.Log?.LogInfo($"[Patch] TryFindVictimHomeRooftop: Using building '{buildingName}' tokens [{string.Join(",", buildingTokens)}]");

                var selected = FindBestLocationByRule(murder, caseInfo, victimHomeRooftopRule);
                if (selected != null)
                    return selected;

                // Fallback: loosen sub-room name to require any rooftop name within the same building via rule
                if (!string.IsNullOrEmpty(buildingName))
                {
                    var fallbackRule = new LocationRule
                    {
                        Key = "allowVictimHomeRooftop-Mayhem",
                        PresetNames = null,
                        NameContains = Array.Empty<string>(),
                        NameExcludes = Array.Empty<string>(),
                        FloorNameContains = Array.Empty<string>(),
                        FloorNameExcludes = Array.Empty<string>(),
                        // Allow name-based rooftop rooms, still requiring building token(s)
                        SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                        SubRoomPresets = new[] { "Rooftop" }
                    };
                    Plugin.Log?.LogInfo($"[Patch] TryFindVictimHomeRooftop: Primary failed; trying building token rule for rooftop");
                    selected = FindBestLocationByRule(murder, caseInfo, fallbackRule);
                }

                if (selected != null)
                    return selected;

                // Final fallback: global scan, but still prefer building match and rooftop rooms
                try
                {
                    NewGameLocation best = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        var rooms = addr.rooms;
                        bool hasRooftopRoom = false;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var roomPreset = r?.preset?.name;
                                if (!string.IsNullOrEmpty(roomPreset) && string.Equals(roomPreset, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasRooftopRoom = true;
                                }
                                // name contains rooftop and building token
                                if (!hasRooftopRoom)
                                {
                                    var rn = r?.name;
                                    if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        if (addr.building == building)
                                        {
                                            hasRooftopRoom = true;
                                        }
                                        else if (buildingTokens.Count > 0)
                                        {
                                            foreach (var bt in buildingTokens)
                                            {
                                                if (!string.IsNullOrEmpty(bt) && rn.IndexOf(bt, StringComparison.OrdinalIgnoreCase) >= 0)
                                                { hasRooftopRoom = true; break; }
                                            }
                                        }
                                    }
                                }
                                if (hasRooftopRoom)
                                {
                                    considered++;
                                    bool ok = IsLocationUsable(murder, loc, caseInfo);
                                    if (!ok)
                                    {
                                        Plugin.Log?.LogInfo($"[Patch] Rooftop global-scan rejected (not usable): '{loc.name}' building='{buildingName}'");
                                        hasRooftopRoom = false;
                                        break;
                                    }
                                    usable++;
                                    int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                                    if (best == null || occ < bestOcc)
                                    {
                                        best = loc;
                                        bestOcc = occ;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Rooftop global-scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{best?.name ?? "null"}'");
                    return best;
                }
                catch (Exception ex2)
                {
                    Plugin.Log?.LogWarning($"[Patch] Rooftop global-scan fallback error: {ex2.Message}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Patch] TryFindVictimHomeRooftop error: {ex.Message}");
                return null;
            }
        }

        // --- Victim work rooftop helpers ---
        internal static NewGameLocation TryFindVictimWorkRooftop(MurderController.Murder murder, CustomCaseInfo caseInfo, out NewGameLocation victimWorkplace)
        {
            victimWorkplace = null;
            try
            {
                var workplace = murder?.victim?.job?.employer?.placeOfBusiness;
                if (workplace == null)
                    return null;
                victimWorkplace = workplace;
                // Derive building name as prefix for rooftop sub-room matching, and prefer same-building locations
                var building = workplace.thisAsAddress?.building;
                string buildingName = building?.name;
                if (string.IsNullOrEmpty(buildingName)) buildingName = building?.preset?.name;
                buildingName = (buildingName ?? string.Empty).Trim();
                var buildingTokens = new List<string>();
                if (!string.IsNullOrEmpty(buildingName))
                {
                    foreach (var t in buildingName.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var tl = t.Trim();
                        if (tl.Length >= 2) buildingTokens.Add(tl);
                    }
                }
                // Same building scan
                try
                {
                    NewGameLocation bestSameBuilding = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        if (addr.building != building) continue;
                        var rooms = addr.rooms;
                        bool hasRooftop = false;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var rp = r?.preset?.name;
                                if (!string.IsNullOrEmpty(rp) && string.Equals(rp, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasRooftop = true;
                                    break;
                                }
                                var rn = r?.name;
                                if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    if (buildingTokens.Count == 0)
                                    {
                                        hasRooftop = true; break;
                                    }
                                    foreach (var bt in buildingTokens)
                                    {
                                        if (!string.IsNullOrEmpty(bt) && rn.IndexOf(bt, StringComparison.OrdinalIgnoreCase) >= 0)
                                        { hasRooftop = true; break; }
                                    }
                                    if (hasRooftop) break;
                                }
                            }
                        }
                        if (!hasRooftop) continue;
                        considered++;
                        if (!IsLocationUsable(murder, loc, caseInfo))
                        {
                            Plugin.Log?.LogInfo($"[Patch] Work Rooftop same-building rejected (not usable): '{loc.name}' building='{buildingName}'");
                            continue;
                        }
                        usable++;
                        int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                        if (bestSameBuilding == null || occ < bestOcc)
                        {
                            bestSameBuilding = loc;
                            bestOcc = occ;
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Work Rooftop same-building scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{bestSameBuilding?.name ?? "null"}'");
                    if (bestSameBuilding != null)
                        return bestSameBuilding;
                }
                catch (Exception exSB)
                {
                    Plugin.Log?.LogWarning($"[Patch] Work Rooftop same-building scan error: {exSB.Message}");
                }
                // Dynamic rule
                var victimWorkRooftopRule = new LocationRule
                {
                    Key = "allowVictimWorkRooftop-Mayhem",
                    PresetNames = null,
                    NameContains = Array.Empty<string>(),
                    NameExcludes = Array.Empty<string>(),
                    FloorNameContains = Array.Empty<string>(),
                    FloorNameExcludes = Array.Empty<string>(),
                    SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                    SubRoomPresets = new[] { "Rooftop" }
                };
                Plugin.Log?.LogInfo($"[Patch] TryFindVictimWorkRooftop: Using building '{buildingName}' tokens [{string.Join(",", buildingTokens)}]");
                var selected = FindBestLocationByRule(murder, caseInfo, victimWorkRooftopRule);
                if (selected != null)
                    return selected;
                // Fallback
                if (!string.IsNullOrEmpty(buildingName))
                {
                    var fallbackRule = new LocationRule
                    {
                        Key = "allowVictimWorkRooftop-Mayhem",
                        PresetNames = null,
                        NameContains = Array.Empty<string>(),
                        NameExcludes = Array.Empty<string>(),
                        FloorNameContains = Array.Empty<string>(),
                        FloorNameExcludes = Array.Empty<string>(),
                        SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                        SubRoomPresets = new[] { "Rooftop" }
                    };
                    Plugin.Log?.LogInfo($"[Patch] TryFindVictimWorkRooftop: Primary failed; trying building token rule for rooftop");
                    selected = FindBestLocationByRule(murder, caseInfo, fallbackRule);
                }
                if (selected != null)
                    return selected;
                // Global scan
                try
                {
                    NewGameLocation best = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        var rooms = addr.rooms;
                        bool hasRooftopRoom = false;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var roomPreset = r?.preset?.name;
                                if (!string.IsNullOrEmpty(roomPreset) && string.Equals(roomPreset, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasRooftopRoom = true;
                                }
                                if (!hasRooftopRoom)
                                {
                                    var rn = r?.name;
                                    if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        if (addr.building == building)
                                        {
                                            hasRooftopRoom = true;
                                        }
                                        else if (buildingTokens.Count > 0)
                                        {
                                            foreach (var bt in buildingTokens)
                                            {
                                                if (!string.IsNullOrEmpty(bt) && rn.IndexOf(bt, StringComparison.OrdinalIgnoreCase) >= 0)
                                                { hasRooftopRoom = true; break; }
                                            }
                                        }
                                    }
                                }
                                if (hasRooftopRoom)
                                {
                                    considered++;
                                    bool ok = IsLocationUsable(murder, loc, caseInfo);
                                    if (!ok)
                                    {
                                        Plugin.Log?.LogInfo($"[Patch] Work Rooftop global-scan rejected (not usable): '{loc.name}' building='{buildingName}'");
                                        hasRooftopRoom = false;
                                        break;
                                    }
                                    usable++;
                                    int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                                    if (best == null || occ < bestOcc)
                                    {
                                        best = loc;
                                        bestOcc = occ;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Work Rooftop global-scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{best?.name ?? "null"}'");
                    return best;
                }
                catch (Exception ex2)
                {
                    Plugin.Log?.LogWarning($"[Patch] Work Rooftop global-scan fallback error: {ex2.Message}");
                }
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Patch] TryFindVictimWorkRooftop error: {ex.Message}");
                return null;
            }
        }

        // Try to pick an anchor node from a suitable (sub-)room inside an address location using rule-driven filters
        internal static NewNode TryFindAnchorNodeFromRooms(NewGameLocation loc, LocationRule rule)
        {
            var addr = loc?.thisAsAddress;
            var rooms = addr?.rooms;
            if (rooms == null || rooms.Count == 0)
                return null;

            // Helper to choose a representative anchor node for a room
            // Preference: a furniture anchor node if available, otherwise any room node
            NewNode RoomAnchor(NewRoom room)
            {
                if (room == null) return null;
                try
                {
                    if (room.individualFurniture != null)
                    {
                        foreach (var fl in room.individualFurniture)
                        {
                            if (fl?.anchorNode != null)
                                return fl.anchorNode;
                        }
                    }
                }
                catch { }
                if (room.nodes != null && room.nodes.Count > 0)
                {
                    // Use the first node available as a simple anchor
                    foreach (var n in room.nodes)
                    {
                        return n;
                    }
                }
                return null;
            }

            // Local helpers without LINQ
            bool AnySpecified(IEnumerable<string> arr)
            {
                if (arr == null) return false;
                foreach (var s in arr) { if (!string.IsNullOrEmpty(s)) return true; }
                return false;
            }
            bool ContainsCI(string src, string needle)
            {
                return !string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(needle) && src.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            bool ListContainsCI(string src, IEnumerable<string> needles)
            {
                if (string.IsNullOrEmpty(src) || needles == null) return false;
                foreach (var n in needles)
                {
                    if (!string.IsNullOrEmpty(n) && src.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                return false;
            }

            // Step 1: filter candidate MAIN rooms
            // Build initial candidate list (non-null rooms)
            var mainCandidates = new List<NewRoom>();
            foreach (var r in rooms) if (r != null) mainCandidates.Add(r);

            // Floor filtering (optional)
            if (rule != null)
            {
                if (AnySpecified(rule?.FloorNameContains))
                {
                    var filtered = new List<NewRoom>();
                    foreach (var r in mainCandidates)
                    {
                        var f = r?.floor;
                        string fname = f?.floorName;
                        if (string.IsNullOrEmpty(fname) && f?.transform != null) fname = f.transform.name;
                        if (!string.IsNullOrEmpty(fname) && ListContainsCI(fname, rule.FloorNameContains))
                            filtered.Add(r);
                    }
                    mainCandidates = filtered;
                }
                if (AnySpecified(rule?.FloorNameExcludes))
                {
                    var filtered = new List<NewRoom>();
                    foreach (var r in mainCandidates)
                    {
                        var fn = r?.floor?.name ?? string.Empty;
                        if (!ListContainsCI(fn, rule.FloorNameExcludes))
                            filtered.Add(r);
                    }
                    mainCandidates = filtered;
                }
            }

            // Use existing rule fields for MAIN room selection: PresetNames (room preset), NameContains/Excludes (room name)
            bool hasNameCrit = AnySpecified(rule?.NameContains) || AnySpecified(rule?.NameExcludes);
            bool hasPresetCrit = AnySpecified(rule?.PresetNames);
            if (rule != null && (hasNameCrit || hasPresetCrit))
            {
                var filtered = new List<NewRoom>();
                foreach (var r in mainCandidates)
                {
                    string rName = r?.name ?? string.Empty;
                    string rPreset = r?.preset?.name ?? string.Empty;

                    bool nameOk = true;
                    if (AnySpecified(rule.NameContains))
                        nameOk = ListContainsCI(rName, rule.NameContains);
                    if (nameOk && AnySpecified(rule.NameExcludes))
                        nameOk = !ListContainsCI(rName, rule.NameExcludes);

                    bool presetOk = true;
                    if (AnySpecified(rule.PresetNames))
                        presetOk = ListContainsCI(rPreset, rule.PresetNames);

                    if (nameOk && presetOk)
                        filtered.Add(r);
                }
                mainCandidates = filtered;
            }

            // Prefer lowest-occupancy or anchored rooms; pick best main room
            NewRoom bestMain = null;
            int bestScore = int.MinValue;
            foreach (var r in mainCandidates)
            {
                int s = 0;
                var rAnchor = RoomAnchor(r);
                if (rAnchor != null) s += 3;
                // proximity bonus to location anchor if available
                var locAnchor = loc?.anchorNode;
                if (locAnchor != null && rAnchor != null)
                {
                    float d = Vector3.Distance(locAnchor.position, rAnchor.position);
                    s += Mathf.Clamp(10 - Mathf.RoundToInt(d), -5, 5);
                }
                // occupation heuristic
                int occ = r?.gameLocation?.currentOccupants?.Count ?? 0;
                s += (occ == 0 ? 2 : 0);
                if (s > bestScore) { bestScore = s; bestMain = r; }
            }

            if (bestMain == null)
            {
                // fall back to simple heuristic (previous behavior)
                foreach (var r in rooms)
                {
                    var anchor = RoomAnchor(r);
                    if (anchor != null)
                        return anchor;
                }
                return null;
            }

            // Step 2: if sub-room filters are supplied, try to find a sub-room relative to the main room
            bool subCriteriaSupplied =
                !string.IsNullOrEmpty(rule?.SubRoomName) ||
                !string.IsNullOrEmpty(rule?.SubRoomPreset) ||
                AnySpecified(rule?.SubRoomNames) ||
                AnySpecified(rule?.SubRoomPresets);

            if (subCriteriaSupplied)
            {
                // Build a prefix from company name or the main room name's prefix
                string locationPrefix = string.Empty;
                var companyName = bestMain?.gameLocation?.thisAsAddress?.company?.name;
                if (!string.IsNullOrEmpty(companyName)) locationPrefix = companyName;
                else
                {
                    var rn = bestMain?.name ?? string.Empty;
                    int lastSpace = rn.LastIndexOf(' ');
                    if (lastSpace > 0) locationPrefix = rn.Substring(0, lastSpace);
                }

                var subCandidates = new List<NewRoom>();
                foreach (var r in rooms)
                {
                    if (r != null && !string.IsNullOrEmpty(r.name)) subCandidates.Add(r);
                }

                // Apply sub-room filters
                {
                    var filtered = new List<NewRoom>();
                    foreach (var r in subCandidates)
                    {
                        string rName = r.name;
                        string rPreset = r?.preset?.name ?? string.Empty;

                        bool nameMatch = false;
                        if (!string.IsNullOrEmpty(rule.SubRoomName) && ContainsCI(rName, rule.SubRoomName)) nameMatch = true;
                        if (!nameMatch && AnySpecified(rule.SubRoomNames) && ListContainsCI(rName, rule.SubRoomNames)) nameMatch = true;
                        // If prefix exists, allow combined prefix+name contains as a helper
                        if (!nameMatch && !string.IsNullOrEmpty(locationPrefix) && !string.IsNullOrEmpty(rule.SubRoomName) && ContainsCI(rName, locationPrefix) && ContainsCI(rName, rule.SubRoomName)) nameMatch = true;

                        bool presetMatch = true;
                        if (!string.IsNullOrEmpty(rule.SubRoomPreset)) presetMatch = ContainsCI(rPreset, rule.SubRoomPreset);
                        if (presetMatch && AnySpecified(rule.SubRoomPresets)) presetMatch = ListContainsCI(rPreset, rule.SubRoomPresets);

                        if (nameMatch && presetMatch)
                            filtered.Add(r);
                    }
                    subCandidates = filtered;
                }

                // Choose the best sub-room similar heuristics
                NewRoom bestSub = null;
                bestScore = int.MinValue;
                foreach (var r in subCandidates)
                {
                    int s = 0;
                    var rAnchor = RoomAnchor(r);
                    if (rAnchor != null) s += 3;
                    var bestMainAnchor = RoomAnchor(bestMain);
                    if (bestMainAnchor != null && rAnchor != null)
                    {
                        float d = Vector3.Distance(bestMainAnchor.position, rAnchor.position);
                        s += Mathf.Clamp(10 - Mathf.RoundToInt(d), -5, 5);
                    }
                    int occ = r?.gameLocation?.currentOccupants?.Count ?? 0;
                    s += (occ == 0 ? 2 : 0);
                    if (s > bestScore) { bestScore = s; bestSub = r; }
                }

                var bestSubAnchor = RoomAnchor(bestSub);
                if (bestSubAnchor != null)
                    return bestSubAnchor;

                // Try furniture anchors in best sub-room
                if (bestSub?.individualFurniture != null)
                {
                    foreach (var fl in bestSub.individualFurniture)
                    {
                        if (fl?.anchorNode != null)
                            return fl.anchorNode;
                    }
                }
            }

            // No sub-room specified or found; use main room anchor or furniture
            var mainAnchor = RoomAnchor(bestMain);
            if (mainAnchor != null)
                return mainAnchor;

            if (bestMain.individualFurniture != null)
            {
                foreach (var fl in bestMain.individualFurniture)
                {
                    if (fl?.anchorNode != null)
                        return fl.anchorNode;
                }
            }

            // Final fallback: any room with an anchor
            foreach (var r in rooms)
            {
                var anchor = RoomAnchor(r);
                if (anchor != null)
                    return anchor;
            }

            return null;
        }

        // Find a best-matching location by iterating active rules in random order
        internal static NewGameLocation FindBestLocationByRulesRandom(MurderController.Murder murder, CustomCaseInfo caseInfo, IEnumerable<LocationRule> rules)
        {
            if (murder == null || rules == null) return null;
            var ruleList = new List<LocationRule>();
            foreach (var r in rules) { if (r != null) ruleList.Add(r); }
            if (ruleList.Count == 0) return null;

            // Shuffle rules using a local RNG to add variety across murders
            // Simple Fisher-Yates shuffle
            var shuffled = new List<LocationRule>(ruleList);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(Rng.NextDouble() * (i + 1));
                var tmp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = tmp;
            }
            foreach (var r in shuffled)
            {
                var loc = FindBestLocationByRule(murder, caseInfo, r);
                if (loc != null) return loc;
            }
            return null;
        }

        // Find the best-matching location for a rule (prioritize preset matches, then name matches; choose lowest occupancy)
        internal static NewGameLocation FindBestLocationByRule(MurderController.Murder murder, CustomCaseInfo caseInfo, LocationRule rule)
        {
            if (murder == null || rule == null) return null;

            NewGameLocation chosen = null;
            int chosenOcc = int.MaxValue;

            // Phase 1: preset matches (with optional floor constraint)
            foreach (var loc in CityData.Instance.gameLocationDirectory)
            {
                if (loc == null) continue;

                // Match only by preset
                var addrPreset = loc.thisAsAddress?.addressPreset?.presetName;
                bool presetMatch = !string.IsNullOrEmpty(addrPreset) && rule.PresetNames != null &&
                                   rule.PresetNames.Any(p => string.Equals(addrPreset, p, StringComparison.OrdinalIgnoreCase));
                if (!presetMatch)
                    continue;

                // Enforce floor constraint if specified
                if (!LocationFloorsMatch(loc, rule))
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

        // --- Murderer home rooftop helpers ---
        internal static NewGameLocation TryFindMurdererHomeRooftop(MurderController.Murder murder, CustomCaseInfo caseInfo, out NewGameLocation murdererHome)
        {
            murdererHome = null;
            try
            {
                var homeAddr = murder?.murderer?.home; // NewAddress in most cases
                if (homeAddr == null)
                    return null;
                murdererHome = homeAddr as NewGameLocation;
                // Derive building name as prefix for rooftop sub-room matching, and prefer same-building locations
                var building = homeAddr.building;
                string buildingName = building?.name;
                var buildingTokens = new List<string>();
                if (!string.IsNullOrEmpty(buildingName))
                {
                    foreach (var t in buildingName.Split(' '))
                    {
                        if (!string.IsNullOrEmpty(t)) buildingTokens.Add(t);
                    }
                }
                // Prefer same-building rooftop locations
                try
                {
                    NewGameLocation bestSameBuilding = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        if (addr.building != building) continue;
                        // Rooftop sub-room check
                        bool hasRooftop = false;
                        var rooms = addr.rooms;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var roomPreset = r?.preset?.name;
                                if (!string.IsNullOrEmpty(roomPreset) && string.Equals(roomPreset, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                { hasRooftop = true; }
                                if (!hasRooftop)
                                {
                                    var rn = r?.name;
                                    if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    { hasRooftop = true; }
                                }
                                if (hasRooftop) break;
                            }
                        }
                        if (!hasRooftop) continue;
                        considered++;
                        if (!IsLocationUsable(murder, loc, caseInfo))
                        {
                            Plugin.Log?.LogInfo($"[Patch] Murderer Home Rooftop same-building rejected (not usable): '{loc.name}' building='{buildingName}'");
                            continue;
                        }
                        usable++;
                        int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                        if (bestSameBuilding == null || occ < bestOcc)
                        {
                            bestSameBuilding = loc;
                            bestOcc = occ;
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Murderer Home Rooftop same-building scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{bestSameBuilding?.name ?? "null"}'");
                    if (bestSameBuilding != null)
                        return bestSameBuilding;
                }
                catch (Exception exSB)
                {
                    Plugin.Log?.LogWarning($"[Patch] Murderer Home Rooftop same-building scan error: {exSB.Message}");
                }

                // Dynamic rule fallback using building tokens
                var rule = new LocationRule
                {
                    Key = "allowMurdererHomeRooftop-Mayhem",
                    PresetNames = null,
                    NameContains = Array.Empty<string>(),
                    NameExcludes = Array.Empty<string>(),
                    FloorNameContains = Array.Empty<string>(),
                    FloorNameExcludes = Array.Empty<string>(),
                    SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                    SubRoomPresets = new[] { "Rooftop" }
                };
                var selected = FindBestLocationByRule(murder, caseInfo, rule);
                if (selected != null) return selected;

                // Global rooftop scan (same heuristics)
                try
                {
                    NewGameLocation best = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        var rooms = addr.rooms;
                        bool hasRooftopRoom = false;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var roomPreset = r?.preset?.name;
                                if (!string.IsNullOrEmpty(roomPreset) && string.Equals(roomPreset, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                { hasRooftopRoom = true; }
                                if (!hasRooftopRoom)
                                {
                                    var rn = r?.name;
                                    if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    { hasRooftopRoom = true; }
                                }
                                if (hasRooftopRoom)
                                {
                                    considered++;
                                    bool ok = IsLocationUsable(murder, loc, caseInfo);
                                    if (!ok)
                                    {
                                        Plugin.Log?.LogInfo($"[Patch] Murderer Home Rooftop global-scan rejected (not usable): '{loc.name}'");
                                        hasRooftopRoom = false;
                                        break;
                                    }
                                    usable++;
                                    int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                                    if (best == null || occ < bestOcc)
                                    {
                                        best = loc;
                                        bestOcc = occ;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Murderer Home Rooftop global-scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{best?.name ?? "null"}'");
                    return best;
                }
                catch (Exception ex2)
                {
                    Plugin.Log?.LogWarning($"[Patch] Murderer Home Rooftop global-scan fallback error: {ex2.Message}");
                }
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Patch] TryFindMurdererHomeRooftop error: {ex.Message}");
                return null;
            }
        }

        // --- Murderer work rooftop helpers ---
        internal static NewGameLocation TryFindMurdererWorkRooftop(MurderController.Murder murder, CustomCaseInfo caseInfo, out NewGameLocation murdererWorkplace)
        {
            murdererWorkplace = null;
            try
            {
                var workplace = murder?.murderer?.job?.employer?.placeOfBusiness;
                if (workplace == null)
                    return null;
                murdererWorkplace = workplace;
                var building = workplace.thisAsAddress?.building;
                string buildingName = building?.name;
                var buildingTokens = new List<string>();
                if (!string.IsNullOrEmpty(buildingName))
                {
                    foreach (var t in buildingName.Split(' '))
                    {
                        if (!string.IsNullOrEmpty(t)) buildingTokens.Add(t);
                    }
                }
                // Prefer same-building rooftop
                try
                {
                    NewGameLocation bestSameBuilding = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        if (addr.building != building) continue;
                        bool hasRooftop = false;
                        var rooms = addr.rooms;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var roomPreset = r?.preset?.name;
                                if (!string.IsNullOrEmpty(roomPreset) && string.Equals(roomPreset, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                { hasRooftop = true; }
                                if (!hasRooftop)
                                {
                                    var rn = r?.name;
                                    if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        if (addr.building == building) { hasRooftop = true; }
                                        else if (buildingTokens.Count > 0)
                                        {
                                            foreach (var bt in buildingTokens)
                                            {
                                                if (!string.IsNullOrEmpty(bt) && rn.IndexOf(bt, StringComparison.OrdinalIgnoreCase) >= 0)
                                                { hasRooftop = true; break; }
                                            }
                                        }
                                    }
                                }
                                if (hasRooftop) break;
                            }
                        }
                        if (!hasRooftop) continue;
                        considered++;
                        if (!IsLocationUsable(murder, loc, caseInfo))
                        {
                            Plugin.Log?.LogInfo($"[Patch] Murderer Work Rooftop same-building rejected (not usable): '{loc.name}' building='{buildingName}'");
                            continue;
                        }
                        usable++;
                        int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                        if (bestSameBuilding == null || occ < bestOcc)
                        {
                            bestSameBuilding = loc;
                            bestOcc = occ;
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Murderer Work Rooftop same-building scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{bestSameBuilding?.name ?? "null"}'");
                    if (bestSameBuilding != null)
                        return bestSameBuilding;
                }
                catch (Exception exSB)
                {
                    Plugin.Log?.LogWarning($"[Patch] Murderer Work Rooftop same-building scan error: {exSB.Message}");
                }
                // Dynamic rule using building tokens
                var murdererWorkRooftopRule = new LocationRule
                {
                    Key = "allowMurdererWorkRooftop-Mayhem",
                    PresetNames = null,
                    NameContains = Array.Empty<string>(),
                    NameExcludes = Array.Empty<string>(),
                    FloorNameContains = Array.Empty<string>(),
                    FloorNameExcludes = Array.Empty<string>(),
                    SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                    SubRoomPresets = new[] { "Rooftop" }
                };
                var selected = FindBestLocationByRule(murder, caseInfo, murdererWorkRooftopRule);
                if (selected != null) return selected;
                // Fallback additional pass
                if (!string.IsNullOrEmpty(buildingName))
                {
                    var fallbackRule = new LocationRule
                    {
                        Key = "allowMurdererWorkRooftop-Mayhem",
                        PresetNames = null,
                        NameContains = Array.Empty<string>(),
                        NameExcludes = Array.Empty<string>(),
                        FloorNameContains = Array.Empty<string>(),
                        FloorNameExcludes = Array.Empty<string>(),
                        SubRoomNames = buildingTokens.Count > 0 ? buildingTokens.ToArray() : new[] { buildingName },
                        SubRoomPresets = new[] { "Rooftop" }
                    };
                    selected = FindBestLocationByRule(murder, caseInfo, fallbackRule);
                }
                if (selected != null) return selected;

                // Global scan
                try
                {
                    NewGameLocation best = null;
                    int bestOcc = int.MaxValue;
                    int scanned = 0, considered = 0, usable = 0;
                    foreach (var loc in CityData.Instance.gameLocationDirectory)
                    {
                        scanned++;
                        var addr = loc?.thisAsAddress;
                        if (addr == null) continue;
                        var rooms = addr.rooms;
                        bool hasRooftopRoom = false;
                        if (rooms != null)
                        {
                            foreach (var r in rooms)
                            {
                                var roomPreset = r?.preset?.name;
                                if (!string.IsNullOrEmpty(roomPreset) && string.Equals(roomPreset, "Rooftop", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasRooftopRoom = true;
                                }
                                if (!hasRooftopRoom)
                                {
                                    var rn = r?.name;
                                    if (!string.IsNullOrEmpty(rn) && (rn.IndexOf("rooftop", StringComparison.OrdinalIgnoreCase) >= 0 || rn.IndexOf("roof", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        if (addr.building == building)
                                        {
                                            hasRooftopRoom = true;
                                        }
                                        else if (buildingTokens.Count > 0)
                                        {
                                            foreach (var bt in buildingTokens)
                                            {
                                                if (!string.IsNullOrEmpty(bt) && rn.IndexOf(bt, StringComparison.OrdinalIgnoreCase) >= 0)
                                                { hasRooftopRoom = true; break; }
                                            }
                                        }
                                    }
                                }
                                if (hasRooftopRoom)
                                {
                                    considered++;
                                    bool ok = IsLocationUsable(murder, loc, caseInfo);
                                    if (!ok)
                                    {
                                        Plugin.Log?.LogInfo($"[Patch] Murderer Work Rooftop global-scan rejected (not usable): '{loc.name}' building='{buildingName}'");
                                        hasRooftopRoom = false;
                                        break;
                                    }
                                    usable++;
                                    int occ = loc.currentOccupants != null ? loc.currentOccupants.Count : 0;
                                    if (best == null || occ < bestOcc)
                                    {
                                        best = loc;
                                        bestOcc = occ;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    Plugin.Log?.LogInfo($"[Patch] Murderer Work Rooftop global-scan: scanned={scanned}, considered={considered}, usable={usable}, chosen='{best?.name ?? "null"}'");
                    return best;
                }
                catch (Exception ex2)
                {
                    Plugin.Log?.LogWarning($"[Patch] Murderer Work Rooftop global-scan fallback error: {ex2.Message}");
                }
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[Patch] TryFindMurdererWorkRooftop error: {ex.Message}");
                return null;
            }
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
                
                // Dynamic rules: if any active rules exist, redirect to a matching location
                string moName = activeMurder?.mo?.name ?? "(null)";
                
                // Check if this is a custom case
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                bool isCustomCase = caseInfo != null;
                
                // For custom cases, enable trespassing for the GoTo goal
                if (isCustomCase)
                {
                    try
                    {
                        // Try to set allowTrespass directly if it's a public property
                        var allowTrespassProperty = newPreset.GetType().GetProperty("allowTrespass");
                        if (allowTrespassProperty != null)
                        {
                            Plugin.Log?.LogInfo($"[Patch] CreateNewGoalPatch: Enabling trespassing via property for victim in custom case {moName}");
                            allowTrespassProperty.SetValue(newPreset, true);
                        }
                        else
                        {
                            // Fall back to field if property doesn't exist
                            var allowTrespassField = newPreset.GetType().GetField("allowTrespass");
                            if (allowTrespassField != null)
                            {
                                Plugin.Log?.LogInfo($"[Patch] CreateNewGoalPatch: Enabling trespassing via field for victim in custom case {moName}");
                                allowTrespassField.SetValue(newPreset, true);
                            }
                            else
                            {
                                Plugin.Log?.LogWarning($"[Patch] CreateNewGoalPatch: Could not find allowTrespass property or field on AIGoalPreset");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogError($"[Patch] CreateNewGoalPatch: Error setting allowTrespass: {ex.Message}");
                    }
                }
                
                var rules = MurderPatchHelpers.GetActiveRules(caseInfo);
                if (rules.Count == 0)
                    return true;

                var chosen = MurderPatchHelpers.FindBestLocationByRulesRandom(activeMurder, caseInfo, rules);
                if (chosen != null)
                {
                    var matched = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules)?.Key ?? "dynamic-rule";
                    Plugin.Log?.LogInfo($"[Patch] CreateNewGoalPatch: Intercepted toGoGoal for victim {human.GetCitizenName()} -> '{chosen.name}' due to {matched}");
                    
                    // Override the target location
                    newPassedGameLocation = chosen;
                    // Prefer sub-room anchor if the rule specifies sub-room filters; otherwise safe teleport
                    NewNode selNode = null;
                    var matchedRuleObj = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules);
                    bool hasSubFilters = matchedRuleObj != null && (
                        !string.IsNullOrEmpty(matchedRuleObj.SubRoomName) ||
                        !string.IsNullOrEmpty(matchedRuleObj.SubRoomPreset) ||
                        (matchedRuleObj.SubRoomNames != null && matchedRuleObj.SubRoomNames.Length > 0) ||
                        (matchedRuleObj.SubRoomPresets != null && matchedRuleObj.SubRoomPresets.Length > 0)
                    );
                    if (hasSubFilters)
                    {
                        selNode = MurderPatchHelpers.TryFindAnchorNodeFromRooms(chosen, matchedRuleObj);
                        if (selNode == null)
                        {
                            selNode = human.FindSafeTeleport(chosen, false, true);
                        }
                    }
                    else
                    {
                        selNode = human.FindSafeTeleport(chosen, false, true);
                        if (selNode == null)
                        {
                            selNode = MurderPatchHelpers.TryFindAnchorNodeFromRooms(chosen, matchedRuleObj);
                        }
                    }
                    newPassedNode = selNode;
                    
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
                        var node = m.victim.FindSafeTeleport(chosen, false, true);
                        if (node == null)
                        {
                            var matchedRuleObj = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, MurderPatchHelpers.GetActiveRules(caseInfo));
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(chosen, matchedRuleObj);
                            if (node != null)
                                Plugin.Log?.LogInfo($"[Patch] UpdatePatch: Using sub-room anchor fallback for location {chosen.name}");
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, chosen, null, null, -2);
                        return;
                    }
                }

                // Custom: allowVictimHomeRooftop-Mayhem -> send to victim's building rooftop
                if (caseInfo.HasAllowVictimHomeRooftopMayhem)
                {
                    var rooftop = MurderPatchHelpers.TryFindVictimHomeRooftop(m, caseInfo, out var victimHome);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(m, rooftop, caseInfo))
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM victim rooftop for victim {m.victim.GetCitizenName()} to: {rooftop.name}", 2);
                        var ai = m.victim.ai;
                        NewNode node = m.victim.FindSafeTeleport(rooftop, false, true);
                        if (node == null)
                        {
                            // Try to anchor inside a Rooftop sub-room if possible
                            var rule = new MurderPatchHelpers.LocationRule { SubRoomPresets = new[] { "Rooftop" }, SubRoomNames = Array.Empty<string>() };
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(rooftop, rule);
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, rooftop, null, null, -2);
                        return;
                    }
                }

                // Custom: allowVictimWorkRooftop-Mayhem -> send to victim's workplace building rooftop
                if (caseInfo.HasAllowVictimWorkRooftopMayhem)
                {
                    var rooftop = MurderPatchHelpers.TryFindVictimWorkRooftop(m, caseInfo, out var victimWork);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(m, rooftop, caseInfo))
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM victim WORK rooftop for victim {m.victim.GetCitizenName()} to: {rooftop.name}", 2);
                        var ai = m.victim.ai;
                        NewNode node = m.victim.FindSafeTeleport(rooftop, false, true);
                        if (node == null)
                        {
                            // Try to anchor inside a Rooftop sub-room if possible
                            var rule = new MurderPatchHelpers.LocationRule { SubRoomPresets = new[] { "Rooftop" }, SubRoomNames = Array.Empty<string>() };
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(rooftop, rule);
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, rooftop, null, null, -2);
                        return;
                    }
                }

                // Custom: allowMurdererHomeRooftop-Mayhem -> send victim to the murderer's building rooftop
                if (caseInfo.HasAllowMurdererHomeRooftopMayhem)
                {
                    var rooftop = MurderPatchHelpers.TryFindMurdererHomeRooftop(m, caseInfo, out var murdererHome);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(m, rooftop, caseInfo))
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM murderer HOME rooftop for victim {m.victim.GetCitizenName()} to: {rooftop.name}", 2);
                        var ai = m.victim.ai;
                        NewNode node = m.victim.FindSafeTeleport(rooftop, false, true);
                        if (node == null)
                        {
                            var rule = new MurderPatchHelpers.LocationRule { SubRoomPresets = new[] { "Rooftop" }, SubRoomNames = Array.Empty<string>() };
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(rooftop, rule);
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, rooftop, null, null, -2);
                        return;
                    }
                }

                // Custom: allowMurdererWorkRooftop-Mayhem -> send victim to the murderer's workplace building rooftop
                if (caseInfo.HasAllowMurdererWorkRooftopMayhem)
                {
                    var rooftop = MurderPatchHelpers.TryFindMurdererWorkRooftop(m, caseInfo, out var murdererWork);
                    if (rooftop != null && MurderPatchHelpers.IsLocationUsable(m, rooftop, caseInfo))
                    {
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM murderer WORK rooftop for victim {m.victim.GetCitizenName()} to: {rooftop.name}", 2);
                        var ai = m.victim.ai;
                        NewNode node = m.victim.FindSafeTeleport(rooftop, false, true);
                        if (node == null)
                        {
                            var rule = new MurderPatchHelpers.LocationRule { SubRoomPresets = new[] { "Rooftop" }, SubRoomNames = Array.Empty<string>() };
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(rooftop, rule);
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, rooftop, null, null, -2);
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
                        var node = m.victim.FindSafeTeleport(chosen, false, true);
                        if (node == null)
                        {
                            var matchedRuleObj = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, MurderPatchHelpers.GetActiveRules(caseInfo));
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(chosen, matchedRuleObj);
                            if (node != null)
                                Plugin.Log?.LogInfo($"[Patch] UpdatePatch: Using sub-room anchor fallback for location {chosen.name}");
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, chosen, null, null, -2);
                        return;
                    }
                }
                
                // Dynamic rules: if any active rules exist, choose one at random order and send victim there
                var rules = MurderPatchHelpers.GetActiveRules(caseInfo);
                if (rules.Count > 0)
                {
                    var chosen = MurderPatchHelpers.FindBestLocationByRulesRandom(m, caseInfo, rules);
                    if (chosen != null)
                    {
                        var matched = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules)?.Key ?? "dynamic-rule";
                        Game.Log($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM ({matched}) for victim {m.victim.GetCitizenName()} to: {chosen.name}", 2);
                        Plugin.Log?.LogInfo($"[Patch] Murder: Waiting too long! Creating GoTo CUSTOM ({matched}) for victim {m.victim.GetCitizenName()} to: {chosen.name}");
                        var ai = m.victim.ai;
                        NewNode node = null;
                        var matchedRuleObj = MurderPatchHelpers.GetMatchingRuleForLocation(chosen, rules);
                        bool hasSubFilters = matchedRuleObj != null && (
                            !string.IsNullOrEmpty(matchedRuleObj.SubRoomName) ||
                            !string.IsNullOrEmpty(matchedRuleObj.SubRoomPreset) ||
                            (matchedRuleObj.SubRoomNames != null && matchedRuleObj.SubRoomNames.Length > 0) ||
                            (matchedRuleObj.SubRoomPresets != null && matchedRuleObj.SubRoomPresets.Length > 0)
                        );
                        if (hasSubFilters)
                        {
                            node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(chosen, matchedRuleObj);
                            if (node == null)
                            {
                                node = m.victim.FindSafeTeleport(chosen, false, true);
                            }
                        }
                        else
                        {
                            node = m.victim.FindSafeTeleport(chosen, false, true);
                            if (node == null)
                            {
                                node = MurderPatchHelpers.TryFindAnchorNodeFromRooms(chosen, matchedRuleObj);
                            }
                        }
                        ai.CreateNewGoal(RoutineControls.Instance.toGoGoal, 0f, 0f, node, null, chosen, null, null, -2);
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
