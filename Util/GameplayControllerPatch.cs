using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MurderMayhem
{
    // Patch to customize the crime scene objective text ONLY for our custom cases.
    // We re-run the small body of GameplayController.NewMurderCaseNotify when the
    // active murder corresponds to one of our scanned custom cases, and skip the
    // original method in that scenario. Vanilla behavior is preserved otherwise.
    [HarmonyPatch(typeof(GameplayController), "NewMurderCaseNotify")]
    public static class NewMurderCaseNotifyPatch
    {
        public static bool Prefix(NewGameLocation newLocation)
        {
            try
            {
                var activeMurders = MurderController.Instance?.activeMurders;
                if (activeMurders == null)
                    return true; // let vanilla handle

                foreach (var m in activeMurders)
                {
                    bool victimAtLocAndDead = false;
                    if (newLocation?.currentOccupants != null)
                    {
                        // Manual iteration instead of using Exists with lambda to avoid IL2CPP issues
                        foreach (var occupant in newLocation.currentOccupants)
                        {
                            if (occupant == m.victim && occupant.isDead)
                            {
                                victimAtLocAndDead = true;
                                break;
                            }
                        }
                    }

                    bool chapterReady = Game.Instance.sandboxMode ||
                        (ChapterController.Instance != null &&
                         ChapterController.Instance.chapterScript as ChapterIntro != null &&
                         (ChapterController.Instance.chapterScript as ChapterIntro).completed);

                    if (chapterReady && victimAtLocAndDead)
                    {
                        // Determine if this is one of our custom cases
                        string moName = m?.mo?.name ?? string.Empty;
                        var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                        bool isCustomCase = caseInfo != null;

                        if (!isCustomCase)
                        {
                            // Vanilla case: allow the original method to run unchanged
                            return true;
                        }

                        // Recreate original behavior with a minimal text adjustment for custom cases
                        InterfaceController.Instance.NewGameMessage(
                            InterfaceController.GameMessageType.notification,
                            0,
                            Strings.Get("ui.gamemessage", "New Enforcer Call", Strings.Casing.asIs, false, false, false, null) + ": " + newLocation.name,
                            InterfaceControls.Icon.skull,
                            AudioControls.Instance.enforcerScannerMsg,
                            false,
                            default(Color),
                            -1,
                            0f,
                            null,
                            GameMessageController.PingOnComplete.none,
                            null,
                            null,
                            null);

                        MurderController.Instance.OnVictimDiscovery();

                        if (MurderController.Instance.currentActiveCase != null)
                        {
                            Game.Log("Murder: Adding next crime scene objective... (custom)", 2);

                            var objectiveTrigger = new Objective.ObjectiveTrigger(
                                Objective.ObjectiveTriggerType.exploreCrimeScene,
                                string.Empty,
                                false,
                                0f,
                                null,
                                null,
                                null,
                                null,
                                null,
                                newLocation,
                                null,
                                string.Empty,
                                false,
                                default(Vector3));

                            // Enhanced location naming for custom cases
                            string baseText = Strings.Get("missions.postings", "Explore Reported Crime Scene", Strings.Casing.asIs, false, false, false, null);
                            string locationName = GetEnhancedLocationName(newLocation, caseInfo);
                            string text = baseText + locationName;

                            MurderController.Instance.currentActiveCase.AddObjective(
                                text,
                                objectiveTrigger,
                                false,
                                default(Vector3),
                                InterfaceControls.Icon.lookingGlass,
                                Objective.OnCompleteAction.nothing,
                                0f,
                                false,
                                string.Empty,
                                false,
                                false,
                                null,
                                false,
                                true,
                                false);
                        }

                        // We have handled the custom case path; skip the original method
                        return false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log?.LogError($"Error in NewMurderCaseNotifyPatch: {ex}");
            }

            // Default: allow original method
            return true;
        }

        // Helper method to get enhanced location name for special locations
        private static string GetEnhancedLocationName(NewGameLocation location, CustomCaseInfo caseInfo)
        {
            if (location == null)
                return string.Empty;

            string locationName = location.name;
            Plugin.Log?.LogInfo($"[Patch] GetEnhancedLocationName: location={locationName}");
            
            // Always try to get the building name for any location
            string parentName = GetParentLocationName(location);
            
            if (!string.IsNullOrEmpty(parentName))
            {
                Plugin.Log?.LogInfo($"[Patch] GetEnhancedLocationName: Found building name: {parentName}");
                return $"{parentName} - {locationName}";
            }
            else
            {
                Plugin.Log?.LogInfo($"[Patch] GetEnhancedLocationName: No building name found, using original location name");
            }
            
            // Default to original name if we couldn't enhance it
            return locationName;
        }
        
        // Helper to find parent location name - simplified to only use building name
        private static string GetParentLocationName(NewGameLocation location)
        {
            if (location == null)
            {
                Plugin.Log?.LogInfo("[Patch] GetParentLocationName: Location is null");
                return string.Empty;
            }
                
            Plugin.Log?.LogInfo($"[Patch] GetParentLocationName: Looking for parent of {location.name}");
            
            // Only use the building name
            var asAddress = location.thisAsAddress;
            if (asAddress?.building != null)
            {
                // Log the building name for debugging
                Plugin.Log?.LogInfo($"[Patch] GetParentLocationName: Building info - Name: {asAddress.building.name ?? "null"}");
                
                if (!string.IsNullOrEmpty(asAddress.building.name))
                {
                    Plugin.Log?.LogInfo($"[Patch] GetParentLocationName: Using building name: {asAddress.building.name}");
                    return asAddress.building.name;
                }
            }
            else
            {
                Plugin.Log?.LogInfo("[Patch] GetParentLocationName: No building information found");
            }
            
            Plugin.Log?.LogInfo("[Patch] GetParentLocationName: No parent location found");
            return string.Empty;
        }
        
        // Removed unused GetParentLocationFromFloor method since we're now only using building names
    }
}
