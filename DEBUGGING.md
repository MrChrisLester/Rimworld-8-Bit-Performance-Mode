# Debugging Log

This document records the major implementation issues, attempted solutions, and architectural decisions made during development of the texture downscaling system for RimWorld.

The primary development constraint is maintaining compatibility with RimWorld's content-loading pipeline and Unity's texture ownership model. In particular, processing must not invalidate resources managed by RimWorld or Unity.

---

## Version 1: Broad LoadTexture Interception

### Observed Issue
- RimWorld sound resources were incorrectly being treated as textures.
- Errors referenced assets such as:
  - `UI/FlickWhoosh`
  - `Weapon/GunShotA`
- Sound-loading operations were being intercepted by a hook intended for image assets.

### Attempted Solution
- Introduced filtering for sound-related file extensions and directory paths.
- Restricted processing to files believed to contain image assets.

### Outcome
- Sound-related errors were substantially reduced or eliminated.
- Filtering proved overly restrictive, however, and prevented most textures from being processed.

### Conclusion
A broad interception strategy was insufficiently selective. Filtering by file path and extension alone also risked excluding legitimate texture assets.

---

## Version 2: Reflection-Based Loader Identification

### Observed Issue
- The referenced API package did not expose the expected `Verse.VirtualFile` type.
- Direct compilation against the initially assumed API type was unreliable.

### Attempted Solution
- Replaced direct method references with reflection-based inspection.
- Inspected the installed RimWorld runtime assembly to identify the actual type definitions and method signatures.

### Findings
The runtime inspection identified the correct type as:

`RimWorld.IO.VirtualFile`

The relevant texture-loading method was identified as:

`ModContentLoader<Texture2D>.LoadTextureViaImageConversion(VirtualFile)`

### Outcome
- Corrected the previously assumed namespace and type reference.
- Identified a more appropriate interception point within RimWorld's texture-loading pipeline.

### Conclusion
Runtime assembly inspection provided a more reliable representation of the installed game's API than relying exclusively on initially assumed or externally referenced type definitions.

---

## Version 3: Direct LoadTextureViaImageConversion Hook

### Observed Issue
- The original broad interception approach caused sound-resource failures.
- Filtering alone did not provide sufficiently reliable texture coverage.

### Attempted Solution
Introduced a Harmony postfix targeting:

`ModContentLoader<Texture2D>.LoadTextureViaImageConversion(VirtualFile)`

The implementation:
- Used the returned `Texture2D` as the input for texture processing.
- Continued excluding:
  - Sound-related file extensions.
  - Paths containing `Sounds`.
  - Null texture results.
  - Disabled processing presets and the `Off` preset.

### Outcome
- Game startup and sound loading became stable.
- Some PNG textures were successfully downscaled.
- Overall coverage remained limited:
  - Only a relatively small subset of modded assets was affected.
  - Most base-game assets were not processed.
  - Asset bundles and `.assets` content remained outside the hook's coverage.

### Conclusion
The direct image-conversion method provided a substantially safer interception point and demonstrated that texture processing could work without disrupting sound loading. However, it did not cover every texture-loading pathway used by RimWorld.

---

## Version 4: ContentFinder<Texture2D>.Get Hook

### Observed Issue
- The direct image-conversion hook did not intercept enough textures to achieve the desired coverage.
- Additional investigation suggested that some textures were being retrieved through RimWorld's content cache rather than the previously identified conversion method.

### Attempted Solution
Introduced an additional Harmony hook targeting:

`ContentFinder<Texture2D>.Get`

The intention was to process textures returned through RimWorld's content-retrieval system.

### Outcome
The implementation caused severe runtime problems, including:
- Missing UI textures.
- Missing-texture icons.
- `NullReferenceException` errors in the save-file dialog.
- Corrupted save-file UI.
- Scroll-view rendering problems.

The behavior indicated that modifying or replacing textures obtained through this pathway could invalidate resources relied upon by RimWorld's content cache and other UI systems.

### Resolution
- Removed the `ContentFinder<Texture2D>.Get` patch entirely.
- Stopped modifying or replacing cache-owned textures through this interception point.
- Also removed source texture destruction from `TextureProcessor` to eliminate another potential source of resource invalidation.

### Conclusion
Content retrieval and texture creation are not interchangeable interception points. A texture returned by a content cache may have existing references and ownership dependencies that make replacement unsafe.

---

## Version 5: Source Texture Destruction Removed

### Observed Issue
- The texture processor previously destroyed the original source texture after creating a replacement.
- This introduced the possibility of invalidating textures still referenced by RimWorld, Unity, or other internal systems.

### Attempted Solution
Removed the following operation:

`Object.Destroy(src);`

The processor now generates and returns a replacement texture without explicitly destroying the original source texture.

### Outcome
- Reduced the risk of invalidating cached or shared texture objects.
- Eliminated source destruction as a potential cause of cache-related UI failures.
- Increased the possibility of unused texture objects remaining in memory.

### Engineering Trade-off
Retaining the original source texture is a temporary safety measure.

Eventually, memory management will need to be addressed to prevent unnecessary texture retention. However, explicitly destroying source textures during the current iterative development phase presents a greater compatibility risk than temporarily allowing unused objects to remain allocated.

### Conclusion
Texture ownership must be established before introducing explicit destruction or cleanup. Memory optimization is deferred until the loading pipeline and resource lifecycle are better understood.

---

## Version 6: ModContentLoader<Texture2D>.LoadItem Hook

### Observed Issue
- The direct image-conversion method continued to provide insufficient asset coverage.
- A more general interception point within RimWorld's content-item loading system appeared potentially suitable.

### Attempted Solution
Introduced a Harmony hook targeting:

`ModContentLoader<Texture2D>.LoadItem`

The intention was to process textures after they had entered RimWorld's content-item loading system.

The implementation also investigated interactions with `LoadedContentItem` and related internal loading structures.

### Outcome
- RimWorld crashed during initialization.
- The implementation interacted unsafely with the internal content-loading and cache-ownership mechanisms.
- Mutating or replacing loaded content items resulted in startup failure.

### Resolution
- Removed the `LoadItem` patch.
- Removed reflection-based manipulation of `LoadedContentItem` internals.
- Reverted to the direct image-conversion hook.

### Conclusion
Although `LoadItem` offered a potentially broader interception point, its interaction with RimWorld's internal content-item lifecycle made it unsuitable for the current implementation.

Further investigation is necessary before attempting to modify resources at this stage of the loading pipeline.

---

# Current Implementation: Startup-Safe Direct Conversion Hook

## Active Interception Point

The current implementation exclusively patches:

`ModContentLoader<Texture2D>.LoadTextureViaImageConversion(VirtualFile)`

The postfix follows this processing sequence:

1. Receives the `Texture2D` returned by the original loader method.
2. Retrieves the source asset path through `VirtualFile.FullPath`.
3. Skips null texture results.
4. Checks whether texture processing is enabled.
5. Skips sound-related file extensions.
6. Skips paths containing `Sounds`.
7. Passes eligible textures to `TextureProcessor`.
8. Returns the processed replacement texture.

## Current Build Status

- Target framework: `.NET Framework 4.7.2`.
- Source code compiles successfully.
- The active DLL has been regenerated successfully.
- The compiled assembly contains only `Patch_LoadTexture` as its active texture-loading patch.
- The unsafe `ContentFinder<Texture2D>.Get` and `LoadItem` hooks have been removed.
- Source texture destruction has been removed from `TextureProcessor`.

## Current Stability Position

<ins>Startup stability takes priority over complete texture coverage.</ins>

The current implementation represents
