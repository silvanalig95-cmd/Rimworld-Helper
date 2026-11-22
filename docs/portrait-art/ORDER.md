# The order of everything (the portrait kit)

The player, 22 November 2026: "please give me the instructions how to set up qwen and then the full order in which i
must run all these programs and scripts". The details of each step are in `SETUP-ComfyUI.md`.

## Once

1. Unpack the newest `Rimfolk-portrait-kit-N.zip` over your `Rimfolk-portraits` folder (every kit is full), and delete
   old lists it no longer has from `batches` (`wave1-people-*`, `wave2-people-*`).
2. Put `Copy-ToLibrary.cmd` and `Copy-ToLibrary.ps1` into ComfyUI's `output\Rimfolk` folder.
3. Five exported workflows in `Rimfolk-portraits` (ComfyUI: Workflow, Export (API)):
   - `rimfolk-workflow-api.json`: people, with the cut-out (steps 3 and 6 of the guide);
   - `rimfolk-backgrounds-workflow-api.json`: backdrops, without the cut-out;
   - `rimfolk-check-workflow-api.json`: the picture check, Qwen3.5 (step 8);
   - `rimfolk-depth-workflow-api.json`: depth maps, Depth Anything 3 (step 9);
   - `rimfolk-variant-workflow-api.json`: expressions, FLUX.1 Kontext Dev (step 10).
4. In RimWorld, Rimfolk's settings, "Portrait library folder": the path of `Rimfolk-portraits\library`.
5. The style, until it's chosen: train the `rmwstyle` LoRA (`STYLE-RIMWORLD.md`), then run `style-test` with
   `style-rmw` and with the comic candidates `style-comic-a` to `e`, and compare them with `style`. Copy the one you like
   over `style.json` (keep the old one under another name). Every list takes its drawing from `style.json`.

### Starting a new library
1. Move the old `library` folder out of `Rimfolk-portraits` (or rename it `library-old`). The copier makes a new, empty
   one the first time it copies, and the batch tool then makes every row of a list again.
2. Move the old batch folders out of ComfyUI's `output\Rimfolk` (keep the two Copy-ToLibrary files there): the copier
   copies every batch folder it finds there, so old pictures would come into the new library.
3. In the game, after the first copy: Rimfolk settings, "Reload the library". Everyone's old picture is no longer in the
   library, so each pawn gets a new one by the usual rules the next time they are shown.

### Qwen3.5, the picture check's model (step 8)
1. Start ComfyUI and take its update if it offers one.
2. Workflow, Browse Templates, search `Qwen3.5`, open "Qwen3.5: Text Generation"; let it download
   `qwen3.5_4b_bf16.safetensors` (about 9 GB). If it doesn't offer: https://huggingface.co/Comfy-Org/Qwen3.5/tree/main/text_encoders
   into `Documents\ComfyUI\models\text_encoders`, then press R.
3. Try it: a portrait in the Load Image box, Run; a description appears in "Preview as Text".
4. In the Generate Text box: the prompt becomes exactly `%QUESTION%`, Sampling Mode off.
5. Export (API) as `rimfolk-check-workflow-api.json`.

## Each round

1. `Run-PortraitBatch.ps1`: a people list with `style`, overnight. In a new library: first `colonists` (your own people),
   then `characters-01`, `02`, `03` (every kind of person, each with a face of their own), then `people-01` to `04`.
2. `Run-PortraitBatch.ps1` with `backdrops` and `style-backdrop`, when you want more backdrops.
3. In `output\Rimfolk`: delete the pictures you don't want.
4. `Copy-ToLibrary.cmd` (in `output\Rimfolk`): the new pictures into the library; it writes the records.
5. `Check-Pictures.cmd`: the model looks at each new picture; it writes `library\check-report.csv` and `corrections.csv`.
6. `library\corrections.csv` in Excel: change each "ask" to yes or no.
7. `Update-PortraitLibrary.ps1`: the corrections go into the records.
8. `Make-DepthMaps.cmd`: depth maps for new backdrops (it writes the records).
9. `Make-Variants.cmd`, Enter: blink, smile, frown and pain for your colonists' pictures first (it writes the records).
10. In the game: Rimfolk settings, "Reload the library".
11. Play; send the lead a save and `library\missing.csv`; the lead writes the next lists.

The check comes before the depth maps and expressions, so a picture it leaves out gets none. Steps 4 to 9 only ever
handle what's new and can be run again at any time. Steps 8 and 9 are optional.
