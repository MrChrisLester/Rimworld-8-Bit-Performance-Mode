# Rimworld-8-Bit-Performance-Mode
Low performance mode for RimWorld 1.6. Downscales textures as they load (and optionally reduces color depth) so the game runs on very low-end hardware, with a chunky 8-bit look.

Presets: Off, Half, Quarter, 8-bit (32px / 256 colors), 4-bit (16px / 16 colors). Restart required after changing.

## Build
Requires the .NET SDK. From `8BitMode/Source`: `dotnet build -c Release`. Output goes to `8BitMode/1.6/Assemblies/`.
Copy the `8BitMode` folder into RimWorld's `Mods` folder. Requires the Harmony mod.
