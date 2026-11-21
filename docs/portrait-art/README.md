# Rimfolk's portrait library: how to make the pictures

Rimfolk shows each person as a painted head-and-shoulders portrait chosen from a library: it picks the picture closest
to the pawn (sex, age, skin, hair colour and length, beard, the age of their clothes) and keeps it for that pawn for good.
The pictures are made with an image AI from the prompts in `prompts.csv`, one row per picture. A pawn the library has no
close picture for keeps the game's own face.

## 1. Pick a tool

Any good image AI works. Ones that do this well (check their current prices and terms yourself):
- **Midjourney**: the most attractive results with the least effort, and a "style reference" option that keeps a whole
  series in one look. Paid.
- **ChatGPT (image generation)**: follows long, exact prompts well, can match a reference image you attach, and can give a
  transparent background.
- **Google Gemini (image generation)**: good at keeping one look across many pictures when you attach a reference.
- **Stable Diffusion or Flux on your own computer** (with ComfyUI or Forge): free and can make all 200 in one batch from
  the list, with the same style every time; needs a strong graphics card and some setup.

If you ever publish the mod with the pictures, check that the tool's terms allow it.

### Free, step by step (no paid licence)

Midjourney has no free plan; ChatGPT's free plan allows only a few pictures a day. These cost nothing (their daily limits
change, so spread the work over a few days if one runs out):

**Google Gemini (recommended)**
1. Open https://gemini.google.com in your browser and sign in with a Google account (a free one is enough).
2. Open `prompts.csv` in Excel or Google Sheets. In this README pick a style line (section 2 below).
3. In a new Gemini chat, write: the style line, then row 1's `subject`, then row 1's `frame`. Send it.
4. Not right yet? Answer in plain words ("more detailed", "no hat", "the background plain flat green", "just the head and
   shoulders"). When it's right, open the picture, press the download button, and rename the file to row 1's `file`
   name. Keep all pictures in one folder, for example `Rimfolk portraits` on your Desktop.
5. Next row, in the same chat: "Same style, framing and background as the last picture. Now: " and the row's `subject`.
   Download and rename as before. If the look starts to drift, attach your best picture and write "Match the style of
   the attached picture exactly."
6. After the 16 trial rows: right-click the folder, Send to, "Zip for upload", and send the zip.

**Microsoft Bing Image Creator (a second free one)**: https://www.bing.com/images/create with a free Microsoft account;
the same prompts; it gives four pictures at a time; download the best and rename it.

**On your own computer, free and without limits (needs a recent NVIDIA graphics card, 6 GB or more)**: Fooocus, a
one-click app for Stable Diffusion (https://github.com/lllyasviel/Fooocus: download the Windows package, unzip, run
`run.bat`; the first start downloads the model). Put the style line in its prompt with the row's `subject` and `frame`,
set the aspect ratio to 896x1152, generate, and save under the row's `file` name.

A small mark or watermark in a corner doesn't matter: the picture is cropped to the head and shoulders anyway.

## 2. Pick a style

Put one of these in front of every prompt (or write your own):
- **Painted fantasy**: "Digital painting of a character portrait for a fantasy colony game, painterly brushwork, soft warm
  rim light, rich natural colours, detailed face, clean silhouette."
- **Anime**: "Anime character portrait, clean confident lineart, cel shading with soft gradients, expressive eyes, vibrant
  but harmonious colours, polished key-visual quality."
- **Graphic novel**: "Graphic-novel character portrait, bold ink lines, flat colours with crisp shadows, strong shapes,
  subtle halftone texture."
- **Storybook**: "Storybook gouache illustration of a character portrait, soft textured paint, gentle colours, warm and
  charming."
- **Pixel art**: "Detailed pixel art character portrait, 16-bit RPG style, a clear pixel grid, limited palette with
  hue-shifted shading, crisp dark outlines." (AI pixel art often has an uneven grid; the lead snaps it to a true one.)

## 3. Make one picture you love

Take the first row of `prompts.csv`. The full prompt is: the style, then the row's `subject`, then the row's `frame`.
Generate until you love the result. Save it as `style_reference.png`: from now on attach it to every prompt as the style
reference (Midjourney: `--sref` with its link; ChatGPT or Gemini: attach it and add "Match the style of the attached
image exactly.").

## 4. The trial: 16 pictures

Make the rows marked `yes` in the `pilot` column. Save each as PNG under the exact name in the `file` column (the name
carries what Rimfolk matches by). Square or 4:5, any size from 1024 pixels up. Make again any that comes out with a hat,
text, a frame, hands, two people, or hair or a beard different from the prompt.

Zip them with "Zip for upload" and send them. The lead checks each, takes out the green background, lines them up so the
eyes sit at the same height, shrinks them to the mod's size and puts them in the game; you look at them in the game.

## 5. The rest

If the trial looks good, the other 184 rows the same way, in batches of any size. Afterwards a second, smaller list can
add the mods' peoples (pointed ears, horns, unusual skin) and more variety where the game shows gaps.

The list is made by `Tools/PortraitArt/make_prompts.py`; change it there and run it again rather than editing the CSV.
