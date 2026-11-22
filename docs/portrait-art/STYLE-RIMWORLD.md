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

Round 2 (47 pictures, from 18 characters), each with a caption of what it shows and the trigger word `rmwstyle` first.
The captions describe the content only, so the LoRA learns the drawing, not these people.
- The three storytellers at full size (2,500 pixels, from the RimWorld wiki; the player's copies were 560): each whole on
  a dark and a light plain grey ground (one mirrored), its head and shoulders, and a close-up of the face, where the fine
  ink lines and the hatching show at full detail.
- The fourth portrait (the bald man with the bionic headset), as in round 1: whole and its top part, dark and light.
- The three DLC pictures (Royalty, Biotech, Ideology), as in round 1: whole, mirrored, and a crop around the figure.
- Eleven portraits from the storyteller mods in the player's list (screenshots of the storyteller screen, on its
  near-black ground): each whole, and its head and shoulders mirrored (not the one with writing on its armour).
  Three of the fourteen the player sent are left out: two women drawn closer to an anime pin-up, with large glossy eyes
  and idealised faces (the look the player doesn't want), and a second faceless armoured soldier like one already in.

Not used: the wiki's and Steam's Biotech, Anomaly and Odyssey pictures are screenshots of the game or small banners.
More that would help: the Anomaly and Odyssey key art at full size (the woman with long brown hair; the red-bearded man
with the raven), each a new face; and any other mod portrait drawn this way with an ordinary, characterful face.
The pictures are not in the repository (they are the game's and the mods' art); the script that cut them is in the
lead's scratchpad (`build2.py`).

## Training it with FluxGym (on the player's computer)

1. Install **Pinokio** (https://pinokio.co), open it, **Discover**, search **FluxGym**, **Install** (one click; it sets up
   its own Python). It downloads FLUX.1 Dev for itself the first time it trains: about 35 GB, so keep space free.
2. Start FluxGym from Pinokio. Fill in:
   - **The name of your LoRA**: `rmwstyle` (the name of the file it makes, which `style-rmw.json` asks for);
     **Trigger word/sentence**: `rmwstyle`.
   - **Base model**: `flux-dev`; **VRAM**: `16G` (not 20G: that needs more memory than your card has).
   - **Expected training steps** fills in by itself once the pictures are in: it should read 2256.
   - **Repeat trains per image**: `4`; **Max train epochs**: `12` (47 pictures: about 2,250 steps; a guess of 1 to 3
     hours on your card).
   - **Resize dataset images**: `512` (fast; `768` keeps more of the fine lines but takes about twice as long).
   - **Sample Image Prompts** (one per line; `--d 42` keeps the seed, so each round shows the same picture changing),
     and **Sample Image Every N Steps**: `400`:
     - `rmwstyle, a weathered old man with a grey beard and a scar across his cheek, a patched brown coat, seen from the waist up, a plain dark grey background --d 42`
     - `rmwstyle, a young woman with short black hair and freckles, a green work jacket, arms crossed, a plain dark grey background --d 42`
     - `rmwstyle, a saurid man with fine green scales and amber eyes, a leather vest, a plain dark grey background --d 42`
3. **Upload** all 47 pictures and all 47 `.txt` files together (drag them in at once): the captions fill in by
   themselves (each `.txt` belongs to the picture of the same name). Check that each caption starts with `rmwstyle`.
   Don't press "Add AI captions with Florence-2": it would write over the captions.
4. Open **Advanced options** (under the fields of step 1) and set **Learning Rate** `4e-4`, **Save every N epochs** `4`
   and **LoRA Rank** `16`. Leave the rest. The **Train script** on the right shows the settings it will use: it should
   now say `--network_dim 16`, `--learning_rate 4e-4` and `--max_train_epochs 12`, and the **Train config** below it
   `num_repeats = 4`.
5. **Start training**. The sample pictures appear as it goes: they should look more and more like the game's art.
6. When it ends, the files are in FluxGym's `outputs\rmwstyle\` folder (Pinokio, FluxGym, Files): `rmwstyle.safetensors`
   (the last) and `rmwstyle-000004`, `-000008` (the earlier saves). The last is usually best; if its samples copy the
   training faces too closely (Randy's grin, Cassandra's face), an earlier one is better.
7. Copy the chosen file into ComfyUI's `models\loras` and press R in ComfyUI.

## Using it

`batches/style-rmw.json`: the trigger word and the style above in words, the LoRA at 0.9, the usual frame (from the
waist up, cut out). Run `style-test` with it and compare with the five comic candidates. If the chosen file has another
name (an earlier save), change the name in `style-rmw.json`. Later: train again with 20 to 40 of our own best pictures
in this style added, so the LoRA leans less on the seven.
