using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.IO;
using UnityEngine;
using Verse;

namespace EightBitMode
{
    // Runs once per texture, at the moment RimWorld loads it from disk, so everything
    // downstream (graphics, materials, atlases, other mods' art) just sees the small texture.
    [HarmonyPatch]
    public static class Patch_LoadTexture
    {
        // Textures we already produced, so nested patched loaders never process one twice.
        private static readonly HashSet<int> done = new HashSet<int>();

        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(ModContentLoader<Texture2D>))
                .Where(m => m.ReturnType == typeof(Texture2D)
                         && !m.IsAbstract
                         && m.GetParameters().Length >= 1
                         && m.GetParameters()[0].ParameterType == typeof(VirtualFile))
                .Cast<MethodBase>();
        }

        // If the API changes in a future version, skip quietly instead of crashing.
        static bool Prepare()
        {
            var targets = TargetMethods().ToList();
            if (targets.Count == 0)
            {
                Log.Warning("[8-Bit Performance Mode] No texture loader methods found on ModContentLoader<Texture2D>; mod is inactive.");
                return false;
            }
            Log.Message("[8-Bit Performance Mode] Patching: " + string.Join(", ", targets.Select(m => m.Name).ToArray()));
            return true;
        }

        static void Postfix(VirtualFile __0, ref Texture2D __result)
        {
            Texture2D original = __result;
            Texture2D processed = Process(original, __0 == null ? null : __0.FullPath);
            if (processed != null && processed != original)
            {
                done.Add(processed.GetInstanceID());
                // Safe here: the loader just created `original` and nothing else holds it yet.
                Object.Destroy(original);
            }
            __result = processed;
        }

        internal static Texture2D Process(Texture2D texture, string path)
        {
            if (texture == null) return null;
            if (done.Contains(texture.GetInstanceID())) return texture;

            var settings = EightBitMod.Settings;
            if (settings == null || settings.preset == Preset.Off) return texture;

            if (path != null && !IsTextureFile(path.Replace('\\', '/'))) return texture;

            string name = path == null ? texture.name : System.IO.Path.GetFileNameWithoutExtension(path);
            return TextureProcessor.Process(texture, name, settings.preset);
        }

        internal static bool IsTextureFile(string path)
        {
            string extension = System.IO.Path.GetExtension(path);
            if (extension.Equals(".wav", System.StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ogg", System.StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".mp3", System.StringComparison.OrdinalIgnoreCase)) return false;

            return path.IndexOf("/Sounds/", System.StringComparison.OrdinalIgnoreCase) < 0
                && !path.StartsWith("Sounds/", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}