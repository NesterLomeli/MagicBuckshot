"""
Generates all of the game's pixel art as PNGs (run: python3 Tools/make_sprites.py).

Style: chunky 16x16 top-down sprites with a limited palette and dark
outlines, in the spirit of Risk of Rain 1. Characters face RIGHT
(Sprite Facing Offset = 0 in Unity). Everything is drawn from simple
shapes (ellipses, lines, rects) plus an automatic outline pass, so
sprites can be tweaked by editing numbers here and re-running.

Output: Assets/Art/{Characters,Tiles,Props,Effects,UI}/*.png
"""
import math
import os
import random
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art")

# ---------------------------------------------------------------- palette
def hexc(h, a=255):
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

C = {
    "outline": hexc("14101c"),
    # flannel / cap
    "flannel": hexc("b8423a"), "flannel_d": hexc("7a2626"), "flannel_l": hexc("d8634f"),
    "cap_w": hexc("ece6d6"), "cap_wd": hexc("b9b2a2"),
    "cap": hexc("e07b2a"), "cap_d": hexc("a8541a"),
    "denim": hexc("3a5f8f"), "denim_d": hexc("283e5e"),
    "skin": hexc("e0a47a"), "skin_d": hexc("b5774f"),
    "beard": hexc("6b4226"), "beard_d": hexc("4a2d1a"),
    "wood": hexc("8a5a34"), "wood_d": hexc("5e3a20"), "wood_l": hexc("b07a45"),
    "metal": hexc("8f93a6"), "metal_d": hexc("5c6075"), "metal_l": hexc("c9ccd8"), "blade": hexc("e4e7f0"),
    "gun": hexc("4a4a5e"), "gun_l": hexc("7a7a90"),
    "royal": hexc("3d5aa8"), "royal_d": hexc("2a3e78"), "gold": hexc("e0b13a"), "gold_d": hexc("a87a1f"),
    "plume": hexc("c23b3b"), "plume_d": hexc("7e2222"),
    "robe": hexc("6a3d9a"), "robe_d": hexc("44265f"), "robe_l": hexc("8f62c4"),
    "tape": hexc("b8b8c4"), "tape_d": hexc("7e7e8c"),
    "arcane": hexc("d9b3ff"), "arcane_d": hexc("8f62c4"), "star_d": hexc("e0b13a"),
    "wiz": hexc("2f4f9a"), "wiz_d": hexc("1f3466"), "wiz_l": hexc("4f74c4"), "star": hexc("ffe680"),
    "glow": hexc("d9b3ff"), "white": hexc("fff8e8"),
    "cyan": hexc("6fe0ff"), "cyan_d": hexc("2f9cc4"),
    "blood": hexc("8c1c1c"), "blood_d": hexc("5a0f12"), "blood_l": hexc("b02a2a"),
    "stone": hexc("5a5a6e"), "stone_d": hexc("45455a"), "stone_l": hexc("6e6e84"),
    "wall": hexc("3b3a4d"), "wall_d": hexc("2a2938"), "wall_l": hexc("57566e"),
    "grass": hexc("2f5a2e"), "grass_d": hexc("234622"), "grass_l": hexc("3f7a3a"),   # dark lush
    "dirt": hexc("7a5a3a"), "dirt_d": hexc("634829"), "dirt_l": hexc("8f6b47"),
    "carpet": hexc("2f4a8c"), "carpet_d": hexc("22386a"),   # royal blue, the King's colours
    "water": hexc("2d4f7a"), "water_d": hexc("233f63"), "water_l": hexc("4f79a8"),
    "jug": hexc("c9a36b"), "jug_d": hexc("9b7a48"), "cork": hexc("6b4a2a"),
    "potion_r": hexc("e04848"), "potion_g": hexc("5fd35f"), "potion_b": hexc("4f8fe0"),
    "flash": hexc("fff2a8"), "flash_o": hexc("ffb347"),
    "red": hexc("e03c3c"), "yellow": hexc("ffd84a"),
}


class Canvas:
    """A tiny pixel canvas with shape helpers and an auto-outline pass."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = {}

    def set(self, x, y, col):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h and col is not None:
            self.px[(x, y)] = col

    def get(self, x, y):
        return self.px.get((int(x), int(y)))

    def ellipse(self, cx, cy, rx, ry, col, shade=None, light=None, pattern=None):
        """Filled ellipse lit from the top-left (light) with shadow bottom-right."""
        for y in range(self.h):
            for x in range(self.w):
                nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                if nx * nx + ny * ny <= 1.0:
                    c = col
                    if pattern:
                        c = pattern(x, y) or col
                    if shade and nx + ny > 0.75:
                        c = shade
                    elif light and nx + ny < -0.85:
                        c = light
                    self.set(x, y, c)

    def rect(self, x0, y0, x1, y1, col):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.set(x, y, col)

    def line(self, x0, y0, x1, y1, col):
        """Bresenham line."""
        x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.set(x0, y0, col)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def outline(self, col=None, diagonal=False):
        """Adds a 1px outline around every filled shape."""
        col = col or C["outline"]
        filled = set(self.px)
        dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)]
        if diagonal:
            dirs += [(1, 1), (1, -1), (-1, 1), (-1, -1)]
        for (x, y) in list(filled):
            for dx, dy in dirs:
                n = (x + dx, y + dy)
                if n not in filled and 0 <= n[0] < self.w and 0 <= n[1] < self.h:
                    self.px[n] = col

    def save(self, folder, name):
        img = Image.new("RGBA", (self.w, self.h), (0, 0, 0, 0))
        for (x, y), c in self.px.items():
            img.putpixel((x, y), c)
        path = os.path.join(ROOT, folder)
        os.makedirs(path, exist_ok=True)
        img.save(os.path.join(path, name + ".png"))
        return img


SHEET = []  # (label, image) for the preview sheet


def out(canvas, folder, name):
    SHEET.append((name, canvas.save(folder, name)))


# ---------------------------------------------------------------- characters
# Characters are hand-placed pixel maps (16x16, facing right, seen from above).
# Each letter is a palette colour; '.' is transparent. The outline is added
# automatically afterwards, so maps only contain the fill colours.
LEGEND = {
    "F": "flannel", "f": "flannel_d", "D": "denim", "d": "denim_d",
    "C": "cap", "c": "cap_d", "W": "cap_w", "w": "cap_wd",
    "S": "skin", "s": "skin_d", "B": "beard",
    "T": "wood", "t": "wood_d", "M": "metal", "m": "metal_d", "L": "metal_l",
    "N": "gun_l", "n": "gun",
    "E": "blade", "H": "wood", "G": "gold", "g": "gold_d",
    "R": "royal", "r": "royal_d", "P": "plume", "p": "plume_d",
    "O": "wiz", "o": "wiz_d", "l": "wiz_l", "*": "star", "Y": "yellow", "A": "glow",
    "Z": "white", "X": "cyan", "x": "cyan_d", "k": "outline",
    "U": "tape", "u": "tape_d", "J": "jug", "j": "jug_d", "K": "cork",
}


def from_map(rows):
    assert len(rows) == 16 and all(len(r) == 16 for r in rows), "maps must be 16x16"
    c = Canvas(16, 16)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                c.set(x, y, C[LEGEND[ch]])
    c.outline()
    return c


CLETUS_TOP = [
    "................",
    "................",
    "....FfF.........",
    "...FFfFF........",
    "...FFfFFFF......",
    "...DDDcCWW......",
    "...FfcCCWWwCc...",
    "...FFCcCWWWCC...",
    "...fFcCcWWWCC...",
    "...FFCcCwWwcc...",
    "...DDDcCwwBB....",
]


def cletus(weapon="shotgun"):
    if weapon == "shotgun":
        # Pa's pump shotgun: wood stock, dark receiver, single barrel on top,
        # tube magazine underneath, wooden pump grip held by the front hand.
        bottom = [
            "...FfFFFTnNNNNNN",
            "...FFfFFtnnTSTn.",
            "....FfFFFS......",
        ]
    else:
        # Gatling Staff held like a chainsaw: front hand on the top loop handle,
        # rear hand on the back of the messy taped bundle, staffs pointing forward.
        bottom = [
            "...FfFFF.MMMS...",
            "...FFTUTMTUTTUXX",
            "....StuttUttutAZ",
            ".....TTUTTUTTUYY",
            "......u...u.....",
        ]
    filler = ["................"] * (16 - len(CLETUS_TOP) - len(bottom))
    return from_map(CLETUS_TOP + bottom + filler)


def cletus_dead():
    return from_map([
        "................",
        "..........F.....",
        ".D.......F...WC.",
        "..D.....F.....c.",
        "...D...F........",
        "...DDFFFFF......",
        "..DDDFfFFFFSS...",
        "..DDDFFfFFSSSS..",
        "..DDDDDDDFSSSSB.",
        "..DDDFFfFFSSSBB.",
        "..DDDFfFFFFSBB..",
        "...DDFFFFF......",
        "...D...F........",
        "..D.....F.......",
        ".D.......FS.....",
        "................",
    ])


def knight():
    return from_map([
        "................",
        "........RRR.....",
        "....LLLRRGRr....",
        "...LMMMRRgRr....",
        "...MMLMMRrr.....",
        "...MRRLLLM......",
        "...MRLLMMMm.....",
        "...mPPPPMMk.....",
        "...pPPPPPMk.....",
        "...MRMMMmmk.....",
        "...MRRmmmm......",
        "...MLMMMHG......",
        "....LLMMHGEEEEEL",
        "....MMmm.g......",
        "................",
        "................",
    ])


def knight_dead():
    return from_map([
        "................",
        "................",
        ".m.......M......",
        "..m.....M.......",
        "...m...M........",
        "...mmMMMMM......",
        "..mmMRRRRMMLLM..",
        "..mMMRRRRMLMMMk.",
        "..mMMRRRRMPPMMk.",
        "..mMMRRRRMMMMmk.",
        "..mmMRRRRMMmm...",
        "...mmMMMMM......",
        "...m...M........",
        "..m.....M.......",
        ".GEEEEEE........",
        "................",
    ])


# Wizard: "Midnight Stars" — deep blue robe and hat scattered with gold stars.
def wizard():
    return from_map([
        "................",
        "................",
        "....GGGO......A.",
        "...lOOOOOSTTTTZA",
        "...OOooooo....A.",
        "..OO*oooooo.....",
        "..OOoollO*o.....",
        "..OOolGOOOoZ....",
        "..OOolOYOOoZZ...",
        "..OOoOOOOOoZ....",
        "..oOo*OOOoo.....",
        "...oooo*oo......",
        "...oOOOOo.SA....",
        "....ggg.........",
        "................",
        "................",
    ])


def wizard_dead():
    return from_map([
        "................",
        ".ooo............",
        ".oGo......O.....",
        ".ooo.....O......",
        "........O.......",
        "...oOOOOOO......",
        "..oOOlOOOOOSSS..",
        "..oOlOOOOOSSSSZ.",
        "..oOO*OOOOSSSSZ.",
        "..oOOOOO*OSSSSZ.",
        "..ooOOOOOOOSSS..",
        "...ooOOOOO......",
        "........O.......",
        ".........O......",
        ".TTTTTTTA.......",
        "................",
    ])


# ---------------------------------------------------------------- tiles (all tile seamlessly at 16px)
def tile_noise(base, shades, seed, density=0.18):
    rnd = random.Random(seed)
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            c.set(x, y, base if rnd.random() > density else rnd.choice(shades))
    return c


def floor_stone(seed):
    c = tile_noise(C["stone"], [C["stone_l"], C["stone_d"]], seed, 0.12)
    # Bricks 8x4, every other row offset by 4 — the pattern repeats every 16px.
    for y in range(16):
        for x in range(16):
            row = y // 4
            off = 4 if row % 2 else 0
            if y % 4 == 3 or (x + off) % 8 == 7:
                c.set(x, y, C["stone_d"])
    return c


def wall_stone():
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            bx, by = x % 8, y % 8
            col = C["wall"]
            if bx == 0 or by == 0:
                col = C["wall_l"]           # bevel highlight top/left
            if bx == 7 or by == 7:
                col = C["wall_d"]           # shadow bottom/right
            c.set(x, y, col)
    rnd = random.Random(7)
    for _ in range(10):
        c.set(rnd.randrange(1, 15), rnd.randrange(1, 15), C["wall_d"])
    return c


def grass(seed):
    c = tile_noise(C["grass"], [C["grass_l"], C["grass_d"]], seed, 0.22)
    rnd = random.Random(seed * 3)
    for _ in range(4):  # little tufts
        x, y = rnd.randrange(16), rnd.randrange(1, 16)
        c.set(x, y, C["grass_l"]); c.set(x, y - 1, C["grass_l"])
    return c


def wood_floor():
    c = Canvas(16, 16)
    rnd = random.Random(11)
    for y in range(16):
        for x in range(16):
            col = C["wood"] if rnd.random() > 0.15 else C["wood_l"]
            if y % 4 == 3:
                col = C["wood_d"]                                  # plank seam
            elif (x + (y // 4) * 5) % 16 == 0:
                col = C["wood_d"]                                  # plank ends, staggered
            c.set(x, y, col)
    return c


def carpet():
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            d = abs((x % 8) - 3.5) + abs((y % 8) - 3.5)
            col = C["carpet"]
            if 3.4 < d < 4.6:
                col = C["gold_d"]                                  # diamond lattice
            elif (x + y) % 2 == 0 and d < 1.5:
                col = C["carpet_d"]
            c.set(x, y, col)
    return c


def water(seed):
    c = tile_noise(C["water"], [C["water_d"]], seed, 0.15)
    for y in (3, 11):
        for x in range(16):
            if (x + y) % 8 < 3:
                c.set(x, y, C["water_l"])
    return c


def dirt(seed):
    return tile_noise(C["dirt"], [C["dirt_l"], C["dirt_d"]], seed, 0.25)


# ---------------------------------------------------------------- props
def barrel(broken=False):
    """Dark-wood barrel with a bright iron hoop; the broken version is scattered dark staves."""
    red, red_d, red_l = hexc("5e3a24"), hexc("3e2618"), hexc("7a4e30")   # dark walnut staves
    iron = hexc("8f93a6")
    c = Canvas(16, 16)
    if not broken:
        c.ellipse(8, 8, 6.2, 6.2, red, shade=red_d, light=red_l)
        for x in (5, 8, 11):                                   # stave lines
            c.line(x, 3, x, 13, red_d)
        for y in range(16):                                    # iron hoop ring
            for x in range(16):
                d = math.hypot(x + 0.5 - 8, y + 0.5 - 8)
                if 4.6 < d < 5.5:
                    c.set(x, y, iron)
        c.ellipse(8, 8, 1.4, 1.4, red_d)                 # bung
        c.outline()
    else:
        rnd = random.Random(3)
        for _ in range(7):                                     # loose staves
            x, y = rnd.randrange(2, 13), rnd.randrange(2, 13)
            l = rnd.randrange(2, 5)
            if rnd.random() < 0.5:
                c.line(x, y, x + l, y, red)
            else:
                c.line(x, y, x, y + l, red_l)
        c.line(3, 12, 6, 13, iron); c.line(10, 3, 13, 5, iron)
        c.outline()
    return c


def crate():
    c = Canvas(16, 16)
    c.rect(1, 1, 14, 14, C["wood"])
    c.rect(1, 1, 14, 2, C["wood_l"]); c.rect(1, 13, 14, 14, C["wood_d"])
    c.rect(1, 1, 2, 14, C["wood_l"]); c.rect(13, 1, 14, 14, C["wood_d"])
    c.line(3, 3, 12, 12, C["wood_d"]); c.line(12, 3, 3, 12, C["wood_d"])
    c.outline()
    return c


def potion_shelf(broken=False):
    c = Canvas(16, 16)
    c.rect(0, 5, 15, 10, C["wood"])
    c.line(0, 5, 15, 5, C["wood_l"]); c.line(0, 10, 15, 10, C["wood_d"])
    bottles = [(2, 7, C["potion_r"]), (5, 8, C["potion_g"]), (8, 7, C["potion_b"]),
               (11, 8, C["potion_r"]), (13, 7, C["potion_g"])]
    for i, (x, y, col) in enumerate(bottles):
        if broken and i % 2 == 0:
            continue
        c.rect(x, y, x + 1, y + 1, col)
        c.set(x, y, C["white"])                                # glint
    c.outline()
    return c


def moonshine_jug():
    c = Canvas(16, 16)
    c.ellipse(8, 8.5, 5, 5, C["jug"], shade=C["jug_d"], light=C["cap_w"])
    c.line(4, 9, 12, 9, C["jug_d"])                            # glaze band
    c.ellipse(8, 8.5, 1.6, 1.6, C["cork"])                     # cork from above
    c.line(11, 4, 13, 6, C["jug_d"])                           # handle
    c.set(5, 11, C["outline"]); c.set(7, 11, C["outline"]); c.set(9, 11, C["outline"])  # "XXX"
    c.outline()
    return c


def pillar():
    c = Canvas(16, 16)
    c.ellipse(8, 8, 6.5, 6.5, C["stone_l"], shade=C["stone_d"], light=C["metal_l"])
    c.ellipse(8, 8, 4, 4, C["stone"], shade=C["stone_d"])
    c.outline()
    return c


STALL_COLOURS = {
    "red":   ("b8423a", "7a2626", "d8634f", "ece6d6"),   # red + white
    "blue":  ("2f4a8c", "22386a", "4f74c4", "e0b13a"),   # royal blue + gold
    "green": ("3f6a3a", "284626", "5f9052", "ece6d6"),   # green + cream
}


def market_stall(colourway="green"):
    """Market stall seen from above: a striped awning. The market cycles through three colourways."""
    main, dark, light, stripe = (hexc(h) for h in STALL_COLOURS[colourway])
    c = Canvas(32, 16)
    for y in range(1, 15):
        for x in range(1, 31):
            c.set(x, y, main if (x // 4) % 2 == 0 else stripe)
    c.line(1, 14, 30, 14, dark)
    c.line(1, 1, 30, 1, light)
    c.outline()
    return c


def banner():
    c = Canvas(16, 16)
    c.rect(4, 1, 11, 13, C["royal"])
    c.set(4, 14, C["royal"]); c.set(7, 14, C["royal"]); c.set(8, 14, C["royal"]); c.set(11, 14, C["royal"])
    c.rect(6, 5, 9, 8, C["gold"]); c.set(7, 4, C["gold"]); c.set(8, 4, C["gold"])     # little crown
    c.line(3, 0, 12, 0, C["wood"])
    c.outline()
    return c


# ---------------------------------------------------------------- effects
def pellet():
    """Buckshot: a short hot-orange streak with a pale-yellow head (reads as hot lead)."""
    c = Canvas(6, 4)
    c.line(0, 2, 5, 2, hexc("ff8a2a"))
    c.line(3, 1, 5, 1, hexc("fff2a8"))
    c.set(5, 2, hexc("fff2a8"))
    return c


def staff_bolt(core="cyan", edge="cyan_d"):
    """Gatling Staff bolt. Three colours, one per crystal on the bundle, fired in turn."""
    c = Canvas(8, 4)
    c.line(0, 2, 7, 2, C[edge]); c.line(3, 1, 7, 1, C[core]); c.line(5, 2, 7, 2, C["white"])
    return c


def magic_bolt():
    c = Canvas(12, 12)
    # Matches the Midnight Stars wizard: blue glow, gold star core.
    c.ellipse(6, 6, 5.5, 5.5, hexc("4f74c4", 120))
    c.ellipse(6, 6, 4, 4, C["wiz_l"])
    c.ellipse(6, 6, 2.6, 2.6, C["star"])
    c.ellipse(5.5, 5.5, 1.2, 1.2, C["white"])
    return c


def muzzle_flash():
    c = Canvas(12, 12)
    for x in range(0, 12):                                     # long forward blast
        for y in range(4, 8):
            t = x / 11
            if abs(y - 5.5) <= 2 - t * 1.6:
                c.set(x, y, C["white"] if t < 0.3 else C["flash"] if t < 0.7 else C["flash_o"])
    c.line(2, 2, 6, 5, C["flash"]); c.line(2, 9, 6, 6, C["flash"])  # side spikes
    c.set(1, 1, C["flash_o"]); c.set(1, 10, C["flash_o"])
    return c


def slash():
    c = Canvas(16, 16)
    for y in range(16):
        for x in range(16):
            d = math.hypot(x + 0.5 - 4, y + 0.5 - 8)
            if 8 < d < 10.5 and x > 6:
                c.set(x, y, C["white"] if d > 9.4 else hexc("c9ccd8", 180))
    return c


def blood(seed):
    rnd = random.Random(seed)
    c = Canvas(16, 16)
    c.ellipse(8, 8, 3 + rnd.random() * 1.5, 2.5 + rnd.random() * 1.5, C["blood"], shade=C["blood_d"])
    for _ in range(9):                                         # droplets sprayed outward (to the right)
        a = rnd.uniform(-1.0, 1.0)
        r = rnd.uniform(4, 7.5)
        x, y = 8 + math.cos(a) * r, 8 + math.sin(a) * r
        c.set(x, y, C["blood_l"] if rnd.random() < 0.3 else C["blood"])
        if rnd.random() < 0.5:
            c.set(x + 1, y, C["blood_d"])
    return c


def potion_puddle():
    c = Canvas(16, 16)
    c.ellipse(8, 8, 6, 4, hexc("5fd35f", 170))
    c.ellipse(7, 7.5, 3.5, 2.2, hexc("9cf59c", 200))
    for (x, y) in [(3, 4), (12, 11), (13, 5), (4, 12)]:
        c.set(x, y, C["white"])                                # glass shards
    return c


def splinters():
    c = Canvas(16, 16)
    rnd = random.Random(21)
    for _ in range(8):
        x, y = rnd.randrange(1, 14), rnd.randrange(1, 14)
        c.line(x, y, x + rnd.choice([1, 2]), y + rnd.choice([0, 1]), C["wood_l"] if rnd.random() < 0.5 else C["wood"])
    return c


def shell():
    c = Canvas(4, 4)
    c.rect(0, 1, 2, 2, C["flannel"]); c.set(3, 1, C["gold"]); c.set(3, 2, C["gold_d"])
    return c


def sparks(col, col2):
    c = Canvas(8, 8)
    for (x, y) in [(3, 3), (4, 4), (3, 4), (4, 3)]:
        c.set(x, y, C["white"])
    for (x, y) in [(1, 1), (6, 1), (1, 6), (6, 6), (0, 3), (7, 4), (3, 0), (4, 7)]:
        c.set(x, y, col if (x + y) % 2 else col2)
    return c


def portal():
    """Emerald exit portal: green spiral arms with a pale-lime core."""
    P = {"edge": hexc("1d4d2f"), "arm": hexc("a6f0b8"), "core_arm": hexc("e6ff8a"),
         "mid": hexc("52b06f"), "base": hexc("2f7a4a")}
    c = Canvas(32, 32)
    for y in range(32):
        for x in range(32):
            dx, dy = x + 0.5 - 16, y + 0.5 - 16
            d = math.hypot(dx, dy)
            if d > 14.5:
                continue
            a = math.atan2(dy, dx)
            swirl = math.sin(a * 3 + d * 0.6)                  # three spiral arms
            if d > 13:
                col = P["edge"]
            elif swirl > 0.35:
                col = P["core_arm"] if d < 6 else P["arm"]
            elif swirl > -0.3:
                col = P["mid"]
            else:
                col = P["base"]
            if d < 2.5:
                col = C["white"]
            c.set(x, y, col)
    c.outline()
    return c


def gatling_staff_pickup():
    """
    The Gatling Staff: a messy bundle of confiscated wizard staffs, uneven
    lengths, wrapped in crooked duct tape, with a loop handle on top and a
    rear grip. Held like a chainsaw. Each staff keeps its crystal tip
    (ice, arcane, star).
    """
    return from_map([
        "................",
        "................",
        "................",
        "................",
        "......MMMM......",
        "......M..M......",
        "...TTUTTUTTTUTXX",
        "HttuuttUttttutAZ",
        "H.TTTUTTUUTTUTYY",
        "..ttuttuttttut..",
        "....u..u....u...",
        "................",
        "................",
        "................",
        "................",
        "................",
    ])


def icon(char):
    c = Canvas(8, 8)
    if char == "!":
        c.rect(3, 0, 4, 4, C["red"]); c.rect(3, 6, 4, 7, C["red"])
    else:
        for (x, y) in [(2, 0), (3, 0), (4, 0), (5, 1), (5, 2), (4, 3), (3, 4), (3, 6), (3, 7)]:
            c.set(x, y, C["yellow"])
        c.set(1, 1, C["yellow"])
    c.outline()
    return c


def ui_pixel():
    c = Canvas(4, 4)
    c.rect(0, 0, 3, 3, C["white"])
    return c



# ---------------------------------------------------------------- title art
HEART = [".XX.XX.",
         "XXXXXXX",
         "XXXXXXX",
         ".XXXXX.",
         "..XXX..",
         "...X..."]


def draw_heart(c, ox, oy, col, highlight):
    for y, row in enumerate(HEART):
        for x, ch in enumerate(row):
            if ch == "X":
                c.set(ox + x, oy + y, col)
    c.set(ox + 1, oy + 1, highlight)   # little shine


def title_skeeter_detailed():
    """
    Detailed source drawing (64x64, front view): Skeeter, eyes closed and
    blushing, hugging his beloved moonshine jug, with hearts floating around.
    """
    c = Canvas(64, 64)
    flannel = lambda x, y: C["flannel_d"] if y % 5 == 0 or x % 6 == 0 else (C["flannel"] if x % 2 else C["flannel_l"])

    # Shoulders and torso (red flannel), overall bib and straps
    c.ellipse(32, 54, 19, 14, C["flannel"], shade=C["flannel_d"], pattern=flannel)
    c.rect(24, 44, 40, 63, C["denim"])
    c.rect(24, 44, 40, 45, C["denim_d"])
    c.line(22, 38, 25, 45, C["denim"]); c.line(42, 38, 39, 45, C["denim"])
    c.set(26, 47, C["gold"]); c.set(38, 47, C["gold"])                    # overall buttons

    # The moonshine jug, held tight against his chest
    c.ellipse(32, 49, 10.6, 11.6, C["outline"])                          # dark rim separates jug from shirt
    c.rect(28, 32, 36, 40, C["outline"])
    c.ellipse(32, 49, 9.5, 10.5, C["jug"], shade=C["jug_d"], light=C["cap_w"])
    c.rect(29, 36, 35, 40, C["jug"]); c.rect(29, 36, 29, 40, C["cap_w"])  # jug neck
    c.rect(29, 33, 35, 35, C["cork"])                                     # cork
    c.line(37, 38, 41, 42, C["jug_d"]); c.line(41, 42, 40, 46, C["jug_d"])  # handle
    c.line(24, 52, 40, 52, C["jug_d"])                                    # glaze band
    for i, x0 in enumerate((26, 31, 36)):                                 # "XXX" label
        c.set(x0, 47, C["outline"]); c.set(x0 + 2, 47, C["outline"])
        c.set(x0 + 1, 48, C["outline"])
        c.set(x0, 49, C["outline"]); c.set(x0 + 2, 49, C["outline"])

    # Arms wrapped around the jug, hands meeting at the front
    for dx in range(-1, 5):                                               # sleeves with dark edges
        edge = dx in (-1, 4)
        c.line(14 + dx, 42, 24 + dx, 53, C["outline"] if edge else C["flannel_l"] if dx == 0 else C["flannel"])
        c.line(50 - dx, 42, 40 - dx, 53, C["outline"] if edge else C["flannel_l"] if dx == 0 else C["flannel"])
    c.ellipse(27, 54, 3.4, 3.0, C["outline"]); c.ellipse(37, 54, 3.4, 3.0, C["outline"])
    c.ellipse(27, 54, 2.6, 2.2, C["skin"], shade=C["skin_d"])
    c.ellipse(37, 54, 2.6, 2.2, C["skin"], shade=C["skin_d"])

    # Trucker cap crown first (only the part above the brim), then the face over it
    for y in range(64):
        for x in range(64):
            nx, ny = (x + 0.5 - 32) / 12, (y + 0.5 - 14) / 8
            if nx * nx + ny * ny <= 1 and y <= 12:
                front = 26 <= x <= 38                                     # white foam front panel
                c.set(x, y, (C["cap_w"] if front else C["cap"]) if nx + ny < 0.6 else (C["cap_wd"] if front else C["cap_d"]))
    c.set(32, 6, C["cap_d"])                                              # button on top

    # Face and big brown beard resting on the jug's cork
    c.ellipse(32, 22, 10, 9.5, C["skin"], shade=C["skin_d"])
    c.ellipse(32, 30, 11, 6.5, C["beard"], shade=C["beard_d"])
    c.rect(29, 28, 35, 28, C["beard_d"])                                  # smile
    c.set(28, 27, C["beard_d"]); c.set(36, 27, C["beard_d"])
    for ex in (27, 37):                                                   # happy closed eyes ^ ^
        c.set(ex - 2, 21, C["outline"]); c.set(ex - 1, 20, C["outline"]); c.set(ex, 19, C["outline"])
        c.set(ex + 1, 20, C["outline"]); c.set(ex + 2, 21, C["outline"])
    for bx in (25, 39):                                                   # blush
        c.rect(bx - 1, 23, bx + 1, 24, hexc("f08a8a"))
    c.rect(31, 22, 33, 24, C["skin_d"]); c.set(32, 22, C["skin"])         # nose

    # Cap brim across the forehead
    c.rect(19, 13, 45, 15, C["cap_d"])
    c.rect(19, 13, 45, 13, C["cap"])

    return c


TINY_HEART = ["XX.XX",
              "XXXXX",
              ".XXX.",
              "..X.."]


def title_skeeter():
    """
    Title-screen Skeeter, modelled on a Hotline Miami floor sprite: seen from
    above, sprawled on his back, one arm flung up past his head and one down,
    legs bent into a "C". Limbs are drawn as thick polylines, then every
    material gets three-tone shading (light top-left, dark bottom-right) and
    a dark outline, which gives the gritty, solid HM look rather than a
    cartoon. His moonshine jug lies by his lower hand. 44x32.
    """
    W, H = 44, 32
    mat = [[None] * W for _ in range(H)]          # material name per pixel

    def disk(cx, cy, r, m):
        for y in range(H):
            for x in range(W):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    mat[y][x] = m

    def limb(points, r, m):
        """Thick polyline: a disk stamped every half pixel along each segment."""
        for (x0, y0), (x1, y1) in zip(points, points[1:]):
            steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
            for i in range(steps + 1):
                t = i / steps
                disk(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r, m)

    def ellipse(cx, cy, rx, ry, m):
        for y in range(H):
            for x in range(W):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    mat[y][x] = m

    # Legs first (under the torso): jeans bent into a "C", boots at the ends
    limb([(23, 13), (30, 7.5), (36, 10.5)], 2.1, "jeans")
    limb([(23, 19), (30, 24.5), (37, 21.5)], 2.1, "jeans")
    disk(37.5, 11.5, 2.0, "boot"); disk(38.5, 21, 2.0, "boot")

    # Arms: one flung up past the head, one down by his side
    limb([(15, 11), (11, 6), (6, 3.5)], 1.7, "flannel")
    limb([(15, 21), (12, 26), (7, 28)], 1.7, "flannel")
    disk(4.6, 3.4, 1.6, "skin"); disk(5.6, 28.2, 1.6, "skin")

    # Torso: flannel shirt with the overalls' bib and straps on top
    ellipse(17.5, 16, 6.8, 7.2, "flannel")
    for y in range(12, 21):
        for x in range(17, 23):
            if mat[y][x] == "flannel":
                mat[y][x] = "denim"
    limb([(17, 12.5), (14.5, 10.5)], 0.6, "denim")
    limb([(17, 19.5), (14.5, 21.5)], 0.6, "denim")

    # Head: face up and blank (like HM bodies), beard toward the chest
    disk(9.5, 16, 4.0, "skin")
    for y in range(13, 20):
        for x in range(11, 14):
            if (x - 11) + abs(y - 16) * 0.6 < 3.2:
                mat[y][x] = "beard"

    # Tall trucker cap on top of his head (pointing left): white foam front
    # panel facing up, orange mesh back and sides, a curved brim over the forehead
    for y in range(H):
        for x in range(W):
            dx, dy = (x + 0.5 - 6.0) / 4.6, (y + 0.5 - 16) / 4.6
            if dx * dx + dy * dy <= 1 and x <= 8:
                mat[y][x] = "mesh"
    for y in range(13, 20):
        for x in range(2, 8):
            if mat[y][x] == "mesh" and abs(y + 0.5 - 16) < 3.2:
                mat[y][x] = "panel"
    for y in range(12, 21):
        if abs(y + 0.5 - 16) < 4.3:
            mat[y][8] = "brim"
            if abs(y + 0.5 - 16) < 3.0:
                mat[y][9] = "brim"

    # The jug, knocked over by his lower hand
    ellipse(3.2, 23.5, 2.4, 2.8, "jug")
    disk(3.2, 20.4, 0.9, "cork")

    palette = {   # base, light, dark
        "flannel": (C["flannel"], C["flannel_l"], C["flannel_d"]),
        "denim": (C["denim"], hexc("4f77a8"), C["denim_d"]),
        "jeans": (hexc("33547f"), hexc("466a99"), hexc("223a5a")),
        "boot": (hexc("4a2e1c"), hexc("6b4429"), hexc("2e1c12")),
        "skin": (C["skin"], hexc("eebb93"), C["skin_d"]),
        "mesh": (C["cap"], hexc("f29a4e"), C["cap_d"]),
        "panel": (C["cap_w"], hexc("fffaf0"), C["cap_wd"]),
        "brim": (C["cap"], hexc("f29a4e"), C["cap_d"]),
        "beard": (C["beard"], hexc("845535"), C["beard_d"]),
        "jug": (C["jug"], C["cap_w"], C["jug_d"]),
        "cork": (C["cork"], C["cork"], C["cork"]),
    }
    c = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            m = mat[y][x]
            if m is None:
                continue
            base, light, dark = palette[m]
            below = mat[y + 1][x] if y + 1 < H else None
            right = mat[y][x + 1] if x + 1 < W else None
            above = mat[y - 1][x] if y > 0 else None
            left = mat[y][x - 1] if x > 0 else None
            if below != m or right != m:
                col = dark                                   # shadowed edge
            elif above != m or left != m:
                col = light                                  # lit edge
            else:
                col = base
            c.set(x, y, col)
    # Flannel check lines and the closed eyes
    for y in range(H):
        for x in range(W):
            if mat[y][x] == "flannel" and (x % 4 == 0 or y % 4 == 0) and c.get(x, y) == C["flannel"]:
                c.set(x, y, C["flannel_d"])
    c.outline()
    return c



# ---------------------------------------------------------------- menu art
def castle_background():
    """
    Muted pixel-art backdrop for the menus (320x180, scaled 6x to 1920x1080):
    a dusk sky, a pale moon, distant hills and a dark castle silhouette with a
    few dim, warm windows. Desaturated on purpose so the title and buttons pop.
    """
    W, H = 320, 180
    c = Canvas(W, H)
    rnd = random.Random(42)
    top, bottom = (24, 22, 38), (58, 48, 66)                     # muted dusk gradient
    for y in range(H):
        t = y / (H - 1)
        band = round(t * 7) / 7                                    # banded, like a limited palette
        col = tuple(int(top[i] + (bottom[i] - top[i]) * band) for i in range(3)) + (255,)
        for x in range(W):
            c.set(x, y, col)
    for _ in range(70):                                            # faint stars, upper sky only
        x, y = rnd.randrange(W), rnd.randrange(0, 90)
        c.set(x, y, (120, 116, 140, 255) if rnd.random() < 0.8 else (170, 166, 190, 255))
    for y in range(H):                                             # pale moon
        for x in range(W):
            d = math.hypot(x - 250, y - 38)
            if d < 13:
                c.set(x, y, (196, 190, 176, 255) if math.hypot(x - 245, y - 34) > 12 or d < 10 else (170, 164, 152, 255))
    def hills(base, amp, freq, phase, col):
        for x in range(W):
            h = base + amp * math.sin(x * freq + phase) + amp * 0.4 * math.sin(x * freq * 2.7 + phase * 2)
            for y in range(int(h), H):
                c.set(x, y, col)
    hills(128, 6, 0.02, 1.0, (52, 44, 60, 255))
    hills(140, 5, 0.035, 2.5, (42, 36, 50, 255))

    castle = (28, 24, 36, 255)
    def block(x0, y0, x1, y1, col=castle):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                if 0 <= x < W and 0 <= y < H:
                    c.set(x, y, col)
    def crenels(x0, x1, y):
        for x in range(x0, x1 + 1, 4):
            block(x, y - 3, x + 1, y - 1)
    # Curtain wall, towers, keep and spires
    block(150, 112, 300, 150); crenels(150, 300, 112)
    for (tx, top_y, w) in [(146, 92, 12), (196, 80, 14), (246, 88, 12), (292, 96, 12)]:
        block(tx, top_y, tx + w, 150); crenels(tx, tx + w, top_y)
    block(212, 60, 236, 150); crenels(212, 236, 60)                 # keep
    for (sx, sy, w) in [(202, 80, 4), (250, 88, 3), (224, 60, 4)]:  # pointed roofs
        for i in range(14):
            block(sx - w + i // 3, sy - i, sx + w - i // 3, sy - i)
    block(0, 150, W, H, (22, 19, 30, 255))                         # foreground ground
    for (wx, wy) in [(200, 96), (218, 74), (230, 74), (224, 100), (252, 104), (170, 124), (275, 126), (150, 104)]:
        block(wx, wy, wx + 1, wy + 2, (148, 112, 58, 255))          # dim warm windows
    for i in range(16):                                            # gate
        block(222 - i // 4, 150 - i, 228 + i // 4, 150 - i, (16, 13, 22, 255)) if i < 12 else None
    return c


def wood_button(width=48, height=14):
    """Rustic wood-plank sign button (stretched as a 9-slice in Unity)."""
    c = Canvas(width, height)
    rnd = random.Random(5)
    for y in range(height):
        for x in range(width):
            col = C["wood"] if rnd.random() > 0.12 else C["wood_l"]
            if y == height // 2:
                col = C["wood_d"]                                  # seam between two planks
            c.set(x, y, col)
    for x in range(width):
        c.set(x, 0, C["wood_l"]); c.set(x, height - 1, C["wood_d"])
    for (x, y) in [(2, 2), (width - 3, 2), (2, height - 3), (width - 3, height - 3)]:
        c.set(x, y, C["metal_l"])                                  # nails
    c.outline()
    return c


def stone_button(width=48, height=14):
    """Castle stone tablet with a thin gold inlay border."""
    c = Canvas(width, height)
    rnd = random.Random(9)
    for y in range(height):
        for x in range(width):
            c.set(x, y, C["stone"] if rnd.random() > 0.15 else C["stone_d"])
    for x in range(width):
        c.set(x, 0, C["stone_l"]); c.set(x, height - 1, C["wall_d"])
    for y in range(height):
        c.set(0, y, C["stone_l"]); c.set(width - 1, y, C["wall_d"])
    for x in range(2, width - 2):
        c.set(x, 2, C["gold_d"]); c.set(x, height - 3, C["gold_d"])
    for y in range(2, height - 2):
        c.set(2, y, C["gold_d"]); c.set(width - 3, y, C["gold_d"])
    c.outline()
    return c


def menu_arrow():
    """Gold selection arrow for the minimal menu style."""
    c = Canvas(6, 7)
    for y in range(7):
        for x in range(4 - abs(3 - y) + 1):
            c.set(x + 1, y, C["gold"])
    c.outline()
    return c



def ending_field():
    """
    Ending backdrop, top-down and at the same pixel density as the levels
    (480x270, shown at 4x = 1920x1080), so Skeeter's sprite appears at roughly
    his in-game size. A morning field outside his place: a trailer and his red
    pickup scaled against him (he's ~38px lying down, about 1.8 m), the copper
    moonshine still, a dirt patch and a scorch ring where the "portal" was.
    """
    W, H = 480, 270
    c = Canvas(W, H)
    rnd = random.Random(77)

    def rect(x0, y0, x1, y1, col):
        for y in range(max(0, y0), min(H, y1 + 1)):
            for x in range(max(0, x0), min(W, x1 + 1)):
                c.set(x, y, col)

    # Grass with flowers, a trampled dirt patch and the scorch ring
    g, gd, gl = hexc("3f6a36"), hexc("2f5229"), hexc("557f45")
    for y in range(H):
        for x in range(W):
            r = rnd.random()
            c.set(x, y, gl if r < 0.12 else gd if r < 0.24 else g)
    for _ in range(140):
        c.set(rnd.randrange(W), rnd.randrange(H), rnd.choice([hexc("e8d35a"), hexc("ece6d6"), hexc("d9708a")]))
    for y in range(H):
        for x in range(W):
            if ((x - 150) / 70) ** 2 + ((y - 165) / 42) ** 2 <= 1 and rnd.random() < 0.85:
                c.set(x, y, C["dirt"] if rnd.random() > 0.2 else C["dirt_d"])
            d = math.hypot(x - 330, y - 200)
            if 18 < d < 24 and rnd.random() < 0.75:
                c.set(x, y, hexc("2a2420"))

    # Trailer (top-down roof): ~10 m x 3 m -> 170 x 56 px
    tx, ty = 280, 22
    rect(tx + 4, ty + 4, tx + 173, ty + 59, hexc("1e2a1c"))                          # shadow
    for y in range(ty, ty + 56):
        for x in range(tx, tx + 170):
            c.set(x, y, hexc("d8d6cc") if (x - tx) % 6 < 3 else hexc("bcbab0"))       # corrugated roof
    rect(tx, ty, tx + 169, ty + 1, hexc("8a5a3a")); rect(tx, ty + 54, tx + 169, ty + 55, hexc("8a5a3a"))  # rusty edges
    rect(tx + 120, ty + 18, tx + 140, ty + 34, hexc("9a9890")); rect(tx + 122, ty + 20, tx + 138, ty + 32, hexc("7a7870"))  # AC unit
    rect(tx + 40, ty + 22, tx + 50, ty + 32, hexc("8a8880"))                          # roof vent
    for i in range(10):                                                               # hitch
        rect(tx - 1 - i, ty + 28 - i // 2, tx - 1 - i, ty + 28 + i // 2, hexc("3a3a40"))

    # Red pickup (top-down): ~5.5 m x 2 m -> 100 x 38 px, nose pointing left
    px, py = 200, 82
    rect(px + 3, py + 3, px + 103, py + 41, hexc("1e2a1c"))                           # shadow
    for (wx, wy) in [(px + 14, py - 2), (px + 14, py + 34), (px + 72, py - 2), (px + 72, py + 34)]:
        rect(wx, wy, wx + 13, wy + 5, hexc("1a1a1e"))                                 # tyres peeking out
    red, red_d, red_l = hexc("b02a26"), hexc("7a1c1a"), hexc("d04a3e")
    rect(px, py + 2, px + 99, py + 35, red)
    rect(px, py + 2, px + 99, py + 3, red_l); rect(px, py + 34, px + 99, py + 35, red_d)
    rect(px + 2, py + 6, px + 26, py + 31, red_l)                                     # hood
    rect(px, py + 4, px + 1, py + 8, hexc("ffe680")); rect(px, py + 29, px + 1, py + 33, hexc("ffe680"))  # headlights
    rect(px + 27, py + 5, px + 33, py + 32, hexc("3a4f6a"))                           # windshield
    rect(px + 28, py + 6, px + 29, py + 14, hexc("6f8fb0"))                           # glare
    rect(px + 34, py + 4, px + 55, py + 33, red)                                      # cab roof
    rect(px + 56, py + 5, px + 58, py + 32, hexc("3a4f6a"))                           # rear window
    rect(px + 60, py + 5, px + 97, py + 32, hexc("4a3a34"))                           # truck bed
    for bx in range(64, 96, 6):
        rect(px + bx, py + 6, px + bx, py + 31, hexc("3a2c26"))                       # bed ribs
    rect(px + 97, py + 3, px + 99, py + 34, red_d)                                    # tailgate
    rect(px + 30, py, px + 33, py + 2, red_d); rect(px + 30, py + 35, px + 33, py + 37, red_d)  # mirrors
    rect(px + 66, py + 10, px + 74, py + 18, C["jug"]); rect(px + 69, py + 13, px + 71, py + 15, C["cork"])  # a spare jug in the bed

    # Copper moonshine still beside the trailer: pot, cap and coil
    sx, sy = 245, 40
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - sx, y - sy)
            if d < 13:
                c.set(x, y, hexc("b8743a") if d < 10 else hexc("7a4a22"))
    rect(sx - 3, sy - 3, sx + 2, sy + 2, hexc("e0a060"))
    for i in range(16):
        c.set(sx + 14 + i // 2, sy - 4 + (i % 6), hexc("b8743a"))
    return c


def ammo_shell(style):
    """
    HUD shotgun shell, standing upright (7x15). Each slot in the ammo counter
    uses a different shell so the row looks like a handful of real, mismatched
    shells: plain, worn and scuffed, one with a doodled face, a dented one,
    and a green hull. 'empty' is the dim outline of a spent slot.
    """
    c = Canvas(7, 15)
    hull, hull_d, hull_l = hexc("b8322a"), hexc("7a1f1a"), hexc("d9574a")
    brass, brass_d = C["gold"], C["gold_d"]
    if style == "worn":
        hull, hull_d, hull_l = hexc("9a4a40"), hexc("6a2e28"), hexc("b8685c")
        brass, brass_d = hexc("b8954a"), hexc("8a6a30")
    if style == "green":
        hull, hull_d, hull_l = hexc("3f7a3a"), hexc("2a5226"), hexc("5f9a52")
    if style == "empty":
        dark = hexc("3a3446")
        for y in range(1, 14):
            for x in range(1, 6):
                c.set(x, y, dark)
        c.outline()
        return c
    for y in range(1, 10):                          # plastic hull
        for x in range(1, 6):
            c.set(x, y, hull_l if x == 1 else hull_d if x == 5 else hull)
    for x in (2, 4):                                # folded crimp on top
        c.set(x, 1, hull_d)
    for y in range(10, 14):                         # brass head
        for x in range(1, 6):
            c.set(x, y, brass if x < 5 else brass_d)
    c.set(1, 10, hexc("fff2a8"))                    # brass shine
    if style == "worn":
        for (x, y) in [(2, 3), (4, 5), (3, 7), (2, 8)]:
            c.set(x, y, hull_l)                     # scuffs
    if style == "face":
        c.set(2, 4, C["outline"]); c.set(4, 4, C["outline"])          # eyes
        c.set(2, 6, C["outline"]); c.set(3, 7, C["outline"]); c.set(4, 6, C["outline"])  # smile
    if style == "dented":
        c.set(5, 4, C["outline"]); c.set(4, 5, hull_d); c.set(5, 5, hull_d)   # a dent in the side
    c.outline()
    return c



# ---------------------------------------------------------------- market stands (breakable)
# Two tiles wide like the stalls, but seen as an open table: a short striped
# awning along the back edge and the goods laid out in front, so the player
# can tell at a glance what will happen when it's shot.

def stand_table(c, awning):
    """Wooden table top with a strip of awning along the back (top) edge."""
    main, dark, light, stripe = (hexc(h) for h in STALL_COLOURS[awning])
    for y in range(4, 15):
        for x in range(1, 31):
            c.set(x, y, C["wood_l"] if y == 4 else C["wood_d"] if y == 14 else C["wood"])
    for x in range(1, 31, 6):
        c.line(x, 5, x, 13, C["wood_d"])                          # plank seams
    for y in range(0, 4):
        for x in range(1, 31):
            c.set(x, y, main if (x // 4) % 2 == 0 else stripe)
    c.line(1, 3, 30, 3, dark)


def fruit_stand(broken=False):
    c = Canvas(32, 16)
    stand_table(c, "green")
    if not broken:
        rnd = random.Random(5)
        # three crates: apples, oranges, melons; bananas in the corner
        for bx, col, coll in ((3, C["red"], hexc("ff8a7a")), (12, hexc("f08a24"), hexc("ffc070")), (21, hexc("5fa83a"), hexc("9ad86a"))):
            c.rect(bx, 6, bx + 7, 12, C["wood_d"])
            for y in range(7, 12, 2):
                for x in range(bx + 1, bx + 7, 2):
                    c.rect(x, y, x + 1, y + 1, col); c.set(x, y, coll)
        for x in range(28, 30):
            c.set(x, 6, C["yellow"]); c.set(x, 7, C["yellow"])
    else:
        c.line(15, 4, 17, 14, C["outline"])                          # split down the middle
        for (x, y, col) in ((5, 8, C["red"]), (24, 10, hexc("5fa83a")), (10, 12, hexc("f08a24"))):
            c.rect(x, y, x + 1, y + 1, col)
    c.outline()
    return c


def sword_rack(broken=False):
    c = Canvas(32, 16)
    stand_table(c, "blue")
    if not broken:
        for i, y in enumerate((6, 9, 12)):
            x0 = 4 + (i % 2) * 3
            c.line(x0 + 6, y, x0 + 21, y, C["blade"])               # blade
            c.line(x0 + 7, y - 0, x0 + 20, y, C["blade"])
            c.set(x0 + 21, y, C["metal_l"])                          # point
            c.line(x0 + 5, y - 1, x0 + 5, y + 1, C["gold"])           # crossguard
            c.line(x0 + 1, y, x0 + 4, y, C["wood_d"])                 # grip
            c.set(x0, y, C["gold_d"])                                 # pommel
    c.outline()
    return c


def potion_stand(broken=False):
    c = Canvas(32, 16)
    stand_table(c, "red")
    cols = [C["potion_r"], C["potion_g"], C["potion_b"], hexc("e070d0"), hexc("f0c040")]
    for i in range(7):
        if broken and i % 3 != 1:
            continue
        x = 2 + i * 4; y = 7 + (i % 2) * 3
        col = cols[i % len(cols)]
        c.rect(x, y, x + 2, y + 2, col)                               # round bottle
        c.set(x + 1, y - 1, C["cork"])                                # cork
        c.set(x, y, C["white"])                                       # glint
    c.outline()
    return c


def fruit_mush():
    """Decal left by a smashed fruit stand: squashed fruit and juice."""
    c = Canvas(24, 24)
    rnd = random.Random(8)
    for col in (C["red"], hexc("f08a24"), hexc("5fa83a"), C["yellow"], hexc("c02a40")):
        for _ in range(4):
            x, y = rnd.randrange(3, 20), rnd.randrange(3, 20)
            c.rect(x, y, x + rnd.choice([0, 1]), y + rnd.choice([0, 1]), col)
    for _ in range(10):
        x, y = rnd.randrange(2, 22), rnd.randrange(2, 22)
        c.set(x, y, hexc("8c2030", 200))                              # juice drops
    return c


def fruit_piece(kind):
    """A single fruit that rolls away when a stand is shot."""
    c = Canvas(4, 4)
    col, light = {"apple": (C["red"], hexc("ff8a7a")), "orange": (hexc("f08a24"), hexc("ffc070")),
                  "melon": (hexc("5fa83a"), hexc("9ad86a"))}[kind]
    c.rect(1, 1, 2, 2, col); c.set(1, 1, light)
    c.outline()
    return c


def loose_sword():
    """A sword that falls off the rack and skids across the floor (14x3)."""
    c = Canvas(16, 5)
    c.line(6, 2, 14, 2, C["blade"]); c.set(15, 2, C["metal_l"])
    c.line(5, 1, 5, 3, C["gold"])
    c.line(2, 2, 4, 2, C["wood_d"]); c.set(1, 2, C["gold_d"])
    c.outline()
    return c


def smoke_puff():
    """Soft white puff, tinted at runtime (potion clouds, cigarette smoke)."""
    c = Canvas(8, 8)
    c.ellipse(4, 4, 3.6, 3.6, hexc("ffffff", 230), shade=hexc("d8d8d8", 230))
    return c



def ammo_bolt(kind):
    """HUD icon for the Gatling Staff's ammo belt (5x9): one star bolt, in the three alternating colours."""
    c = Canvas(5, 9)
    if kind == "empty":
        c.rect(1, 1, 3, 7, hexc("3a3446")); c.rect(2, 2, 2, 6, hexc("4a4456"))
    else:
        col = {"cyan": C["cyan"], "arcane": C["arcane"], "star": C["star"]}[kind]
        c.rect(1, 1, 3, 7, col); c.rect(2, 2, 2, 6, C["white"]); c.set(2, 0, C["white"])
    c.outline()
    return c


def cigarette_hud(lit=False):
    """HUD cigarette (20x5), filter on the left. The HUD crops it from the right as it burns."""
    c = Canvas(20, 5)
    for x in range(1, 5):
        c.set(x, 1, hexc("d8963a")); c.set(x, 2, hexc("d8963a")); c.set(x, 3, hexc("b0742a"))
    for x in range(5, 19):
        c.set(x, 1, C["white"]); c.set(x, 2, C["white"]); c.set(x, 3, hexc("d8d0c0"))
    c.outline()
    return c


def ember():
    """Glowing tip of the cigarette (3x5 with outline)."""
    c = Canvas(3, 5)
    c.set(1, 1, hexc("6a6a6a")); c.set(1, 2, hexc("ff5a20")); c.set(1, 3, hexc("ffb040"))
    c.outline()
    return c


def cig_mouth():
    """The cigarette in Skeeter's mouth (6x3): tiny white stick with a glowing end."""
    c = Canvas(6, 3)
    c.set(1, 1, C["white"]); c.set(2, 1, C["white"]); c.set(3, 1, C["white"]); c.set(4, 1, hexc("ff5a20"))
    return c


def vignette():
    """Soft dark-edged frame (64x36, white with alpha), tinted green on screen during bullet time."""
    img = Image.new("RGBA", (64, 36), (0, 0, 0, 0))
    for y in range(36):
        for x in range(64):
            dx, dy = (x + 0.5 - 32) / 32, (y + 0.5 - 18) / 18
            d = (dx * dx + dy * dy) ** 0.5
            a = max(0.0, min(1.0, (d - 0.85) / 0.45))   # only the outer edge, so the view stays wide
            img.putpixel((x, y), (255, 255, 255, int(a * 255)))
    return img



def bookshelf():
    """Library bookshelf seen from above (32x16): dark wood frame, rows of coloured book spines."""
    c = Canvas(32, 16)
    c.rect(1, 2, 30, 14, C["wood_d"])
    c.line(1, 2, 30, 2, C["wood"])
    spines = [C["plume"], C["royal"], C["robe"], C["grass_l"], C["gold_d"], C["flannel_d"], C["wiz_l"], C["jug_d"]]
    rnd = random.Random(12)
    for row_y in (4, 9):
        x = 2
        while x < 29:
            w = rnd.choice([1, 2, 2, 3])
            col = rnd.choice(spines)
            c.rect(x, row_y, min(x + w - 1, 29), row_y + 3, col)
            if w > 1: c.set(x, row_y, C["gold"] if rnd.random() < 0.3 else col)   # gilt lettering on a few
            x += w
        c.line(2, row_y + 4, 29, row_y + 4, C["wood_d"])
    c.outline()
    return c


def page():
    """A torn page fluttering out of a shot bookshelf (5x4)."""
    c = Canvas(6, 5)
    c.rect(1, 1, 4, 3, hexc("ece2c6")); c.line(2, 2, 3, 2, hexc("8a7a5a"))
    c.outline()
    return c


def lectern():
    """Stone pedestal the Gatling Staff rests on (16x16), with a purple cloth."""
    c = Canvas(16, 16)
    c.ellipse(8, 8, 6.5, 6.5, C["stone"], shade=C["stone_d"], light=C["stone_l"])
    c.ellipse(8, 8, 4.5, 4.5, C["robe"], shade=C["robe_d"], light=C["robe_l"])
    c.outline()
    return c


GLYPHS = [  # tiny made-up runes (3 wide, 5 tall) for the enchantment particles
    ["#.#", "###", "..#", "..#", "..#"], ["###", "#..", "##.", "#..", "#.."], ["#..", "#..", "###", "..#", "..#"],
    [".#.", "#.#", ".#.", "#.#", ".#."], ["###", "..#", ".#.", "#..", "###"], ["#.#", "#.#", ".#.", ".#.", "#.#"],
    ["##.", "#.#", "##.", "#..", "#.."], [".##", "#..", ".#.", "..#", "##."], ["#..", "##.", "#.#", "##.", "#.."],
    ["###", ".#.", ".#.", ".#.", "#.."], ["#.#", "###", "#.#", "#.#", "#.#"], [".#.", "###", ".#.", ".#.", "###"],
]


def glyph(i):
    c = Canvas(3, 5)
    for y, row in enumerate(GLYPHS[i]):
        for x, ch in enumerate(row):
            if ch == "#":
                c.set(x, y, C["white"])
    return c



# ---------------------------------------------------------------- the King's feast (Level 3)
def king():
    """The King, seen from above like the others: gold crown ring, ermine collar, red robe, goblet in hand."""
    return from_map([
        "................",
        "................",
        "....ppppp.......",
        "...pPPPPPp......",
        "..pPZZZZZPp.....",
        "..pZkZGYGZPp....",
        "..PZGgggGGZS....",
        "..PZYgSgYGZSZ...",
        "..PZGgggGGZZZ...",
        "..pZkZGYGZPZ.G..",
        "..pPZZZZZPPSGJG.",
        "...pPPPPPPp..G..",
        "....ppPPpp......",
        "................",
        "................",
        "................",
    ])


def king_dead():
    return from_map([
        "................",
        "...........G.Y..",
        "............GG..",
        "..ppppp.........",
        ".pPPPPPPp.......",
        ".pPZZZZZPPSS....",
        ".PPZkZZZZSSSZ...",
        ".PPZZZZZZSSSZZ..",
        ".PPZkZZZZSSSZ...",
        ".pPZZZZZPPSS....",
        ".pPPPPPPp...J...",
        "..ppppp.....GG..",
        "................",
        "................",
        "................",
        "................",
    ])


def feast_table():
    """One 16x16 piece of the long banquet table: dark wood with a white tablecloth runner."""
    c = Canvas(16, 16)
    c.rect(0, 1, 15, 14, C["wood"])
    c.line(0, 1, 15, 1, C["wood_l"]); c.line(0, 14, 15, 14, C["wood_d"])
    c.rect(0, 4, 15, 11, C["cap_w"])                                 # cloth runner
    c.line(0, 4, 15, 4, C["white"]); c.line(0, 11, 15, 11, C["cap_wd"])
    for x in (3, 11):
        c.set(x, 7, C["cap_wd"])                                     # cloth weave flecks
    return c


def food(kind, broken=False):
    """Food on the table (16x16, drawn over a table piece). Broken = crumbs and a spill."""
    c = Canvas(16, 16)
    if broken:
        rnd = random.Random(len(kind))
        spill = {"turkey": C["wood_l"], "wine": C["blood_l"], "fruit": C["red"], "pie": C["jug"]}[kind]
        for _ in range(9):
            c.set(rnd.randrange(3, 13), rnd.randrange(4, 12), spill)
        return c
    if kind == "turkey":
        c.ellipse(8, 8, 6, 4.5, C["cap_wd"])                          # platter
        c.ellipse(8, 8, 4.5, 3.3, hexc("b8743a"), shade=hexc("8a4f22"), light=hexc("e0a060"))
        c.rect(12, 6, 13, 6, C["cap_w"]); c.rect(12, 10, 13, 10, C["cap_w"])   # drumstick bones
    elif kind == "wine":
        c.ellipse(5, 8, 2.6, 2.6, C["gold"], shade=C["gold_d"])           # goblet
        c.ellipse(5, 8, 1.5, 1.5, C["blood"])
        c.ellipse(11, 7, 2.6, 2.6, C["gold"], shade=C["gold_d"])
        c.ellipse(11, 7, 1.5, 1.5, C["blood"])
        c.rect(8, 10, 9, 12, C["potion_r"]); c.set(8, 10, C["white"])    # little bottle
    elif kind == "fruit":
        c.ellipse(8, 8, 5.5, 4.5, C["gold_d"], shade=C["wood_d"])        # bowl
        for (x, y, col) in ((6, 7, C["red"]), (9, 6, hexc("5fa83a")), (8, 9, hexc("f08a24")), (10, 9, C["robe_l"]), (5, 9, C["red"])):
            c.rect(x, y, x + 1, y + 1, col)
    elif kind == "pie":
        c.ellipse(8, 8, 5, 5, hexc("c9a36b"), shade=hexc("9b7a48"), light=hexc("e8c890"))
        for (x, y) in ((6, 6), (10, 6), (8, 9), (6, 10), (10, 10)):
            c.set(x, y, C["blood_l"])                                 # berries poking through
        c.line(5, 8, 11, 8, hexc("9b7a48"))
    c.outline()
    return c


def chair(broken=False):
    """Dining chair from above (16x16), back along the top edge. Rotated in the level to face the table."""
    c = Canvas(16, 16)
    c.rect(3, 5, 12, 13, C["wood"])
    c.line(3, 5, 12, 5, C["wood_l"]); c.line(3, 13, 12, 13, C["wood_d"])
    c.rect(4, 7, 11, 12, C["plume"])                                  # red cushion
    c.rect(2, 2, 13, 4, C["wood_d"]); c.line(2, 2, 13, 2, C["wood"])  # chair back
    c.outline()
    return c


def throne():
    """The King's throne from above (16x16): tall gold-trimmed back, red velvet seat. Faces right."""
    c = Canvas(16, 16)
    c.rect(1, 1, 4, 14, C["gold"]); c.line(1, 1, 1, 14, C["gold_d"])  # tall back (left side)
    c.set(2, 1, C["red"]); c.set(2, 14, C["red"]); c.set(3, 7, C["royal"])
    c.rect(5, 3, 13, 12, C["plume"]); c.rect(6, 4, 12, 11, C["flannel_l"])   # seat
    c.rect(5, 1, 13, 2, C["gold_d"]); c.rect(5, 13, 13, 14, C["gold_d"])     # arms
    c.outline()
    return c


def great_door(broken=False):
    """The double door into the feast hall (16x48, vertical): oak planks with iron bands and a ring handle."""
    c = Canvas(16, 48)
    if broken:
        # just the hinges and a few jagged splinters left in the frame
        for y in (3, 4, 22, 23, 43, 44):
            c.rect(1, y, 3, y, C["metal_d"])
        for (y0, y1) in ((0, 7), (40, 47)):
            for y in range(y0, y1 + 1):
                c.rect(4, y, 5 + (y % 3), y, C["wood_d"])
        c.outline()
        return c
    c.rect(2, 0, 13, 47, C["wood"])
    for x in (5, 9):
        c.line(x, 0, x, 47, C["wood_d"])                              # plank seams
    c.line(2, 0, 2, 47, C["wood_l"])
    for y in (4, 23, 43):
        c.rect(2, y, 13, y + 1, C["metal_d"]); c.line(2, y, 13, y, C["metal"])   # iron bands
    c.line(2, 24, 13, 24, C["outline"])                               # gap between the two doors
    c.ellipse(11, 20, 1.6, 1.6, C["gold"]); c.ellipse(11, 28, 1.6, 1.6, C["gold"])  # ring handles
    c.outline()
    return c


def plank():
    """A broken plank from the great door (8x3)."""
    c = Canvas(10, 5)
    c.rect(1, 1, 8, 3, C["wood"]); c.line(1, 1, 8, 1, C["wood_l"]); c.set(8, 3, C["wood_d"])
    c.outline()
    return c


def drumstick():
    c = Canvas(6, 4)
    c.rect(1, 1, 3, 2, hexc("b8743a")); c.set(4, 1, C["cap_w"]); c.set(4, 2, C["cap_w"])
    c.outline()
    return c



# ---------------------------------------------------------------- more feast breakables
def candelabra(broken=False):
    """Gold candle stand from above: round base, three lit candles. Broken: knocked over, wax spilled."""
    c = Canvas(16, 16)
    if broken:
        c.line(3, 12, 12, 4, C["gold_d"]); c.line(3, 13, 12, 5, C["gold"])
        for (x, y) in ((11, 3), (13, 6), (9, 2)):
            c.rect(x, y, x + 1, y, C["white"])
        c.outline()
        return c
    c.ellipse(8, 8, 4.5, 4.5, C["gold"], shade=C["gold_d"], light=C["yellow"])
    for (x, y) in ((4, 8), (12, 8), (8, 4)):
        c.rect(x - 1, y - 1, x, y, C["white"])                       # candle
        c.set(x, y - 2, C["flash_o"]); c.set(x - 1, y - 2, C["flash"])  # flame
    c.outline()
    return c


def wine_cask():
    """Big wine barrel lying on its side (16x16): staves, iron hoops, red-stained end with a tap."""
    c = Canvas(16, 16)
    c.rect(1, 3, 13, 12, C["wood"])
    for y in (5, 8, 10):
        c.line(1, y, 13, y, C["wood_d"])
    for x in (3, 11):
        c.line(x, 3, x, 12, C["metal_d"])
    c.rect(13, 3, 14, 12, C["blood"]); c.rect(14, 4, 14, 11, C["blood_l"])
    c.rect(15, 7, 15, 8, C["gold"])                                  # tap
    c.outline()
    return c


def cake():
    """Three-tier celebration cake from above (16x16): pink icing rings and cherries."""
    c = Canvas(16, 16)
    c.ellipse(8, 8, 7, 7, hexc("f2b8d0"), shade=hexc("d890b0"))
    c.ellipse(8, 8, 5, 5, C["white"], shade=hexc("e8d8e0"))
    c.ellipse(8, 8, 3, 3, hexc("f2b8d0"), light=C["white"])
    for (x, y) in ((8, 2), (13, 8), (8, 13), (3, 8), (8, 8)):
        c.set(x, y, C["red"])
    c.outline()
    return c


def armor_stand():
    """Empty suit of armour on a stand, seen from above (16x16): plumed helmet, pauldrons, crossed halberd."""
    c = Canvas(16, 16)
    c.line(2, 2, 13, 13, C["wood_d"]); c.rect(12, 12, 13, 13, C["metal_l"])   # halberd shaft and blade
    c.ellipse(8, 8, 5, 4, C["metal"], shade=C["metal_d"], light=C["metal_l"])  # shoulders
    c.ellipse(8, 8, 2.6, 2.6, C["metal_l"], shade=C["metal"])                 # helmet
    c.set(8, 8, C["outline"]); c.set(9, 8, C["outline"])                        # visor slit
    c.rect(7, 4, 9, 5, C["plume"])                                             # plume
    c.outline()
    return c


def treasure_chest():
    """Open treasure chest (16x16) heaped with gold coins."""
    c = Canvas(16, 16)
    c.rect(1, 5, 14, 14, C["wood"]); c.line(1, 5, 14, 5, C["wood_l"]); c.line(1, 14, 14, 14, C["wood_d"])
    c.rect(1, 2, 14, 4, C["wood_d"])                                           # open lid
    for x in (1, 14):
        c.line(x, 2, x, 14, C["gold_d"])
    c.rect(3, 6, 12, 12, C["gold"])
    rnd = random.Random(4)
    for _ in range(10):
        c.set(rnd.randrange(3, 13), rnd.randrange(6, 13), rnd.choice([C["yellow"], C["gold_d"], C["white"]]))
    c.outline()
    return c


def ice_sculpture():
    """Ice sculpture of the King (16x16): pale blue, crowned, seen from above, on a little plinth."""
    c = Canvas(16, 16)
    c.rect(2, 2, 13, 13, hexc("8fb8d0"))                                       # plinth
    c.ellipse(8, 8, 5, 5, hexc("cfefff"), shade=hexc("9fd8f0"), light=C["white"])
    for (x, y) in ((5, 5), (8, 4), (11, 5), (5, 11), (8, 12), (11, 11)):
        c.set(x, y, hexc("6fb8e0"))                                            # crown points
    c.set(7, 7, C["white"])
    c.outline()
    return c


def coin():
    c = Canvas(4, 4)
    c.rect(1, 1, 2, 2, C["gold"]); c.set(1, 1, C["yellow"])
    c.outline()
    return c


def helmet_piece():
    c = Canvas(7, 7)
    c.ellipse(3.5, 3.5, 2.6, 2.6, C["metal_l"], shade=C["metal"])
    c.set(3, 3, C["outline"]); c.set(4, 3, C["outline"])
    c.outline()
    return c


def ice_shard():
    c = Canvas(5, 4)
    c.line(1, 2, 3, 1, hexc("cfefff")); c.set(2, 2, hexc("9fd8f0"))
    c.outline(hexc("4f8fb8"))
    return c


def candle_piece():
    c = Canvas(5, 3)
    c.line(1, 1, 3, 1, C["white"])
    c.outline()
    return c


# ---------------------------------------------------------------- write every sprite
def main():
    out(cletus("shotgun"), "Characters", "cletus_shotgun")
    out(cletus("staff"), "Characters", "cletus_staff")
    out(cletus_dead(), "Characters", "cletus_dead")
    out(knight(), "Characters", "knight")
    out(knight_dead(), "Characters", "knight_dead")
    out(wizard(), "Characters", "wizard")
    out(king(), "Characters", "king")
    out(king_dead(), "Characters", "king_dead")
    out(wizard_dead(), "Characters", "wizard_dead")

    out(floor_stone(1), "Tiles", "floor_stone_a")
    out(floor_stone(2), "Tiles", "floor_stone_b")
    out(wall_stone(), "Tiles", "wall_stone")
    out(grass(1), "Tiles", "grass_a")
    out(grass(2), "Tiles", "grass_b")
    out(dirt(1), "Tiles", "dirt")
    out(wood_floor(), "Tiles", "wood_floor")
    out(carpet(), "Tiles", "carpet_royal")
    out(water(1), "Tiles", "water")

    out(barrel(), "Props", "barrel")
    out(barrel(True), "Props", "barrel_broken")
    out(crate(), "Props", "crate")
    out(potion_shelf(), "Props", "potion_shelf")
    out(potion_shelf(True), "Props", "potion_shelf_broken")
    out(moonshine_jug(), "Props", "moonshine_jug")
    out(pillar(), "Props", "pillar")
    for colourway in STALL_COLOURS:
        out(market_stall(colourway), "Props", f"market_stall_{colourway}")
    out(banner(), "Props", "banner")
    out(gatling_staff_pickup(), "Props", "gatling_staff_pickup")
    out(bookshelf(), "Props", "bookshelf")
    out(lectern(), "Props", "lectern")
    out(feast_table(), "Props", "feast_table")
    for kind in ("turkey", "wine", "fruit", "pie"):
        out(food(kind), "Props", f"food_{kind}")
        out(food(kind, True), "Props", f"food_{kind}_broken")
    out(chair(), "Props", "chair")
    out(candelabra(), "Props", "candelabra")
    out(candelabra(True), "Props", "candelabra_broken")
    out(wine_cask(), "Props", "wine_cask")
    out(cake(), "Props", "cake")
    out(armor_stand(), "Props", "armor_stand")
    out(treasure_chest(), "Props", "treasure_chest")
    out(ice_sculpture(), "Props", "ice_sculpture")
    out(throne(), "Props", "throne")
    out(great_door(), "Props", "great_door")
    out(great_door(True), "Props", "great_door_broken")
    out(fruit_stand(), "Props", "fruit_stand")
    out(fruit_stand(True), "Props", "fruit_stand_broken")
    out(sword_rack(), "Props", "sword_rack")
    out(sword_rack(True), "Props", "sword_rack_broken")
    out(potion_stand(), "Props", "potion_stand")
    out(potion_stand(True), "Props", "potion_stand_broken")
    out(portal(), "Props", "portal")

    out(pellet(), "Effects", "pellet")
    out(staff_bolt("cyan", "cyan_d"), "Effects", "staff_bolt")          # ice
    out(staff_bolt("arcane", "arcane_d"), "Effects", "staff_bolt_arcane")
    out(staff_bolt("star", "star_d"), "Effects", "staff_bolt_star")
    out(magic_bolt(), "Effects", "magic_bolt")
    out(muzzle_flash(), "Effects", "muzzle_flash")
    out(slash(), "Effects", "slash")
    for i in range(3):
        out(blood(i + 1), "Effects", f"blood_{i + 1}")
    out(potion_puddle(), "Effects", "potion_puddle")
    out(fruit_mush(), "Effects", "fruit_mush")
    for kind in ("apple", "orange", "melon"):
        out(fruit_piece(kind), "Effects", f"fruit_{kind}")
    out(loose_sword(), "Effects", "loose_sword")
    out(smoke_puff(), "Effects", "smoke_puff")
    out(page(), "Effects", "page")
    out(drumstick(), "Effects", "drumstick")
    out(plank(), "Effects", "plank")
    out(coin(), "Effects", "coin")
    out(helmet_piece(), "Effects", "helmet_piece")
    out(ice_shard(), "Effects", "ice_shard")
    out(candle_piece(), "Effects", "candle_piece")
    for i in range(len(GLYPHS)):
        out(glyph(i), "Effects", f"glyph_{i}")
    out(splinters(), "Effects", "splinters")
    out(shell(), "Effects", "shell")
    out(sparks(C["flash"], C["flash_o"]), "Effects", "sparks")
    out(sparks(C["glow"], C["cyan"]), "Effects", "magic_sparks")

    out(title_skeeter(), "UI", "title_skeeter")
    out(castle_background(), "UI", "menu_background")
    out(ending_field(), "UI", "ending_field")
    out(wood_button(), "UI", "button_wood")
    out(stone_button(), "UI", "button_stone")
    out(menu_arrow(), "UI", "menu_arrow")
    for style in ("plain", "worn", "face", "dented", "green", "empty"):
        out(ammo_shell(style), "UI", f"ammo_shell_{style}")
    for kind in ("cyan", "arcane", "star", "empty"):
        out(ammo_bolt(kind), "UI", f"ammo_bolt_{kind}")
    out(cigarette_hud(), "UI", "cigarette")
    out(ember(), "UI", "cigarette_ember")
    out(cig_mouth(), "Effects", "cig_mouth")
    vig = vignette()
    vig.save(os.path.join(ROOT, "UI", "vignette.png"))
    out(icon("!"), "UI", "icon_alert")
    out(icon("?"), "UI", "icon_curious")
    out(ui_pixel(), "UI", "ui_pixel")

    # Preview sheet (not used by the game): every sprite at 6x on a dark background.
    scale, cell = 6, 34 * 6
    cols = 8
    rows = math.ceil(len(SHEET) / cols)
    sheet = Image.new("RGBA", (cols * cell, rows * cell), (40, 36, 48, 255))
    for i, (_, img) in enumerate(SHEET):
        big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
        x = (i % cols) * cell + (cell - big.width) // 2
        y = (i // cols) * cell + (cell - big.height) // 2
        sheet.alpha_composite(big, (x, y))
    sheet.save(os.path.join(ROOT, "..", "..", "Tools", "sprite_preview.png"))
    print(f"wrote {len(SHEET)} sprites")


if __name__ == "__main__":
    main()
