# Tuning the workflow: one artist's hand, and faces of their own

The player, 22 November 2026: "we still have exactly more or less the same problem. So the classic AI face. And also just
general inconsistency within the style ... maybe by adjusting the workflow ... It should ideally be that each image was
kind of created by the same artist or studio ... very consistent in their style, but of course have a lot of variety in
what they actually image."

Why the plain workflow can't do it: FLUX.1 Dev ignores the negative prompt (it was trained so that the usual guidance is
built in), so "not pretty, not glamorous" changes nothing, and with words alone every subject pulls the drawing its own
way (a reptile towards a creature painting, an elf towards fantasy art). What does it, in order:

1. **Steady settings** (lower guidance: less of the glossy AI look and closer to the style words).
2. **A style LoRA**: a small add-on file that teaches the model one artist's hand; every picture made with it looks like
   the same studio's, whatever the prompt asks to be drawn. This is the main step.
3. **A same-face fix**: a LoRA made to push faces away from FLUX's favourite face (used at a negative strength), or a
   model that obeys a negative prompt (Chroma).
4. **Our own studio LoRA**, trained on 25 to 40 pictures we've chosen: the look fixed for good, for every kind, age and
   look the library will ever need.

The batch tool now sets all of this from the style files, so a test of settings is a new style file, not a new workflow:
`guidance`, `steps`, `cfg`, `sampler_name`, `scheduler`, and `loras` (the workflow's LoRA boxes in order, each a file
name and a strength). The `style-G1`, `G2`, `G3` files are F1 to F3 with steady settings and two LoRA slots.

## Step 1: two LoRA boxes in the workflow (once, 5 minutes)

1. Open your Rimfolk workflow in ComfyUI (the FLUX template you exported before).
2. Double-click an empty spot of the canvas, type `LoraLoaderModelOnly`, click it. Do it again for a second one.
3. Find the box that loads the model ("Load Diffusion Model" or "Load Checkpoint"). Drag from its purple MODEL dot to the
   first LoRA box's `model` input; from the first LoRA box's MODEL output to the second's `model` input; and from the
   second's MODEL output to the input the model went to before (the KSampler's `model`, or "ModelSamplingFlux"). A new
   wire into an input replaces the old one.
4. Once you have LoRA files (step 2), pick any in each box; the style files will set the real ones.
5. **Workflow, Export (API)**, over `rimfolk-workflow-api.json`.

## Step 2: get a style LoRA and the same-face fix (15 minutes)

1. Make a free account on https://civitai.com (needed for some downloads).
2. Search for Flux LoRAs: filters **Model type: LoRA** and **Base model: Flux.1 D** (not FLUX.2, not SDXL: they don't load
   in this workflow). Search words: `comic`, `ink`, `cel shading`, `graphic novel`. Prefer ones with many downloads, good
   reviews and portraits in their examples. Some found for a start (check each page yourself): "yfg danimated flux" (pulp
   comic portraits, no trigger word), "comicbook-flux", and on Hugging Face "Wizard's Pen and Ink". "personal comicks
   FLUX2" is for FLUX.2 and won't work with FLUX.1.
3. The same-face fix: search Civitai for **"SameFace Fix" Flux LoRA**; its page says to use it at a negative strength.
4. Put the downloaded `.safetensors` files in ComfyUI's `models\loras` folder (ComfyUI Desktop: `Documents\ComfyUI\models\
   loras`), then press `R` in ComfyUI (or restart) so it sees them.
5. In `batches\style-G1.json` replace `PUT-YOUR-COMIC-STYLE-LORA-FILE-NAME-HERE.safetensors` and
   `PUT-THE-SAMEFACE-FIX-LORA-FILE-NAME-HERE.safetensors` with the files' exact names (or tell the lead the names and get
   the files back). If a style LoRA has a trigger word, add it at the start of `style`.
6. Run `colonists` with `style-G1`. Then try other strengths (copy the file to `style-G1b.json` and change `0.8` to `0.6`
   or `1.0`, and `-0.7` to `-0.5` or `-1.0`) and other style LoRAs, and send the folders.

### The lead's picks (22 November 2026, from Civitai's own listing of Flux.1 D LoRAs, 373 looked at)

Closest to the game's key art, each with a ready style file (`batches/style-L1` to `L4`, which also set the trigger
words, strengths and sampler):

| Style file | LoRA (Civitai page) | File to download (the Flux.1 D version) | Trigger | Strength |
|---|---|---|---|---|
| L1 | Eldritch Comics, https://civitai.com/models/671064 (version v1.1) | `Eldritch_Comics_for_Flux_1.1.safetensors` | illustration | 0.9 |
| L2 | COMMIX, https://civitai.com/models/879845 (version "COMMIX (FLUX)") | `commixv3-f.safetensors` | A illustration in the commix style | 1.0 (its page: 0.8 to 2.2) |
| L3 | Comic Book Realism - CE, https://civitai.com/models/1812625 (version "V01a - Flux.1 D") | `ComicRealism01a_CE_FLUX_AIT2k.safetensors` | cmcrlsmCE_style, comic book realism | 0.9 |
| L4 | Comic Book Tome 3, https://civitai.com/models/1764315 (version V2) | `Comics_Factory_2.safetensors` | ComicsFact2 style | 0.8 (its page: 0.6 to 1.0, dpmpp_2m, sgm_uniform, 20 steps, guidance 3.5) |

For the second LoRA box, all four use the **Flux Facial Features Softener**, https://civitai.com/models/907580 (version
"FFS Omni v.6", file `FFS_v.6.safetensors`, strength 0.6): made against FLUX's own face (cleft chin, heavy jaw, blush,
full lips) and meant to keep art styles intact. The SameFace Fix (https://civitai.com/models/766608) is for women's faces
only (its page), so it's left out of the mixed library. Not chosen: vintage underground comics (too cartoony), the painted
and abstract "Tome 1" sets.

### More picks, not comic (the player: "not necessarily that much on the comic style ... appropriate for a RimWorld ...
not too realistic ... still be a game style")

From a second search (729 Flux.1 D LoRAs: painterly, stylised, game, concept, splash and card art, gouache, watercolour,
storybook, flat colour, vector, sketch, retro sci-fi, pulp and more; the examples seen), each with a style file:

| Style file | LoRA (Civitai page) | File (the Flux.1 D version) | Trigger | Strength | Look |
|---|---|---|---|---|---|
| L5 | D&D Painterly, https://civitai.com/models/2140031 (version "Flux") | `DD_Painterly-000005.safetensors` | D&D Painterly, | 0.9 | painted tabletop character art, clean shapes, muted colours |
| L6 | Retro Dark Fantasy Illustration, https://civitai.com/models/1041229 (version "Flux") | `dark_fantasy_illustration_Flux-000031.safetensors` | dark_fantasy_illustration | 1.0 (its page: 0.8 to 1.2) | old RPG-book illustration, limited palette, print texture |
| L7 | Graphic portrait, https://civitai.com/models/170039 (version "FLUX") | `Graphic Portrait FLUX.safetensors` | raw illustration, Drawing | 0.8 | flat, sketchy pop-art portraits (likes round pink cheeks) |
| L8 | Shrekman Style MIX, https://civitai.com/models/707010 (version "PaintedComic-Flux") | `PaintedComicFLUX-000015.safetensors` | PaintedComicstyleV1 | 0.9 | warm painted cartoon, cosier than gritty |

Left out: too realistic (Painterly Fantasy, the fantasy digital-painting sets, Mythic Fantasy), too surreal (Daubrez),
anime looks, minimal vector silhouettes, and the Fallout pack (no people in its examples).

### Z-Image Turbo, a second model (22 November 2026)

Comic Book Tome 3's newer versions, V3 and V4, whose examples fit the game best (inked European-comic portraits with
washed colour and faces with character), are made for **Z-Image Turbo**, not FLUX; its V1 and V2 are for FLUX. Z-Image
Turbo is a newer, fast model (about 8 steps a picture) that ComfyUI supports, with a licence that allows publishing. To
use it as a second workflow:
1. **Browse Templates**, search `Z-Image`, open the Z-Image Turbo text-to-image template and let it download its files.
2. If it is one bundled box, unpack it (right-click, Unpack Subgraph) as before; type `%PROMPT%` in its prompt box.
3. Add one `LoraLoaderModelOnly` box between its Load Diffusion Model and the box the model went to.
4. **Export (API)** as `rimfolk-workflow-zimage.json` next to the other one; the batch tool asks which to use.
5. Put Tome 3's V4 file (`Comic Book V4T3_E10.safetensors`) and, to compare, V3 (`comics_factory_V3.safetensors`) in
   `models\loras`; run `colonists` with `style-Z4` (V4: trigger `Bradhamel art style`, 0.75, 8 steps, heunpp2, normal)
   or `style-Z3` (V3: `ComFact_V3 art style`, 0.75, 8 steps, heunpp2, beta), choosing the Z-Image workflow.
FLUX LoRAs (the SameFace Fix, the softener) don't work with Z-Image Turbo.

## Step 3 (if faces stay alike): Chroma, a model that obeys a negative prompt

1. In ComfyUI, **Browse Templates**, find **Chroma** (text to image), let it download its files.
2. Type `%PROMPT%` in its prompt box and `%NEGATIVE%` in its negative box, size 1024 x 1024.
3. **Export (API)** as `rimfolk-workflow-chroma.json` in the same folder. The batch tool then asks which workflow to use.
4. Run `colonists` with `style-H1` (F1's words; CFG 3.8, 30 steps; the negative words now count).
Chroma's licence (Apache 2.0) allows publishing the pictures. Flux LoRAs don't always carry over to Chroma.

## Step 4: our own studio LoRA (the look fixed for good)

Once 25 to 40 pictures are chosen (people of every kind, age and look the library needs, all in the look we want), the
lead checks and writes a caption for each (what it shows, not the style, plus a trigger word such as `rmfk style`), and
the player trains the LoRA on their own card with FluxGym (installed in one click through Pinokio, https://pinokio.co; it
has a setting for 16 GB cards; about 1 to 2 hours). Every later batch uses it at about 0.9, and a new kind, age or look
comes out in the same hand. The lead writes the exact steps when the pictures are chosen.
