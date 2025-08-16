using BepInEx;
using SOD.Common.BepInEx;
using System.Reflection;
using BepInEx.Configuration;
using System.Collections.Generic;

namespace MurderMayhem
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(SOD.Common.Plugin.PLUGIN_GUID)]
    public class Plugin : PluginController<Plugin>
    {
        public const string PLUGIN_GUID = "ShaneeexD.MurderMayhem";
        public const string PLUGIN_NAME = "MurderMayhem";
        public const string PLUGIN_VERSION = "1.0.0";
        public static ConfigEntry<bool> exampleConfigVariable;

        public static List<CustomCaseInfo> CustomCases { get; private set; } = new List<CustomCaseInfo>();

        public override void Load()
        {
            Harmony.PatchAll(Assembly.GetExecutingAssembly());
            SaveGameHandlers eventHandler = new SaveGameHandlers();

            // Scan r2modman profiles/plugins for custom case JSONs
            CustomCases = CustomCaseScanner.ScanAllProfilesForCustomCases();
            Log.LogInfo("Plugin is patched.");

            exampleConfigVariable = Config.Bind("General", "ExampleConfigVariable", false, new ConfigDescription("Example config description."));
        }
    }
}