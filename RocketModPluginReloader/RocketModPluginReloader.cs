using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AsmResolver.PE;
using AsmResolver.PE.DotNet.Builder;
using AsmResolver.PE.DotNet.Metadata.Strings;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Logger = Rocket.Core.Logging.Logger;
using AsmResolver.IO;
using Rocket.Core.Assets;
using System.Linq;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using UnityEngine;
using Rocket.Unturned.Chat;

namespace RocketModPluginReloader
{
    public class RocketModPluginReloader : IModuleNexus
    {
        public const string HarmonyId = "com.Kaze.rmfix";
        public Harmony? harmony;

        internal void RegisterInput() 
        {
            CommandWindow.onCommandWindowInputted += HandleInput;
            UnturnedPlayerEvents.OnPlayerChatted += OnPlayerChatted;
        }
        internal void UnregisterInput()
        {
            CommandWindow.onCommandWindowInputted -= HandleInput;
            UnturnedPlayerEvents.OnPlayerChatted -= OnPlayerChatted;
        }
        public void initialize()
        {
            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();

            //var list = harmony.GetPatchedMethods().ToList();
            //Logger.Log($"Count of patched method {list.Count}");

            AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
            Logger.Log($"{assemblyName.Name} {assemblyName.Version} has been loaded!");

            RegisterInput();
            
        }
        public void shutdown()
        {
            UnregisterInput();
            harmony?.UnpatchAll(HarmonyId);
        }
        private void HandleInput(string Text, ref bool ShouldExecuteCommand)
        {
            if(!Text.StartsWith("/")) return;
            if (!Text.ToLower().Contains("rm rel")) return;

            var reloadMethod = AccessTools.Method(typeof(RocketPluginManager), "Reload");
            reloadMethod.Invoke(R.Plugins, null);

            UnturnedChat.Say("Plugins reloaded!", Color.green);
        }
        private void OnPlayerChatted(UnturnedPlayer player, ref Color color, string message, EChatMode chatMode, ref bool cancel)
        {
            if(player.IsAdmin) HandleInput(message, ref cancel);
        }
}
    [HarmonyPatch]
    public class HarmonyFix
    {
        public static int x = 0;
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RocketPluginManager),nameof(RocketPluginManager.LoadAssembliesFromDirectory))]
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
       
        [HarmonyPatch(typeof(RocketPlugin), MethodType.Constructor)]
        [HarmonyPrefix]
        public static bool RocketPluginPrefix(object __instance)
        {
            //Logger.Log("Patching RocketPlugin constructor...");

            var type = __instance.GetType();
            var assembly = type.Assembly;

            PrivateSet(__instance, "Assembly", assembly);

            var name = assembly.GetName().Name;
            int lastUnderscore = name.LastIndexOf('_');
            string unifiedName = lastUnderscore > -1 ? name.Substring(0, lastUnderscore) : name;

            PrivateSet(__instance, "Name", unifiedName);

            var directory = Path.Combine(Rocket.Core.Environment.PluginsDirectory, unifiedName);
            PrivateSet(__instance, "Directory", directory);

            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            
            //Translations
            var defaultTranslationsProp = AccessTools.Property(type, "DefaultTranslations");
            var defaultTranslations = defaultTranslationsProp?.GetValue(__instance) as TranslationList;

            if (defaultTranslations != null | defaultTranslations.Count() != 0)
            {
                var translationPath = Path.Combine(
                    directory,
                    string.Format(Rocket.Core.Environment.PluginTranslationFileTemplate, unifiedName, R.Settings.Instance.LanguageCode));

                var xmlAsset = new XMLFileAsset<TranslationList>(
                    translationPath,
                    new Type[]
                    {
                    typeof(TranslationList),
                    typeof(TranslationListEntry)
                    },
                    defaultTranslations!
                );

                var translationsField = AccessTools.Field(type, "translations");
                translationsField?.SetValue(__instance, xmlAsset);

                //Logger.Log("Translations loaded: " + translationsField?.GetValue(__instance) != null ? "Correctly" : "Not Correctly");

                defaultTranslations.AddUnknownEntries(xmlAsset);
            }

            var stateField = AccessTools.Field(type, "state");
            stateField?.SetValue(__instance, PluginState.Unloaded);

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
        static void PrivateSet<T>(object instance, string propName, T value)
        {
            var backingField = AccessTools.Field(instance.GetType(), $"<{propName}>k__BackingField");
            backingField?.SetValue(instance, value);
        }
    }
}
