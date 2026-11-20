from pathlib import Path
from PIL import Image, ImageDraw
import json
root=Path(__file__).resolve().parent.parent
size=512
im=Image.new('RGBA',(size,size),(0,0,0,0));d=ImageDraw.Draw(im)
d.rounded_rectangle((16,16,496,496),radius=112,fill='#205db2')
d.rounded_rectangle((30,30,482,482),radius=100,outline='#99cbf8',width=4)
d.rounded_rectangle((92,192,420,398),radius=24,fill='#f7fcff')
d.line([(101,205),(256,315),(411,205)],fill='#205db2',width=16,joint='curve')
d.ellipse((244,124,268,148),fill='#f7fcff')
d.arc((197,74,315,192),211,329,fill='#f7fcff',width=13)
d.arc((153,30,359,236),214,326,fill='#cae7ff',width=11)
im.save(root/'src'/'QslPost.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
im.save(root/'docs'/'QslPost-icon.png')
(root/'docs'/'QslPost-icon.png.json').write_text(json.dumps({'origin':'Original geometric envelope and radio-wave icon drawn by tools/MakeIcon.py for QSL 邮务台; no external artwork.'},ensure_ascii=False,indent=2),encoding='utf-8')
