using HarmonyLib;
using System;

namespace MurderMayhem
{
    // Allow the active victim in custom cases to select destinations in any area
    // by widening FindNearestWithAction's search setting from restrictive modes
    // (onlyPublic/nonTrespassing) to allAreas.
    [HarmonyPatch(typeof(Toolbox), nameof(Toolbox.FindNearestWithAction))]
    public static class VictimGoalSearchPatch
    {
        // We only need the human and the findSetting arg to adjust the behavior.
        public static void Prefix(Human person, ref AIActionPreset.FindSetting findSetting)
        {
            try
            {
                var mc = MurderController.Instance;
                var murder = mc != null ? mc.GetCurrentMurder() : null;
                if (murder == null || person == null || murder.victim != person)
                    return;

                // Only for our managed custom cases
                string moName = murder?.mo?.name ?? string.Empty;
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                if (caseInfo == null)
                    return;

                // Widen search scope for the victim so private/unauthorised areas are eligible
                if (findSetting == AIActionPreset.FindSetting.onlyPublic ||
                    findSetting == AIActionPreset.FindSetting.nonTrespassing)
                {
                    findSetting = AIActionPreset.FindSetting.allAreas;
                    Plugin.Log?.LogInfo("[Patch] VictimGoalSearchPatch: Overriding FindSetting to allAreas for victim goal search");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"VictimGoalSearchPatch error: {ex.Message}");
            }
        }
    }
}
