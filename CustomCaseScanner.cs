using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MurderMayhem
{
    public sealed class CustomCaseInfo
    {
        public string FilePath { get; }
        public string PresetName { get; }
        public string ProfileName { get; }

        public CustomCaseInfo(string filePath, string presetName, string profileName)
        {
            FilePath = filePath;
            PresetName = presetName;
            ProfileName = profileName;
        }

        public override string ToString()
        {
            return $"{PresetName} ({ProfileName}) - {FilePath}";
        }
    }

    public static class CustomCaseScanner
    {
        public static List<CustomCaseInfo> ScanAllProfilesForCustomCases()
        {
            var results = new List<CustomCaseInfo>();
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var profilesRoot = Path.Combine(appData, "r2modmanPlus-local", "ShadowsofDoubt", "profiles");

                if (!Directory.Exists(profilesRoot))
                {
                    Plugin.Log?.LogWarning($"Profiles path not found: {profilesRoot}");
                    return results;
                }

                foreach (var profileDir in Directory.EnumerateDirectories(profilesRoot))
                {
                    var profileName = Path.GetFileName(profileDir);
                    var pluginsDir = Path.Combine(profileDir, "BepInEx", "plugins");
                    if (!Directory.Exists(pluginsDir))
                        continue;

                    foreach (var jsonPath in Directory.EnumerateFiles(pluginsDir, "*.json", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var content = File.ReadAllText(jsonPath);
                            // Quick check for fileType: "MurderMO"
                            if (!Regex.IsMatch(content, "\\\"fileType\\\"\\s*:\\s*\\\"MurderMO\\\"", RegexOptions.IgnoreCase))
                                continue;

                            // Extract presetName's string value
                            var match = Regex.Match(content, "\\\"presetName\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;
                            var presetName = match.Groups[1].Value;
                            if (string.IsNullOrWhiteSpace(presetName)) continue;

                            results.Add(new CustomCaseInfo(jsonPath, presetName, profileName));
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogDebug($"Failed to parse JSON '{jsonPath}': {ex.Message}");
                        }
                    }
                }

                if (results.Count > 0)
                {
                    Plugin.Log?.LogInfo($"Custom cases detected: {results.Count}");
                    foreach (var c in results.Take(10))
                        Plugin.Log?.LogInfo($" - {c}");
                    if (results.Count > 10)
                        Plugin.Log?.LogInfo($" ... and {results.Count - 10} more.");
                }
                else
                {
                    Plugin.Log?.LogInfo("No custom cases found.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error scanning custom cases: {ex}");
            }

            return results;
        }
    }
}
