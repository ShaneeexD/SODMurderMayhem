using HarmonyLib;
using System;

namespace MurderMayhem
{
    // Minimal, safe trespass override: only for active victim in a custom case
    [HarmonyPatch(typeof(Human), "IsTrespassing")]
    public class HumanIsTrespassingVictimPatch
    {
        public static bool Prefix(Human __instance, NewRoom room, ref int trespassEscalation, bool enforcersAllowedEverywhere, ref bool __result)
        {
            try
            {
                // Only apply while a murder is active and this human is the victim
                var mc = MurderController.Instance;
                var murder = mc != null ? mc.GetCurrentMurder() : null;
                if (murder == null || murder.victim != __instance)
                    return true;

                // Only apply to custom cases we manage
                string moName = murder?.mo?.name ?? string.Empty;
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                if (caseInfo == null)
                    return true;

                // Bypass trespass: allow AI actions to proceed
                trespassEscalation = 0;
                __result = false;

                Plugin.Log?.LogInfo($"[Patch] HumanIsTrespassingVictimPatch: Allowing trespass for victim {__instance.GetCitizenName()} in custom case '{caseInfo.PresetName}' at room '{room?.name ?? "(null)"}'");
                return false; // Skip original IsTrespassing
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in HumanIsTrespassingVictimPatch: {ex.Message}");
                return true; // Fail open to avoid breaking base behavior
            }
        }
    }
}
