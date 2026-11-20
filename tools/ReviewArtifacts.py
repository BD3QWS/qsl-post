"""Verify and index the installed application's native-size QA output."""

import csv
import json
import re
import sys
from collections import Counter
from pathlib import Path

import pymupdf
import zxingcpp
from PIL import Image, ImageDraw, ImageFont
from pypdf import PdfReader


ROOT = Path(__file__).resolve().parent.parent
MANUAL = ("https://www.upu.int/UPU/media/upu/files/aboutUpu/acts/"
          "06-manualsInThreeVolumes/actInThreeVolumesConventionManual202609En.pdf")
ATTACHMENTS = {
    "cp85": "袋牌.pdf 第 1 页", "cp84": "袋牌.pdf 第 2 页", "cp83": "袋牌.pdf 第 3 页",
    "domplain": "国内袋牌.pdf 第 1 页", "domregistered": "国内袋牌.pdf 第 2 页",
    "domcombined": "国内袋牌.pdf 第 3 页", "domempty": "国内袋牌.pdf 第 4 页",
    "manifest": "封发卡片清单.pdf", "waybill": "收发邮件路单.pdf",
    "verify": "验单.pdf", "reply": "复验.pdf", "domquery": "国内查单.pdf 正面",
    "domreply": "国内查单.pdf 背面", "intlquery": "查单.pdf 正面", "intlreply": "查单.pdf 背面",
}
BARCODES = [
    "s8", "s9", "cardlabel", "cp83large", "cp84large", "cp85large",
    "upu-cn34", "upu-cn35", "upu-cn36", "upu-cn04", "upu-cn05bis",
    "upu-cn06", "upu-cp73", "upu-cp74",
    "upu-cn31", "upu-cn32", "upu-cp87",
]


def row_dimensions(page):
    return tuple(round(float(value) * 25.4 / 72, 3)
                 for value in (page.mediabox.width, page.mediabox.height))


def verify_size(page, expected):
    actual = row_dimensions(page)
    assert all(abs(a - b) < 0.03 for a, b in zip(actual, expected)), (actual, expected)


def barcode(path, page=0, dpi=210):
    with pymupdf.open(path) as document:
        picture = document[page].get_pixmap(dpi=dpi, alpha=False)
        found = zxingcpp.read_barcodes(Image.frombytes("RGB", (picture.width, picture.height), picture.samples))
    assert len(found) == 1 and found[0].format == zxingcpp.BarcodeFormat.Code128, (path, page, found)
    return found[0].text


def load_catalog(manual_path):
    with (ROOT / "src" / "UpuForms.tsv").open(encoding="utf-8", newline="") as stream:
        upu = list(csv.DictReader(stream, delimiter="\t"))
    with pymupdf.open(manual_path) as manual:
        index = "".join(manual[page].get_text() for page in range(38, 42))
    codes = re.findall(r"^((?:CN|CP)\s+\d{2}(?:bis|ter)?)", index, re.MULTILINE)
    assert set(codes) == {row["code"] for row in upu}
    assert Counter(code[:2] for code in codes) == {"CN": 69, "CP": 18}
    assert {"upu-cn08", "upu-cn08-receipt"} <= {r["key"] for r in upu}
    original = (ROOT / "src" / "Documents.cs").read_text(encoding="utf-8")
    rows = []
    for key, title, width, height in re.findall(r'new FormTemplate\("([^"]+)","([^"]+)","[^"]+",(\d+),(\d+),', original):
        page_source = ATTACHMENTS.get(key)
        rows.append({"key": key, "code": "", "title": title, "variant": "",
                     "width_mm": width, "height_mm": height, "manual_pages": page_source or "本软件辅助单据",
                     "size_basis": "附件 PDF 页面成品尺寸（标称）" if page_source else "软件定义的标签或回执尺寸"})
    assert len(rows) == 20 and set(ATTACHMENTS) <= {r["key"] for r in rows}
    catalog = rows + upu
    keys = {row["key"]: row for row in catalog}
    pairs = re.findall(r'ConnectPages\("([^"]+)", "([^"]+)", (true|false)\)', original)
    for first, second, duplex in pairs:
        keys[first]["following"] = second
        keys[first]["duplex"] = duplex == "true"
    reverse = {second for _, second, _ in pairs}
    return [row for row in catalog if row["key"] not in reverse | {"upu-cn01-back"}]


def make_sheet(qa, keys, output):
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 15)
    sheet = Image.new("RGB", (1040, 810), "#e7edf4")
    draw = ImageDraw.Draw(sheet)
    for index, key in enumerate(keys):
        x, y = (index % 4) * 260, (index // 4) * 270
        with Image.open(qa / "forms" / (key + ".png")) as image:
            image.thumbnail((246, 235))
            sheet.paste(image, (x + (260 - image.width) // 2, y + 29))
        draw.text((x + 8, y + 5), key, font=font, fill="#16324b")
    sheet.save(output)


def review(qa, release, manual, driver):
    catalog = load_catalog(manual)
    forms = qa / "forms"
    aggregate = qa / "QSL邮务台-内置单据样张.pdf"
    expected_keys = {r["key"] for r in catalog}
    assert len(expected_keys) == len(catalog) == 126
    assert {p.stem for p in forms.glob("*.pdf")} == expected_keys
    output = release / "QSL邮务台-原尺寸单据样张.pdf"
    release.mkdir(parents=True, exist_ok=True)
    review_dir = qa / "review"
    review_dir.mkdir(exist_ok=True)
    contents = ["# QSL 邮务台 1.4.0 · 原式尺寸与样张目录",
                "", "成品规格按宽 × 高列出，单位为毫米。样张页码对应同目录的原尺寸混合页 PDF。"
                "样张中的通联、运单与流转均为演示记录。", "",
                "UPU 依据为 2026 年 9 月《公约手册》，表单目录印刷页 XXXIX–XLII。"
                "下表中的 UPU 页码为手册印刷页码，链接指向对应的 PDF 页。"
                f"[UPU《公约手册》表单目录]({MANUAL}#page=39)", "",
                "| 样张页 | UPU 编号 | QSL 单据 | 成品尺寸 | 来源页 | 尺寸依据 |",
                "|---:|---|---|---|---|---|"]
    reader = PdfReader(aggregate)
    toc, pages, positions = [], [], {}
    current = 0
    previous_family = None
    for row in catalog:
        key, code = row["key"], row["code"]
        title = row["title"] + (" · " + row["variant"] if row["variant"] else "")
        group = ("UPU 函件 CN" if code.startswith("CN ") else
                 "UPU 包裹 CP" if code.startswith("CP ") else "附件与辅助单据")
        if group != previous_family:
            toc.append([1, group, current + 1])
            previous_family = group
        w, h = int(row["width_mm"]), int(row["height_mm"])
        individual = PdfReader(forms / (key + ".pdf"))
        positions[key] = current
        if row.get("following"):
            assert len(individual.pages) >= 2, key
            positions[row["following"]] = current + 1
        toc.append([2, ((code + " · ") if code else "") + title, current + 1])
        for index, page in enumerate(individual.pages):
            dimensions = row_dimensions(page)
            if index == 0 or index == 1 and row.get("duplex"):
                verify_size(page, (w, h))
            elif dimensions != (w, h):
                verify_size(page, (148, 210))
            verify_size(reader.pages[current], dimensions)
            pages.append(dimensions)
            current += 1
        label = row["manual_pages"]
        source = f"[{label}]({MANUAL}#page={int(re.match(r'\d+', label).group()) + 42})" if code else label
        first = positions[key] + 1
        page_range = str(first) if current == first else f"{first}–{current}"
        contents.append(f"| {page_range} | {code} | {title} | {w} × {h} | {source} | {row['size_basis']} |")
    assert current == len(reader.pages)
    with pymupdf.open(aggregate) as sample:
        sample.set_toc(toc)
        sample.set_metadata({"title": "QSL 邮务台 1.4.0 原式单据样张", "subject": "QSL 邮务台测试资料"})
        sample.save(output, garbage=3, deflate=True)
    delivered = PdfReader(output)
    for page, dimensions in zip(delivered.pages, pages):
        verify_size(page, dimensions)
    assert len(delivered.pages) == len(pages)
    decoded = {}
    for key in BARCODES:
        dpi = 203 if key in {"upu-cn04", "upu-cn05bis", "upu-cn06", "upu-cp73", "upu-cp74"} else 300
        individual = barcode(forms / (key + ".pdf"), dpi=dpi)
        assert barcode(output, positions[key], dpi=dpi) == individual
        decoded[key] = individual
    assert decoded["cp85large"] == decoded["s9"] and decoded["s9"].startswith(decoded["s8"])
    assert decoded["upu-cn31"] == decoded["s8"]
    assert decoded["upu-cn32"] == decoded["upu-cp87"]
    assert re.fullmatch(r"[A-Z]{15}[0-9]{5}", decoded["upu-cp87"])
    if driver:
        driver_size = row_dimensions(PdfReader(driver).pages[0])
        assert all(abs(a - b) < 0.15 for a, b in zip(driver_size, (210, 297)))
        assert barcode(driver, dpi=300) == decoded["cp85large"]
    representative = ["cp85large", "cp85", "domregistered", "manifest", "waybill", "verify", "reply",
                      "domquery", "domreply", "intlquery", "intlreply", "upu-cn08", "upu-cn08-back",
                      "upu-cn31", "upu-cn38", "upu-cn43", "upu-cp72", "upu-cp78", "upu-cp87"]
    featured = release / "QSL邮务台-重点单据样张.pdf"
    with pymupdf.open(output) as sample, pymupdf.open() as selection:
        by_position = {entry[2]-1: entry[1] for entry in toc if entry[0] == 2}
        featured_toc = []
        for number, key in enumerate(representative, 1):
            position = positions[key]
            selection.insert_pdf(sample, from_page=position, to_page=position)
            featured_toc.append([1, by_position.get(position,key), number])
        selection.set_toc(featured_toc)
        selection.set_metadata({"title": "QSL 邮务台 1.4.0 重点单据样张", "subject": "QSL 邮务台测试资料"})
        selection.save(featured, garbage=3, deflate=True)
    for page, key in zip(PdfReader(featured).pages, representative):
        verify_size(page, pages[positions[key]])
    for number, keys in enumerate((
        ["cp85", "cp85large", "domregistered", "manifest", "waybill", "verify", "reply", "domquery",
         "intlquery", "s8", "cardlabel", "rd10"],
        ["upu-cn01", "upu-cn04", "upu-cn08", "upu-cn14", "upu-cn22", "upu-cn23", "upu-cn25-tr",
         "upu-cn29", "upu-cn31", "upu-cn37", "upu-cn54bis", "upu-cn70"],
        ["upu-cp72", "upu-cp72-customs", "upu-cp73", "upu-cp77", "upu-cp78", "upu-cp82",
         "upu-cp88", "upu-cp91", "upu-cp92", "upu-cp94bis", "upu-cp95", "upu-cn45"],
    ), 1):
        make_sheet(qa, keys, review_dir / f"版式检查-{number}.png")
    contents += ["", "装订联、回单和正反面各自保留成品规格。封套样张为正面。"
                 "尺寸存在范围的表单可在软件里挑选规格或使用自定义纸张。"]
    (release / "QSL邮务台-原版尺寸与样张目录.md").write_text("\n".join(contents) + "\n", encoding="utf-8")
    report = {"templates": len(catalog), "sample_pdf_pages": len(pages),
              "distinct_media_sizes": len(set(pages)), "barcode_templates_roundtripped": len(decoded),
              "virtual_driver_A4_roundtrip": bool(driver)}
    (review_dir / "verification.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    review(*(Path(arg).resolve() for arg in sys.argv[1:4]), Path(sys.argv[4]).resolve() if len(sys.argv) > 4 else None)
