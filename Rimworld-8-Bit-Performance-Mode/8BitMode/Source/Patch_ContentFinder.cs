using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EightBitMode
{
    // Safety net: anything that reaches the game through ContentFinder without having been
    // shrunk at load time (e.g. official content) gets shrunk here, once, then cached.
    [HarmonyPatch]
    public static class Patch_ContentFinder
    {
        // Textures already handled by Patch_LoadTexture.
        public static readonly HashSet<Texture2D> Processed = new HashSet<Texture2D>();
        private static readonly Dictionary<Texture2D, Texture2D> cache = new Dictionary<Texture2D, Texture2D>();
        private static int logged;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ContentFinder<Texture2D>), "Get", new[] { typeof(string), typeof(bool) });
        }

        static bool Prepare()
        {
            if (TargetMethod() == null)
            {
                Log.Warning("[8-Bit Performance Mode] Could not find ContentFinder<Texture2D>.Get; fallback patch inactive.");
                return false;
            }
            return true;
        }

		static void Postfix(string itemPath, ref object __result)
		{
			// This patch also fires for ContentFinder<AudioClip> etc., so only touch real textures.
			Texture2D tex = __result as Texture2D;
			if (tex == null) return;

			var settings = EightBitMod.Settings;
			if (settings == null || settings.preset == Preset.Off) return;
			if (Processed.Contains(tex)) return;
			if (itemPath != null && itemPath.StartsWith("UI/")) return; // keep UI crisp

			Texture2D done;
			if (!cache.TryGetValue(tex, out done))
			{
				if (logged < 5)
				{
					logged++;
					Log.Message("[8-Bit] late: " + itemPath + " (" + tex.width + "x" + tex.height + ")");
				}
				string name = System.IO.Path.GetFileName(itemPath ?? tex.name);
				// Don't destroy the original: the game's content holder still points at it.
				done = TextureProcessor.Process(tex, name, settings.preset, false);
				cache[tex] = done;
				Processed.Add(done);
			}
			__result = done;
		}
	}
}