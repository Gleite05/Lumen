# Marca do Lumen: um "squircle" de vidro escuro com as camadas de luz do fundo
# a acenderem-se de baixo para cima (a mesma ideia do fundo que conta as horas).
#   python gerar-marca.py <pasta-de-saida> [tema]
# Escreve lumen-1024.png, lumen-256.png, lumen.ico (16..256) e lumen-banner.png.
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

TEMAS = {
    "esmeralda": dict(fundo=("#04110b", "#0d2c1d"), camadas=["#15402a", "#1f5537", "#2f7a4c", "#4c9f69", "#7ccb93"], luz="#c9ffdc"),
    "oceano":    dict(fundo=("#020913", "#062742"), camadas=["#08385a", "#0a4a70", "#0e6a8f", "#1b8eae", "#5cc3d6"], luz="#d6f7ff"),
    "ambar":     dict(fundo=("#100503", "#2a0f05"), camadas=["#5f240a", "#7b330e", "#a8501a", "#d07a2e", "#f3b066"], luz="#ffe9c9"),
    "indigo":    dict(fundo=("#06041a", "#1b1250"), camadas=["#28196c", "#372286", "#5236a6", "#7a5cc8", "#b49cf0"], luz="#ece4ff"),
}


def hx(s):
    s = s.lstrip("#"); return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def squircle(n, k=5.0):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    u = (x + .5) / n * 2 - 1; v = (y + .5) / n * 2 - 1
    r = np.abs(u) ** k + np.abs(v) ** k
    return np.clip((1 - r) * n / 6, 0, 1)  # borda suave (anti-aliasing)


def icone(n=1024, tema="esmeralda"):
    t = TEMAS[tema]
    S = n * 2
    y, x = np.mgrid[0:S, 0:S].astype(np.float32)
    X, Y = x / S, y / S
    a, b = hx(t["fundo"][0]), hx(t["fundo"][1])
    g = np.clip((X * .35 + Y * .9) / 1.1, 0, 1)[..., None]
    col = a * (1 - g) + b * g
    # as camadas: ondas suaves, cada uma mais clara e mais baixa
    cams = t["camadas"]
    for i, c in enumerate(cams):
        base = .50 + i * .095
        onda = base + .045 * np.sin(2 * math.pi * (X * .9 + i * .17)) + .015 * np.sin(2 * math.pi * (X * 2.1 + i * .31))
        d = Y - onda
        # sombra por baixo da aresta da camada de cima
        sh = .45 * np.exp(np.minimum(d, 0) / .012) * (d < 0)
        col *= (1 - sh)[..., None]
        din = np.maximum(d, 0)
        shade = .88 + .12 * np.exp(-din / .2) + .14 * np.exp(-din / .006)
        al = np.clip(d * S + .5, 0, 1)[..., None]
        col = col * (1 - al) + (hx(c) * shade[..., None]) * al
    # fio de luz na crista da camada mais alta acesa (a "hora em curso")
    i = len(cams) - 3
    onda = .50 + i * .095 + .045 * np.sin(2 * math.pi * (X * .9 + i * .17)) + .015 * np.sin(2 * math.pi * (X * 2.1 + i * .31))
    fio = np.exp(-((Y - onda) / .0045) ** 2) * np.clip(1 - np.abs(X - .5) * 1.2, 0, 1)
    col = col + (hx(t["luz"]) - col) * (fio * .9)[..., None]
    # brilho: o "lúmen", um ponto de luz acima das camadas
    cx, cy = .70, .30
    d2 = (X - cx) ** 2 + (Y - cy) ** 2
    glow = np.exp(-d2 / .010) * .55 + np.exp(-d2 / .0009) * .9
    col = col + (hx(t["luz"]) - col) * np.clip(glow, 0, 1)[..., None]
    # vidro: reflexo no topo e aro interior
    m = squircle(S)
    topo = np.clip(1 - Y / .5, 0, 1) ** 2 * .10
    col = col + (255 - col) * topo[..., None]
    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    alpha = Image.fromarray((m * 255).astype(np.uint8), "L")
    img.putalpha(alpha)
    aro = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    inset = int(S * .006)
    ImageDraw.Draw(aro).rounded_rectangle([inset, inset, S - inset, S - inset], radius=int(S * .225), outline=(255, 255, 255, 46), width=max(2, S // 340))
    aro.putalpha(Image.fromarray(np.minimum(np.array(aro.getchannel("A")), (m * 255).astype(np.uint8)), "L"))
    img = Image.alpha_composite(img, aro)
    return img.resize((n, n), Image.LANCZOS)


def banner(icon, out, tema):
    W, H = 1600, 520
    t = TEMAS[tema]
    bg = Image.new("RGB", (W, H), tuple(int(v) for v in hx(t["fundo"][0])))
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    g = np.clip(x / W * .6 + y / H * .5, 0, 1)[..., None]
    arr = hx(t["fundo"][0]) * (1 - g) + hx(t["fundo"][1]) * g
    bg = Image.fromarray(arr.astype(np.uint8), "RGB").convert("RGBA")
    ic = icon.resize((300, 300), Image.LANCZOS)
    sombra = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sombra.paste((0, 0, 0, 140), (150, 130, 450, 430), ic.getchannel("A"))
    bg = Image.alpha_composite(bg, sombra.filter(ImageFilter.GaussianBlur(28)))
    bg.alpha_composite(ic, (150, 110))
    dr = ImageDraw.Draw(bg)
    f1 = ImageFont.truetype(r"C:\Windows\Fonts\segoeuisl.ttf", 190)
    f2 = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 40)
    dr.text((520, 120), "Lumen", font=f1, fill=(255, 255, 255))
    dr.text((530, 345), "O teu Windows, com luz própria.", font=f2, fill=tuple(int(v) for v in hx(t["luz"])) + (230,))
    bg.convert("RGB").save(out)


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    tema = sys.argv[2] if len(sys.argv) > 2 else "esmeralda"
    os.makedirs(out, exist_ok=True)
    big = icone(1024, tema)
    big.save(os.path.join(out, "lumen-1024.png"))
    big.resize((256, 256), Image.LANCZOS).save(os.path.join(out, "lumen-256.png"))
    big.save(os.path.join(out, "lumen.ico"), sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)])
    banner(big, os.path.join(out, "lumen-banner.png"), tema)
    print("ok", out)


# Imagens do assistente de instalação (Inno Setup): a lateral e o canto, em 100/150/200 %.
def instalador(out, tema="esmeralda"):
    t = TEMAS[tema]
    ic = icone(512, tema)
    for esc, nome in ((1, "100"), (1.5, "150"), (2, "200")):
        W, H = int(164 * esc), int(314 * esc)
        y, x = np.mgrid[0:H, 0:W].astype(np.float32)
        X, Y = x / W, y / H
        col = hx(t["fundo"][0]) * (1 - Y[..., None]) + hx(t["fundo"][1]) * Y[..., None]
        for i, c in enumerate(t["camadas"]):
            onda = .58 + i * .085 + .03 * np.sin(2 * math.pi * (X * .8 + i * .21))
            d = Y - onda
            col *= (1 - .4 * np.exp(np.minimum(d, 0) / .01) * (d < 0))[..., None]
            al = np.clip(d * H + .5, 0, 1)[..., None]
            col = col * (1 - al) + hx(c) * al
        img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
        s = int(96 * esc)
        img.alpha_composite(ic.resize((s, s), Image.LANCZOS), ((W - s) // 2, int(46 * esc)))
        dr = ImageDraw.Draw(img)
        f = ImageFont.truetype(r"C:\Windows\Fonts\segoeuisl.ttf", int(30 * esc))
        tw = dr.textlength("Lumen", font=f)
        dr.text(((W - tw) / 2, int(152 * esc)), "Lumen", font=f, fill=(255, 255, 255))
        img.convert("RGB").save(os.path.join(out, f"lado-{nome}.bmp"))
        c = int(55 * esc)
        canto = Image.new("RGBA", (c, c), (255, 255, 255, 255))
        canto.alpha_composite(ic.resize((c - int(6 * esc), c - int(6 * esc)), Image.LANCZOS), (int(3 * esc), int(3 * esc)))
        canto.convert("RGB").save(os.path.join(out, f"canto-{nome}.bmp"))
    print("ok instalador", out)
