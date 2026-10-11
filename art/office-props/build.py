"""
office-props - build script.  Run:  cad build build.py
Twenty-two low-poly props that fill the nine rooms of the BREATHE.EXE office (units mm, +Z up, FRONT faces -Y).
Each prop is one named object (touching shells joined into one mesh). Unity recolours and re-textures props per mode (safe / horror), and assigns
materials by the slot NAME suffix: _main, _dark, _metal, _glow, _accent. Origins: floor level at the footprint centre; wall props: centre of the back face;
DoorSlab: the hinge edge at floor level. The props are laid out in a grid for review (origin offsets `O`), and exported per part with the origin restored.
"""
import math
import os
import sys

sys.path.insert(0, os.environ.get("CADLIB_DIR", "/home/anon/.claude/skills/blender-cad/scripts"))
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import cadlib as cad  # noqa: E402

cad.new_scene(units="mm")
cad.collection("Office props")

# ---------------------------------------------------------------- layout for review
NAMES = ["OfficeChair", "ConferenceChair", "FilingCabinet", "ServerRack", "VendingMachine", "WaterCooler", "Copier", "Fridge", "Sofa", "PottedPlant", "MonitorLCD",
         "KeyboardMouse", "DeskLamp", "WallClock", "TrashBin", "BoxStack", "ShelfUnit", "Microwave", "CoffeeMaker", "Whiteboard", "DoorSlab", "DoorFrame"]
O = {}
for i, n in enumerate(NAMES):
    O[n] = (-3000 + (i % 8) * 1900, -(i // 8) * 2400, 0)


def at(prop, x=0.0, y=0.0, z=0.0):
    o = O[prop]
    return (o[0] + x, o[1] + y, o[2] + z)


# ---------------------------------------------------------------- materials (the colour here is only for review; Unity re-skins by slot name)
PAL = {}


def palette(prop, main, dark="#23262e", metal="#b9bec8", glow="#8fe3d0", accent="#4a7bd0", main_preset="plastic_matte"):
    PAL[prop] = dict(
        main=cad.mat(main_preset, name=prop + "_main", color=main, roughness=0.65),
        dark=cad.mat("plastic_matte", name=prop + "_dark", color=dark, roughness=0.6),
        metal=cad.mat("steel_brushed", name=prop + "_metal", color=metal, roughness=0.4),
        glow=cad.mat("luminous", name=prop + "_glow", color=glow),
        accent=cad.mat("plastic_matte", name=prop + "_accent", color=accent, roughness=0.6),
        glass=cad.mat("glass", name=prop + "_glass", color="#cfeffa", alpha=0.18),
    )


def B(prop, slot, size, loc, bevel=None, rot=(0, 0, 0), name="s"):
    return cad.box(name, size, loc=at(prop, *loc), rot=rot, bevel=bevel, mat=PAL[prop][slot])


def Cy(prop, slot, r, h, loc, axis="Z", seg=16, rot=(0, 0, 0), bevel=None, name="c"):
    return cad.cylinder(name, r, h, segments=seg, axis=axis, loc=at(prop, *loc), rot=rot, bevel=bevel, mat=PAL[prop][slot])


def Co(prop, slot, r1, r2, h, loc, axis="Z", seg=16, rot=(0, 0, 0), name="k"):
    return cad.cone(name, r1, r2, h, segments=seg, axis=axis, loc=at(prop, *loc), rot=rot, mat=PAL[prop][slot])


def set_origin(obj, point):
    bpy.context.view_layer.update()
    delta = obj.location - Vector(point)
    obj.data.transform(Matrix.Translation(delta))
    obj.location = Vector(point)
    bpy.context.view_layer.update()


def finish(prop, shells):
    obj = cad.join(shells, prop)
    set_origin(obj, O[prop])
    return obj


def squash(obj, sx, sy, sz):
    obj.data.transform(Matrix.Diagonal(Vector((sx, sy, sz, 1))))


# ---------------------------------------------------------------- 1. office chair
def office_chair():
    p = "OfficeChair"; palette(p, "#3f5f8f")
    s = []
    s.append(B(p, "main", (500, 480, 90), (0, 0, 470), bevel=22))
    s.append(B(p, "main", (470, 80, 520), (0, 235, 800), bevel=24, rot=(-7, 0, 0)))
    s.append(B(p, "dark", (360, 30, 130), (0, 276, 770), bevel=8, rot=(-7, 0, 0)))
    s.append(B(p, "dark", (70, 50, 330), (0, 215, 560), bevel=6))
    s.append(B(p, "dark", (340, 340, 40), (0, 0, 415), bevel=6))
    for sx in (-1, 1):
        s.append(B(p, "dark", (36, 36, 210), (sx * 265, 20, 585), bevel=5))
        s.append(B(p, "dark", (64, 300, 36), (sx * 265, 40, 700), bevel=10))
        s.append(B(p, "dark", (150, 30, 30), (sx * 190, 20, 480), bevel=5))
    s.append(Cy(p, "metal", 26, 230, (0, 0, 300), seg=16))
    s.append(Cy(p, "dark", 44, 110, (0, 0, 370), seg=16))
    s.append(Cy(p, "dark", 56, 56, (0, 0, 122), seg=16))
    leg = B(p, "dark", (66, 320, 34), (0, 160, 100), bevel=8)
    cad.polar_array(leg, 5, center=at(p, 0, 0, 0)); s.append(leg)
    fork = B(p, "dark", (22, 22, 54), (0, 310, 78)); cad.polar_array(fork, 5, center=at(p)); s.append(fork)
    wheel = cad.sphere("w", 26, segments=12, rings=8, loc=at(p, 0, 310, 28), mat=PAL[p]["metal"]); cad.polar_array(wheel, 5, center=at(p)); s.append(wheel)
    return finish(p, s)


# ---------------------------------------------------------------- 2. conference chair
def conference_chair():
    p = "ConferenceChair"; palette(p, "#6e7686")
    s = [B(p, "main", (480, 460, 60), (0, 0, 450), bevel=14), B(p, "main", (450, 50, 400), (0, 235, 700), bevel=16, rot=(-5, 0, 0)),
         B(p, "dark", (420, 20, 120), (0, 262, 760), bevel=5, rot=(-5, 0, 0))]
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(B(p, "metal", (30, 30, 430), (sx * 205, sy * 190, 215 + (0 if sy < 0 else 0)), bevel=5))
            s.append(B(p, "dark", (44, 44, 16), (sx * 205, sy * 190, 8), bevel=3))
        s.append(B(p, "metal", (24, 420, 24), (sx * 205, 0, 170), bevel=4))
    s.append(B(p, "metal", (410, 24, 24), (0, -190, 170), bevel=4)); s.append(B(p, "metal", (410, 24, 24), (0, 190, 170), bevel=4))
    s.append(B(p, "metal", (30, 30, 300), (-205, 235, 640), bevel=5)); s.append(B(p, "metal", (30, 30, 300), (205, 235, 640), bevel=5))
    return finish(p, s)


# ---------------------------------------------------------------- 3. filing cabinet
def filing_cabinet():
    p = "FilingCabinet"; palette(p, "#8b929b", main_preset="plastic_matte")
    s = [B(p, "main", (460, 620, 1290), (0, 0, 685), bevel=6), B(p, "dark", (430, 8, 1250), (0, -312, 685))]
    for i in range(4):
        z = 160 + i * 310
        s.append(B(p, "main", (420, 24, 282), (0, -318, z), bevel=7))
        s.append(B(p, "metal", (210, 30, 26), (0, -340, z + 60), bevel=6))
        s.append(B(p, "metal", (130, 8, 52), (0, -332, z - 52), bevel=3))
        s.append(B(p, "dark", (112, 4, 36), (0, -337, z - 52)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(B(p, "dark", (50, 50, 40), (sx * 190, sy * 260, 20), bevel=4))
    return finish(p, s)


# ---------------------------------------------------------------- 4. server rack
def server_rack():
    p = "ServerRack"; palette(p, "#202329", dark="#15171c", metal="#9ca3ae", glow="#39ff7e", accent="#e8c14a")
    s = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(B(p, "dark", (44, 44, 1960), (sx * 270, sy * 450, 1000), bevel=5))
    s.append(B(p, "dark", (600, 1000, 40), (0, 0, 20), bevel=6)); s.append(B(p, "dark", (600, 1000, 40), (0, 0, 1980), bevel=6))
    for u in range(10):
        z = 150 + u * 175
        s.append(B(p, "dark", (500, 880, 150), (0, 0, z), bevel=4))
        s.append(B(p, "metal", (520, 22, 150), (0, -462, z), bevel=4))
        for k in range(5):
            s.append(B(p, "dark", (12, 6, 100), (-210 + k * 24, -475, z)))
        if u % 3 == 0:
            for bay in range(4):
                s.append(B(p, "dark", (90, 6, 110), (-150 + bay * 100, -475, z)))
        else:
            for k in range(2):
                s.append(B(p, "dark", (180, 6, 14), (120, -475, z - 30 + k * 40)))
        for l in range(3):
            s.append(B(p, "glow", (12, 6, 12), (-235 - l * 0, -475, z - 40 + l * 34)) if False else B(p, "glow", (14, 6, 14), (230, -475, z - 40 + l * 34)))
    for sx in (-1, 1):
        s.append(Cy(p, "accent", 22, 820, (sx * 120, 0, 2012), axis="Y", seg=10))
    s.append(Cy(p, "accent", 14, 300, (-150, -460, 1900), seg=8)); s.append(Cy(p, "accent", 14, 260, (140, -460, 1880), seg=8))
    return finish(p, s)


# ---------------------------------------------------------------- 5. vending machine
def vending_machine():
    p = "VendingMachine"; palette(p, "#b02a30", dark="#16181d", glow="#7be4ff", accent="#e8b030")
    body = B(p, "main", (900, 800, 1850), (0, 0, 925), bevel=10)
    cad.subtract(body, B(p, "main", (580, 400, 1210), (-130, -210, 1100)))
    cad.subtract(body, B(p, "main", (540, 80, 190), (-130, -395, 180)))
    s = [body]
    s.append(B(p, "dark", (590, 6, 1220), (-130, -4, 1100)))
    s.append(B(p, "glass", (578, 5, 1208), (-130, -392, 1100)))
    for r in range(6):
        z = 560 + r * 200
        s.append(B(p, "metal", (580, 340, 14), (-130, -190, z), bevel=2))
        for c in range(7):
            x = -130 - 240 + c * 80
            if r % 2 == 0:
                s.append(Cy(p, "accent", 31, 110, (x, -230, z + 62), seg=10))
                s.append(Cy(p, "main", 32, 20, (x, -230, z + 126), seg=10))
            else:
                s.append(B(p, "accent", (66, 30, 100), (x, -250, z + 58), bevel=4)); s.append(B(p, "dark", (50, 4, 24), (x, -268, z + 70)))
    s.append(B(p, "dark", (190, 24, 1250), (310, -388, 1060), bevel=6))
    s.append(B(p, "glow", (140, 6, 70), (310, -402, 1560)))
    for r in range(3):
        for c in range(3):
            s.append(B(p, "metal", (34, 12, 34), (255 + c * 55, -402, 1400 - r * 55), bevel=4))
    s.append(B(p, "dark", (100, 6, 18), (310, -402, 1180))); s.append(B(p, "dark", (44, 8, 70), (310, -402, 1110), bevel=3))
    s.append(B(p, "dark", (540, 14, 160), (-130, -380, 180), bevel=6)); s.append(B(p, "metal", (200, 10, 20), (-130, -390, 230)))
    s.append(B(p, "glow", (560, 6, 100), (-130, -402, 1790)))
    return finish(p, s)


# ---------------------------------------------------------------- 6. water cooler
def water_cooler():
    p = "WaterCooler"; palette(p, "#d9dde3", glow="#9fe0ff", accent="#4aa8ff")
    s = [B(p, "main", (340, 340, 820), (0, 0, 410), bevel=10), B(p, "dark", (340, 340, 60), (0, 0, 850), bevel=8), B(p, "dark", (240, 12, 200), (0, -172, 700), bevel=4)]
    s.append(B(p, "metal", (210, 100, 12), (0, -185, 605), bevel=3))
    for sx, slot in ((-1, "accent"), (1, "dark")):
        s.append(Cy(p, "metal", 16, 70, (sx * 60, -190, 750), axis="Y", seg=10)); s.append(B(p, slot, (26, 26, 26), (sx * 60, -222, 750), bevel=4))
    s.append(Cy(p, "metal", 38, 140, (-110, -60, 770), seg=12)); s.append(B(p, "metal", (30, 100, 14), (-110, -118, 700), bevel=2))
    s.append(Co(p, "dark", 62, 110, 70, (0, 0, 915), seg=16)); s.append(Cy(p, "accent", 140, 300, (0, 0, 1090), seg=20)); s.append(Co(p, "accent", 140, 100, 70, (0, 0, 1275), seg=20))
    s.append(Cy(p, "metal", 100, 12, (0, 0, 884), seg=16))
    return finish(p, s)


# ---------------------------------------------------------------- 7. copier
def copier():
    p = "Copier"; palette(p, "#cfd3d8", glow="#7bff9a")
    s = [B(p, "main", (760, 660, 840), (0, 0, 480), bevel=10), B(p, "dark", (740, 640, 90), (0, 0, 945), bevel=10), B(p, "metal", (560, 260, 24), (0, -60, 1002), bevel=3)]
    s.append(B(p, "dark", (560, 30, 110), (0, 100, 1020), bevel=4))
    for i in range(3):
        z = 150 + i * 250
        s.append(B(p, "dark", (700, 14, 220), (0, -335, z), bevel=5)); s.append(B(p, "main", (680, 12, 204), (0, -341, z), bevel=5)); s.append(B(p, "dark", (220, 14, 30), (0, -350, z + 62), bevel=4))
    s.append(B(p, "dark", (320, 160, 40), (190, -280, 995), bevel=8, rot=(-22, 0, 0)))
    s.append(B(p, "glow", (170, 8, 80), (190, -283, 1008), rot=(-22, 0, 0)))
    for r in range(2):
        for c in range(4):
            s.append(B(p, "metal", (28, 20, 14), (110 + c * 40, -318 + r * -18, 972 + r * -6), bevel=3, rot=(-22, 0, 0)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(Cy(p, "dark", 28, 70, (sx * 330, sy * 280, 36), seg=10))
    return finish(p, s)


# ---------------------------------------------------------------- 8. fridge
def fridge():
    p = "Fridge"; palette(p, "#e6e8e4", accent="#d94a4a")
    s = [B(p, "main", (700, 700, 1790), (0, 0, 955), bevel=16), B(p, "dark", (690, 10, 1750), (0, -350, 955)),
         B(p, "main", (684, 24, 1150), (0, -356, 1330), bevel=12), B(p, "main", (684, 24, 580), (0, -356, 620), bevel=12),
         B(p, "metal", (26, 34, 520), (275, -388, 1520), bevel=6), B(p, "metal", (26, 34, 320), (275, -388, 720), bevel=6)]
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(B(p, "dark", (60, 60, 60), (sx * 290, sy * 290, 30), bevel=6))
    for i in range(5):
        s.append(B(p, "accent", (46 - i * 4, 8, 46 - i * 4), (-130 + i * 60, -372, 1560 - (i % 2) * 120), bevel=2))
    return finish(p, s)


# ---------------------------------------------------------------- 9. sofa
def sofa():
    p = "Sofa"; palette(p, "#a8333f", main_preset="fabric")
    s = [B(p, "main", (1900, 800, 300), (0, 0, 230), bevel=36), B(p, "main", (1900, 220, 540), (0, 290, 620), bevel=44)]
    for sx in (-1, 1):
        s.append(B(p, "main", (210, 800, 540), (sx * 845, 0, 520), bevel=44))
        s.append(B(p, "main", (790, 600, 150), (sx * 395, -60, 400), bevel=38)); s.append(B(p, "main", (780, 160, 400), (sx * 395, 190, 600), bevel=36, rot=(-9, 0, 0)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(Cy(p, "dark", 36, 80, (sx * 800, sy * 320, 40), seg=10))
    return finish(p, s)


# ---------------------------------------------------------------- 10. potted plant
def potted_plant():
    p = "PottedPlant"; palette(p, "#c9c1b0", dark="#3a2b22", accent="#3f9a4e")
    pot = cad.revolve("pot", cad.fillet([(0, 0), (130, 0), (190, 330), (205, 330), (205, 370), (0, 370)], [0, 6, 3, 3, 3, 0], segments=4), segments=20, loc=at(p), mat=PAL[p]["main"])
    s = [pot, Cy(p, "dark", 196, 20, (0, 0, 372), seg=18), Cy(p, "dark", 22, 330, (0, 0, 540), seg=8)]
    n = 12
    for i in range(n):
        yaw = i * 360 / n + (i % 2) * 14; tilt = 28 + (i % 3) * 16; length = 620 - (i % 3) * 110; a = math.radians(tilt); y = math.radians(yaw)
        d = Vector((math.sin(a) * math.sin(y), -math.sin(a) * math.cos(y), math.cos(a)))
        base = Vector(at(p, 0, 0, 470 + (i % 4) * 25))
        leaf = cad.cone("leaf", 62, 4, length, segments=6, loc=tuple(base + d * length / 2), mat=PAL[p]["accent"])
        squash(leaf, 1, .3, 1)
        leaf.rotation_euler = (a, 0, y); bpy.context.view_layer.update(); s.append(leaf)
    return finish(p, s)


# ---------------------------------------------------------------- 11. monitor + keyboard
def monitor_lcd():
    p = "MonitorLCD"; palette(p, "#22252c", glow="#8fe3d0")
    s = [B(p, "dark", (250, 190, 18), (0, 0, 9), bevel=5), B(p, "metal", (70, 40, 180), (0, 20, 100), bevel=5), B(p, "dark", (560, 30, 340), (0, 0, 290), bevel=9),
         B(p, "glow", (520, 4, 300), (0, -17, 290)), B(p, "dark", (200, 20, 40), (0, 30, 130))]
    s.append(B(p, "dark", (20, 4, 8), (240, -16, 128), bevel=1))
    return finish(p, s)


def keyboard_mouse():
    p = "KeyboardMouse"; palette(p, "#b9b7a8")
    s = [B(p, "dark", (420, 150, 18), (0, 0, 9), bevel=5)]
    for r in range(5):
        for c in range(14 if r < 4 else 1):
            if r == 4: s.append(B(p, "main", (190, 22, 8), (0, -50 + r * 25 - 62 + 12, 21), bevel=2)); continue
            s.append(B(p, "main", (24, 20, 8), (-170 + c * 26, -58 + r * 25, 22), bevel=2))
    s.append(B(p, "dark", (64, 104, 28), (270, 0, 14), bevel=14)); s.append(B(p, "main", (62, 4, 6), (270, -30, 29)))
    return finish(p, s)


# ---------------------------------------------------------------- 12. lamp, clock, bin, boxes
def desk_lamp():
    p = "DeskLamp"; palette(p, "#e8c14a", glow="#fff2b0")
    s = [Cy(p, "dark", 70, 22, (0, 0, 11), seg=16), Cy(p, "dark", 20, 40, (0, 0, 40), seg=10)]
    s.append(B(p, "metal", (14, 14, 236), (0, 15, 155), bevel=3, rot=(-7.4, 0, 0)))
    s.append(cad.sphere("j", 16, segments=10, rings=8, loc=at(p, 0, 30, 270), mat=PAL[p]["dark"]))
    s.append(B(p, "metal", (14, 14, 182), (0, -40, 325), bevel=3, rot=(51.8, 0, 0)))
    s.append(Co(p, "main", 32, 82, 110, (0, -137, 332), seg=16, rot=(150, 0, 0)))
    s.append(cad.sphere("b", 20, segments=10, rings=8, loc=at(p, 0, -150, 305), mat=PAL[p]["glow"]))
    return finish(p, s)


def wall_clock():
    p = "WallClock"; palette(p, "#f4f4ee", glow="#ffffff")
    s = [Cy(p, "main", 195, 20, (0, -12, 0), axis="Y", seg=28), cad.torus("rim", 200, 14, segments=28, ring_segments=8, axis="Y", loc=at(p, 0, -20, 0), mat=PAL[p]["dark"])]
    for h in range(12):
        a = math.radians(h * 30); r = 165
        s.append(B(p, "dark", (14 if h % 3 else 22, 6, 40 if h % 3 else 56), (math.sin(a) * r, -26, math.cos(a) * r), rot=(0, -h * 30, 0)))
    s.append(B(p, "dark", (14, 6, 110), (0, -30, 50), rot=(0, 0, 0))); h2 = B(p, "dark", (10, 6, 150), (60, -33, 15), rot=(0, -70, 0)); s.append(h2)
    s.append(Cy(p, "dark", 14, 8, (0, -34, 0), axis="Y", seg=10))
    return finish(p, s)


def trash_bin():
    p = "TrashBin"; palette(p, "#2c2f38")
    s = [Co(p, "dark", 118, 150, 400, (0, 0, 200), seg=16), cad.torus("lip", 152, 9, segments=16, ring_segments=8, loc=at(p, 0, 0, 398), mat=PAL[p]["metal"]),
         Cy(p, "metal", 100, 8, (0, 0, 6), seg=16)]
    for k in range(6):
        a = math.radians(k * 60)
        s.append(B(p, "metal", (8, 4, 220), (math.sin(a) * 132, -math.cos(a) * 132 * 1.0, 200), rot=(0, 0, -k * 60)))
    return finish(p, s)


def box_stack():
    p = "BoxStack"; palette(p, "#b58a5a", dark="#2a2a2a", accent="#e0d6b0", main_preset="plastic_matte")
    s = []
    z = 0
    for i, (w, d, h, yaw) in enumerate(((520, 420, 220, 0), (450, 360, 200, 8), (380, 300, 190, -6))):
        zc = z + h / 2
        s.append(B(p, "main", (w, d, h), (0, 0, zc), bevel=4, rot=(0, 0, yaw)))
        s.append(B(p, "accent", (60, d + 4, 4), (0, 0, z + h + 2), rot=(0, 0, yaw)))
        s.append(B(p, "accent", (60, 4, h - 6), (0, -d / 2 - 2, zc), rot=(0, 0, yaw)))
        s.append(B(p, "dark", (150, 4, 90), (w * .22, -d / 2 - 4, zc + 10), rot=(0, 0, yaw)))
        z += h
    return finish(p, s)


# ---------------------------------------------------------------- 13. shelf unit
def shelf_unit():
    p = "ShelfUnit"; palette(p, "#8a919c", dark="#2c3038", metal="#a9b0ba")
    s = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            s.append(B(p, "metal", (40, 40, 1900), (sx * 480, sy * 190, 950), bevel=4))
    for i in range(5):
        z = 120 + i * 400
        s.append(B(p, "metal", (1000, 420, 22), (0, 0, z), bevel=3)); s.append(B(p, "metal", (1000, 18, 70), (0, -200, z + 30), bevel=3)); s.append(B(p, "dark", (1000, 10, 40), (0, 205, z + 20)))
    for k in range(2):
        s.append(B(p, "dark", (16, 8, 1620), (-240 + k * 480, 205, 1000), rot=(0, 18 if k == 0 else -18, 0)))
    return finish(p, s)


# ---------------------------------------------------------------- 14. microwave, coffee maker, whiteboard
def microwave():
    p = "Microwave"; palette(p, "#d9d9d2", glow="#7bff9a")
    s = [B(p, "main", (500, 380, 300), (0, 0, 150), bevel=8), B(p, "dark", (310, 10, 215), (-75, -190, 150), bevel=3), B(p, "dark", (320, 6, 225), (-75, -186, 150)),
         B(p, "metal", (14, 26, 215), (110, -198, 150), bevel=3), B(p, "dark", (100, 8, 250), (182, -190, 150), bevel=3), B(p, "glow", (70, 4, 34), (182, -196, 245))]
    for r in range(4):
        s.append(B(p, "metal", (30, 6, 20), (182, -196, 195 - r * 28), bevel=2))
    for sx in (-1, 1):
        s.append(B(p, "dark", (30, 30, 12), (sx * 200, 140, 6)))
    return finish(p, s)


def coffee_maker():
    p = "CoffeeMaker"; palette(p, "#24262c")
    s = [B(p, "dark", (260, 300, 40), (0, 0, 20), bevel=8), B(p, "dark", (110, 110, 360), (0, 100, 200), bevel=10), B(p, "dark", (260, 260, 70), (0, 40, 345), bevel=12),
         Cy(p, "metal", 90, 10, (0, -50, 45), seg=16), Cy(p, "main", 66, 150, (0, -50, 125), seg=16), Co(p, "dark", 60, 70, 20, (0, -50, 210), seg=16),
         B(p, "dark", (24, 90, 110), (88, -50, 125), bevel=6), B(p, "metal", (50, 40, 30), (0, -80, 305), bevel=5), cad.sphere("led", 8, segments=8, rings=6, loc=at(p, 90, -60, 300), mat=PAL[p]["glow"])]
    return finish(p, s)


def whiteboard():
    p = "Whiteboard"; palette(p, "#f4f5f2", accent="#d94a4a")
    s = [B(p, "main", (1440, 10, 940), (0, -14, 0), bevel=2)]
    for sz in (-1, 1):
        s.append(B(p, "metal", (1500, 40, 30), (0, -22, sz * 485), bevel=4)); s.append(B(p, "metal", (30, 40, 1000), (sz * 735, -22, 0), bevel=4))
    s.append(B(p, "metal", (520, 50, 20), (0, -50, -495), bevel=3))
    for i, color in enumerate(("accent", "dark", "metal")):
        s.append(Cy(p, color, 8, 130, (-80 + i * 70, -58, -475), axis="X", seg=8, rot=(0, 0, 0)))
    s.append(B(p, "dark", (80, 10, 40), (500, -26, 360), bevel=1)); s.append(B(p, "dark", (200, 6, 10), (-380, -22, 380)))
    return finish(p, s)


# ---------------------------------------------------------------- 15. door slab and frame
def door_slab():
    p = "DoorSlab"; palette(p, "#6a5240", dark="#2a2420", main_preset="plastic_matte")
    s = [B(p, "main", (920, 44, 2000), (460, 0, 1000), bevel=6)]
    for x in (230, 690):
        for z, h in ((1470, 700), (520, 760)):
            for side in (-1, 1):
                s.append(B(p, "main", (350, 14, h), (x, side * 28, z), bevel=7))
                s.append(B(p, "dark", (320, 4, h - 40), (x, side * 36, z)))
    s.append(B(p, "metal", (920, 8, 150), (460, -26, 75), bevel=2))
    for z in (1000,):
        s.append(Cy(p, "metal", 32, 16, (830, -30, z), axis="Y", seg=14)); s.append(B(p, "metal", (150, 16, 22), (760, -46, z + 0), bevel=5)); s.append(Cy(p, "metal", 32, 16, (830, 30, z), axis="Y", seg=14)); s.append(B(p, "metal", (150, 16, 22), (760, 46, z + 0), bevel=5))
    for z in (250, 1000, 1750):
        s.append(B(p, "metal", (22, 54, 110), (10, 0, z), bevel=3)); s.append(Cy(p, "metal", 9, 120, (6, 0, z), seg=8))
    return finish(p, s)


def door_frame():
    p = "DoorFrame"; palette(p, "#6a5240", metal="#b9bec8")
    s = [B(p, "main", (90, 120, 2100), (-505, 0, 1050), bevel=6), B(p, "main", (90, 120, 2100), (505, 0, 1050), bevel=6), B(p, "main", (1100, 120, 80), (0, 0, 2060), bevel=6),
         B(p, "dark", (920, 100, 12), (0, 0, 6)), B(p, "main", (10, 30, 2020), (-455, -45, 1030)), B(p, "main", (10, 30, 2020), (455, -45, 1030)), B(p, "main", (920, 30, 10), (0, -45, 2015))]
    s.append(B(p, "metal", (8, 40, 170), (459, -45, 1000), bevel=2)); s.append(B(p, "dark", (6, 10, 30), (459, -45, 1000)))
    for z in (250, 1000, 1750):
        s.append(B(p, "metal", (8, 40, 110), (-459, -40, z), bevel=2))
    return finish(p, s)


BUILDERS = [office_chair, conference_chair, filing_cabinet, server_rack, vending_machine, water_cooler, copier, fridge, sofa, potted_plant, monitor_lcd, keyboard_mouse,
            desk_lamp, wall_clock, trash_bin, box_stack, shelf_unit, microwave, coffee_maker, whiteboard, door_slab, door_frame]
OBJ = {}
for builder in BUILDERS:
    obj = builder()
    OBJ[obj.name] = obj

# ---------------------------------------------------------------- checks: size and origin per part (mm)
EXPECT = {
    "OfficeChair": (620, 620, 1000), "ConferenceChair": (480, 540, 900), "FilingCabinet": (460, 650, 1330), "ServerRack": (600, 1000, 2050), "VendingMachine": (900, 800, 1850),
    "WaterCooler": (340, 405, 1310), "Copier": (760, 690, 1075), "Fridge": (700, 700, 1850), "Sofa": (1900, 860, 840), "PottedPlant": (None, None, None), "MonitorLCD": (560, 220, 460),
    "KeyboardMouse": (None, None, None), "DeskLamp": (None, None, None), "WallClock": (420, None, 420), "TrashBin": (None, None, 420), "BoxStack": (None, None, 610),
    "ShelfUnit": (1000, 440, 1900), "Microwave": (500, 400, 300), "CoffeeMaker": (260, 300, 380), "Whiteboard": (1500, None, 1000), "DoorSlab": (920, None, 2000), "DoorFrame": (1100, 120, 2100),
}
for name, dims in EXPECT.items():
    cad.expect(OBJ[name], dims=dims, tol=60.0)
for name, obj in OBJ.items():
    lo, hi = cad.bbox(obj); o = O[name]
    if name in ("WallClock", "Whiteboard"):
        assert hi[1] - o[1] < 2 and abs((lo[0] + hi[0]) / 2 - o[0]) < 30, f"{name}: origin must be the back-face centre"
    elif name == "DoorSlab":
        assert abs(lo[0] - o[0]) < 20 and lo[2] - o[2] > -2 and lo[2] - o[2] < 2, "DoorSlab origin must be the hinge edge at floor level"
    else:
        assert lo[2] - o[2] > -2 and lo[2] - o[2] < 90, f"{name}: must stand on the floor (origin at floor level)"
        assert abs((lo[0] + hi[0]) / 2 - o[0]) < 200 and abs((lo[1] + hi[1]) / 2 - o[1]) < 200, f"{name}: origin must be near the footprint centre"
cad.render_hints(bg="dark", hdri="city", hdri_saturation=0.2, hdri_rotation=40)
cad.save("model")
