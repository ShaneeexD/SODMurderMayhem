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

                            // Simple change: append a small marker to indicate custom to the player.
                            string baseText = Strings.Get("missions.postings", "Explore Reported Crime Scene", Strings.Casing.asIs, false, false, false, null);
                            string text = baseText + newLocation.name + " [Custom]";

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
    }
}
