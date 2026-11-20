# Wallpaper vetorial procedural: camadas de "papel" definidas por curvas suaves,
# com sombra projetada, luz na aresta e iluminação global. Independente da resolução.
#   python wallpaper_vetorial.py preview <saida.png>
#   python wallpaper_vetorial.py render <variante> <largura> <altura> <saida.png>
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

def hx(s):
    s = s.lstrip("#"); return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)

VARIANTES = {
    "esmeralda": dict(nome="Esmeralda", pal=["#06170f", "#0d2c1d", "#15402a", "#1f5537", "#2b6a45", "#3a8055", "#4c9567", "#62aa7b"], ang=-58, seed=3),
    "grafite":   dict(nome="Grafite", pal=["#050608", "#0b0d11", "#12151b", "#1a1e26", "#232833", "#2d3340", "#383f4f", "#454d60"], ang=-58, seed=3),
    "oceano":    dict(nome="Oceano", pal=["#020b16", "#04182c", "#062742", "#08385a", "#0a4a70", "#0e5e84", "#167596", "#2290a8"], ang=-58, seed=7),
    "indigo":    dict(nome="Índigo", pal=["#07051a", "#110b33", "#1b1250", "#28196c", "#372286", "#482d9c", "#5b3bb0", "#7150c2"], ang=-58, seed=11),
    "ambar":     dict(nome="Âmbar", pal=["#120603", "#2a0f05", "#441807", "#5f240a", "#7b330e", "#984515", "#b45a1f", "#cf722d"], ang=-58, seed=5),
    "glaciar":   dict(nome="Glaciar (claro)", pal=["#9fb3c8", "#b1c3d5", "#c1d0df", "#cfdbe7", "#dbe5ee", "#e6edf4", "#eff4f8", "#f8fafc"], ang=-58, seed=3, sombra=.32),
}


def render(var, W, H):
    v_ = VARIANTES[var]
    pal = [hx(c) for c in v_["pal"]]
    n = len(pal)
    rng = np.random.default_rng(v_["seed"])
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    X, Y = x / H, y / H                      # unidades = altura do ecrã
    a = math.radians(v_["ang"])
    u = X * math.cos(a) + Y * math.sin(a)
    v = -X * math.sin(a) + Y * math.cos(a)
    cx = np.array([0, W / H, 0, W / H], np.float32); cy = np.array([0, 0, 1, 1], np.float32)
    vc = -cx * math.sin(a) + cy * math.cos(a)
    vmin, vmax = float(vc.min()), float(vc.max())
    edges = np.linspace(vmin + 0.08, vmax - 0.05, n - 1)
    px = 1.0 / H
    sombra = v_.get("sombra", .58)

    col = np.broadcast_to(pal[0], (H, W, 3)).astype(np.float32).copy()
    for i in range(1, n):
        p, q, r = rng.uniform(0, 2 * math.pi, 3)
        Vi = (edges[i - 1] + 0.075 * np.sin(1.55 * u + p) + 0.03 * np.sin(3.4 * u + q)
              + 0.012 * np.sin(7.1 * u + r)).astype(np.float32)
        d = v - Vi                           # > 0 dentro da camada i
        # sombra projetada sobre o que está por baixo (lado de fora da aresta)
        out = np.minimum(d, 0)
        sh = sombra * (0.65 * np.exp(out / 0.018) + 0.35 * np.exp(out / 0.07)) * (d < 0)
        col *= (1 - sh)[..., None]
        # a própria camada: luz na aresta, escurece para dentro
        din = np.maximum(d, 0)
        shade = 0.86 + 0.14 * np.exp(-din / 0.22) + 0.10 * np.exp(-din / 0.006)
        lc = pal[i][None, None, :] * shade[..., None]
        alpha = np.clip(d / px + 0.5, 0, 1)[..., None]
        col = col * (1 - alpha) + lc * alpha
    # luz global vinda de cima à esquerda + vinheta suave
    gl = 0.80 + 0.32 * np.clip(1 - (X * 0.55 + Y * 0.75) / 1.6, 0, 1)
    rx, ry = (X - W / H / 2) / (W / H), (Y - 0.5)
    vig = 1 - 0.22 * np.clip(rx * rx * 1.6 + ry * ry * 1.6, 0, 1)
    col *= (gl * vig)[..., None]
    col += rng.uniform(-0.6, 0.6, col.shape).astype(np.float32)  # dither contra faixas
    return Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB")


def preview(out):
    tw, th, pad = 640, 400, 24
    keys = list(VARIANTES)
    cols = 3; rows = math.ceil(len(keys) / cols)
    sheet = Image.new("RGB", (pad + cols * (tw + pad), pad + rows * (th + 56 + pad)), (243, 244, 247))
    dr = ImageDraw.Draw(sheet); f = ImageFont.truetype(r"C:\Windows\Fonts\segoeuib.ttf", 22)
    for i, k in enumerate(keys):
        x = pad + (i % cols) * (tw + pad); y = pad + (i // cols) * (th + 56 + pad)
        img = render(k, tw * 2, th * 2).resize((tw, th), Image.LANCZOS)
        m = Image.new("L", (tw, th), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, tw - 1, th - 1], 16, fill=255)
        sheet.paste(img, (x, y), m)
        dr.text((x + 4, y + th + 14), f"{i + 1}  {VARIANTES[k]['nome']}", font=f, fill=(25, 25, 32))
        print("ok", k, flush=True)
    sheet.save(out)


if __name__ == "__main__":
    if sys.argv[1] == "preview":
        preview(sys.argv[2])
    else:
        render(sys.argv[2], int(sys.argv[3]), int(sys.argv[4])).save(sys.argv[5])
