# Gerador de conjuntos de cursores com estilos (neon, branco, minimal...)
#   python cursores.py preview <saida.png>
#   python cursores.py build <estilo> <pasta>
import math, struct, sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import distance_transform_edt, gaussian_filter

SIZES = [32, 48, 64, 96, 128]
K = 4
FONT = r"C:\Windows\Fonts\segoeuib.ttf"
FONT_R = r"C:\Windows\Fonts\segoeui.ttf"

CYAN = (34, 211, 238); MAG = (232, 121, 249); VIO = (168, 85, 247); PINK = (236, 72, 153)
INK = (15, 23, 42); NIGHT = (8, 10, 18); WHITE = (255, 255, 255); RED = (239, 68, 68)
LIME = (163, 230, 53)

STYLES = {
    "neon-ciano": dict(label="Neon Ciano", scale=.86, body=NIGHT, body_a=.92,
        in_w=1.25, in_col=CYAN, in_core=.42, glow=CYAN, glow_r=2.0, glow_a=1.0,
        badge=NIGHT, glyph=CYAN, detail=CYAN, nib=CYAN, comet=[(14, 116, 144), CYAN, (207, 250, 254)],
        danger=NIGHT, danger_edge=(251, 113, 133)),
    "neon-rosa": dict(label="Neon Rosa", scale=.86, body=NIGHT, body_a=.92,
        in_w=1.25, in_col=MAG, in_core=.42, glow=(217, 70, 239), glow_r=2.0, glow_a=1.0,
        badge=NIGHT, glyph=MAG, detail=MAG, nib=MAG, comet=[(134, 25, 143), MAG, (250, 232, 255)],
        danger=NIGHT, danger_edge=(251, 113, 133)),
    "neon-vapor": dict(label="Neon Vapor", scale=.86, body=(12, 8, 24), body_a=.9,
        in_w=1.3, in_col=[CYAN, VIO, PINK], in_core=.4, glow=[CYAN, VIO, PINK], glow_r=2.0, glow_a=1.0,
        badge=(12, 8, 24), glyph=[CYAN, PINK], detail=VIO, nib=PINK, comet=[CYAN, VIO, PINK],
        danger=(12, 8, 24), danger_edge=(251, 113, 133)),
    "neon-lima": dict(label="Neon Lima (contorno)", scale=.86, body=NIGHT, body_a=.35,
        in_w=1.15, in_col=LIME, in_core=.4, glow=LIME, glow_r=1.8, glow_a=.9,
        badge=NIGHT, glyph=LIME, detail=LIME, nib=LIME, comet=[(63, 98, 18), LIME, (236, 252, 203)],
        danger=NIGHT, danger_edge=(251, 113, 133)),
    "branco-minimal": dict(label="Branco Minimal", body=WHITE, out_w=1.0, out_col=INK, shadow=.35,
        badge=INK, glyph=WHITE, detail=(156, 163, 175), nib=INK, comet=[(203, 213, 225), INK],
        danger=RED),
    "branco-puro": dict(label="Branco Puro (seta curta)", arrow="compact", body=WHITE, out_w=.6,
        out_col=(100, 116, 139), shadow=.75, badge=(241, 245, 249), glyph=INK, detail=(203, 213, 225),
        nib=(148, 163, 184), comet=[(226, 232, 240), (59, 130, 246)], danger=(244, 63, 94)),
    "branco-halo": dict(label="Branco Halo (brilho ciano)", scale=.86, arrow="compact", body=WHITE,
        out_w=.7, out_col=INK, glow=(56, 189, 248), glow_r=2.2, glow_a=.85, shadow=.15,
        badge=INK, glyph=WHITE, detail=(148, 163, 184), nib=INK, comet=[(186, 230, 253), (14, 165, 233)],
        danger=RED),
    "preto-minimal": dict(label="Preto Minimal", body=(10, 10, 12), out_w=1.2, out_col=WHITE, shadow=.35,
        badge=WHITE, glyph=(10, 10, 12), detail=(82, 82, 91), nib=WHITE, comet=[(63, 63, 70), WHITE],
        danger=RED),
}

BLUE = (10, 132, 255); PURPLE = (191, 90, 242); MPINK = (255, 55, 95); GRAPH = (28, 28, 30)
HAND_W = dict(body=WHITE, out_col=(0, 0, 0), detail=(0, 0, 0))
def _mac(label, body, out, spin="spokes", hand=None, **kw):
    d = dict(label=label, arrow="mac", dbl="slim", ibeam_w=1.45, body=body, out_w=1.55, out_col=out,
             shadow=.5, badge=body, glyph=out, detail=out, nib=body, danger=body, spin=spin,
             spoke=body, comet=[(200, 200, 200), body])
    if hand: d["hand"] = hand
    d.update(kw); return d
STYLES.update({
    "mac-preto": _mac("Mac Preto", (0, 0, 0), WHITE, hand=HAND_W),
    "mac-arcoiris": _mac("Mac Preto + anel de cores", (0, 0, 0), WHITE, spin="rainbow", hand=HAND_W),
    "mac-branco": _mac("Mac Branco", WHITE, (0, 0, 0), spoke=(120, 120, 128)),
    "mac-contorno-azul": _mac("Mac Preto, contorno azul", (0, 0, 0), BLUE, hand=dict(body=WHITE, out_col=BLUE, detail=BLUE)),
    "mac-branco-roxo": _mac("Mac Branco, contorno roxo", WHITE, PURPLE, spoke=PURPLE, detail=PURPLE),
    "mac-azul": _mac("Mac Azul", BLUE, WHITE, hand=dict(body=WHITE, out_col=BLUE, detail=BLUE)),
    "mac-rosa": _mac("Mac Rosa", MPINK, WHITE, hand=dict(body=WHITE, out_col=MPINK, detail=MPINK)),
    "mac-grafite": _mac("Mac Grafite (contorno fino)", GRAPH, WHITE, out_w=1.15, shadow=.6, hand=dict(body=WHITE, out_col=GRAPH, detail=GRAPH)),
})


def lerp_pal(pal, t):
    t = np.clip(t, 0, 1)
    if not isinstance(pal, list):
        pal = [pal, pal]
    n = len(pal) - 1
    idx = np.minimum((t * n).astype(int), n - 1)
    f = t * n - idx
    p = np.array(pal, float)
    return p[idx] * (1 - f[..., None]) + p[idx + 1] * f[..., None]


class Ctx:
    def __init__(self, S, scale=1.0):
        self.S, self.N = S, S * K
        self.u = self.N / 32.0
        self.s = scale
        self.su = self.u * scale

    def T(self, x, y):  # unidades -> unidades transformadas
        return (16 + (x - 16) * self.s, 16 + (y - 16) * self.s)

    def P(self, x, y):
        tx, ty = self.T(x, y)
        return (tx * self.u, ty * self.u)

    def new(self):
        m = Image.new("L", (self.N, self.N), 0)
        return m, ImageDraw.Draw(m)


def rot(pts, ang, c=(16, 16)):
    a = math.radians(ang); ca, sa = math.cos(a), math.sin(a)
    return [(c[0] + (x - c[0]) * ca - (y - c[1]) * sa, c[1] + (x - c[0]) * sa + (y - c[1]) * ca) for x, y in pts]


def poly(c, d, pts): d.polygon([c.P(*p) for p in pts], fill=255)
def circle(c, d, x, y, r):
    cx, cy = c.P(x, y); R = r * c.su
    d.ellipse([cx - R, cy - R, cx + R, cy + R], fill=255)
def line(c, d, a, b, w):
    d.line([c.P(*a), c.P(*b)], fill=255, width=max(1, int(round(w * c.su))))
    circle(c, d, a[0], a[1], w / 2); circle(c, d, b[0], b[1], w / 2)
def rrect(c, d, x0, y0, x1, y1, r):
    d.rounded_rectangle([c.P(x0, y0), c.P(x1, y1)], radius=r * c.su, fill=255)
def ring(c, d, x, y, r, w):
    cx, cy = c.P(x, y); R = r * c.su
    d.ellipse([cx - R, cy - R, cx + R, cy + R], outline=255, width=max(1, int(round(w * c.su))))


def opening(b, r):
    if r <= 0:
        return b
    er = distance_transform_edt(b) > r
    return distance_transform_edt(~er) <= r


ARROWS = {
    "classic": [(3, 2), (3, 21), (7.6, 16.8), (10.6, 23.6), (13.6, 22.3), (10.7, 15.8), (16.8, 15.8)],
    "compact": [(4, 2.5), (4, 21.2), (9.0, 16.6), (16.4, 16.0)],
    "mac": [(4, 2.5), (4, 20.8), (8.1, 16.9), (10.9, 23.3), (13.5, 22.2), (10.8, 15.9), (16.3, 15.9)],
}
DBL = [(16, 3), (22.5, 10), (17.7, 10), (17.7, 22), (22.5, 22), (16, 29), (9.5, 22), (14.3, 22), (14.3, 10), (9.5, 10)]


def make_arrow(kind): return lambda c, d: poly(c, d, ARROWS[kind])
def badge(c, d): circle(c, d, 22.6, 22.6, 6.2)
DBL_SLIM = [(16, 3.5), (21.2, 9.4), (17.3, 9.4), (17.3, 22.6), (21.2, 22.6), (16, 28.5), (10.8, 22.6), (14.7, 22.6), (14.7, 9.4), (10.8, 9.4)]
def dbl(ang, slim=False): return lambda c, d: poly(c, d, rot(DBL_SLIM if slim else DBL, ang))
def size_all_slim(c, d):
    line(c, d, (16, 7.5), (16, 24.5), 2.6); line(c, d, (7.5, 16), (24.5, 16), 2.6)
    for ang in (0, 90, 180, 270):
        poly(c, d, rot([(16, 3.0), (20.0, 8.4), (12.0, 8.4)], ang))
def spokes(cx, cy, n, rin, rout, w):
    def f(c, d):
        for k in range(n):
            a = 2 * math.pi * k / n
            line(c, d, (cx + rin * math.cos(a), cy + rin * math.sin(a)), (cx + rout * math.cos(a), cy + rout * math.sin(a)), w)
    return f
def size_all(c, d):
    line(c, d, (16, 7.5), (16, 24.5), 3.0); line(c, d, (7.5, 16), (24.5, 16), 3.0)
    for ang in (0, 90, 180, 270):
        poly(c, d, rot([(16, 2.6), (20.6, 8.6), (11.4, 8.6)], ang))
def up_arrow(c, d):
    poly(c, d, [(16, 3), (23.6, 12.6), (18.4, 12.6), (18.4, 28.5), (13.6, 28.5), (13.6, 12.6), (8.4, 12.6)])
def cross(c, d):
    w = 1.9
    line(c, d, (16, 3.5), (16, 12.2), w); line(c, d, (16, 19.8), (16, 28.5), w)
    line(c, d, (3.5, 16), (12.2, 16), w); line(c, d, (19.8, 16), (28.5, 16), w)
    circle(c, d, 16, 16, 1.5)
IBEAM_W = [1.9]
PHASE = [0.0]
def ibeam(c, d):
    w = IBEAM_W[0]
    line(c, d, (16, 5.6), (16, 26.4), w); line(c, d, (12.6, 5.6), (19.4, 5.6), w); line(c, d, (12.6, 26.4), (19.4, 26.4), w)
def _pen(t, s):
    a = (1 / math.sqrt(2), -1 / math.sqrt(2)); n = (1 / math.sqrt(2), 1 / math.sqrt(2))
    return (5 + t * a[0] + s * n[0], 27 + t * a[1] + s * n[1])
def pen_body(c, d):
    poly(c, d, [_pen(6.2, -3.1), _pen(25, -3.1), _pen(25, 3.1), _pen(6.2, 3.1)])
    e = _pen(25, 0); circle(c, d, e[0], e[1], 3.1)
def pen_nib(c, d): poly(c, d, [_pen(0, 0), _pen(6.4, -3.1), _pen(6.4, 3.1)])
def no_ring(c, d):
    ring(c, d, 16, 16, 10.2, 3.2); line(c, d, (9.2, 22.8), (22.8, 9.2), 3.2)
def hand(c, d):
    rrect(c, d, 10.0, 2.6, 14.7, 18, 2.35); rrect(c, d, 14.3, 11.2, 18.8, 21, 2.25)
    rrect(c, d, 18.4, 12.4, 22.7, 21, 2.15); rrect(c, d, 22.2, 14.2, 25.8, 22, 1.8)
    rrect(c, d, 10.0, 16.5, 25.8, 29.0, 4.6); line(c, d, (6.6, 17.4), (11.6, 23.6), 4.0)
def hand_lines(c, d):
    w = 0.9
    line(c, d, (14.5, 13.4), (14.5, 18.6), w); line(c, d, (18.6, 14.4), (18.6, 19.0), w); line(c, d, (22.45, 16.0), (22.45, 19.4), w)
def help_glyph(c, d):
    f = ImageFont.truetype(FONT, max(6, int(9.6 * c.su)))
    d.text(c.P(22.6, 22.9), "?", font=f, fill=255, anchor="mm")
def pin_glyph(c, d):
    circle(c, d, 22.6, 21.0, 2.7); poly(c, d, [(20.25, 22.3), (24.95, 22.3), (22.6, 26.6)])
def pin_hole(c, d): circle(c, d, 22.6, 21.0, 1.0)
def person_glyph(c, d):
    circle(c, d, 22.6, 20.2, 2.0); cx0, cy0 = c.P(18.9, 23.3); cx1, cy1 = c.P(26.3, 30.5)
    d.ellipse([cx0, cy0, cx1, cy1], fill=255)
def spinner(cx, cy, r, w): return lambda c, d: ring(c, d, cx, cy, r, w)


def cursors(st):
    if st.get("family") == "fluent":
        return fluent_cursors(st)
    A = make_arrow(st.get("arrow", "classic"))
    hot_a = ARROWS[st.get("arrow", "classic")][0]
    slim = st.get("dbl") == "slim"
    spin = st.get("spin", "comet")
    if spin == "spokes":
        small = [(spokes(23.2, 23.2, 8, 2.4, 5.6, 1.45), "spokes", 0)]
        big = [(spokes(16, 16, 12, 5.2, 11.0, 2.0), "spokes", 0)]
        cs, cb = (23.2, 23.2, 8), (16, 16, 12)
    else:
        role = "rainbow" if spin == "rainbow" else "comet"
        small = [(spinner(23.2, 23.2, 4.6, 2.3), role, 0)]
        big = [(spinner(16, 16, 9.4, 3.6), role, 0)]
        cs, cb = (23.2, 23.2, 0), (16, 16, 0)
    return {
        # nome: ([(forma, papel, abrir_cantos)], hotspot, animado, centro_cometa)
        "Arrow": ([(A, "main", .55)], hot_a, False, None),
        "Help": ([(A, "main", .55), (badge, "badge", 0), (help_glyph, "glyph", 0)], hot_a, False, None),
        "AppStarting": ([(A, "main", .55)] + small, hot_a, True, cs),
        "Wait": (big, (16, 16), True, cb),
        "Crosshair": ([(cross, "main", 0)], (16, 16), False, None),
        "IBeam": ([(ibeam, "main", 0)], (16, 16), False, None),
        "NWPen": ([(pen_body, "main", .4), (pen_nib, "nib", 0)], (5, 27), False, None),
        "No": ([(no_ring, "danger", 0)], (16, 16), False, None),
        "SizeNS": ([(dbl(0, slim), "main", .45)], (16, 16), False, None),
        "SizeWE": ([(dbl(90, slim), "main", .45)], (16, 16), False, None),
        "SizeNWSE": ([(dbl(-45, slim), "main", .45)], (16, 16), False, None),
        "SizeNESW": ([(dbl(45, slim), "main", .45)], (16, 16), False, None),
        "SizeAll": ([(size_all_slim if slim else size_all, "main", .45)], (16, 16), False, None),
        "UpArrow": ([(up_arrow, "main", .45)], (16, 3), False, None),
        "Hand": ([(hand, "main", 0), (hand_lines, "detail", 0)], (12.3, 2.8), False, None),
        "Pin": ([(A, "main", .55), (badge, "badge", 0), (pin_glyph, "glyph", 0), (pin_hole, "badge", 0)], hot_a, False, None),
        "Person": ([(A, "main", .55), (badge, "badge", 0), (person_glyph, "glyph", 0)], hot_a, False, None),
    }


def diag_field(c, col, bbox):
    x0, y0, x1, y1 = bbox
    yy, xx = np.mgrid[0:c.N, 0:c.N].astype(float)
    t = ((xx - x0) + (yy - y0)) / max(1.0, (x1 - x0) + (y1 - y0))
    return lerp_pal(col, t)


def render(stname, name, S, phase=0.0):
    st = STYLES[stname]
    layers, hot, _, cc = cursors(st)[name]
    if name == "Hand" and "hand" in st:
        st = {**st, **st["hand"]}
    IBEAM_W[0] = st.get("ibeam_w", 1.9)
    PHASE[0] = phase
    c = Ctx(S, st.get("scale", 1.0))
    masks = []
    badge_inner = None
    for fn, role, op in layers:
        m, d = c.new(); fn(c, d)
        b = opening(np.array(m) > 127, op * c.su)
        if fn is person_glyph:
            if badge_inner is None:
                bm, bd = c.new(); badge(c, bd)
                badge_inner = distance_transform_edt(np.array(bm) > 127) > 0.9 * c.su
            b &= badge_inner
        masks.append((b, role))
    U = np.zeros((c.N, c.N), bool)
    for b, _ in masks:
        U |= b
    ys, xs = np.nonzero(U)
    bbox = (xs.min(), ys.min(), xs.max(), ys.max())
    d_out = distance_transform_edt(~U); d_in = distance_transform_edt(U)
    danger = name == "No"
    Us = np.zeros_like(U); Uo = np.zeros_like(U)
    for b, role in masks:
        if role == "spokes":
            Us |= b
        elif role != "arc":   # o arco não leva contorno
            Uo |= b
    big = 1e9
    d_out_o = distance_transform_edt(~Uo) if Uo.any() else np.full(U.shape, big)
    d_out_s = distance_transform_edt(~Us) if Us.any() else np.full(U.shape, big)
    def outline_alpha(w_units):
        ow = w_units * c.u
        return np.maximum(np.clip(ow - d_out_o + 0.5, 0, 1), np.clip(min(ow, 0.6 * c.u) - d_out_s + 0.5, 0, 1))

    rgb = np.zeros((c.N, c.N, 3)); a = np.zeros((c.N, c.N))
    def over(col, al):
        nonlocal rgb, a
        al = np.clip(al, 0, 1)
        out_a = al + a * (1 - al)
        rgb = (col * al[..., None] + rgb * a[..., None] * (1 - al[..., None])) / np.maximum(out_a, 1e-6)[..., None]
        a = out_a

    # sombra
    if st.get("shadow"):
        base = outline_alpha(st["out_w"]) if st.get("out_w") else U.astype(float)
        sh = gaussian_filter(base, 1.15 * c.u)
        sh = np.roll(np.roll(sh, int(round(0.9 * c.u)), 0), int(round(0.45 * c.u)), 1)
        over(np.zeros((c.N, c.N, 3)), sh * st["shadow"])

    # brilho (neon / halo)
    if st.get("glow"):
        gcol = st["danger_edge"] if danger and "danger_edge" in st else st["glow"]
        band = ((U & (d_in <= 0.9 * c.u)) | (~U & (d_out <= 0.4 * c.u))).astype(float)
        colf = diag_field(c, gcol, bbox)
        r = st["glow_r"] * c.u
        acc_a = np.zeros_like(band); acc_c = np.zeros((c.N, c.N, 3))
        for sig, w in ((r, 1.0), (r * 0.4, 0.8)):
            ga = gaussian_filter(band, sig) * w
            acc_a += ga
            for ch in range(3):
                acc_c[..., ch] += gaussian_filter(band * colf[..., ch], sig) * w
        gc = acc_c / np.maximum(acc_a, 1e-6)[..., None]
        over(gc, np.clip(acc_a * 2.2, 0, 1) * st["glow_a"])

    # contorno exterior
    if st.get("out_w"):
        outline = outline_alpha(st["out_w"])
        over(np.broadcast_to(np.array(st["out_col"], float), (c.N, c.N, 3)), outline * st.get("out_a", 1.0))

    # preenchimentos
    for b, role in masks:
        if role in ("spokes", "rainbow"):
            cx, cy = c.P(cc[0], cc[1])
            yy, xx = np.mgrid[0:c.N, 0:c.N].astype(float)
            th = np.arctan2(yy - cy, xx - cx)
            if role == "spokes":
                n = cc[2]; step = 2 * math.pi / n
                ph = math.floor(phase / step + 1e-6) * step
                t = ((th - ph + step / 2) % (2 * math.pi)) / (2 * math.pi)
                tq = np.floor(t * n) / (n - 1)
                over(np.broadcast_to(np.array(st["spoke"], float), (c.N, c.N, 3)), b * (0.18 + 0.82 * tq ** 1.3))
            else:
                hues = [(255, 59, 48), (255, 149, 0), (255, 204, 0), (52, 199, 89), (0, 199, 190), (10, 132, 255), (175, 82, 222), (255, 45, 85), (255, 59, 48)]
                t = ((th - phase) % (2 * math.pi)) / (2 * math.pi)
                over(lerp_pal(hues, t), b * 1.0)
            continue
        if role == "comet":
            cx, cy = c.P(cc[0], cc[1])
            yy, xx = np.mgrid[0:c.N, 0:c.N].astype(float)
            t = ((np.arctan2(yy - cy, xx - cx) - phase) % (2 * math.pi)) / (2 * math.pi)
            if st.get("in_w"):  # neon: anel escuro por baixo
                over(np.broadcast_to(np.array(st["body"], float), (c.N, c.N, 3)), b * st.get("body_a", 1))
            elif st.get("body"):
                over(np.broadcast_to(np.array(st["body"], float), (c.N, c.N, 3)), b * 1.0)
            over(lerp_pal(st["comet"], t), b * (0.1 + 0.9 * t ** 1.6))
            continue
        key = {"main": "body", "danger": "danger"}.get(role, role)
        col = st[key]
        al = st.get("body_a", 1.0) if role in ("main", "danger") else (.9 if role == "detail" else 1.0)
        if role == "glyph" and st.get("in_w") is None:
            al = 1.0
        over(diag_field(c, col, bbox), b * al)

    # tubo neon (contorno interior + núcleo branco)
    if st.get("in_w"):
        ecol = st["danger_edge"] if danger and "danger_edge" in st else st["in_col"]
        iw = st["in_w"] * c.u
        band = np.clip(iw - d_in + 0.5, 0, 1) * U
        over(diag_field(c, ecol, bbox), band)
        core_c = st["in_core"] * c.u
        core = np.clip(core_c - np.abs(d_in - iw * 0.5) + 0.5, 0, 1) * U
        over(np.full((c.N, c.N, 3), 255.0), core * 0.85)

    img = Image.fromarray(np.dstack([rgb, a * 255]).clip(0, 255).astype(np.uint8), "RGBA")
    img = img.convert("RGBa").resize((S, S), Image.LANCZOS).convert("RGBA")
    hx, hy = c.T(*hot)
    return img, (min(S - 1, int(round(hx * S / 32))), min(S - 1, int(round(hy * S / 32))))


def dib(img):
    w, h = img.size
    bgra = np.array(img)[::-1][..., [2, 1, 0, 3]].tobytes()
    andmask = b"\x00" * (((w + 31) // 32) * 4 * h)
    return struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(bgra) + len(andmask), 0, 0, 0, 0) + bgra + andmask


def cur_bytes(frames):
    n = len(frames); off = 6 + 16 * n
    entries, blobs = b"", b""
    for img, (hx, hy) in frames:
        data = dib(img); w, h = img.size
        entries += struct.pack("<BBBBHHII", w % 256, h % 256, 0, 0, hx, hy, len(data), off + len(blobs))
        blobs += data
    return struct.pack("<HHH", 0, 2, n) + entries + blobs


def chunk(cid, data):
    return cid + struct.pack("<I", len(data)) + data + (b"\x00" if len(data) % 2 else b"")


def build(stname, out):
    os.makedirs(out, exist_ok=True)
    for name, (_, _, animated, _) in cursors(STYLES[stname]).items():
        if animated:
            nf = 24; icons = b""
            for i in range(nf):
                icons += chunk(b"icon", cur_bytes([render(stname, name, S, 2 * math.pi * i / nf) for S in SIZES]))
            body = b"ACON" + chunk(b"anih", struct.pack("<9I", 36, nf, nf, 0, 0, 0, 0, 3, 1)) + chunk(b"LIST", b"fram" + icons)
            open(os.path.join(out, f"{name}.ani"), "wb").write(b"RIFF" + struct.pack("<I", len(body)) + body)
        else:
            open(os.path.join(out, f"{name}.cur"), "wb").write(cur_bytes([render(stname, name, S) for S in SIZES]))
        print("ok", name, flush=True)


def preview(outpng, names=None):
    show = ["Arrow", "Hand", "IBeam", "Wait", "AppStarting", "SizeNWSE", "SizeAll", "No", "Help"]
    cell, lab, pad = 84, 345, 14
    names = names or list(STYLES)
    W = lab + len(show) * cell * 2 + pad
    H = 70 + len(names) * (cell + 16)
    sheet = Image.new("RGBA", (W, H), (250, 250, 252, 255))
    dr = ImageDraw.Draw(sheet)
    fb = ImageFont.truetype(FONT, 22); fr = ImageFont.truetype(FONT_R, 15)
    x_light, x_dark = lab, lab + len(show) * cell
    dr.text((x_light + 10, 22), "em fundo claro", font=fr, fill=(100, 100, 110))
    dr.text((x_dark + 10, 22), "em fundo escuro", font=fr, fill=(100, 100, 110))
    for i, sn in enumerate(names):
        y = 56 + i * (cell + 16)
        dr.rounded_rectangle([x_light, y, x_dark - 4, y + cell], 14, fill=(236, 238, 243))
        dr.rounded_rectangle([x_dark, y, W - pad, y + cell], 14, fill=(22, 24, 32))
        dr.text((20, y + cell / 2), f"{i + 1}  {STYLES[sn]['label']}", font=fb, fill=(20, 20, 28), anchor="lm")
        for j, cn in enumerate(show):
            img = render(sn, cn, 64, 1.4)[0]
            for xb in (x_light, x_dark):
                sheet.alpha_composite(img, (xb + j * cell + (cell - 64) // 2, y + (cell - 64) // 2))
        print("preview", sn, flush=True)
    sheet.convert("RGB").save(outpng)


# ================= família Fluent (sem cauda) =================
ARROWS["tailless"] = [(4, 2.5), (4, 20.6), (8.7, 16.5), (15.7, 16.3)]

def help_q(c, d):
    f = ImageFont.truetype(FONT, max(6, int(11.5 * c.su)))
    d.text(c.P(20.3, 13.2), "?", font=f, fill=255, anchor="mm")

def arc_fn(cx, cy, r, w, span=270):
    def f(c, d):
        st_deg = math.degrees(PHASE[0])
        x, y = c.P(cx, cy); R = r * c.su
        d.arc([x - R, y - R, x + R, y + R], st_deg, st_deg + span, fill=255, width=max(1, int(round(w * c.su))))
        for ang in (st_deg, st_deg + span):
            a = math.radians(ang)
            rr = r - w / 2
            circle(c, d, cx + rr * math.cos(a), cy + rr * math.sin(a), w / 2)
    return f

PIECE = [(16, 3.2), (20.6, 9.6), (17.1, 9.6), (17.1, 12.9), (14.9, 12.9), (14.9, 9.6), (11.4, 9.6)]
def sep_dbl(ang):
    def f(c, d):
        poly(c, d, rot(PIECE, ang)); poly(c, d, rot(PIECE, ang + 180))
    return f
PIECE4 = [(16, 3.2), (19.9, 8.5), (17.05, 8.5), (17.05, 11.3), (14.95, 11.3), (14.95, 8.5), (12.1, 8.5)]
def four_way(c, d):
    for ang in (0, 90, 180, 270):
        poly(c, d, rot(PIECE4, ang))
def up_slim(c, d):
    poly(c, d, [(16, 3), (21.2, 9.8), (17.1, 9.8), (17.1, 28.6), (14.9, 28.6), (14.9, 9.8), (10.8, 9.8)])
def reticle(c, d):
    ring(c, d, 16, 16, 7.2, 1.6)
    for a, b in (((16, 3.5), (16, 11)), ((16, 21), (16, 28.5)), ((3.5, 16), (11, 16)), ((21, 16), (28.5, 16))):
        line(c, d, a, b, 1.6)
    circle(c, d, 16, 16, 1.15)
def fbadge(c, d): circle(c, d, 24.4, 8.4, 5.4)
def fpin(c, d):
    circle(c, d, 24.4, 7.2, 2.4); poly(c, d, [(22.3, 8.4), (26.5, 8.4), (24.4, 12.2)])
def fpin_hole(c, d): circle(c, d, 24.4, 7.2, 0.9)
def fperson(c, d):
    circle(c, d, 24.4, 6.6, 1.75)
    x0, y0 = c.P(21.3, 9.4); x1, y1 = c.P(27.5, 15.0)
    d.chord([x0, y0, x1, y1], 180, 360, fill=255)

def fluent_cursors(st):
    A = make_arrow("tailless"); ha = ARROWS["tailless"][0]; hh = (12.3, 2.8)
    return {
        "Arrow": ([(A, "main", .9)], ha, False, None),
        "Help": ([(A, "main", .9), (help_q, "main", 0)], ha, False, None),
        "AppStarting": ([(A, "main", .9), (arc_fn(22.6, 22.4, 4.7, 2.3), "arc", 0)], ha, True, None),
        "Wait": ([(arc_fn(16, 16, 9.4, 3.0), "arc", 0)], (16, 16), True, None),
        "Crosshair": ([(reticle, "main", 0)], (16, 16), False, None),
        "IBeam": ([(ibeam, "main", 0)], (16, 16), False, None),
        "NWPen": ([(pen_body, "main", .4), (pen_nib, "nib", 0)], (5, 27), False, None),
        "No": ([(no_ring, "danger", 0)], (16, 16), False, None),
        "SizeNS": ([(sep_dbl(0), "main", .35)], (16, 16), False, None),
        "SizeWE": ([(sep_dbl(90), "main", .35)], (16, 16), False, None),
        "SizeNWSE": ([(sep_dbl(-45), "main", .35)], (16, 16), False, None),
        "SizeNESW": ([(sep_dbl(45), "main", .35)], (16, 16), False, None),
        "SizeAll": ([(four_way, "main", .35)], (16, 16), False, None),
        "UpArrow": ([(up_slim, "main", .35)], (16, 3), False, None),
        "Hand": ([(hand, "main", 0), (hand_lines, "detail", 0)], hh, False, None),
        "Pin": ([(hand, "main", 0), (hand_lines, "detail", 0), (fbadge, "badge", 0), (fpin, "glyph", 0), (fpin_hole, "badge", 0)], hh, False, None),
        "Person": ([(hand, "main", 0), (hand_lines, "detail", 0), (fbadge, "badge", 0), (fperson, "glyph", 0)], hh, False, None),
    }

WIN_BLUE = [(96, 205, 255), (0, 120, 212), (0, 84, 166)]
STYLES.update({
    "fluent-claro": dict(label="Fluent Claro", family="fluent", body=WHITE, out_w=.95, out_col=(0, 0, 0), shadow=.45,
        ibeam_w=1.45, detail=(0, 0, 0), nib=(0, 0, 0), badge=(0, 0, 0), glyph=WHITE, danger=(232, 17, 35), arc=WIN_BLUE),
    "fluent-escuro": dict(label="Fluent Escuro", family="fluent", body=(0, 0, 0), out_w=1.0, out_col=WHITE, shadow=.35,
        ibeam_w=1.45, detail=WHITE, nib=WHITE, badge=WHITE, glyph=(0, 0, 0), danger=(232, 17, 35), arc=WIN_BLUE),
})

def preview_full(outpng, names):
    order = ["Arrow", "AppStarting", "Wait", "Help", "No", "Hand", "Pin", "Person", "NWPen",
             "SizeAll", "SizeNWSE", "SizeNESW", "SizeNS", "SizeWE", "UpArrow", "Crosshair", "IBeam"]
    cs, gap = 60, 18
    per = 9
    pw = per * (cs + gap) + 60
    ph = 2 * (cs + gap) + 90
    W, H = pw * 2 + 30, ph * len(names) + 20 * (len(names) + 1)
    sheet = Image.new("RGBA", (W, H), (245, 246, 250, 255))
    fb = ImageFont.truetype(FONT, 22)
    for i, sn in enumerate(names):
        y0 = 20 + i * (ph + 20)
        for k, (c0, c1) in enumerate((((150, 200, 240), (40, 120, 220)), ((10, 40, 110), (4, 18, 50)))):
            x0 = 10 + k * (pw + 10)
            g = np.linspace(0, 1, pw)[None, :, None]
            grad = (np.array(c0) * (1 - g) + np.array(c1) * g).repeat(ph, 0)
            panel = Image.fromarray(grad.astype(np.uint8), "RGB").convert("RGBA")
            m = Image.new("L", (pw, ph), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, pw - 1, ph - 1], 18, fill=255)
            sheet.paste(panel, (x0, y0), m)
            dr = ImageDraw.Draw(sheet)
            dr.text((x0 + 30, y0 + 18), STYLES[sn]["label"] + ("  ·  fundo claro" if k == 0 else "  ·  fundo escuro"), font=fb, fill=(255, 255, 255))
            for j, cn in enumerate(order):
                img = render(sn, cn, cs, 1.0)[0]
                sheet.alpha_composite(img, (x0 + 30 + (j % per) * (cs + gap), y0 + 64 + (j // per) * (cs + gap)))
        print("preview", sn, flush=True)
    sheet.convert("RGB").save(outpng)


if __name__ == "__main__":
    if sys.argv[1] == "full":
        preview_full(sys.argv[2], sys.argv[3].split(","))
    elif sys.argv[1] == "preview":
        preview(sys.argv[2], sys.argv[3].split(",") if len(sys.argv) > 3 else None)
    else:
        build(sys.argv[2], sys.argv[3])
