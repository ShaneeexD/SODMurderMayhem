using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace MurderMayhem
{
    // This patch targets the GetGameLocationRoute method in PathFinder
    // to allow victims to path through trespassing areas
    [HarmonyPatch(typeof(PathFinder), "GetGameLocationRoute", new Type[] { typeof(NewNode), typeof(NewNode), typeof(Human) })]
    public class PathFinderGetGameLocationRoutePatch
    {

        // Prefix patch to bypass trespassing checks for victims
        public static void Prefix(ref Human human)
        {
            try
            {
                // Skip if no human
                if (human == null)
                    return;

                // Only apply to victims in custom cases
                var mc = MurderController.Instance;
                var murder = mc != null ? mc.GetCurrentMurder() : null;
                if (murder == null || murder.victim != human)
                    return;

                // Only for our managed custom cases
                string moName = murder?.mo?.name ?? string.Empty;
                var caseInfo = MurderPatchHelpers.GetCustomCaseInfoForMO(moName);
                if (caseInfo == null)
                    return;

                // Temporarily set human to null to bypass trespassing checks
                // This is a bit of a hack, but it's the most reliable way to bypass the checks
                // without modifying the original method
                Plugin.Log?.LogInfo($"[Patch] PathFinderGetGameLocationRoutePatch: Bypassing trespassing checks for victim {human.GetCitizenName()} in custom case '{caseInfo.PresetName}'");
                human = null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in PathFinderGetGameLocationRoutePatch.Prefix: {ex.Message}");
            }
        }
    }
}
