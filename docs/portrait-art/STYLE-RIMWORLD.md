# A style LoRA from the game's own art (rmwstyle)

The player, 22 November 2026: "maybe I do still want something that's closer to the Rimworld style, where how it draws,
for example, its own storytellers and DLC characters", then: "a LoRa based on that, that we can then load into the Comfy
UI for it to create images", with seven pictures of the game's art (the three storytellers, a fourth portrait in the same
manner, and three DLC scenes). The LoRA is trained on the player's own computer and used only in their own game: it
learns from the game artist's own work, so neither it nor the pictures made with it are published.

## The style, in words (what the seven share)

- **Lines**: confident black ink outlines, heavier along the outer silhouette and finer inside; fine ink lines on the
  face for the eye sockets, cheekbones, laugh lines, wrinkles and stubble (dots).
- **Shading**: clean cel shading in two or three tones with hard edges, a soft airbrushed gradient inside the larger
  shadows; crisp white highlights on metal, bionics and jewellery.
- **Colour**: saturated local colours on the clothing (a red coat, a green shirt, a purple jacket, a blue jacket) against
  warm skin shaded with pink and orange; coloured irises; in the scenes a deep blue space with stars and a planet.
- **Light**: a soft key light from the front; a cool cyan rim light along the edges in the scenes.
- **Faces**: semi-realistic and angular, never pretty-generic: defined brows, a clear nose line, small, sharply drawn
  eyes, knowing or weary expressions with character (a sly grin, a scowl, a tired stare).
- **Figure**: from the waist up or head and shoulders, three-quarter or front view, one hand often showing in a
  gesture; the storytellers cut out on a plain ground.

## The training set

25 pictures made from the seven (each portrait whole and its top part, on a dark and a light plain grey ground, one of
each mirrored; each scene whole, mirrored, and a crop around the figure), each with a caption of what it shows and the
trigger word `rmwstyle` first. The captions describe the content only, so the LoRA learns the drawing, not these people.
The pictures are not in the repository (they are the game's art); the script that cut them is in the lead's scratchpad.

## Training it with FluxGym (on the player's computer)

1. Install **Pinokio** (https://pinokio.co), open it, **Discover**, search **FluxGym**, **Install** (one click; it sets up
   its own Python). It downloads FLUX.1 Dev for itself the first time it trains: about 35 GB, so keep space free.
2. Start FluxGym from Pinokio. Fill in:
   - **The name of your LoRA**: `rmwstyle`; **Trigger word**: `rmwstyle`.
   - **Base model**: `flux-dev`; **VRAM**: `16G`.
   - **Repeat trains per image**: `6`; **Max train epochs**: `16` (about 2,400 steps; 1 to 2 hours on your card).
   - **Resize dataset images**: `512` (the pictures are about 600 pixels; larger only slows it down).
   - **Sample Image Prompts** (one per line), and **Sample Image Every N Steps**: `400`:
     - `rmwstyle, a weathered old man with a grey beard and a scar across his cheek, a patched brown coat, seen from the waist up, a plain dark grey background`
     - `rmwstyle, a young woman with short black hair and freckles, a green work jacket, arms crossed, a plain dark grey background`
     - `rmwstyle, a saurid man with fine green scales and amber eyes, a leather vest, a plain dark grey background`
3. **Upload** all 25 pictures and all 25 `.txt` files together (drag them in at once): the captions fill in by
   themselves (each `.txt` belongs to the picture of the same name). Check that each caption starts with `rmwstyle`.
4. In **Advanced options** set: `network_dim` (the LoRA's rank) `16`, `learning_rate` `4e-4`, `save_every_n_epochs` `4`.
   Leave the rest.
5. **Start training**. The sample pictures appear as it goes: they should look more and more like the game's art.
6. When it ends, the files are in FluxGym's `outputs\rmwstyle\` folder (Pinokio, FluxGym, Files): `rmwstyle.safetensors`
   (the last) and `rmwstyle-000004`, `-000008`, `-000012` (the earlier saves). The last is usually best; if its samples
   copy the seven people too closely, an earlier one is better.
7. Copy the chosen file into ComfyUI's `models\loras` and press R in ComfyUI.

## Using it

`batches/style-rmw.json`: the trigger word and the style above in words, the LoRA at 0.9, the usual frame (from the
waist up, cut out). Run `style-test` with it and compare with the five comic candidates. If the chosen file has another
name (an earlier save), change the name in `style-rmw.json`. Later: train again with 20 to 40 of our own best pictures
in this style added, so the LoRA leans less on the seven.
