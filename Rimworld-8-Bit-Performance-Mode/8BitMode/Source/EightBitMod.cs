using HarmonyLib;
using UnityEngine;
using Verse;

namespace EightBitMode
{
    public class EightBitMod : Mod
    {
        public static EightBitSettings Settings;

        public EightBitMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<EightBitSettings>();
            new Harmony("YewChicken.8BitPerformanceMode").PatchAll();
        }

        public override string SettingsCategory() => "8-Bit Performance Mode";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}
