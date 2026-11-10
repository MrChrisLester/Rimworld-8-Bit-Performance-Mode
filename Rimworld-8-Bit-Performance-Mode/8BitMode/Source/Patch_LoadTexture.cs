using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EightBitMode
{
    // Runs once per texture, at the moment RimWorld loads it from disk, so everything
    // downstream (graphics, materials, atlases, other mods' art) just sees the small texture.
    [HarmonyPatch]
    public static class Patch_LoadTexture
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(ModContentLoader<Texture2D>))
                .FirstOrDefault(m => m.Name == "LoadTexture");
        }

        // If the method isn't there in some future version, skip quietly instead of crashing.
        static bool Prepare()
        {
            if (TargetMethod() == null)
            {
                Log.Warning("[8-Bit Performance Mode] Could not find ModContentLoader<Texture2D>.LoadTexture; mod is inactive.");
                return false;
            }
            return true;
        }

        // __0 = the method's first argument (the file), taken as object so we don't need its type name.
        static void Postfix(object __0, ref Texture2D __result)
        {
            if (__result == null) return;
            var settings = EightBitMod.Settings;
            if (settings == null || settings.preset == Preset.Off) return;

            string path = null;
            if (__0 != null)
            {
                path = Traverse.Create(__0).Property("FullPath").GetValue<string>();
                if (path == null) path = __0.ToString();
            }
            path = (path ?? "").Replace('\\', '/');

            if (path.Contains("/Textures/UI/")) return; // keep UI crisp

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(name)) name = __result.name;
            __result = TextureProcessor.Process(__result, name, settings.preset);
            Patch_ContentFinder.Processed.Add(__result);
        }
    }
}