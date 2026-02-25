GPU Infinite Grass v.1.1.0
Find this asset here:
Patreon: https://www.patreon.com/posts/150937086/
Booth: https://redsim.booth.pm/items/7997362

Technical Support: https://discord.gg/TCRAUBs

# GPU Grass - Usage Guide (EN)

This guide explains how the asset works, what each feature controls, and which small details are worth paying attention to while tuning.

## Quick Setup (Basic)

1. Drag `Grass Particle Surface Manager` prefab into your scene.
2. Assign your ground surface mesh objects to `Surfaces` list.
3. Use `Paint` tool in the manager inspector to paint grass on your meshes. Note that your mesh should have enough vertices to paint on!
4. Tweak `Draw Amount` and `Draw Distance` values to control the grass density and render distance.
5. Set up your own grass textures and tune the final look and behavior in the grass material at the bottom of the `Particle Surface Manager`.


## Manager Settings

### Surface Settings

- `Surface Camera`: system camera that renders the Surface Mask texture. You don't have to do anything with this camera; it just needs to be assigned and have a render texture target. It doesn't really matter where this camera faces or where it's placed, because it is only used for rendering the heightmap and color masks required by the system. (but don't move it too far away from the world center)
- `Camera Layer`: culling layer used by the surface camera. It should be an empty layer, otherwise the grass could be rendered incorrectly and add extra rendering overhead.
- `Mask Material`: defines how surface mesh data is encoded into the Surface Mask Render Texture. You should usually use it with the `Surface Mask` material that comes with this asset by default, unless you want to make your own special grass rendering logic.
- `Surfaces`: list of target `MeshFilter` objects to draw the grass on top. This asset doesn't support rendering multiple vertical grass levels, unless you use several `Particle Surface Manager` objects for each level.
- `Draw Distance`: controls the radius around the player where grass will be rendered.
- `Target Override`: switches grass rendering center from the player camera to a fixed/moving transform. If you have a relatively small world, you can target the world center and keep grass static this way, so the visible grass region will not follow the player anymore.
- `Always Update Surface`: forces mask redraw every frame. Useful only when surface shape or mask data changes dynamically. If this option is turned off, the Surface Mask Render Texture is redrawn only when the player moves, saving performance.
- `Surface RT Resolution`: Surface Mask Render Texture size. Can be set to `Custom` to configure it manually in the render texture properties. Still be sure that the aspect ratio is 1:1, otherwise the grass will be rendered wrong! Use smaller resolution for mobile to save performance.

### Trail Settings

- `Enable Trail`: You can disable the trail feature to save some performance. It also disables it in the grass shader.
- `Trail Material`: writes trail information. You don't really need to change this material to anything custom, just leave it with the `Trail` material, as it is in the provided prefab by default.
- `Trail CRT`: persistent Custom Render Texture storing grass bend vectors, decaying over time.
- `Trail Decay`: fade speed of existing trail data.
- `Trail Targets`: additional moving transforms that affect grass. Their scale affects bending radius.
- `Trail CRT Resolution`: Trail Custom Render Texture size. Can be set to `Custom` to configure it manually in the render texture properties. Still be sure that the aspect ratio is 1:1, otherwise the grass will be rendered wrong! Use smaller resolution for mobile to save performance.

### Particle Settings

- `Particle Material`: Main Grass material.
- `Rendering Layer`: layer the grass is rendered on. Can be any visible layer actually.
- `Cast Shadows` / `Receive Shadows`: realtime shadow features.
- `Draw Amount`: number of rendered grass batches, with 16383 grass quads per batch. This means that the minimum render count is 16383 particles.

## Surface Painting

Painting edits vertex colors on surface meshes:
- `R`, `G`, `B` channels map to `Grass Type R/G/B` in the shader.
- Painted intensity affects type presence and size behavior.
- Multiple active channels in one area allow mixed grass types.
- Don't forget to enable required grass channels in the grass shader and disable the ones you don't need to save performance.

Tools:
- `Brush`: local paint with soft falloff.
- `Eraser`: removes the selected channel with soft falloff.
- `Fill`: flood-fills a connected mesh region from the hit point.

Important behavior:
- On first paint, the tool creates a mesh copy (`*_Painted`) so source meshes stay untouched.
- Paint detail is limited by vertex density. Smooth painting needs enough vertices in the target area.

## Grass Material

### Grass Type R / G / B
Each type is an independent style block:
- enable/disable per type to save performance
- choose texture source mode (`Single Texture`, `Array Random`, `Array By Size`) `Array By Size` ties texture selection to blade size progression, while `Array Random` focuses on variation.
- tune color gradient, blade shape, randomness, local wind response and more

### Common

- `Visible Amount`: global density visibility control.
- `Cutoff`: alpha clip threshold for blade textures.
- `Bottom Blending`: softens the blade-to-ground transition.
- `Mask Threshold`: minimum mask level required to spawn grass.
- `Size Threshold`: removes very small blades after scaling.
- `YBias`: global vertical offset of grass.
- `Enable Triple Cross`: renders each particle as a three-blade cluster.

### Grass Simplifying

- `Fade`: scales down grass toward area borders.
- `Culling`: increases random culling toward borders.
- `Simplifying`: starts distant simplification earlier/later.
- `Simplifying Fade`: controls how soft the simplification transition is.

### Trail

- `Trail Brightness`: color darkening on affected grass.
- `Trail Bend`: bend strength from trail data.


### Clouds / Subsurface Scattering / Wind

- `Clouds`: animated lighting modulation over the grass surface. Not always what you need, because cloud shadows should also be implemented in the terrain shader and synced with the grass shader.
- `Subsurface Scattering`: adds a backlit/translucent look when looking toward a light source. Also takes VRC Light Volumes lighting into account.
- `Wind`: layered world-space wind motion.

### Advanced

- `ShadowPass Depth Write`: controls depth writing in the shadow caster pass.
- `Render Queue`: only change this if you understand the sorting implications in your scene.

## Optimization Tips

Use this order when reducing cost:

1. Adjust `DrawAmount` first. Larger values increase vertex count and overdrawing at the same time
2. Use simplification/fade/culling controls to keep balance between quality and performance
3. Keep overall grass density smaller (DrawAmount/RenderDistance ratio)
4. Disable all the unused grass features in the grass material
5. Disable expensive lighting options (`CastShadows` / `ReceiveShadows`) if your scene uses realtime shadows and you don't need shadows for your grass.
6. Keep all the Render Textures small, especially for mobile and Quest. (512x512 is probably the max size I recommend for Quest. Smaller is still better.)

- Inspector performance indicators are guidance tools, not final profiling results. Don't fully believe them, always manually check your scene performance!
- Assign only meshes that truly need grass drawing on top.
- Validate visual quality and performance on your real target platform.

## Troubleshooting

If grass is missing or inconsistent, check:

- `Surfaces` is not empty.
- Grass types (`Enable Type R/G/B`) are actually enabled in the material.
- `SurfaceCamera`, `MaskMaterial`, `ParticleMaterial`, and RT references are valid.
- `MaskThreshold` / `SizeThreshold` are not effectively filtering everything.
- You set up the right rendering layers.
- Grass is not painted after being uploaded to VRChat: go to `Project Settings -> Player -> Optimize Mesh Data` and turn it off.
- Seeing a striped grass pattern or noise-like pattern: this is the example Surface Mask material from the default example scene. Select the current Surface Mask material in `Particle Surface Manager` and remove the grass pattern texture from it.

## Extra Tips

This asset forces the Editor to render Scene View at your monitor's refresh rate. This is intentional and helps fix performance issues from background apps that can remain when this feature is disabled and your scene has a lot of grass.
You can undo these changes manually: `Preferences -> General -> Interaction Mode -> Default`

This asset also turns off the `Optimize Mesh Data` feature in your Project Settings. This Unity feature removes unused vertex attributes from all meshes. GPU Grass uses the Vertex Color attribute, which Unity can remove when this feature is enabled.
You can enable it back manually: `Project Settings -> Player -> Optimize Mesh Data`

## How the Asset Works

- Mesh generation and painting:
  - Editor script generates a copy of a specified surface mesh or terrain to leave original untouched
  - User paints grass mask that is stored as RGB vertex color of the surface mesh
- Surface mask pass:
  - A dedicated orthographic camera renders the assigned surface meshes with `MaskMaterial` into a render texture.
  - Mask RGB comes from vertex color and defines grass type distribution.
  - Mask A stores world-height information used to place particles vertically.
- Optional trail interaction pass:
  - `TrailCRT` stores dynamic bend vectors.
  - `TrailMaterial` writes interaction from players/targets into that texture.
  - The grass shader reads it to bend and dim touched grass.
- Grass particle render pass:
  - Grass is rendered as GPU-instanced particles using `ParticleMaterial`.
  - Grass is generated around the active render center (player camera or `TargetOverride`).
  - Grass generation order follows the hexagonal spiral shape to have the correct sorting, reducing overdrawing.
