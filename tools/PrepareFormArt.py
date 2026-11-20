"""Extract the specimens used by the catalog from the supplied PDFs.

Run with the directory containing the nine reference PDFs. All page numbers in
UpuForms.tsv use the manual's printed numbering. Its PDF has 42 preliminary pages.
"""
import argparse
import csv
import io
import re
from pathlib import Path

import numpy as np
import pymupdf
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
ATTACHMENTS = {
    "袋牌": ("cp85", "cp84", "cp83"),
    "国内袋牌": ("domplain", "domregistered", "domcombined", "domempty"),
    "封发卡片清单": ("manifest",), "收发邮件路单": ("waybill",),
    "验单": ("verify",), "复验": ("reply",),
    "国内查单": ("domquery", "domreply"), "查单": ("intlquery", "intlreply"),
}

# Pages that contain multiple specimens or different parts of one document.
SPECIMENS = {
    "upu-cn05bis": (244, 2), "upu-cn05bis-prio": (244, 1),
    "upu-cn23-back": (340, 2), "upu-cn35": (150, 2),
    "upu-cn54": (517, 2), "upu-cn54bis-back": (518, 2),
    "upu-cn57bis": (527, 2), "upu-cn71bis": (525, 2),
    "upu-cp72-back": (174, 2), "upu-cp72-label-back": (175, 2),
    "upu-cp72-customs-back": (176, 2), "upu-cp74": (224, 2),
    "upu-cp82": (184, 2), "cp85large": (196, 2),
    "upu-cp92": (204, 2), "upu-cp95": (230, 2),
}

# Fractional bounds cut out the form itself when the manual shows multiple items
# together or prints a paper-size annotation below the specimen.
CROPS = {
    "upu-cn04": (.018, .167, .978, .783),
    "upu-cn05bis": (.004, .170, .996, .865),
    "upu-cn06": (.500, .145, .982, .670),
    "upu-cn06-mark": (.012, .400, .258, .675),
    "upu-cn15": (.023, .057, .821, .902),
    "upu-cn29": (.054, .171, .730, .775),
    "upu-cn29bis": (.004, .015, .970, .930),
    "upu-cp73": (.028, .206, .983, .845),
    "upu-cp74": (.030, .170, .940, .672),
    "upu-cp95": (.005, .017, .977, .904),
    "upu-cn07": (.003, .006, .995, .949),
    "upu-cn11": (.002, .002, .967, .967),
}

BAR_TAGS = {"upu-cn34", "upu-cn35", "upu-cn36", "cp83large", "cp84large", "cp85large"}
NUMBER_TAGS = {"upu-cn04", "upu-cn05bis", "upu-cn06", "upu-cp73", "upu-cp74"}
NUMBER_SAMPLES = {
    "upu-cn34": (333,226,765,304), "upu-cn35": (331,226,763,304),
    "upu-cn36": (334,228,769,307), "cp83large": (336,228,775,307),
    "cp84large": (325,217,751,294), "cp85large": (323,218,750,296),
    "upu-cn31": (9,24,388,67), "upu-cn32": (7,26,386,69), "upu-cp87": (5,24,386,68),
}


def cut(image, rect):
    w, h = image.size
    x0, y0, x1, y1 = rect
    return image.crop(tuple(round(v) for v in (x0 * w, y0 * h, x1 * w, y1 * h)))


def paint(image, rect, color="white"):
    w, h = image.size
    x0, y0, x1, y1 = rect
    ImageDraw.Draw(image).rectangle((round(x0*w), round(y0*h), round(x1*w), round(y1*h)), fill=color)


def line_spans(indexes):
    groups = np.split(indexes, np.flatnonzero(np.diff(indexes) > 1) + 1)
    return [(int(g[0]), int(g[-1])) for g in groups if len(g)]


def bundle(image, key):
    gray = np.asarray(image.convert("L"))
    edges = line_spans(np.flatnonzero((gray < 170).mean(axis=1) > .88))
    boxes = [(a[0], b[1]) for a, b in zip(edges, edges[1:]) if .24*image.height < b[1]-a[0] < .32*image.height]
    index = 0 if re.search(r"-t?lc(?:-|$)", key) else 1 if re.search(r"-t?ao(?:-|$)", key) else 2
    if len(boxes) != 3:
        raise ValueError(f"Expected three bundle specimen borders for {key}, found {boxes}")
    top, bottom = boxes[index]
    part = image.crop((0, top, image.width, bottom+1))
    ink = part.convert("L").point(lambda p: 255 if p < 170 else 0).getbbox()
    return part.crop((ink[0], 0, ink[2], part.height))


def title(image, rect, label, size):
    paint(image, rect)
    x0, y0, x1, y1 = rect
    font = ImageFont.truetype("arialbd.ttf", max(10, round(image.height * size)))
    ImageDraw.Draw(image).text((x0*image.width+2, (y0+y1)*image.height/2), label,
                               anchor="lm", fill="black", font=font)


def instruction_back(image, wide):
    # The backs of these originals are a single instruction block. Keep its
    # original frame and heading position and replace the postal directions.
    rect = (.012, .110, .990, .980) if wide else (.024, .061, .980, .988)
    paint(image, rect)
    lines = [
        "1. Identifier le bureau QSL d'origine et le destinataire.",
        "2. Inscrire les indicatifs, le numéro de la carte et le suivi réel.",
        "3. Vérifier la quantité et le poids brut du lot.",
        "4. Noter tout écart constaté lors de la réception.",
        "5. Dater et signer la remise entre les bureaux QSL.",
        "",
        "1. Enter the QSL bureau and the destination station.",
        "2. Enter call signs, card ID and the carrier tracking number.",
        "3. Check the card count and the gross weight.",
        "4. Record any discrepancy found on receipt.",
        "5. Date and sign the handover between QSL bureaus.",
    ]
    draw = ImageDraw.Draw(image)
    size = max(8, int(min(image.height*.028, image.width/45)))
    font = ImageFont.truetype("arial.ttf", size)
    x, y = rect[0]*image.width, rect[1]*image.height
    for line in lines:
        remaining = line.split()
        while remaining:
            words = []
            while remaining and (not words or draw.textlength(" ".join(words+[remaining[0]]), font=font) <= .90*image.width):
                words.append(remaining.pop(0))
            draw.text((x, y), " ".join(words), fill="black", font=font)
            y += size*1.36
        if not line:
            y += size*1.36


def specimen(pdf, row):
    key = row["key"]
    page, part = SPECIMENS.get(key, (int(re.match(r"\d+", row["manual_pages"]).group()), 1))
    source = pdf[page + 41]
    xref = source.get_image_info(xrefs=True)[part - 1]["xref"]
    image = Image.open(io.BytesIO(pdf.extract_image(xref)["image"])).convert("RGB")

    if key.startswith(("upu-cp72", "upu-cn23")):
        image = image.transpose(Image.Transpose.ROTATE_270)
        bounds = {
            "upu-cp72": (25,31,1165,836), "upu-cp72-back": (7,18,967,714),
            "upu-cp72-label": (13,20,973,717), "upu-cp72-label-back": (3,22,958,696),
            "upu-cp72-customs": (5,23,966,719), "upu-cp72-customs-back": (4,15,958,688),
            "upu-cn23": (2,7,954,680), "upu-cn23-back": (2,13,956,686),
        }
        image=image.crop(bounds[key])
    elif row["layout"] == "bundle" and key.startswith(("upu-cn25", "upu-cn26")):
        image = bundle(image, key)
    elif key.startswith("upu-cn22"):
        bounds = (.553, .194, .994, .933) if key.endswith("-back") else (.008, .194, .449, .933)
        image = cut(image, bounds)
    elif key in CROPS:
        image = cut(image, CROPS[key])

    if key in NUMBER_SAMPLES:
        ImageDraw.Draw(image).rectangle(NUMBER_SAMPLES[key], fill="white")
    if key in NUMBER_TAGS:
        if key=="upu-cn05bis":
            paint(image, (.295,.205,.950,.365))
            paint(image, (.022,.487,.960,.910))
        else:
            paint(image, (.16,.057,.948,.325))
            paint(image, (.030,.515,.963,.932))
    if key in ("upu-cp72", "upu-cp72-label", "upu-cp72-customs"):
        paint(image, (.650,.009,.955,.153))
    if key in BAR_TAGS:
        # Remove the manual caption under the cut line.
        image = cut(image, (0, 0, 1, .944))
        if key.startswith("cp"):
            title(image, (.130,.033,.345,.103), "QSL CARDS", .041)
    if key in ("upu-cn22", "upu-cn22-max"):
        paint(image, (.338,.006,.646,.085))
        title(image, (.008,.094,.351,.182), "QSL CONTENTS", .041)
    if key in ("upu-cn22-back", "upu-cn22-max-back", "upu-cn23-back", "upu-cp72-customs-back"):
        instruction_back(image, key.startswith(("upu-cn23", "upu-cp72")))
    if key == "upu-cn23":
        title(image, (.50,.021,.82,.059), "QSL CONTENTS LIST", .028)
        title(image, (.627,.432,.79,.466), "QSL card ID", .018)
    if key == "upu-cn03":
        paint(image, (.486,.100,.958,.182))
    if key == "upu-cn06-mark":
        paint(image, (.31,.095,.962,.874))
    if key == "upu-cn01":
        # Retain the specimen's illustration and the two stamp positions.
        # Replace the issuer and redemption wording with a club-only coupon.
        paint(image, (.690,.019,.979,.128), "#e8e9de")
        paint(image, (.617,.667,.979,.897), "#e8e9de")
        paint(image, (.024,.676,.181,.799), "#e8e9de")
        paint(image, (.108,.412,.178,.475), "#e8e9de")
        draw=ImageDraw.Draw(image)
        font=ImageFont.truetype("arial.ttf", max(10, int(image.height*.027)))
        draw.text((image.width*.725,image.height*.040), "QSL Bureau", font=font, fill="#243521")
        draw.multiline_text((image.width*.635,image.height*.685),
                            "Coupon-réponse QSL\nQSL return card voucher\nÉchange interne au bureau QSL\nFor QSL bureau use", font=font, fill="#243521")
    return image


def save(image, output):
    # Bilevel/grayscale artwork is stored losslessly without an RGB triplet per pixel.
    r, g, b = image.split()
    if np.array_equal(np.asarray(r), np.asarray(g)) and np.array_equal(np.asarray(r), np.asarray(b)):
        image = r
    image.save(output, optimize=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("references", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "src" / "FormArt")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)

    for filename, keys in ATTACHMENTS.items():
        with pymupdf.open(args.references / (filename+".pdf")) as pages:
            if len(pages) != len(keys):
                raise ValueError(f"Unexpected number of pages in {filename}")
            for key, page in zip(keys, pages):
                pix = page.get_pixmap(dpi=420, alpha=False, annots=False)
                image = Image.frombytes("RGB", (pix.width,pix.height), pix.samples)
                save(image, args.output/(key+".png"))

    with pymupdf.open(args.references / "actInThreeVolumesConventionManual202609En.pdf") as manual:
        if len(manual) != 600:
            raise ValueError("This catalog expects the 600-page September 2026 PDF")
        with (ROOT/"src"/"UpuForms.tsv").open(encoding="utf-8-sig") as stream:
            for row in csv.DictReader(stream, delimiter="\t"):
                if row["key"] != "upu-cn01-back":
                    save(specimen(manual,row), args.output/(row["key"]+".png"))


if __name__ == "__main__":
    main()
