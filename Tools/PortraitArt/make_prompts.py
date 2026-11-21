"""Writes docs/portrait-art/prompts.csv: one prompt per portrait of Rimfolk's portrait library.

Each row is one picture to make with an image AI. Its file name carries the tags Rimfolk matches a pawn by:
sex_age_skin_hair_length_beard_era_number.png, for example m_adult_s3_brown_short_beard_medieval_04.png.
Run: python3 Tools/PortraitArt/make_prompts.py
"""
import csv, os

SKINS = [("s1", "very fair"), ("s2", "fair"), ("s3", "olive"), ("s4", "brown"), ("s5", "dark brown")]
HAIR = [("black", "black"), ("brown", "dark brown"), ("lbrown", "light brown"), ("blond", "blond"), ("red", "auburn red")]
AGES = {"child": "a child of about ten", "young": "a young", "adult": "an adult", "old": "an elderly"}
CLOTHES = {
    "tribal": ["hide and fur clothing with bone beads", "a woven plant-fibre wrap tied with a leather cord", "a fur-trimmed leather tunic with a carved wooden pendant"],
    "medieval": ["a linen tunic with a wool cloak held by a brooch", "a padded gambeson", "a leather jerkin over a linen shirt", "an embroidered wool dress with a cloak"],
    "industrial": ["a canvas work jacket over a collared shirt", "a long duster coat with a scarf", "a buttoned waistcoat with rolled shirt sleeves", "a flak vest over a work shirt"],
    "spacer": ["a sleek padded flight suit with a high collar", "a light armoured vest with soft glowing seams", "a clean synthetic jacket with geometric trim"],
}
HAIRSTYLE = {
    ("m", "short"): ["short cropped hair", "short messy hair", "shaved sides with a short top", "short swept-back hair"],
    ("m", "long"): ["shoulder-length hair tied back", "long loose hair"],
    ("f", "short"): ["a short bob", "a pixie cut", "short wavy hair"],
    ("f", "long"): ["long straight hair", "long braided hair", "hair in a neat bun", "long wavy hair"],
}
BEARDS = [("beard", "a full beard"), ("stubble", "short stubble"), ("moustache", "a moustache")]
EXPR = ["a calm expression", "a slight smile", "a serious look", "a determined look", "a thoughtful look", "a warm, friendly look"]

FRAME = ("Head-and-shoulders portrait, facing the viewer with a slight three-quarter turn to the left, centred, "
         "the eyes about 40 percent from the top, the shoulders reaching the bottom edge. One person only. "
         "Plain flat solid green background (#00B140), no text, no frame, no props, no hands, no helmet or hat.")


def rows():
    out = []
    n = 0
    for era in ["tribal", "medieval", "industrial", "spacer"]:
        for sex in ["m", "f"]:
            plan = [("adult", 12), ("young", 5), ("old", 5), ("child", 3)]
            k = 0
            for age, count in plan:
                for i in range(count):
                    skin = SKINS[(k + i) % 5]
                    if age == "old":
                        hair = ("grey", "grey") if i % 3 else ("white", "white")
                    else:
                        hair = HAIR[(k * 2 + i) % 5]
                    length = "long" if (i + (1 if sex == "f" else 0)) % 2 else "short"
                    if sex == "f" and age != "child" and i % 3 == 0: length = "long"
                    style = HAIRSTYLE[(sex, length)][(k + i) % len(HAIRSTYLE[(sex, length)])]
                    beard = ("nobeard", "clean-shaven")
                    if sex == "m" and age in ("adult", "old") and i % 2 == 0:
                        beard = BEARDS[(k + i) % 3]
                    if sex == "m" and age in ("adult", "old") and i == count - 1 and length == "short":
                        style = "a bald head"; length = "bald"
                    clothes = CLOTHES[era]
                    cl = clothes[(k + i) % len(clothes)]
                    if sex == "m" and "dress" in cl: cl = clothes[(k + i + 1) % len(clothes)]
                    who = AGES[age] + (" " + ("man" if sex == "m" else "woman") if age not in ("child",) else (" boy" if sex == "m" else " girl"))
                    if age == "child": who = "a " + ("boy" if sex == "m" else "girl") + " of about ten"
                    hair_txt = style if style == "a bald head" else f"{hair[1]} hair, {style}"
                    desc = f"{who} with {skin[1]} skin, {hair_txt}" + (f", {beard[1]}" if beard[0] != "nobeard" else "") + f", wearing {cl}, {EXPR[(n) % len(EXPR)]}."
                    n += 1
                    fname = f"{sex}_{age}_{skin[0]}_{hair[0]}_{length}_{beard[0]}_{era}_{n:03d}.png"
                    out.append(dict(file=fname, pilot="", sex=sex, age=age, skin=skin[0], hair=hair[0], length=length, beard=beard[0], era=era, subject=desc))
                k += 3
    pilot = [0, 13, 30, 47, 52, 66, 78, 95, 101, 117, 128, 141, 150, 163, 177, 190]
    for i in pilot:
        if i < len(out): out[i]["pilot"] = "yes"
    return out


def main():
    root = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "portrait-art")
    os.makedirs(root, exist_ok=True)
    rs = rows()
    with open(os.path.join(root, "prompts.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["file", "pilot", "sex", "age", "skin", "hair", "length", "beard", "era", "subject", "frame"])
        w.writeheader()
        for r in rs:
            r["frame"] = FRAME
            w.writerow(r)
    print(len(rs), "rows,", sum(1 for r in rs if r["pilot"]), "in the pilot")


if __name__ == "__main__":
    main()
