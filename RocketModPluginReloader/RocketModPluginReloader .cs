using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AsmResolver.PE;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Metadata.Strings;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Logger = Rocket.Core.Logging.Logger;
using AsmResolver.IO;

namespace PatchModule
{
    public class RocketModPluginReloader : IModuleNexus
    {
        public const string HarmonyId = "com.Kaze.rmfix";
        public Harmony? harmony;

        internal void RegisterConsoleInput() => CommandWindow.onCommandWindowInputted += HandleInput;

        internal void UnregisterConsoleInput() => CommandWindow.onCommandWindowInputted -= HandleInput;
        public void initialize()
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();

            var list = harmony.GetPatchedMethods().ToList();
            Logger.Log($"Count of patched method {list.Count}");

            AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
            Logger.Log($"{assemblyName.Name} {assemblyName.Version} has been loaded!");

            RegisterConsoleInput();
        }
        public void shutdown()
        {
            UnregisterConsoleInput();
        }
        private void HandleInput(string Text, ref bool ShouldExecuteCommand)
        {
            if (!Text.ToLower().Contains("/rm rel")) return;

            var list = harmony?.GetPatchedMethods().ToList();
            Logger.Log($"Count of patched method {list?.Count}");

            var reloadMethod = AccessTools.Method(typeof(RocketPluginManager), "Reload");
            reloadMethod.Invoke(R.Plugins, null);

            list = harmony?.GetPatchedMethods().ToList();
            Logger.Log($"Count of patched method {list?.Count}");

        }
    }
    [HarmonyPatch(typeof(RocketPluginManager))]
    public class HarmonyFix
    {
        public static int x = 0;

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

                    Assembly assembly = Assembly.Load(ModifyAssembly(rawAssembly));

                    //Assembly assembly = Assembly.Load(rawAssembly);
                    if (RocketHelper.GetTypesFromInterface(assembly, "IRocketPlugin").FindAll((Type x) => !x.IsAbstract).Count == 1)
                    {
                        Logger.Log("Loading " + assembly.GetName().Name + " from the memory");
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
        public static byte[] ModifyAssembly(byte[] rawAssembly)
        {
            using (var ms = new MemoryStream())
            {
                var image = PEImage.FromBytes(rawAssembly);
                var metadata = image.DotNetDirectory!.Metadata!;
                var tablesStream = metadata.GetStream<TablesStream>();
                var oldStringsStream = metadata.GetStream<StringsStream>();

                ref var assemblyRow = ref tablesStream
                    .GetTable<AssemblyDefinitionRow>(TableIndex.Assembly)
                    .GetRowRef(1);

                string originalName = oldStringsStream.GetStringByIndex(assemblyRow.Name)!;

                //string newName = $"{originalName}_{Guid.NewGuid().ToString("N").Substring(0, 6)} {++x}";
                string newName = $"{originalName}_{++x}";

                assemblyRow.Name = oldStringsStream.GetPhysicalSize();

                using var output = new MemoryStream();
                var writer = new BinaryStreamWriter(output);

                writer.WriteBytes(oldStringsStream.CreateReader().ReadToEnd());
                writer.WriteBytes(System.Text.Encoding.UTF8.GetBytes(newName));
                writer.WriteByte(0);
                writer.Align(4);

                var newStringsStream = new SerializedStringsStream(output.ToArray());
                tablesStream.StringIndexSize = newStringsStream.IndexSize;
                metadata.Streams[metadata.Streams.IndexOf(oldStringsStream)] = newStringsStream;

                var builder = new ManagedPEFileBuilder();
                output.SetLength(0);
                builder.CreateFile(image).Write(output);

                return output.ToArray();
            }
        }
    }
}
