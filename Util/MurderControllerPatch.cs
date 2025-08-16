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
                                $"Flags => Alley-Mayhem={caseInfo.HasAllowAlleyMayhem}, Work-Mayhem={caseInfo.HasAllowWorkMayhem}, Work={caseInfo.HasAllowWork}, Public={caseInfo.HasAllowPublic}, Streets={caseInfo.HasAllowStreets}, Home={caseInfo.HasAllowHome}, Anywhere={caseInfo.HasAllowAnywhere}, OccupancyLimit={(caseInfo.OccupancyLimit.HasValue ? caseInfo.OccupancyLimit.Value.ToString() : "null")}"
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

    // Base no-op patches to enable future custom location handling for custom MurderMOs
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
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in UpdatePatch.Postfix: {ex.Message}");
            }
        }
    }
}
