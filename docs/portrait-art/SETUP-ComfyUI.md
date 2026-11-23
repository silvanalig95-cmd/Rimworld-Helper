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

**Keeping a picture for training**: right-click the large portrait, **Keep for training**. Rimfolk copies the picture
to `training\<batch>\` next to `library\` in your `Rimfolk-portraits` folder and adds a line to `training\training.csv`
(file, batch, record, date); the picture stays in the library. When you have kept 20 to 40, double-click
`Make-TrainingSet.cmd` (copy it and `Make-TrainingSet.ps1` from Rimfolk's `Tools/PortraitArt` into the same folder):
it writes `training\set\`, each picture on a plain dark grey ground with a caption of the same name taken from its row,
ready for FluxGym (`STYLE-RIMWORLD.md`, "Training again").

**Growing it**: Rimfolk writes `library\missing.csv` with what it needed and had no picture for (a xenotype, a room kind,
a biome, an age). `Update-PortraitLibrary.ps1` shows its first lines; send it to the lead, who adds new rows to the batch
lists. When the library already holds pictures of a batch, `Run-PortraitBatch.ps1` offers to make only the rows that
have none yet, so a grown list is never made again from the start.

## 8. The picture check (once, about 20 minutes, most of it downloading)

A picture can show more or less than its batch row asked for: a sword or a flame nobody asked for, a woman drawn as a
man. The game gives a picture with a look or a role only to people who have it, so a record has to match its picture.
The check has a describing model on your computer look at each picture in the library, answer a fixed list of
questions about it (how many people, man or woman, how old they look, ears, horns, wings, eyes, eye marks, an
artificial arm, what they hold, weapons, armour, fire or magic, headwear, a book or tools, anything unusual), and
compares the answers with the picture's record. The question tells the model the picture is a comic-style drawn
portrait for a game. The record, taken from the picture's row in the batch list, is the starting point: the model is
not told the row, and a correction is written only where what it saw differs. A second check needs no model: a
cut-out whose figure runs along the left or right edge is cut at the side. You then look through a short list instead
of every picture. It uses ComfyUI's own boxes and Qwen3.5, a free model (Apache 2.0 licence) that fits your card well,
so no node pack is needed.

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
8. Keep ComfyUI open and double-click `Check-Pictures.cmd`. It asks the model only what it hasn't asked before (the
   player: "it only analyzes things that have not yet been analyzed ... not that I have like a really long analyze job
   each time I add new images"), and says so at the start, for example "What the model is asked: 12 new pictures (the
   whole form), 340 to look at again for the arms line only, 0 asked again (the whole form)":
   - a new picture, or one changed since (another file size), gets the whole form;
   - each line of the form has a version of its own: when the lead changes a line's wording (a new kit), only that
     line is asked again, in a short form of just the changed lines, about the pictures asked with the older wording,
     and the answer goes with the ones kept;
   - a change to how the answers are read or compared (also with a new kit) needs no model: every run reads the kept
     answers again and compares all the pictures with their records;
   - to ask every picture the whole form again (after changing the model), open PowerShell in the folder and type
     `powershell -ExecutionPolicy Bypass -File .\Check-Pictures.ps1 -Again`.
   It shows a line of what the model saw for each picture, a few seconds each (the first takes longer while the model
   loads). It finds ComfyUI by itself; if it can't, it asks for the address (ComfyUI's settings, Server-Config) and
   remembers it.
   **A proper queue** (the player: "it should finish all the analysis first and only then start upcoming jobs so it's
   a proper queue"): it first hands every picture to ask to ComfyUI (into ComfyUI's `input\rimfolk-check` folder, one
   file for each picture, written over on later runs; you may delete that folder when no check is running), then
   queues all its questions at once and collects the answers in order. A batch of portraits queued in ComfyUI while
   the check runs waits until every question is answered. A check started while ComfyUI still has jobs waits behind
   them and says so ("ComfyUI has 40 jobs ahead of the check; it starts when they are done"). To stop it, press
   **Ctrl+C**: the questions still waiting are taken out of ComfyUI's queue (an error does the same). Closing the
   window instead leaves them in ComfyUI's queue, where they run for nothing; remove them there.
9. At the end it shows the pictures to look at, the worst first, and writes two files into `library`:
   `check-report.csv`, the whole list, and `corrections.csv`, the changes it proposes for single pictures (both open
   in Excel; Notepad works too).
   Then it asks **Make a zip of examples to send? (Enter for yes, n for no)**. Enter makes
   `library\check-examples-<date>-<time>.zip` and says where it is and how big: `check-report.csv`, `corrections.csv`,
   `checked.csv`, a small `examples.csv` (each picture in the zip with its row's name, subject and pose, what the check
   found and what the model answered) and up to 30 pictures, the flagged ones first (the worst first), then up to 8
   that match their records, of different kinds and ages. Send that zip to the lead. `n` makes none.
   (The lines' own versions came on 23 November 2026: a `checked.csv` from before counts every line as asked, so the
   first run with them asks nothing it has asked already.)
10. Nothing to answer. `corrections.csv` has one line per change for one picture (`picture`, `field`, `value`,
    `apply`, `why`): **yes** where the check is sure, **ask** where it is less sure. Both are applied (the player, 23
    November 2026: "the corrections awaiting approval should just be applied without approval ... I'd rather just later
    correct the image ingame once it actually appears, so on demand"). Correct a picture in the game with "About this
    picture" when you see it (that always wins); or, if you like, change a line to **no** and it is never applied.
    One kind of line waits for you: **hold**, an `out` for limbs that don't make sense (an extra arm, hand or finger,
    a hand that belongs to no one, a limb growing from the wrong place; the form's `limbs` line). Such a picture stays
    out of the game until you keep it (the player, 23 November 2026: "things like extra limbs should be blocked by
    default"). The check says how many are held back.
    **Review-Pictures**: copy `Review-Pictures.ps1` and `Review-Pictures.cmd` next to the others and double-click the
    `.cmd`. It opens a page in your browser (served by the script on your own computer only, `http://localhost:8765`),
    one picture at a time, large (click it for full size), with why it is out, what the check found, what the model saw
    and what its row says. **Keep** (K) marks its out lines no, and the picture goes back as its row says; **Leave
    out** (O) marks them yes. Where it is out for its body (pointed or long ears, horns, a tail, glowing eyes, scales or
    fur), **Keep as the model saw it** (S) keeps it and writes what the model saw into its description instead
    (`features add:pointedEars` and the like, why "kept as the model saw it"): it still goes to people of its kind,
    those who have what it shows first (a feature only makes a picture closer or further, never shuts it away). Each
    choice is written to `corrections.csv` at once (close it in Excel first). **For many at once**, the bar above the
    pictures (the player: "could I have the option for mass "keep as the model saw it" and also mass "import" for all
    with cutoff borders?"): **Keep all cut at the side** and **Keep all as the model saw it**, each with its count and a
    question first. Each clears only its own kind of reason, so a picture also out for something else (two people,
    limbs that don't make sense) stays out for that; the order doesn't matter. The tabs: Held back, Left out where the check was
    less sure (cut at the side, no one in it, a body its kind lacks), Left out, Kept; a choice can be changed there
    later. Press **Done** on the page when you are finished: the window asks whether to run Update-PortraitLibrary
    now. This is not in the game: a held picture isn't in the records, so the game never sees it.
11. Run `Update-PortraitLibrary.ps1`: the check offers it at its end (Enter), and `Copy-ToLibrary.cmd` and the review
    page's Done run it too. It writes the records again from the batch
    lists and puts the corrections marked yes, ask and hold into them, and says how many it used and how many
    pictures are held back.
    Then **Reload the library** in the game.
12. Run the check again whenever pictures have been added. It asks the model only what it hasn't (every answer is
    kept in `library\checked.csv`, a description of every picture in the library: each line's answer and the version
    it was asked with), and compares all the pictures with their records again: a picture whose correction was used
    leaves the list, and an issue you marked no doesn't come back. New corrections are added at the end of
    `corrections.csv`. A later scan may update the check's own line: where the model now answers otherwise for a
    picture and field the check has a line for, that line gets the new value, and yes or ask by the usual rules,
    instead of a second line (marked "changed" in the report); a line you answered no stays no while the model answers
    the same, and a new, different answer starts again as the rules say. Every other line stays as it is.
13. **The corrections made in the game always win** (the player: "the corrections i make through the game client are
    always prioritized. for example if an image is generating a man, qwen thinks it's a woman and writes that in the
    corrections file, i edit the file within the game detail screen to be a man, the library import or other scripts
    should not override that again with the "correction" from gwen of it being a woman"). Rimfolk's **About this
    picture** window writes its lines with the why `set in the game` (race, xenotype, sex, age, skin, hair colour,
    hair, beard, build, the clothes' age and kind, features, marks, looks, role, headwear, side). `Update-PortraitLibrary.ps1` applies such a line over any other line for the same picture and
    field, whatever their order in the file (for looks, marks and features: over another line for the same word). The
    check never writes a correction against it and never changes it; the report only says so (`5 set in the game`:
    "set in the game: sex male; the model sees otherwise (drawn as a woman ...)").

**What the corrections change.** Nothing is renamed: a picture's name ties it to its batch row, and the row's other
pictures may be right, so a correction changes only that picture's record. The fields:
- `age`: the age band the picture shows. The model is asked for a band by name (young adult, middle-aged ...) and what
  to judge it by (grey hair, wrinkles, sagging skin; a child's head and face). Three bands or more from its row (a child
  drawn for an adult or the other way round) is yes; two bands is ask, since a drawing's lines can read older or
  younger than they are meant; one band apart stays as it is.
- `sex`: `male` or `female`, always ask: the row says the sex, and the model's look at a drawing counts for less (the
  player: the check "should really rely first on the data that is already provided kind of by the CSV files"). A
  picture made for one colonist is never changed: it gets `out`, ask.
- `role`: the work it shows (yes when the row has no role: a weapon gives soldier, warrior or hunter, armour soldier or
  warrior, a crown leader, a book scholar), or `clear` (ask).
- `looks`: `add:word` or `remove:word` (adding wings' look, such as `batWings`, is yes; fire or magic is ask, because
  which look it is can't be told; taking a look away is ask, because the picture would then go to anyone).
- `marks`: `add:eyePatch`, `add:bionicEye` and the like (yes), and `add:artificialArm` for an artificial arm (bionic,
  mechanical or prosthetic) its row lacks (yes; not for a row with an arm look, such as `prostheticArm`, nor for an
  android, whose arms may look made); `remove:...` (ask).
- `headwear`: `true`, so a picture with a hat or hood goes only to people wearing something on the head (yes; ask for
  a colonist's own picture).
- `features` (`add:pointedEars`, `remove:horns` ...) and `side` (`left`, `right`, or `clear` for none: the side of a
  mark, as seen from the person): written by the game's **About this picture** only.
- Also written by **About this picture** only (the player, 23 November 2026: "i should be able to correct things like
  races, haircolor, skin color etc."): `race` (the race's def name, such as `Human` or a mod's race; `human` for none),
  `xenotype` (the xenotype's def name, such as `Yttakin`; `baseliner` for none), `skin` and `hairColour` (`#rrggbb`;
  `none` for no hair colour), `hair` (bald, short, medium, long, braids, bun, tied), `beard` (none, stubble, short,
  full, moustache), `build` (average, slim, heavy, broad), `era` (tribal, medieval, industrial, spacer, or `any`) and
  `dress` (rags, work, fine, robes, armour, or `any`).
- `out`: the picture is left out of the records; the file stays where it is (yes for more than one person in a
  cut-out; ask for a body its kind doesn't have, such as pointed ears on a baseliner or an elf without them, for a
  cut-out cut at the side, and for a person in a backdrop; hold for limbs that don't make sense, until you keep it in
  Review-Pictures). A kind whose people have more than two arms of their own, or a row whose prompt asks for more
  arms, is never held for its arms.
Adding a look or a role only narrows who gets a picture, which is why those can be yes; anything that would give it to
more people is ask.

**Reading the report.** One line per picture: the `picture`, its `row` in the batch list, the `check`, `what is
wrong`, `what the model saw`, what `its row says`, a `suggested fix`, and what is `in corrections.csv` for it. The
check is one of:
- **1 wrong**: the other sex than its row, an age two bands or more from its row's (three or more marked yes), more than one person in a
  cut-out, no one at all, a cut-out cut at the side, or a person in a backdrop.
- **2 shows more**: something its row doesn't have: a weapon, armour, fire or magic, wings, a crown, a book or tools,
  a hat, hood or helmet, horns, pointed ears, a tail, glowing eyes, scales or fur, an eye patch, a bionic eye, an
  artificial arm.
- **3 shows less**: its row says something the model didn't see (a fire mage with no fire, a soldier with no weapon or
  armour, a saurid without scales). Look at the picture: the model can miss things, and the clothes or the face may
  show it.
- **4 not compared**: the picture has no record yet (run `Update-PortraitLibrary.ps1`), or the model's answer couldn't
  be read (it is asked again next time).
- **5 set in the game**: the model sees otherwise than a correction you made in the game; your setting is kept and
  nothing is written. These are not counted among the pictures to look at.
What a look or role asks for is taken from the row's own prompt (its subject and pose columns): a row with the
sniperClass look whose prompt asks for a rifle may show one, and should.

Pictures made for one colonist (a name in their row's `pawn` column) go to that colonist only, so for them only the
sex, the age, the number of people, headwear, the body (ears, horns, scales, eyes), the eye and arm marks and the
sides are checked.

**The side check.** A portrait is only ever cut at the bottom (the player: "if anything is cut off, it's just from the
bottom ... not from the side"). For each cut-out the check reads the picture's transparency itself, with no model:
in the top 85% of the picture (below that the body is cut at the bottom and the elbows may reach the sides), it counts
the rows where the figure's own pixels (more than half opaque) lie in the outermost columns (4 at 1,024 pixels wide).
When those rows add up to more than a tenth of the picture's height on one side, the picture is cut at that side: a
shoulder or upper arm cut by the edge runs along about a third of the height, while a hand or fingertips reaching the
edge cover a few percent and a strand of hair less. It is asked (`out`, ask), since a raised weapon or a hand may run
along the edge on purpose. It reads the pictures with Windows' own System.Drawing; a picture it can't read is left to
the model's check.

The model can be wrong: the list and the ask lines say what to look at. The check never deletes, moves or renames a
picture and never changes a batch list or the records. You can still throw a picture out by deleting it from
`library\<batch>`, or fix a whole row in `batches\<batch>.csv`; then run `Update-PortraitLibrary.ps1`, and the check
again.

**Sharper answers (optional).** The larger Qwen3.5 model sees more detail and comes in a file of about the same size:
`qwen3.5_9b_int8_convrot.safetensors` (about 10 GB) from the same page. Put it in `models\text_encoders`, pick it in the
Load CLIP box, export again over `rimfolk-check-workflow-api.json`, and run the check once with `-Again` (or delete
`library\checked.csv`) so every picture is looked at again.

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
   it hands the backdrop to ComfyUI (into its `input\rimfolk-depth` folder), queues them all at once, as the picture
   check does, and writes each depth map next to its backdrop as it comes, a few seconds each, with a line for each.
   Ctrl+C stops it and takes the ones still waiting out of ComfyUI's queue.
8. What comes out: next to `library\backdrops\<name>.png` a `<name>_depth.png`, a grey picture of the same size.
   Nothing else in the library is changed. At the end it writes the records again (as `Update-PortraitLibrary.ps1`
   does), which now tell the game where each backdrop's depth map is. Then **Reload the library** in the game.
9. Run it again whenever backdrops have been added: it makes only the depth maps that are missing. To have one made
   again, delete its `_depth.png` and run it.

A depth map is never a picture of its own: the library script, the picture check, the copy script and the batch tool
all treat it as part of its backdrop. If the workflow ends in a **Save Image** box instead of Preview Image, ComfyUI
also keeps a copy of each depth map in its `output\Rimfolk-extras` folder; you can delete that folder at any time.

## 10. Variants: expressions and marks (once, about 30 minutes, most of it downloading)

The large portrait can blink now and then and show the person's mood if each picture also exists with the eyes closed,
with a small smile, with a frown and with a wince of pain; and it can show a mark the person got in the game (a lost
eye, an eye patch, a bionic eye, a scar, a burn, a missing ear, an artificial arm) if the picture also exists with that
mark. These variants are made from the picture itself by an editing model: you give it the picture and a plain
instruction ("Close the person's eyes ..."), and it changes only that.
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
3. Delete the text in the Image Edit box's prompt and type exactly `%INSTRUCTION%`. The variants script puts the
   instruction for each variant there.
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
8. Keep ComfyUI open and double-click `Make-Variants.cmd`. It makes **what the game wants** first (the player:
   "variants are first and foremost relevant for my actual colonists"): Rimfolk writes `library\variants-wanted.csv`
   when it saves, with each picture someone in the save holds and the variants it lacks: the expressions, and a mark
   variant when a person holding the picture gets a mark (which eye, which arm, as seen from the person), each with the
   exact instruction for the edit model. The colony's own people come first (priority 1, then 2), the longest wanted
   first. It says at the start how many the game wants, how many are there already and how many it makes.
   Without that list (an older Rimfolk) it asks which pictures, as before, and makes the four expressions:
   - **Enter**: the pictures your game has given someone. First the pictures made for your colonists (a name in their
     row's `pawn` column), then the ones your newest RimWorld save has given people: the colony's own first, then
     notable people, then the rest. It reads the save from RimWorld's Saves folder and changes nothing in it.
   - **a**: every person in the library (this takes long: about two minutes a picture).
9. A variant is made once (the player: "it's also important that a variant is then marked as done once it's created
   and in the game, not that it will get created over and over. unless I delete the variant through the ingame about
   screen"): one already in the library is skipped, whatever the list says. A variant deleted in the game's **About
   this picture** may be wanted again; the new one gets the next free name (`<name>_v-smile-2.png`, `-3` ...), since a
   deleted name stays deleted.
   It hands each picture to ComfyUI once (into its `input\rimfolk-variant` folder), queues every variant at once, as
   the picture check does, and writes each as it comes, about half a minute each, with a line for each. A batch of
   portraits queued meanwhile waits until they are done. To stop, press **Ctrl+C**: what is made is kept, the ones
   still waiting are taken out of ComfyUI's queue, and the next run carries on. Closing the window instead leaves
   them in ComfyUI's queue.
10. What comes out: next to `library\colonists\<name>.png` the files `<name>_v-blink.png`, `<name>_v-smile.png`,
    `<name>_v-frown.png`, `<name>_v-pain.png` and the marks the game wants (`<name>_v-lostEyeLeft.png`,
    `<name>_v-scar.png` ...), each the same size with the same outline. A mark variant can have expressions of its own
    (`<name>_v-lostEyeLeft_v-blink.png`). Nothing else in the library is changed. At the end it writes the records
    again: each variant gets a record of its own, the same as its picture's (with the corrections) and marked as a
    variant of that picture (an expression of a mark variant: of the mark variant); a mark variant also has its mark
    and its side. Any person given the picture later, in any save, gets its variants. Then **Reload the library** in
    the game.
11. Look through them in the folder. If one is not right (the eyes not quite shut, the smile too wide, the face changed),
    delete that file and run it again (or delete it in the game's About this picture): it is made again with a new
    seed.

What it asks the model for: the game's instruction for each variant, as it is written in the list; for an expression
the list gives none for (and without the list), each time followed by "Keep everything else exactly the same: the same
person and face, hair, clothes, pose, position, framing, lighting and painting style.":
- blink: "Close the person's eyes, as in the middle of a blink, the eyelids fully shut and relaxed."
- smile: "Give the person a small, warm smile with the mouth closed."
- frown: "Make the person frown, the brows drawn together and down, the mouth set in a slight scowl."
- pain: "Make the person wince in pain, the eyes squeezed half shut, the brows drawn up and together, the teeth
  clenched."

Each expression keeps its picture's size: the script takes the template's own resizing box out of the way by itself.
If a picture comes back in another size or without its clear background, it stops at the first and says what to
check in the workflow. Variants, like depth maps, are never pictures of their own for the other scripts. A picture
left out by the picture check's corrections (`out`) has its variants left out too. A mark that changes the outline (a
missing ear, an arm of another shape) keeps the picture's outline: look at those closely.

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
- A script stopped by closing its window: its jobs still waiting stay in ComfyUI's queue and run for nothing. Remove
  them in ComfyUI's queue list (or Clear queue if nothing else waits there). Stop a script with Ctrl+C instead.
- ComfyUI's `input\rimfolk-check`, `input\rimfolk-variant` and `input\rimfolk-depth` folders hold a copy of each
  picture handed over (written over on later runs): delete them when no script is running, to free the space.
- The depth maps or expressions stop with "ComfyUI refused the workflow": the message names the box. Most often a model
  file is missing from ComfyUI's `models` folder (step 9, point 2, or step 10, point 1).
- The depth maps stop with "the depth map came out ... the backdrop is ...": a box in the workflow changes the size.
  Add an **Upscale Image** box between the depth box and Preview Image; the script sets its size to each backdrop's.
- The expressions stop with "it came out without a clear background": the **Join Image with Alpha** box is missing or
  not wired into the Save Image (or Preview Image) box (step 10, point 4). With "it came out ... x ...": a box in the
  workflow changes the size; take it out (step 10).
