# Генерирует стартовую палитру леса (256x256, 8x4 полос-градиентов) и превью с подписями.
# Запуск: python make_palette.py (нужен Pillow). Результат копировать в Game/Assets/_Project/Art/Textures/Environment/.
from PIL import Image, ImageDraw, ImageFont
import colorsys
rows = [
 [("Листва тёмная","#2f6b3a"),("Листва","#4f9a3f"),("Листва светлая","#8cc24a"),("Листва бирюза","#2f8a6e"),("Осень оранж","#d9822b"),("Осень красн","#b8402e"),("Цветение","#e58fb0"),("Сирень","#8a63c4")],
 [("Трава","#5fa83c"),("Мох","#6f8f2e"),("Сухая трава","#b8a64a"),("Земля","#8a5a36"),("Тёмная земля","#5a3a26"),("Песок","#d8bd82"),("Тропа","#a88a62"),("Грязь","#6b5440")],
 [("Кора тёмная","#4a3426"),("Кора","#6e4b32"),("Кора светлая","#9a7450"),("Береза","#d8d2c0"),("Сухое дерево","#8a8478"),("Доски","#a06c3c"),("Хвоя","#2e5a46"),("Хвоя светл","#4f8060")],
 [("Камень холод","#7d8590"),("Камень тёпл","#9a8f82"),("Камень синий","#6a7a96"),("Камень мох","#6f7f5a"),("Камень тёмный","#4e5058"),("Камень светлый","#bfbab0"),("Цветы жёлт","#f2cc3d"),("Цветы голуб","#5fa8e0")],
]
SW, SH = 32, 64
img = Image.new("RGB", (SW*8, SH*4))
px = img.load()
def hx(h): h=h.lstrip('#'); return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
def grad(c, t):  # t=0 bottom, 1 top
    h,l,s = colorsys.rgb_to_hls(*c)
    # bottom: darker, hue toward blue; top: lighter, hue toward yellow
    dl = -0.17 + 0.30*t
    hshift = (1-t)*0.025 - t*0.02
    hh = h
    # move toward blue (0.6) at bottom, toward yellow (0.15) at top
    target = 0.6 if t<0.5 else 0.15
    w = abs(t-0.5)*0.12
    d = ((target-h+0.5)%1)-0.5
    hh = (h + d*w)%1
    ll = min(0.92, max(0.04, l+dl))
    ss = min(1, s*(0.95+0.1*t))
    return tuple(int(round(v*255)) for v in colorsys.hls_to_rgb(hh, ll, ss))
for r,row in enumerate(rows):
    for c,(name,hexv) in enumerate(row):
        base = hx(hexv)
        for y in range(SH):
            t = 1 - y/(SH-1)
            col = grad(base, t)
            for x in range(SW):
                px[c*SW+x, r*SH+y] = col
img.save("T_Palette_Forest.png")
# labeled preview
S=3
prev = img.resize((img.width*S, img.height*S), Image.NEAREST).convert("RGB")
d = ImageDraw.Draw(prev)
try: f = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 11)
except Exception as e: print(e); f = ImageFont.load_default()
for r,row in enumerate(rows):
    for c,(name,_) in enumerate(row):
        x,y = c*SW*S+3, r*SH*S+SH*S//2-8
        for w in name.split(" "):
            d.text((x+1,y+1), w, font=f, fill=(0,0,0)); d.text((x,y), w, font=f, fill=(255,255,255)); y+=13
prev.save("T_Palette_Forest_preview.png")
print(img.size, prev.size)
