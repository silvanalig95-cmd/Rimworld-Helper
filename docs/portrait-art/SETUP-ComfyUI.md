# Making the portraits on your own computer (ComfyUI)

Your graphics card, an NVIDIA GeForce RTX 4070 Ti SUPER with 16 GB, is well suited: it runs the strong open image models
at full size, about 10 to 25 seconds a picture with FLUX (a few hundred an hour), a few seconds with SDXL models.
Everything below is free, runs offline once set up, and has no daily limit.

## 1. Install (about 30 minutes, most of it downloading)

1. Update the graphics driver: the NVIDIA app (or GeForce Experience), Drivers, install the newest Game Ready or Studio
   driver.
2. Download **ComfyUI Desktop** for Windows from https://www.comfy.org/download and run the installer.
   - Choose **NVIDIA** when it asks for your graphics.
   - Put it on a drive with **60 GB free** (the models are large).
   - It installs its own Python; you don't need to install anything else.
3. Start ComfyUI. A canvas with boxes (nodes) opens: that's a "workflow".

## 2. A model

In ComfyUI: the menu **Workflow**, **Browse Templates** (or the Templates button on the left), open the **Flux** section and
pick the template for **FLUX.1 Dev** (the "fp8" one if offered). ComfyUI says which model files are missing and offers to
download them: let it (about 17 GB). If a newer model family is listed there, it's fine to try it too; the rest of these
steps don't change.

- FLUX.1 Dev: the best at following long descriptions, which is what the portraits need. Its licence allows personal use;
  if you ever want to publish the pictures in a public mod, use **FLUX.1 Schnell** instead (free to publish, faster,
  slightly plainer pictures).
- Test it: type something in the prompt box (the long text box), press **Run** (or Queue). The picture appears on the
  right in 10 to 30 seconds the first time a little longer.

## 3. Make it ready for Rimfolk's batches (once)

1. In the template's prompt box, delete the text and type exactly `%PROMPT%`.
   If the workflow has a second, negative prompt box, type `%NEGATIVE%` there.
2. Set the picture size to **1024 x 1024** (the "Empty Latent Image" box, or the size box in the template).
   (The batch tool sets the size from the style file anyway.)
3. Two LoRA boxes, so the styles' LoRAs have somewhere to go: double-click an empty spot, type `Load LoRA` and add it,
   twice. Wire them in a chain between the model and everything that used it: the model loader's MODEL and CLIP
   outputs into the first LoRA box's model and clip inputs, the first box's MODEL and CLIP outputs into the second's,
   and the second box's MODEL output to where the loader's MODEL went (the KSampler, or a ModelSamplingFlux box), its
   CLIP output to the prompt box's clip input (drag each new wire onto the same input; it replaces the old one). Pick
   any LoRA in them: the batch tool sets the LoRA names and strengths from the style file for every picture, and turns
   a box the style doesn't use down to 0.
4. Save the workflow for the batches: menu **Workflow**, **Export (API)**. Save it as
   `rimfolk-workflow-api.json` in the same folder as the batch files (step 4).
   (Older versions: Settings, enable "Dev mode options", then "Save (API Format)".)
   Also **Workflow, Save** it under a name of your own (for example `rimfolk`): an export can't be opened again for
   changes, a saved workflow can (the Workflows list on the left), and exported again into a new folder at any time.

## 4. The batch files

Put these in one folder, for example `Documents\Rimfolk portraits`:
- `Run-PortraitBatch.ps1` (from Rimfolk's `Tools/PortraitArt`),
- the `batches` folder (from `docs/portrait-art/batches`): style files `style-A.json` ... and batch lists like
  `colonists.csv`,
- your `rimfolk-workflow-api.json` from step 3.

## 5. Run a batch

1. Keep ComfyUI open.
2. Right-click `Run-PortraitBatch.ps1`, **Run with PowerShell**.
3. It asks which list (for example `characters-01`, or several at once: `3,4,5`) and which style (for example
   `style-rimworld-people`), then hands every picture to ComfyUI. Then it shows the lists again: pick the next one right
   away (Enter keeps the same style), or press Enter to close. ComfyUI works through the queue one list after another;
   you can watch it or leave it running overnight.
4. The pictures land in ComfyUI's `output\Rimfolk` folder (ComfyUI Desktop: the Output button, or
   `Documents\ComfyUI\output\Rimfolk`), named by batch, person and style, with a `batch-log.csv` of every prompt and seed.
5. Zip the folder (right-click, Send to, "Zip for upload") and send it.

Changing a LoRA, its strength, the steps or the sampler needs no new export: the batch tool sets them on the workflow's
boxes from the style file for every picture it queues (the LoRAs in the order of the LoRA boxes). What ComfyUI's canvas
shows is not what the batch uses. Export again only when boxes are added or removed (as in step 6).

## 6. The cut-out (once, 10 minutes)

Every picture is also saved without its background, so the game can put the person in front of a backdrop. This adds a
node pack (code made by others, run by ComfyUI): ComfyUI-RMBG, widely used, from https://github.com/1038lab/ComfyUI-RMBG.
1. In ComfyUI, open the **Manager** (the Manager button, or the puzzle-piece Extensions icon), **Custom Nodes Manager**,
   search `ComfyUI-RMBG`, **Install**, then restart ComfyUI when it asks. (Its page: if the Windows app crashes after
   the install, its requirements need installing again with `PYTHONUTF8=1` set; tell the lead and it writes the steps.)
2. Open your Rimfolk FLUX workflow. Double-click an empty spot, type `BiRefNet` and add the BiRefNet node of the RMBG
   pack. Set its model to **BiRefNet-general** (an open model, MIT licence; the one tested here) and its background to
   **Alpha**. Leave the other settings as they are.
3. Drag from the **VAE Decode** box's IMAGE output to the BiRefNet node's image input. (The VAE Decode output can feed
   both the old Save Image and the new node.)
4. Add a second **Save Image** box and drag the BiRefNet node's IMAGE output to its images input.
5. Rename that box: double-click its title (or right-click it, Title) and type `Save cut`. The batch tool finds it by
   the word "cut" and saves its pictures as `..._cut.png`.
6. **Workflow, Export (API)** over `rimfolk-workflow-api.json`.
The first picture after this downloads the BiRefNet model (a few hundred MB) by itself.

Saving only the cut-out (the remover's picture going to the one Save Image box) is fine too: the batch tool names a
picture `..._cut` whenever it comes from the remover, whatever the box is called. For the backdrops, keep a second
export without the remover, named with "rimfolk" and "workflow" in it (the player's: `rimfolk-backgrounds-workflow-
api.json`): the tool takes the one with the remover for people and the one without it for a backdrop style (`"cut": false`) by itself,
and says which it used. (With only one workflow, it takes the remover out of the way for the backdrops.)

The guidance number in the style files only counts with a **FluxGuidance** box between the prompt box and the KSampler's
`positive` input; without one, FLUX uses its built-in 3.5, which is what the chosen pictures were made with.

## 7. Into the game: the library folder

Rimfolk reads your chosen pictures straight from a folder, the **library**: `library\` next to `Run-PortraitBatch.ps1`,
with one folder per batch (`library\colonists`, `library\backdrops`, `library\people-01` ...: the batch's name, as
in `batches\<batch>.csv`). Which lists to make next, and in what order, is in `docs/portrait-art/catalogue/README.md`.
**With one double-click**: put `Copy-ToLibrary.ps1` and `Copy-ToLibrary.cmd` into ComfyUI's `output\Rimfolk` folder
itself, next to the batch folders (if they sit in one batch's folder, they still do every batch). Throw out the pictures you don't want there, then double-click
`Copy-ToLibrary.cmd`: it copies every new picture into the library folder of its batch (people's cut-outs only,
backdrops all), skips what is already there, never renames anything, and writes the records again. The first time it
asks where your `Rimfolk-portraits` folder is. Then "Reload the library" in the game. By hand it goes like this:
1. Copy the pictures you want in the game into those folders, with the names ComfyUI gave them: for people the cut-outs
   (`..._cut_00001_.png`; Ape's from `colonists-redo` go into `library\colonists` too, her row is in `colonists.csv`),
   for backdrops the scenes. Several backdrops of one room are fine; the game picks among them.
2. Right-click `Update-PortraitLibrary.ps1`, **Run with PowerShell**. It writes `library\records.xml`: a record for
   every picture, taken from its row in the batch list (the columns after `seed`) and the picture check's corrections
   marked yes (step 8), and shows the folder's path.
3. In RimWorld, Rimfolk's settings, **Portrait library folder**: paste that path (once on each computer). Then
   **Reload the library** (or restart the game). Each time you add or remove pictures: run the script again, then reload.

**Names never clash**: every picture's name carries its batch row, its style and the run's date and time
(`Avila_style_261122-143005_01_cut_00001_.png`), so pictures from different runs never share a name, and the same
lists can be run again and again for more variety. A copy Windows renamed ("... (2).png", "... - Copy.png") still finds
its row. Never rename a picture by hand: the start of its name is what ties it to its row.

**Two computers**: keep the whole `Rimfolk-portraits` folder (scripts, batches, library) in Google Drive, and in Google
Drive for desktop set it to **Available offline**, so the pictures are on both disks and the game never waits for a
download. Then whatever you add on one computer is on the other after Drive syncs; only the setting is set once on each
(if Drive has another drive letter there, the path differs). The pictures don't go through Rimfolk's GitHub, so a
Rimfolk update never downloads them again.

**Deleting a picture in the game**: right-click the large portrait, **Delete this picture for good...** (or **Delete
this backdrop for good...**), and confirm. Rimfolk moves the picture and its expressions (a backdrop and its depth map)
into the bin, the folder `deleted pictures\<batch>` next to `library\` in your `Rimfolk-portraits` folder, takes its
record out of `library\records.xml` at once, and writes its name into `library\deleted.csv`; whoever had it gets another
picture. Empty the bin yourself when you like (the setting **Deleting a picture** can delete the files at once instead).
The scripts read `deleted.csv`: `Copy-ToLibrary` never copies that picture from ComfyUI's output again,
`Update-PortraitLibrary` never writes a record for it even if the file comes back, and the picture check, the depth maps
and the expressions leave it out. To have one back: take its line out of `deleted.csv` (Excel is fine; close it before
running a script), put its file back into its library folder and run `Update-PortraitLibrary.ps1`. A row whose pictures
are all deleted counts as having none, so `Run-PortraitBatch.ps1` makes it again when you ask for the missing rows.

**Growing it**: Rimfolk writes `library\missing.csv` with what it needed and had no picture for (a xenotype, a room kind,
a biome, an age). `Update-PortraitLibrary.ps1` shows its first lines; send it to the lead, who adds new rows to the batch
lists. When the library already holds pictures of a batch, `Run-PortraitBatch.ps1` offers to make only the rows that
have none yet, so a grown list is never made again from the start.

## 8. The picture check (once, about 20 minutes, most of it downloading)

A picture can show more or less than its batch row asked for: a sword or a flame nobody asked for, a woman drawn as a
man. The game gives a picture with a look or a role only to people who have it, so a record has to match its picture.
The check has a describing model on your computer look at each picture in the library, answer a fixed list of
questions about it (how many people, man or woman, how old they look, ears, horns, wings, eyes, what they hold,
weapons, armour, fire or magic, headwear, a book or tools, anything unusual), and compares the answers with the
picture's record. You then look through a short list instead of every picture. It uses ComfyUI's own boxes and
Qwen3.5, a free model (Apache 2.0 licence) that fits your card well, so no node pack is needed.

**The model**
1. Keep ComfyUI up to date: ComfyUI Desktop offers its updates when it starts; take them.
2. Menu **Workflow**, **Browse Templates** (or the Templates button on the left). Search `Qwen3.5` and open
   **Qwen3.5: Text Generation**. ComfyUI says the model file is missing (`qwen3.5_4b_bf16.safetensors`, about 9 GB) and
   offers to download it: let it.
   (If it doesn't offer: open https://huggingface.co/Comfy-Org/Qwen3.5/tree/main/text_encoders, download that file and
   put it in ComfyUI's `models\text_encoders` folder, for ComfyUI Desktop `Documents\ComfyUI\models\text_encoders`.
   Then press R in ComfyUI so it sees the file.)
3. Try it: in the **Load Image** box, click "choose file to upload" and pick one of your portraits. Press **Run**. The
   first time takes a minute or two while the model loads; then a description of the picture appears in the
   **Preview as Text** box (called Preview Any in older versions).

**Ready for the check**
4. The template has four boxes, already wired: **Load CLIP** (the model) into the **Generate Text** box's clip input,
   **Load Image** into its image input, and its `generated_text` into **Preview as Text**. (The box may be called
   "Generate Text (DEPRECATED)" in the template: it works the same.) If one is missing, double-click an empty spot,
   type its name, add it and drag the wire.
5. In the **Generate Text** box, delete the text in its prompt box and type exactly `%QUESTION%`. Set its
   **Sampling Mode** to **off**, so the same picture always gets the same answer. Leave the rest: the check turns
   thinking off and gives the answer enough room by itself.
6. Menu **Workflow**, **Export (API)**. Save it as `rimfolk-check-workflow-api.json` in your `Rimfolk-portraits` folder
   (the one with `Update-PortraitLibrary.ps1` and `library`). The batch tool leaves this workflow alone.
7. Copy `Check-Pictures.ps1` and `Check-Pictures.cmd` (from Rimfolk's `Tools/PortraitArt`) into the same folder.

**Running it**
8. Keep ComfyUI open and double-click `Check-Pictures.cmd`. It looks at each picture in the library it hasn't looked at
   before, a few seconds each (the first takes longer while the model loads), and shows a line of what the model saw
   for each. It finds ComfyUI by itself; if it can't, it asks for the address (ComfyUI's settings, Server-Config) and
   remembers it. If ComfyUI is still making portraits, the check waits for it.
9. At the end it shows the pictures to look at, the worst first, and writes two files into `library`:
   `check-report.csv`, the whole list, and `corrections.csv`, the changes it proposes for single pictures (both open
   in Excel; Notepad works too).
10. Open `corrections.csv`. Each line is one change for one picture: `picture`, `field`, `value`, `apply`, `why`. Lines
    marked **yes** are changes the check is sure of; lines marked **ask** wait for you: look at the picture, then
    change ask to **yes** (do it) or **no** (keep the picture as it is), and save. You may also change a yes to no, or
    fill in a value (for example the look's word after `add:`).
11. Run `Update-PortraitLibrary.ps1` (`Copy-ToLibrary.cmd` runs it too). It writes the records again from the batch
    lists and puts the corrections marked yes into them, and says how many it used and how many still wait as ask.
    Then **Reload the library** in the game.
12. Run the check again whenever pictures have been added. It asks the model only about new pictures (every answer is
    kept in `library\checked.csv`, a description of every picture in the library), and compares all the pictures with
    their records again: a picture whose correction was used leaves the list, and an issue you marked no doesn't come
    back. Lines already in `corrections.csv` are never changed by it; new ones are added at the end.

**What the corrections change.** Nothing is renamed: a picture's name ties it to its batch row, and the row's other
pictures may be right, so a correction changes only that picture's record. The fields:
- `age`: the age band the picture shows. The model is asked for a band by name (young adult, middle-aged ...) and what
  to judge it by (grey hair, wrinkles, sagging skin; a child's head and face). Three bands or more from its row (a child
  drawn for an adult or the other way round) is yes; two bands is ask, since a drawing's lines can read older or
  younger than they are meant; one band apart stays as it is.
- `sex`: `male` or `female` (yes when the model said it plainly; for a picture made for one colonist it is never
  changed: that picture gets `out`, ask).
- `role`: the work it shows (yes when the row has no role: a weapon gives soldier, warrior or hunter, armour soldier or
  warrior, a crown leader, a book scholar), or `clear` (ask).
- `looks`: `add:word` or `remove:word` (adding wings' look, such as `batWings`, is yes; fire or magic is ask, because
  which look it is can't be told; taking a look away is ask, because the picture would then go to anyone).
- `marks`: `add:eyePatch` and the like (yes), `remove:...` (ask).
- `headwear`: `true`, so a picture with a hat or hood goes only to people wearing something on the head (yes; ask for
  a colonist's own picture).
- `out`: the picture is left out of the records; the file stays where it is (yes for more than one person in a
  cut-out; ask for a body its kind doesn't have, such as pointed ears on a baseliner or an elf without them, and for
  a person in a backdrop).
Adding a look or a role only narrows who gets a picture, which is why those can be yes; anything that would give it to
more people is ask.

**Reading the report.** One line per picture: the `picture`, its `row` in the batch list, the `check`, `what is
wrong`, `what the model saw`, what `its row says`, a `suggested fix`, and what is `in corrections.csv` for it. The
check is one of:
- **1 wrong**: the other sex than its row, an age two bands or more from its row's (three or more marked yes), more than one person in a
  cut-out, no one at all, or a person in a backdrop.
- **2 shows more**: something its row doesn't have: a weapon, armour, fire or magic, wings, a crown, a book or tools,
  a hat, hood or helmet, horns, pointed ears, a tail, glowing eyes, scales or fur, an eye patch.
- **3 shows less**: its row says something the model didn't see (a fire mage with no fire, a soldier with no weapon or
  armour, a saurid without scales). Look at the picture: the model can miss things, and the clothes or the face may
  show it.
- **4 not compared**: the picture has no record yet (run `Update-PortraitLibrary.ps1`), or the model's answer couldn't
  be read (it is asked again next time).
What a look or role asks for is taken from the row's own prompt (its subject and pose columns): a row with the
sniperClass look whose prompt asks for a rifle may show one, and should.

Pictures made for one colonist (a name in their row's `pawn` column) go to that colonist only, so for them only the
sex, the age, the number of people, headwear and the body (ears, horns, scales, eyes) are checked.

The model can be wrong: the list and the ask lines say what to look at. The check never deletes, moves or renames a
picture and never changes a batch list or the records. You can still throw a picture out by deleting it from
`library\<batch>`, or fix a whole row in `batches\<batch>.csv`; then run `Update-PortraitLibrary.ps1`, and the check
again.

**Sharper answers (optional).** The larger Qwen3.5 model sees more detail and comes in a file of about the same size:
`qwen3.5_9b_int8_convrot.safetensors` (about 10 GB) from the same page. Put it in `models\text_encoders`, pick it in the
Load CLIP box, export again over `rimfolk-check-workflow-api.json`, and delete `library\checked.csv` so every picture
is looked at again.

**If there is no Qwen3.5 template** even after an update: install the ComfyUI-QwenVL node pack (from the makers of the
RMBG pack, https://github.com/1038lab/ComfyUI-QwenVL) through the Manager as in step 6. Add its **QwenVL** box (model
`Qwen3-VL-8B-Instruct`, quantization 8-bit), drag Load Image's IMAGE to it and its text output to a Preview as Text box,
type `%QUESTION%` in its custom prompt box, and export as above. The check works with it the same way.

## 9. Depth maps for the backdrops (once, about 10 minutes)

On the large portrait the person and the backdrop shift apart a little when the mouse moves over them, so the picture
looks a little three-dimensional. A depth map makes this better: a grey picture the size of the backdrop, white where
things are near and black where they are far, so the near parts of a room can move more than the far wall. It is made
by Depth Anything 3, a depth model that ComfyUI has had its own boxes for since mid-2026, so no node pack is needed.
Its Mono-Large model has the Apache 2.0 licence (free to use, also for a published mod).

**The workflow**
1. Keep ComfyUI up to date (ComfyUI Desktop offers its updates when it starts).
2. Menu **Workflow**, **Browse Templates**. Search `Depth Anything 3` and open **Depth Estimation: Depth Anything 3**.
   ComfyUI says the model file is missing (`depth_anything_3_mono_large.safetensors`, about 1.3 GB) and offers to
   download it: let it.
   (If it doesn't offer: open https://huggingface.co/Comfy-Org/Depth-Anything-3/tree/main/geometry_estimation, download
   that file and put it in ComfyUI's `models\geometry_estimation` folder, for ComfyUI Desktop
   `Documents\ComfyUI\models\geometry_estimation`. Then press R in ComfyUI so it sees the file.)
3. Try it: in the **Load Image** box, click "choose file to upload" and pick one of your backdrops from
   `library\backdrops`. Press **Run**. A grey picture of the room appears in the **Preview Image** box: the near things
   light, the far wall dark, the sky black.
   **If the template takes a video** (its first box is **Load Video**, the depth box is called "Video Depth Estimation
   (Depth Anything 3)" and the last is **Create Video**; the player found only this one, 22 November 2026): look for
   a template without "Video" in its name first. If there is none, make the video one take a picture:
   a. Right-click the "Video Depth Estimation" box, **Unpack Subgraph**: its inner boxes appear.
   b. Delete **Load Video** and the box that splits the video into frames (such as **Get Video Components**).
   c. Add a **Load Image** box (double-click an empty spot, type it) and drag its IMAGE output into the input the
      frames went into (the first Depth Anything box's image input).
   d. Delete **Create Video** (and any Save Video box). Switch the purple, bypassed **Depth Preview** box on (click it,
      Ctrl+B), or add a **Preview Image** box, and give it the depth IMAGE from the last Depth Anything box.
   e. Leave the depth settings as they are (resolution 504, output depth, normalization v2_style); start time and
      duration belong to the video part and go with it. Then point 3's test, and on to point 5.
4. The template's boxes are already wired: **Load Image** into the **Depth Anything 3** box (it holds the model's three
   boxes: load, run and render), and that box into **Preview Image**. Leave its settings as they come (the
   `mono_large` model, resolution 504, output `depth`, normalization `v2_style`). There is also an **Image Compare**
   box, which shows the backdrop and its depth side by side; you may delete it (click it, press Delete) or leave it.
5. Menu **Workflow**, **Export (API)**. Save it as `rimfolk-depth-workflow-api.json` in your `Rimfolk-portraits` folder.
6. Copy `Make-DepthMaps.ps1` and `Make-DepthMaps.cmd` (from Rimfolk's `Tools/PortraitArt`) into the same folder.

**Running it**
7. Keep ComfyUI open and double-click `Make-DepthMaps.cmd`. For every backdrop in the library that has no depth map yet,
   it hands the backdrop to ComfyUI and writes the depth map next to it, a few seconds each, with a line for each.
8. What comes out: next to `library\backdrops\<name>.png` a `<name>_depth.png`, a grey picture of the same size.
   Nothing else in the library is changed. At the end it writes the records again (as `Update-PortraitLibrary.ps1`
   does), which now tell the game where each backdrop's depth map is. Then **Reload the library** in the game.
9. Run it again whenever backdrops have been added: it makes only the depth maps that are missing. To have one made
   again, delete its `_depth.png` and run it.

A depth map is never a picture of its own: the library script, the picture check, the copy script and the batch tool
all treat it as part of its backdrop. If the workflow ends in a **Save Image** box instead of Preview Image, ComfyUI
also keeps a copy of each depth map in its `output\Rimfolk-extras` folder; you can delete that folder at any time.

## 10. Expressions: blink, smile, frown, pain (once, about 30 minutes, most of it downloading)

The large portrait can blink now and then and show the person's mood if each picture also exists with the eyes closed,
with a small smile, with a frown and with a wince of pain. These are made from the picture itself by an editing model:
you give it the picture and a plain instruction ("Close the person's eyes ..."), and it changes only that.
The model is **FLUX.1 Kontext Dev**, the editing model of the FLUX.1 family that painted your pictures, so the
expressions keep their painting style. Its fp8 file fits your card, and its licence is the same as FLUX.1 Dev's: free
for your own use.
The new picture keeps the outline of the one it was made from (a **Join Image with Alpha** box gives it that outline),
so the person stays exactly in place when the game switches between them, and no background remover is needed.

**The workflow**
1. Menu **Workflow**, **Browse Templates**. Search `Kontext` and open **Flux Kontext Dev Image Edit**. ComfyUI says
   which model files are missing: the edit model `flux1-dev-kontext_fp8_scaled.safetensors` (about 12 GB) and the text
   and picture files that go with it (`clip_l`, `t5xxl_fp8_e4m3fn_scaled` and `ae`, about 5 GB, unless you have them
   already). Let it download them.
2. Try it: the template has two **Load Image** boxes; the second is switched off (grey), leave it so. In the first,
   click "choose file to upload" and pick one of your cut-outs from `library\colonists`. In the **Image Edit** box's
   prompt, type `Close the person's eyes. Keep everything else exactly the same.` and press **Run**. After about half a
   minute (the first time longer, while the model loads) the picture with closed eyes appears in **Save Image**.
3. Delete the text in the Image Edit box's prompt and type exactly `%INSTRUCTION%`. The expressions script puts the
   instruction for each expression there.
4. Add the outline: double-click an empty spot, type `Join Image with Alpha` and add it. Then drag three wires:
   - from the **Image Edit** box's IMAGE output to the Join box's `image` input,
   - from the first **Load Image** box's MASK output to the Join box's `alpha` input,
   - from the Join box's IMAGE output to the **Save Image** box's `images` input (this replaces the old wire).
5. Optional: so that ComfyUI keeps no copies, use a **Preview Image** box instead of Save Image (double-click, type
   `Preview Image`, add it, drag the Join box's IMAGE to it, delete the Save Image box). With Save Image, a copy of each
   expression collects in ComfyUI's `output\Rimfolk-extras` folder, which you can delete at any time.
6. Menu **Workflow**, **Export (API)**. Save it as `rimfolk-variant-workflow-api.json` in your `Rimfolk-portraits`
   folder. The batch tool leaves this workflow and the depth workflow alone.
7. Copy `Make-Variants.ps1` and `Make-Variants.cmd` (from Rimfolk's `Tools/PortraitArt`) into the same folder.

**Running it**
8. Keep ComfyUI open and double-click `Make-Variants.cmd`. It asks which pictures:
   - **Enter**: the pictures your game has given someone. First the pictures made for your colonists (a name in their
     row's `pawn` column), then the ones your newest RimWorld save has given people: the colony's own first, then
     notable people, then the rest. It reads the save from RimWorld's Saves folder and changes nothing in it.
   - **a**: every person in the library (this takes long: about two minutes a picture).
9. For each picture it makes the expressions it doesn't have yet, about half a minute each, with a line for each
   picture. You can close the window at any time: what is made is kept, and the next run carries on where it stopped.
10. What comes out: next to `library\colonists\<name>.png` the files `<name>_v-blink.png`, `<name>_v-smile.png`,
    `<name>_v-frown.png` and `<name>_v-pain.png`, each the same size with the same outline. Nothing else in the library
    is changed. At the end it writes the records again: each expression gets a record of its own, the same as its
    picture's (with the picture check's corrections) and marked as an expression of that picture. Then **Reload the
    library** in the game.
11. Look through them in the folder. If one is not right (the eyes not quite shut, the smile too wide, the face changed),
    delete that file and run it again: it is made again with a new seed.

What it asks the model for, each time followed by "Keep everything else exactly the same: the same person and face,
hair, clothes, pose, position, framing, lighting and painting style.":
- blink: "Close the person's eyes, as in the middle of a blink, the eyelids fully shut and relaxed."
- smile: "Give the person a small, warm smile with the mouth closed."
- frown: "Make the person frown, the brows drawn together and down, the mouth set in a slight scowl."
- pain: "Make the person wince in pain, the eyes squeezed half shut, the brows drawn up and together, the teeth
  clenched."

Each expression keeps its picture's size: the script takes the template's own resizing box out of the way by itself.
If a picture comes back in another size or without its clear background, it stops at the first and says what to
check in the workflow. Expressions, like depth maps, are never pictures of their own for the other scripts. A
picture left out by the picture check's corrections (`out`) has its expressions left out too.

**A newer model (optional).** FLUX 3 Image Klein (October 2026; its template is **FLUX 3 Image Klein Distilled: Image
Edit**) is smaller and faster and is said to edit more exactly; its licence is free for your own use too. It is new, and
its first weeks brought some errors in ComfyUI, so Kontext is the one set up here. To try it later: open that template,
type `%INSTRUCTION%` where its prompt goes, add the Join Image with Alpha box as in point 4, and export over the same
file name; the script works with it the same way.

## If something goes wrong

- "Could not reach ComfyUI": ComfyUI isn't open, or it uses another port; the address is shown in ComfyUI's settings
  (Server-Config); run the script again with that address.
- Pictures come out empty or strange: open the workflow in ComfyUI and check that the prompt box says exactly `%PROMPT%`,
  then export again.
- Out of memory: close games and browsers while it runs, or pick the template's fp8 version.
- The picture check stops with "ComfyUI refused the question" or "ComfyUI could not answer": the message names the box.
  Most often the model file is not in `models\text_encoders` (step 8, point 2); for "out of memory", close what else
  uses the graphics card, or use the smaller `qwen3.5_2b_bf16.safetensors` from the same page.
- "Could not write ... is it open in Excel?": close `check-report.csv` or `corrections.csv` in Excel and run the check
  again.
- The depth maps or expressions stop with "ComfyUI refused the workflow": the message names the box. Most often a model
  file is missing from ComfyUI's `models` folder (step 9, point 2, or step 10, point 1).
- The depth maps stop with "the depth map came out ... the backdrop is ...": a box in the workflow changes the size.
  Add an **Upscale Image** box between the depth box and Preview Image; the script sets its size to each backdrop's.
- The expressions stop with "it came out without a clear background": the **Join Image with Alpha** box is missing or
  not wired into the Save Image (or Preview Image) box (step 10, point 4). With "it came out ... x ...": a box in the
  workflow changes the size; take it out (step 10).
