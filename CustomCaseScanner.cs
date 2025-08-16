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
        
        // Location keys from the JSON file
        public bool HasAllowAnywhere { get; }
        public bool HasAllowHome { get; }
        public bool HasAllowWork { get; }
        public bool HasAllowPublic { get; }
        public bool HasAllowStreets { get; }
        public bool HasAllowDen { get; }

        public CustomCaseInfo(string filePath, string presetName, string profileName, 
            bool hasAllowAnywhere, bool hasAllowHome, bool hasAllowWork, 
            bool hasAllowPublic, bool hasAllowStreets, bool hasAllowDen)
        {
            FilePath = filePath;
            PresetName = presetName;
            ProfileName = profileName;
            HasAllowAnywhere = hasAllowAnywhere;
            HasAllowHome = hasAllowHome;
            HasAllowWork = hasAllowWork;
            HasAllowPublic = hasAllowPublic;
            HasAllowStreets = hasAllowStreets;
            HasAllowDen = hasAllowDen;
        }

        public override string ToString()
        {
            return $"{PresetName} ({ProfileName}) - {FilePath}";
        }
        
        public string GetLocationKeysInfo()
        {
            var keys = new List<string>();
            if (HasAllowAnywhere) keys.Add("Anywhere");
            if (HasAllowHome) keys.Add("Home");
            if (HasAllowWork) keys.Add("Work");
            if (HasAllowPublic) keys.Add("Public");
            if (HasAllowStreets) keys.Add("Streets");
            if (HasAllowDen) keys.Add("Den");
            
            return keys.Count > 0 ? string.Join(", ", keys) : "None";
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

                            // Check for location keys (presence only)
                            bool hasAllowAnywhere = Regex.IsMatch(content, "\\\"allowAnywhere\\\"\\s*:", RegexOptions.IgnoreCase);
                            bool hasAllowHome = Regex.IsMatch(content, "\\\"allowHome\\\"\\s*:", RegexOptions.IgnoreCase);
                            bool hasAllowWork = Regex.IsMatch(content, "\\\"allowWork\\\"\\s*:", RegexOptions.IgnoreCase);
                            bool hasAllowPublic = Regex.IsMatch(content, "\\\"allowPublic\\\"\\s*:", RegexOptions.IgnoreCase);
                            bool hasAllowStreets = Regex.IsMatch(content, "\\\"allowStreets\\\"\\s*:", RegexOptions.IgnoreCase);
                            bool hasAllowDen = Regex.IsMatch(content, "\\\"allowDen\\\"\\s*:", RegexOptions.IgnoreCase);

                            results.Add(new CustomCaseInfo(
                                jsonPath,
                                presetName,
                                profileName,
                                hasAllowAnywhere,
                                hasAllowHome,
                                hasAllowWork,
                                hasAllowPublic,
                                hasAllowStreets,
                                hasAllowDen
                            ));
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
                    {
                        Plugin.Log?.LogInfo($" - {c}");
                        Plugin.Log?.LogInfo($"   Location Keys: {c.GetLocationKeysInfo()}");
                    }
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
