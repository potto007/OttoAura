# Wisp Torch fog design

Sub-feature folder `WispFog/`, namespace `OttoAura.WispFog`.

## Premise

A lit Wisp Torch clears the Mists inside its radius, but the distance fog stays, so a
torch-lit camp still looks washed out. OttoAura clears the fog over the same ground: the
radius is the torch's `Demister.m_forceField.endRange`, the value `ParticleMist.InsideDemister`
uses for the Mists.

## Where the fog comes from

The main camera in `main.unity` renders deferred, with HDR. Deferred shading does not apply
Unity's per-shader fog to opaque surfaces, so their fog comes from one full-screen pass:
Post-processing Stack v1's `FogComponent`, shader `Hidden/Post FX/Fog`, at
`CameraEvent.AfterImageEffectsOpaque`. `PostProcessingBehaviour` clears and refills that
command buffer every frame, which is the hook.

Transparent objects render after this pass and fog themselves in their own shaders, so water,
particles and the Mists keep their haze. The skybox is excluded by the game's own pass.

## The pass

`FogComponentPopulatePatch` prefixes `FogComponent.PopulateCommandBuffer`. When the feature is
on and at least one lit Wisp Torch is in range, `FogBubbles.TryPopulate` fills the buffer
itself and the game's method is skipped; otherwise the game's method runs unchanged.

1. Copy the camera target to `_TempRT`, as the game does. This is the scene before fog.
2. Run the game's fog material, set up exactly as the game sets it up, from `_TempRT` into a
   second temporary, `_OttoAuraFogged`.
3. Blit `_OttoAuraFogged` to the camera target through `Hidden/OttoAura/FogBubble`, which
   reads `_TempRT` as `_OttoAuraPreFog`.

For each pixel the bubble shader rebuilds the world-space view ray from the depth buffer and
the four frustum-corner directions, sums the length of the ray inside each torch sphere, and
works out how much fog the remaining outside stretch would build up:

```
kept = (1 - T(d * (1 - inside / rayLength))) / (1 - T(d))
out  = lerp(preFog, fogged, kept)
```

`T` is the stack's transmittance for the active fog mode. Blending between the two images
instead of recomputing fog keeps the game's fog colour, including its sun glow, which the
mod never has to reproduce. A small mismatch between this `T` and the game's own only changes
how much fog is removed, never the colour.

The same formulas live in `FogBubbleMath`, which the unit tests cover. Change both together.

Up to 16 torches, nearest to the camera first, go to the shader as `float4(center, radius)`.
Only demisters on a built `Piece` count, so the Wisplight a player carries clears nothing.

## The shader bundle

Unity cannot compile a shader at runtime, so `unity/OttoAuraShaders` is a minimal Unity
project that builds `assets/ottoaura_fogbubble.bundle` for Direct3D 11, Direct3D 12 and
Vulkan. The editor version in `ProjectSettings/ProjectVersion.txt` must match the engine
version in Valheim's `globalgamemanagers`, 6000.0.75f1 for Valheim 1.0.16.

```
tools/build-fog-bubble-bundle.sh
```

The script copies the project to a Windows temp folder, runs the Windows editor in batch mode,
and writes the bundle into `assets/`. Commit the bundle: the mod build embeds it, and CI has no
Unity editor. A build without the bundle still loads; the feature logs one warning and stays
off. So does a graphics API the shader cannot run on, and a dedicated server never loads it.

After a Valheim update that changes the engine version, bump `ProjectVersion.txt` and rebuild
the bundle.
