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

## If something goes wrong

- "Could not reach ComfyUI": ComfyUI isn't open, or it uses another port; the address is shown in ComfyUI's settings
  (Server-Config); run the script again with that address.
- Pictures come out empty or strange: open the workflow in ComfyUI and check that the prompt box says exactly `%PROMPT%`,
  then export again.
- Out of memory: close games and browsers while it runs, or pick the template's fp8 version.
