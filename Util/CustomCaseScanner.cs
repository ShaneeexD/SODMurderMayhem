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
        public bool HasAllowAnywhereMayhem { get; }
        public bool HasAllowHome { get; }
        public bool HasAllowWork { get; }
        public bool HasAllowWorkMayhem { get; }
        public bool HasAllowAlleyMayhem { get; }
        public bool HasAllowBackstreetsMayhem { get; }
        public bool HasAllowParkMayhem { get; }
        public bool HasAllowHotelBathroomMayhem { get; }
        public bool HasAllowPublic { get; }
        public bool HasAllowStreets { get; }
        public bool HasAllowDen { get; }
        public bool HasAllowDinerBathroomMayhem { get; }
        public bool HasAllowFathomsYardBasementMayhem { get; }
        public bool HasAllowFathomsYardRooftopMayhem { get; }
        public bool HasAllowHotelRooftopBarMayhem { get; }
        public bool HasAllowHotelRooftopMayhem { get; }
        public bool HasAllowMixedIndustrialRooftopMayhem { get; }
        // Optional overrides
        public int? OccupancyLimit { get; }

        public CustomCaseInfo(string filePath, string presetName, string profileName, 
            bool hasAllowAnywhere, bool hasAllowAnywhereMayhem, bool hasAllowHome, bool hasAllowWork, bool hasAllowWorkMayhem,
            bool hasAllowAlleyMayhem, bool hasAllowBackstreetsMayhem, bool hasAllowParkMayhem, bool hasAllowHotelBathroomMayhem, bool hasAllowPublic, bool hasAllowStreets, bool hasAllowDen, bool hasAllowDinerBathroomMayhem, bool hasAllowFathomsYardBasementMayhem, bool hasAllowHotelRooftopBarMayhem, bool hasAllowHotelRooftopMayhem, bool hasAllowMixedIndustrialRooftopMayhem, bool hasAllowFathomsYardRooftopMayhem, int? occupancyLimit)
        {
            FilePath = filePath;
            PresetName = presetName;
            ProfileName = profileName;
            HasAllowAnywhere = hasAllowAnywhere;
            HasAllowAnywhereMayhem = hasAllowAnywhereMayhem;
            HasAllowHome = hasAllowHome;
            HasAllowWork = hasAllowWork;
            HasAllowWorkMayhem = hasAllowWorkMayhem;
            HasAllowAlleyMayhem = hasAllowAlleyMayhem;
            HasAllowBackstreetsMayhem = hasAllowBackstreetsMayhem;
            HasAllowParkMayhem = hasAllowParkMayhem;
            HasAllowHotelBathroomMayhem = hasAllowHotelBathroomMayhem;
            HasAllowPublic = hasAllowPublic;
            HasAllowStreets = hasAllowStreets;
            HasAllowDen = hasAllowDen;
            HasAllowDinerBathroomMayhem = hasAllowDinerBathroomMayhem;
            HasAllowFathomsYardBasementMayhem = hasAllowFathomsYardBasementMayhem;
            HasAllowFathomsYardRooftopMayhem = hasAllowFathomsYardRooftopMayhem;
            HasAllowHotelRooftopBarMayhem = hasAllowHotelRooftopBarMayhem;
            HasAllowHotelRooftopMayhem = hasAllowHotelRooftopMayhem;
            HasAllowMixedIndustrialRooftopMayhem = hasAllowMixedIndustrialRooftopMayhem;
            OccupancyLimit = occupancyLimit;
        }

        public override string ToString()
        {
            return $"{PresetName} ({ProfileName}) - {FilePath}";
        }
        
        public string GetLocationKeysInfo()
        {
            var keys = new List<string>();
            if (HasAllowAnywhere) keys.Add("Anywhere");
            if (HasAllowAnywhereMayhem) keys.Add("Anywhere-Mayhem");
            if (HasAllowHome) keys.Add("Home");
            if (HasAllowWork) keys.Add("Work");
            if (HasAllowWorkMayhem) keys.Add("Work-Mayhem");
            if (HasAllowAlleyMayhem) keys.Add("Alley-Mayhem");
            if (HasAllowBackstreetsMayhem) keys.Add("Backstreets-Mayhem");
            if (HasAllowParkMayhem) keys.Add("Park-Mayhem");
            if (HasAllowHotelBathroomMayhem) keys.Add("HotelBathroom-Mayhem");
            if (HasAllowPublic) keys.Add("Public");
            if (HasAllowStreets) keys.Add("Streets");
            if (HasAllowDen) keys.Add("Den");
            if (HasAllowDinerBathroomMayhem) keys.Add("DinerBathroom-Mayhem");
            if (HasAllowFathomsYardBasementMayhem) keys.Add("FathomsYardBasement-Mayhem");
            if (HasAllowFathomsYardRooftopMayhem) keys.Add("FathomsYardRooftop-Mayhem");
            if (HasAllowHotelRooftopBarMayhem) keys.Add("HotelRooftopBar-Mayhem");
            if (HasAllowHotelRooftopMayhem) keys.Add("HotelRooftop-Mayhem");
            if (HasAllowMixedIndustrialRooftopMayhem) keys.Add("MixedIndustrialRooftop-Mayhem");
            if (OccupancyLimit.HasValue) keys.Add($"OccupancyLimit={OccupancyLimit.Value}");
            
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
                            content = NormalizeContent(content);
                            // Quick check for fileType: "MurderMO"
                            if (!Regex.IsMatch(content, "\\\"fileType\\\"\\s*:\\s*\\\"MurderMO\\\"", RegexOptions.IgnoreCase))
                                continue;

                            // Extract presetName's string value
                            var match = Regex.Match(content, "\\\"presetName\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
                            if (!match.Success) continue;
                            var presetName = match.Groups[1].Value;
                            if (string.IsNullOrWhiteSpace(presetName)) continue;

                            // Check for location keys, tracking actual boolean-like value (true/false/1/0)
                            bool hasAllowAnywhere = ExtractBool(content, "allowAnywhere");
                            bool hasAllowHome = ExtractBool(content, "allowHome");
                            bool hasAllowWork = ExtractBool(content, "allowWork");
                            bool hasAllowWorkMayhem = ExtractBool(content, "allowWork-Mayhem");
                            bool hasAllowAlleyMayhem = ExtractBool(content, "allowAlley-Mayhem");
                            bool hasAllowPublic = ExtractBool(content, "allowPublic");
                            bool hasAllowStreets = ExtractBool(content, "allowStreets");
                            bool hasAllowDen = ExtractBool(content, "allowDen");
                            bool hasAllowDinerBathroomMayhem = ExtractBool(content, "allowDinerBathroom-Mayhem");
                            bool hasAllowFathomsYardBasementMayhem = ExtractBool(content, "allowFathomsYardBasement-Mayhem");
                            bool hasAllowFathomsYardRooftopMayhem = ExtractBool(content, "allowFathomsYardRooftop-Mayhem");
                            bool hasAllowHotelRooftopBarMayhem = ExtractBool(content, "allowHotelRooftopBar-Mayhem");
                            bool hasAllowHotelRooftopMayhem = ExtractBool(content, "allowHotelRooftop-Mayhem");
                            bool hasAllowMixedIndustrialRooftopMayhem = ExtractBool(content, "allowMixedIndustrialRooftop-Mayhem");
                            int? occupancyLimit = ExtractInt(content, "occupancyLimit");
                            bool hasAllowBackstreetsMayhem = ExtractBool(content, "allowBackstreets-Mayhem");
                            bool hasAllowAnywhereMayhem = ExtractBool(content, "allowAnywhere-Mayhem");
                            bool hasAllowParkMayhem = ExtractBool(content, "allowPark-Mayhem");
                            bool hasAllowHotelBathroomMayhem = ExtractBool(content, "allowHotelBathroom-Mayhem");

                            results.Add(new CustomCaseInfo(
                                jsonPath,
                                presetName,
                                profileName,
                                hasAllowAnywhere,
                                hasAllowAnywhereMayhem,
                                hasAllowHome,
                                hasAllowWork,
                                hasAllowWorkMayhem,
                                hasAllowAlleyMayhem,
                                hasAllowBackstreetsMayhem,
                                hasAllowParkMayhem,
                                hasAllowHotelBathroomMayhem,
                                hasAllowPublic,
                                hasAllowStreets,
                                hasAllowDen,
                                hasAllowDinerBathroomMayhem,
                                hasAllowFathomsYardBasementMayhem,
                                hasAllowFathomsYardRooftopMayhem,
                                hasAllowHotelRooftopBarMayhem,
                                hasAllowHotelRooftopMayhem,
                                hasAllowMixedIndustrialRooftopMayhem,
                                occupancyLimit
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

        // Helpers
        private static string NormalizeContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return content ?? string.Empty;
            // Normalize various Unicode dashes to ASCII '-'
            content = content
                .Replace('\u2010', '-') // hyphen
                .Replace('\u2011', '-') // non-breaking hyphen
                .Replace('\u2012', '-') // figure dash
                .Replace('\u2013', '-') // en dash
                .Replace('\u2014', '-') // em dash
                .Replace('\u2015', '-') // horizontal bar
                .Replace('\u2212', '-') // minus sign
                .Replace('\uFF0D', '-'); // fullwidth hyphen-minus
            return content;
        }

        private static bool ExtractBool(string content, string key)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(key)) return false;
            var pattern = "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(?:\\\"(?<val>true|false|1|0)\\\"|(?<val>true|false|1|0))";
            var m = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            var val = m.Groups["val"].Value;
            return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || val == "1";
        }

        private static int? ExtractInt(string content, string key)
        {
            if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(key)) return null;
            var pattern = "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?\\d+)";
            var m = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            if (int.TryParse(m.Groups[1].Value, out var result)) return result;
            return null;
        }
    }
}
