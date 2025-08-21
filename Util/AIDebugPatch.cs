using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MurderMayhem
{
    // Debug patch to monitor AI goal execution for victims
    [HarmonyPatch(typeof(NewAIGoal), nameof(NewAIGoal.AITick))]
    public class AIGoalTickDebugPatch
    {
        private static Dictionary<int, float> lastLogTimeByInstanceId = new Dictionary<int, float>();
        private const float LOG_INTERVAL = 1.0f; // Log once per second to avoid spam

        public static void Postfix(NewAIGoal __instance)
        {
            try
            {
                // Only monitor victims in active murder cases
                Human human = __instance?.aiController?.human;
                if (human == null || MurderController.Instance == null)
                    return;

                var currentMurder = MurderController.Instance.GetCurrentMurder();
                if (currentMurder == null || currentMurder.victim != human)
                    return;

                // Rate limit logging to avoid spam
                int instanceId = __instance.GetHashCode();
                float currentTime = Time.time;
                if (lastLogTimeByInstanceId.TryGetValue(instanceId, out float lastTime) && 
                    currentTime - lastTime < LOG_INTERVAL)
                {
                    return;
                }
                lastLogTimeByInstanceId[instanceId] = currentTime;

                // Log detailed goal execution state
                string goalName = __instance.preset?.name ?? "Unknown";
                string actionsCount = __instance.actions != null ? __instance.actions.Count.ToString() : "0";
                string currentAction = "None";
                if (__instance.actions != null && __instance.actions.Count > 0)
                {
                    var action = __instance.actions[0]; // Get the first action
                    currentAction = action != null ? $"{action.preset?.name ?? "Unknown"}" : "None";
                }
                string targetLocation = __instance.passedGameLocation != null ? __instance.passedGameLocation.name : "None";
                string targetNode = __instance.passedNode != null ? 
                    $"{__instance.passedNode.name}" : "None";
                string priority = __instance.priority.ToString("F2");
                string murderState = currentMurder.state.ToString();

                // Create a detailed log message
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[VictimDebug] Goal AITick: {human.GetCitizenName()} - {goalName}");
                sb.AppendLine($"  Murder State: {murderState}");
                sb.AppendLine($"  Priority: {priority}");
                sb.AppendLine($"  Target Location: {targetLocation}");
                sb.AppendLine($"  Target Node: {targetNode}");
                sb.AppendLine($"  Actions: {actionsCount}, Current: {currentAction}");
                sb.AppendLine($"  Current Location: {human.currentGameLocation?.name ?? "None"}");
                sb.AppendLine($"  Current Node: {human.currentNode?.name ?? "None"}");
                sb.AppendLine($"  Is Trespassing: {human.isTrespassing}");

                // Log the message
                Plugin.Log?.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in AIGoalTickDebugPatch: {ex.Message}");
            }
        }
    }

    // Debug patch to monitor AI controller goal management
    [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.AITick))]
    [HarmonyPatch(new Type[] { typeof(bool), typeof(bool) })]
    public class AIControllerTickDebugPatch
    {
        private static Dictionary<int, float> lastLogTimeByInstanceId = new Dictionary<int, float>();
        private const float LOG_INTERVAL = 2.0f; // Log once every 2 seconds to avoid spam

        public static void Postfix(NewAIController __instance, bool forceUpdatePriorities, bool ignoreRepeatDelays)
        {
            try
            {
                // Only monitor victims in active murder cases
                Human human = __instance?.human;
                if (human == null || MurderController.Instance == null)
                    return;

                var currentMurder = MurderController.Instance.GetCurrentMurder();
                if (currentMurder == null || currentMurder.victim != human)
                    return;

                // Rate limit logging to avoid spam
                int instanceId = __instance.GetHashCode();
                float currentTime = Time.time;
                if (lastLogTimeByInstanceId.TryGetValue(instanceId, out float lastTime) && 
                    currentTime - lastTime < LOG_INTERVAL)
                {
                    return;
                }
                lastLogTimeByInstanceId[instanceId] = currentTime;

                // Log controller state
                string currentGoalName = __instance.currentGoal?.preset?.name ?? "None";
                string goalCount = __instance.goals != null ? __instance.goals.Count.ToString() : "0";
                string murderState = currentMurder.state.ToString();
                
                // Get list of all goals with priorities
                StringBuilder goalsList = new StringBuilder();
                if (__instance.goals != null && __instance.goals.Count > 0)
                {
                    foreach (var goal in __instance.goals)
                    {
                        string goalName = goal.preset?.name ?? "Unknown";
                        float priority = goal.priority;
                        goalsList.AppendLine($"    - {goalName}: Priority={priority:F2}");
                    }
                }
                else
                {
                    goalsList.AppendLine("    No goals");
                }

                // Create detailed log message
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[VictimDebug] Controller AITick: {human.GetCitizenName()}");
                sb.AppendLine($"  Murder State: {murderState}");
                sb.AppendLine($"  Current Goal: {currentGoalName}");
                sb.AppendLine($"  Goals Count: {goalCount}");
                sb.AppendLine($"  Goals List:");
                sb.Append(goalsList);
                sb.AppendLine($"  Current Location: {human.currentGameLocation?.name ?? "None"}");
                sb.AppendLine($"  Is Trespassing: {human.isTrespassing}");

                // Log the message
                Plugin.Log?.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in AIControllerTickDebugPatch: {ex.Message}");
            }
        }
    }

    // Debug patch to monitor goal creation
    [HarmonyPatch(typeof(NewAIController), nameof(NewAIController.CreateNewGoal))]
    public class AIGoalCreationDebugPatch
    {
        public static void Postfix(NewAIController __instance, AIGoalPreset newPreset, float newTrigerTime, float newDuration, 
            NewNode newPassedNode, Interactable newPassedInteractable, NewGameLocation newPassedGameLocation, 
            GroupsController.SocialGroup newPassedGroup, MurderController.Murder newMurderRef, int newPassedVar, NewAIGoal __result)
        {
            try
            {
                // Only monitor victims in active murder cases
                Human human = __instance?.human;
                if (human == null || MurderController.Instance == null)
                    return;

                var currentMurder = MurderController.Instance.GetCurrentMurder();
                if (currentMurder == null || currentMurder.victim != human)
                    return;

                // Log goal creation details
                string goalName = newPreset?.name ?? "Unknown";
                string targetLocation = newPassedGameLocation != null ? newPassedGameLocation.name : "None";
                string targetNode = newPassedNode != null ? 
                    $"{newPassedNode.name}" : "None";
                string murderState = currentMurder.state.ToString();
                string resultGoal = __result != null ? "Success" : "Failed";
                bool allowTrespass = false;
                
                // Try to read allowTrespass property directly
                try
                {
                    allowTrespass = newPreset.allowTrespass;
                    Plugin.Log?.LogInfo($"[Debug] AIGoalCreationDebugPatch: allowTrespass={allowTrespass} for {human.GetCitizenName()}'s {goalName} goal");
                }
                catch (Exception ex) 
                { 
                    Plugin.Log?.LogError($"Error reading allowTrespass: {ex.Message}");
                }

                // Create log message
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[VictimDebug] Goal Creation: {human.GetCitizenName()} - {goalName} - {resultGoal}");
                sb.AppendLine($"  Murder State: {murderState}");
                sb.AppendLine($"  Target Location: {targetLocation}");
                sb.AppendLine($"  Target Node: {targetNode}");
                sb.AppendLine($"  Allow Trespass: {allowTrespass}");
                sb.AppendLine($"  Current Location: {human.currentGameLocation?.name ?? "None"}");
                sb.AppendLine($"  Is Trespassing: {human.isTrespassing}");

                // Log the message
                Plugin.Log?.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in AIGoalCreationDebugPatch: {ex.Message}");
            }
        }
    }

    // Debug patch to monitor goal activation
    [HarmonyPatch(typeof(NewAIGoal), nameof(NewAIGoal.OnActivate))]
    public class AIGoalActivationDebugPatch
    {
        public static void Postfix(NewAIGoal __instance)
        {
            try
            {
                // Only monitor victims in active murder cases
                Human human = __instance?.aiController?.human;
                if (human == null || MurderController.Instance == null)
                    return;

                var currentMurder = MurderController.Instance.GetCurrentMurder();
                if (currentMurder == null || currentMurder.victim != human)
                    return;

                // Log activation details
                string goalName = __instance.preset?.name ?? "Unknown";
                string targetLocation = __instance.passedGameLocation != null ? __instance.passedGameLocation.name : "None";
                string targetNode = __instance.passedNode != null ? 
                    $"{__instance.passedNode.name}" : "None";
                string murderState = currentMurder.state.ToString();
                
                // Create log message
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[VictimDebug] Goal Activation: {human.GetCitizenName()} - {goalName}");
                sb.AppendLine($"  Murder State: {murderState}");
                sb.AppendLine($"  Target Location: {targetLocation}");
                sb.AppendLine($"  Target Node: {targetNode}");
                sb.AppendLine($"  Current Location: {human.currentGameLocation?.name ?? "None"}");
                sb.AppendLine($"  Is Trespassing: {human.isTrespassing}");

                // Log the message
                Plugin.Log?.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in AIGoalActivationDebugPatch: {ex.Message}");
            }
        }
    }

    // Debug patch to monitor action activation
    [HarmonyPatch(typeof(NewAIAction), nameof(NewAIAction.OnActivate))]
    public class AIActionActivationDebugPatch
    {
        public static void Postfix(NewAIAction __instance)
        {
            try
            {
                // Only monitor victims in active murder cases
                Human human = __instance?.goal?.aiController?.human;
                if (human == null || MurderController.Instance == null)
                    return;

                var currentMurder = MurderController.Instance.GetCurrentMurder();
                if (currentMurder == null || currentMurder.victim != human)
                    return;

                // Log activation details
                string actionName = __instance.preset?.name ?? "Unknown";
                string goalName = __instance.goal?.preset?.name ?? "Unknown";
                
                // Create log message
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[VictimDebug] Action Activation: {human.GetCitizenName()} - {actionName} (Goal: {goalName})");
                sb.AppendLine($"  Current Location: {human.currentGameLocation?.name ?? "None"}");
                sb.AppendLine($"  Current Node: {human.currentNode?.name ?? "None"}");
                sb.AppendLine($"  Is Trespassing: {human.isTrespassing}");

                // Log the message
                Plugin.Log?.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in AIActionActivationDebugPatch: {ex.Message}");
            }
        }
    }
}
