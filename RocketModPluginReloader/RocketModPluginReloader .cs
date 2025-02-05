using HarmonyLib;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Logger = Rocket.Core.Logging.Logger;

namespace PatchModule
{
    public class RocketModFix : IModuleNexus
    {
        public const string HarmonyId = "com.Kaze.rmfix";
        public Harmony? harmony;

        internal static void RegisterConsoleInput() => CommandWindow.onCommandWindowInputted += HandleInput;

        internal static void UnregisterConsoleInput() => CommandWindow.onCommandWindowInputted -= HandleInput;
        public void initialize()
        {

            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();

            //var list = harmony.GetPatchedMethods().ToList();
            //Logger.Log($"Count of patched method {list.Count}");

            RegisterConsoleInput();
        }
        public void shutdown()
        {
            UnregisterConsoleInput();
        }

        private static void HandleInput(string Text, ref bool ShouldExecuteCommand)
        {
            if (!Text.ToLower().Contains("/rmf rel")) return;
            
            var reloadMethod = R.Plugins.GetType().GetMethod("Reload", BindingFlags.NonPublic | BindingFlags.Instance);
            reloadMethod.Invoke(R.Plugins, null);
            
        }
    }

    [HarmonyPatch(typeof(RocketPluginManager))]
    public class HarmonyFix
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(RocketPluginManager.LoadAssembliesFromDirectory))]
        public static bool LoadAssembliesFromDirectoryFix(ref List<Assembly> __result, string directory, string extension = "*.dll")
        {
            __result = new List<Assembly>();
            foreach (FileInfo item in new DirectoryInfo(directory).GetFiles(extension, SearchOption.TopDirectoryOnly))
            {
                try
                {
                    byte[] rawAssembly = File.ReadAllBytes(item.FullName);
                    Assembly assembly = Assembly.Load(rawAssembly);

                    if (RocketHelper.GetTypesFromInterface(assembly, "IRocketPlugin").FindAll((Type x) => !x.IsAbstract).Count == 1)
                    {
                        Logger.Log("Loading " + assembly.GetName().Name + " from the memory" );
                        __result.Add(assembly);
                    }
                    else
                    {
                        Logger.LogError("Invalid or outdated plugin assembly: " + assembly.GetName().Name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Could not load plugin assembly: " + item.Name);
                }
            }
            return false;
        }
    }
}
