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
3. Save the workflow for the batches: menu **Workflow**, **Export (API)**. Save it as
   `rimfolk-workflow-api.json` in the same folder as the batch files (step 4).
   (Older versions: Settings, enable "Dev mode options", then "Save (API Format)".)

## 4. The batch files

Put these in one folder, for example `Documents\Rimfolk portraits`:
- `Run-PortraitBatch.ps1` (from Rimfolk's `Tools/PortraitArt`),
- the `batches` folder (from `docs/portrait-art/batches`): style files `style-A.json` ... and batch lists like
  `colonists.csv`,
- your `rimfolk-workflow-api.json` from step 3.

## 5. Run a batch

1. Keep ComfyUI open.
2. Right-click `Run-PortraitBatch.ps1`, **Run with PowerShell**.
3. It asks which batch (for example `colonists`) and which style (for example `style-A`), then hands every picture to
   ComfyUI and closes. ComfyUI works through the queue; you can watch it or leave it running overnight.
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
api.json`): the tool takes the one with the remover for people and the one without it for `style-backdrop` by itself,
and says which it used. (With only one workflow, it takes the remover out of the way for the backdrops.)

The guidance number in the style files only counts with a **FluxGuidance** box between the prompt box and the KSampler's
`positive` input; without one, FLUX uses its built-in 3.5, which is what the chosen pictures were made with.

## 7. Into the game: the library folder

Rimfolk reads your chosen pictures straight from a folder, the **library**: `library\` next to `Run-PortraitBatch.ps1`,
with one folder per batch (`library\colonists`, `library\backdrops`, `library\wave1-people-01` ...: the batch's name, as
in `batches\<batch>.csv`). Which lists to make next, and in what order, is in `docs/portrait-art/catalogue/README.md`.
**With one double-click**: put `Copy-ToLibrary.ps1` and `Copy-ToLibrary.cmd` into ComfyUI's `output\Rimfolk` folder
(or into one batch's folder in it). Throw out the pictures you don't want there, then double-click
`Copy-ToLibrary.cmd`: it copies every new picture into the library folder of its batch (people's cut-outs only,
backdrops all), skips what is already there, never renames anything, and writes the records again. The first time it
asks where your `Rimfolk-portraits` folder is. Then "Reload the library" in the game. By hand it goes like this:
1. Copy the pictures you want in the game into those folders, with the names ComfyUI gave them: for people the cut-outs
   (`..._cut_00001_.png`; Ape's from `colonists-redo` go into `library\colonists` too, her row is in `colonists.csv`),
   for backdrops the scenes. Several backdrops of one room are fine; the game picks among them.
2. Right-click `Update-PortraitLibrary.ps1`, **Run with PowerShell**. It writes `library\records.xml`: a record for
   every picture, taken from its row in the batch list (the columns after `seed`), and shows the folder's path.
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
9. At the end it shows the pictures to look at, the worst first, and writes the whole list to
   `library\check-report.csv` (it opens in Excel; Notepad works too).
10. Run it again whenever pictures have been added. It asks the model only about new pictures: every answer is kept in
    `library\checked.csv`, a description of every picture in the library. Each run compares all the pictures with
    their records again, so after fixing a row the list shows it at once, without asking the model again.

**Reading the report.** One line per picture: the `picture`, its `row` in the batch list, the `check`, `what is
wrong`, `what the model saw`, what `its row says`, and a `suggested fix`. The check is one of:
- **1 wrong**: the other sex than its row, an age two bands or more from its row's (one band apart is fine), more than
  one person in a cut-out, no one at all, or a person in a backdrop. Throw these out.
- **2 shows more**: something its row doesn't have: a weapon, armour, fire or magic, wings, a crown, a book or tools,
  a hat, hood or helmet, horns, pointed ears, a tail, glowing eyes, scales or fur, an eye patch. Either throw it out,
  or, if the picture is good, add the word the fix names to its row (the `looks`, `role`, `features`, `marks` or
  `dress` column of `batches\<batch>.csv`), so it goes only to people who have it.
- **3 shows less**: its row says something the model didn't see (a fire mage with no fire, a soldier with no weapon or
  armour, a saurid without scales). Look at the picture: the model can miss things, and the clothes or the face may
  show it.
- **4 not compared**: the picture has no record yet (run `Update-PortraitLibrary.ps1`), or the model's answer couldn't
  be read (it is asked again next time).

Pictures made for one colonist (a name in their row's `pawn` column) go to that colonist only, so for them only the
sex, the age, the number of people, headwear and the body (ears, horns, scales, eyes) are checked.

The model can be wrong: the list says what to look at, not what to throw out. The check never deletes, moves or renames
a picture and never changes a batch list or the records. To throw a picture out, delete it from `library\<batch>`; to
fix a row, change it in `batches\<batch>.csv`; then run `Update-PortraitLibrary.ps1`, and the check again.

**Sharper answers (optional).** The larger Qwen3.5 model sees more detail and comes in a file of about the same size:
`qwen3.5_9b_int8_convrot.safetensors` (about 10 GB) from the same page. Put it in `models\text_encoders`, pick it in the
Load CLIP box, export again over `rimfolk-check-workflow-api.json`, and delete `library\checked.csv` so every picture
is looked at again.

**If there is no Qwen3.5 template** even after an update: install the ComfyUI-QwenVL node pack (from the makers of the
RMBG pack, https://github.com/1038lab/ComfyUI-QwenVL) through the Manager as in step 6. Add its **QwenVL** box (model
`Qwen3-VL-8B-Instruct`, quantization 8-bit), drag Load Image's IMAGE to it and its text output to a Preview as Text box,
type `%QUESTION%` in its custom prompt box, and export as above. The check works with it the same way.

## If something goes wrong

- "Could not reach ComfyUI": ComfyUI isn't open, or it uses another port; the address is shown in ComfyUI's settings
  (Server-Config); run the script again with that address.
- Pictures come out empty or strange: open the workflow in ComfyUI and check that the prompt box says exactly `%PROMPT%`,
  then export again.
- Out of memory: close games and browsers while it runs, or pick the template's fp8 version.
- The picture check stops with "ComfyUI refused the question" or "ComfyUI could not answer": the message names the box.
  Most often the model file is not in `models\text_encoders` (step 8, point 2); for "out of memory", close what else
  uses the graphics card, or use the smaller `qwen3.5_2b_bf16.safetensors` from the same page.
