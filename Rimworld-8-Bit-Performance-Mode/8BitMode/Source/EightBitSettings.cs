using UnityEngine;
using Verse;

namespace EightBitMode
{
    public enum Preset { Off, Half, Quarter, EightBit, FourBit }

    public class EightBitSettings : ModSettings
    {
        public Preset preset = Preset.EightBit;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref preset, "preset", Preset.EightBit);
        }

        public void DoWindowContents(Rect rect)
        {
            var list = new Listing_Standard();
            list.Begin(rect);

            list.Label("Graphics preset (restart the game after changing):");
            list.Gap(6f);
            Option(list, Preset.Off, "Off (vanilla graphics)", "No changes.");
            Option(list, Preset.Half, "Half resolution", "Textures at 1/2 size, full color.");
            Option(list, Preset.Quarter, "Quarter resolution", "Textures at 1/4 size, full color.");
            Option(list, Preset.EightBit, "8-bit (32px, 256 colors)", "Longest side capped at 32px, RGB 3-3-2 color.");
            Option(list, Preset.FourBit, "4-bit (16px, 16 colors)", "Longest side capped at 16px, RGB 1-2-1 color. Very chunky.");

            list.End();
        }

        private void Option(Listing_Standard list, Preset p, string label, string tip)
        {
            if (list.RadioButton(label, preset == p, 0f, tip))
                preset = p;
        }
    }
}
