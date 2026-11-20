---
name: "QSL 邮务台"
description: "浅蓝与淡紫玻璃围绕清晰可读的 Windows 邮务工作区。"
colors:
  blue: "#1c5bb3"
  blue-button-top: "#3171c6"
  blue-button-bottom: "#174d9a"
  backdrop-blue: "#cfe7f7"
  backdrop-lavender: "#e1ddf6"
  ink: "#193048"
  quiet: "#4a5f75"
  tint: "#e1edfa"
  glass-top: "#ffffffd3"
  glass-bottom: "#f5faffa5"
  glass-edge: "#ffffffe6"
  table-header: "#e8f0fa"
  table-row: "#f9fcff"
  table-alternate: "#f1f7fd"
  table-line: "#e3ebf4"
  table-selected: "#d6e8fe"
  table-selected-ink: "#124077"
  preview-matte: "#d6e3f1"
  white: "#ffffff"
  print-ink: "#000000"
typography:
  headline:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "26pt"
    fontWeight: 700
  title:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "15pt"
    fontWeight: 700
  body:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "10pt"
    fontWeight: 400
  table-body:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "9.5pt"
    fontWeight: 400
  label:
    fontFamily: "Microsoft YaHei UI"
    fontSize: "9pt"
    fontWeight: 700
rounded:
  panel: "18px"
  button: "16px"
spacing:
  action-gap: "9px"
  panel-inset: "18px"
components:
  button-primary:
    textColor: "{colors.white}"
    typography: "{typography.body}"
    rounded: "{rounded.button}"
    height: "38px"
  button-secondary:
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.button}"
    height: "38px"
  navigation-item:
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.button}"
    width: "160px"
    height: "45px"
  input:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    height: "30px"
  glass-panel:
    rounded: "{rounded.panel}"
    padding: "{spacing.panel-inset}"
  ledger-row:
    backgroundColor: "{colors.table-row}"
    textColor: "{colors.ink}"
    typography: "{typography.table-body}"
    height: "37px"
  ledger-row-selected:
    backgroundColor: "{colors.table-selected}"
    textColor: "{colors.table-selected-ink}"
  print-preview:
    backgroundColor: "{colors.preview-matte}"
---

# Design System: QSL 邮务台

## Overview

**Creative North Star: "现代液态玻璃"**

延续 QSL 卡片工坊和肥龙量化的 Windows 桌面风格。浅蓝到淡紫的底色贯穿窗口，半透明白色工作面承托台账和表单。深蓝标题和大面积实底让呼号、编号和状态可以连续扫读。

- 玻璃导航、工具栏按钮和容器使用细白边。
- 表格、下拉框和页面预览使用实底。
- 单据使用白纸、黑字和细表线，保留实际纸张比例。

## Colors

### Primary

蓝色渐变标记登记、导出 PDF 等主要动作。实蓝用于时间轴节点和浅色按钮上的焦点描边。导航选中与表格选中各使用相应的浅蓝填充。

### Neutral

深蓝墨色用于正文和列值，灰蓝用于提示、状态栏和表头。背景从浅蓝过渡到淡紫，叠加两处柔和光晕。纸张预览的蓝灰底衬托白纸轮廓。

**The Opaque Tables Rule.** 表头、普通行、交替行和选中行使用不透明底色。

## Typography

界面采用 Microsoft YaHei UI，窗口标题为粗体（26 pt），打印工作室标题为粗体（23 pt），详情标题为粗体（15 pt）。通用输入和按钮为常规（10 pt），台账列值为常规（9.5 pt），表头与打印设置标签为粗体（9 pt）。扫码提示和时间轴细节为常规（8 pt）。字号在代码中以 `96/72` 换算为绘制像素。纸面文字使用 Microsoft YaHei，由单据模板指定字号。

## Layout

主窗口默认打开尺寸为 1440 × 910 px，最小尺寸为 1060 × 700 px，启动时按显示器的工作区限制窗口大小。固定的 206 px 导航列配合可伸展的工作区，导航在较矮的工作区可以纵向滚动。标题宽度随窗口调整，右上角为 320 px 的扫码区域。台账列表与详情采用 74:26 的分栏。

打印工作室默认打开尺寸为 1320 × 880 px，最小尺寸为 1050 × 690 px。左侧 330 px 设置列可垂直滚动，输入框随设置列的可用宽度调整。右侧预览随窗口伸展，纸张按比例缩放。预览页脚显示正反面、页码和纸张毫米尺寸。

## Elevation & Depth

GDI+ 缓存按窗口尺寸绘制的渐变和光晕。玻璃容器叠加半透明白色渐变、1 px 高光轮廓和一层低透明度蓝灰底影。按钮悬停和焦点状态通过重绘显示。

**The Paper Preview Rule.** 纸面保持黑白单据样式，玻璃效果用于预览周围的界面。

## Shapes

玻璃容器圆角为 18 px，常规按钮圆角最大为 16 px。台账、表单输入和纸面保留方角。导航采用 21 px GDI+ 线描图标和中文文字，应用图标为原创信封与电波图形。图标来源记录在 `docs/QslPost-icon.png.json`。

## Components

- **按钮和导航。** 主要按钮使用蓝色渐变和白字，次要按钮使用半透明浅色渐变和深蓝字。导航选中使用淡蓝渐变，键盘焦点使用内侧点线。
- **输入与扫码。** 文本框、下拉框、数字输入和复选框沿用 WinForms 交互。右上角原生文本框支持键盘式扫码枪。`Ctrl+F` 聚焦并选中当前输入，回车查询卡号、袋号或物流号。
- **台账与时间轴。** 表头 39 px，标准行 37 px，交替行有浅蓝底和水平细分隔线。整行选择后在邻侧展示记录资料和操作时间轴，时间轴使用实蓝节点与细连线。
- **纸张预览。** 毫米坐标生成白底单据、黑色表线和字迹以及适用模板的 Code 128 条码。A4 和自定义纸张共用模板排版，100% 对应模板成品尺寸。

## Do's and Don'ts

- **Do** 为新台账沿用不透明表头、交替行和整行选中态。
- **Do** 在打印界面保留纸张尺寸、缩放和页码，并按实际尺寸绘制单据。
