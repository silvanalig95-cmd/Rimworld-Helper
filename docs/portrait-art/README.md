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
