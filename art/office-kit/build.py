"""
office-kit - build script.  Run:  cad build build.py
Seven low-poly office props (16 parts) for the lobby and interview room. Units mm, +Z up, FRONT faces -Y.
Props are laid out in a row for review; every part's object origin is set to its design origin
(floor / front-face centre / board centre) so the exported FBX places without offsets.
"""
import math
import os
import sys

sys.path.insert(0, os.environ.get("CADLIB_DIR", "/home/anon/.claude/skills/blender-cad/scripts"))
import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402
import cadlib as cad  # noqa: E402

P = dict(
    # reception desk
    desk_w=5800, desk_d=900, desk_h=1000, top_w=6000, top_d=1300, top_t=80, top_dy=-150, slats=29, slat_pitch=200, slat_w=40, slat_depth=22,
    # CRT monitor (origin: centre of the front face)
    mon_w=1950, mon_h=1450, bezel_d=150, ap_w=1700, ap_h=1200,
    # boards
    board_w=1350, board_h=1550, frame_t=40, frame_d=80, leg=70, board_cz=1600,
    clip_w=1100, clip_h=1450, clip_t=30,
    # door
    door_w=1200, door_t=100, door_h=2200, case_w=100, case_d=150,
    # light
    fix_l=1800, fix_w=300, fix_h=70, tube_r=20, tube_l=1700,
    # interview desk
    iv_w=3600, iv_d=1100, iv_t=80, iv_h=700, ped_w=1000, ped_d=1000,
)
O = dict(desk=(0, 0, 0), monitor=(8000, 0, 0), board=(12000, 0, 0), clip=(15000, 0, 0), door=(18000, 0, 0), fixture=(21000, 0, 0), iv=(24000, 0, 0))

cad.new_scene(units="mm")

MAT = dict(
    wood=cad.mat("wood", name="Counter wood", color="#6e4a2e", roughness=0.7),
    laminate=cad.mat("plastic_matte", name="Dark laminate", color="#2e3140", roughness=0.5),
    beige=cad.mat("plastic_matte", name="Beige plastic", color="#dad3bc", roughness=0.6),
    dark=cad.mat("plastic_matte", name="Black plastic", color="#1d1f26", roughness=0.5),
    board=cad.mat("wood", name="Board wood", color="#8a6a3c", roughness=0.7),
    steel=cad.mat("steel_brushed", name="Steel", color="#b9bec8", roughness=0.4),
    chrome=cad.mat("chrome", name="Chrome", roughness=0.2),
    door=cad.mat("plastic_matte", name="Door paint", color="#2c2f3a", roughness=0.6),
    sign=cad.mat("luminous", name="Exit green", color="#2fdc7a"),
    housing=cad.mat("plastic_matte", name="Fixture housing", color="#c9c9c0", roughness=0.6),
    tube=cad.mat("luminous", name="Tube light", color="#fff6cf"),
    paint=cad.mat("plastic_matte", name="Frame paint", color="#7d5b34", roughness=0.7),
)


def L(prop, x=0.0, y=0.0, z=0.0):
    o = O[prop]
    return (o[0] + x, o[1] + y, o[2] + z)


def set_origin(obj, point):
    """Move the object origin to world `point` without moving the geometry."""
    cad._sync() if hasattr(cad, "_sync") else bpy.context.view_layer.update()
    delta = obj.location - Vector(point)
    obj.data.transform(Matrix.Translation(delta))
    obj.location = Vector(point)
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- 1. reception desk
def make_desk():
    p = P
    body = cad.box("DeskBody", (p["desk_w"], p["desk_d"], p["desk_h"]), loc=L("desk", 0, 0, p["desk_h"] / 2), bevel=6, mat=MAT["wood"])
    groove = cad.box("_groove", (p["slat_w"], 2 * p["slat_depth"], 820), loc=L("desk", -2800, -p["desk_d"] / 2, 560))
    cad.linear_array(groove, p["slats"], (p["slat_pitch"], 0, 0))
    cad.subtract(body, groove)
    kick = cad.box("_kick", (p["desk_w"] + 100, 160, 110), loc=L("desk", 0, -p["desk_d"] / 2, 55))
    cad.subtract(body, kick)
    top = cad.box("DeskTop", (p["top_w"], p["top_d"], p["top_t"]), loc=L("desk", 0, p["top_dy"], p["desk_h"] + p["top_t"] / 2), bevel=8, mat=MAT["laminate"])
    set_origin(body, L("desk")); set_origin(top, L("desk"))
    return body, top


# ---------------------------------------------------------------- 2. CRT monitor
def make_monitor():
    p = P
    bezel = cad.box("MonitorBezel", (p["mon_w"], p["bezel_d"], p["mon_h"]), loc=L("monitor", 0, p["bezel_d"] / 2, 0), bevel=20, mat=MAT["beige"])
    cad.subtract(bezel, cad.box("_ap", (p["ap_w"], 400, p["ap_h"]), loc=L("monitor", 0, p["bezel_d"] / 2, 0)))
    a = cad.box("_a", (1800, 250, 1330), loc=L("monitor", 0, 150 + 125, 0))
    b = cad.box("_b", (1500, 300, 1100), loc=L("monitor", 0, 400 + 150, 0))
    c = cad.box("_c", (1100, 250, 800), loc=L("monitor", 0, 700 + 125, 0))
    body = cad.union(a, b, c); body.name = "MonitorBody"; cad.assign(body, MAT["beige"])
    vent = cad.box("_vent", (60, 150, 50), loc=L("monitor", -480, 275, 665))
    cad.linear_array(vent, 9, (120, 0, 0)); cad.subtract(body, vent)
    neck = cad.box("MonitorNeck", (600, 450, 60), loc=L("monitor", 0, 450, -695), mat=MAT["dark"])
    plate = cad.cylinder("_plate", 300, 50, segments=16, loc=L("monitor", 0, 450, -750))
    cad.union(neck, plate)
    knob = cad.cylinder("_knob", 35, 30, segments=12, axis="Y", loc=L("monitor", 780, -15, -620), mat=MAT["dark"])
    led = cad.cylinder("_led", 14, 14, segments=8, axis="Y", loc=L("monitor", 640, -7, -620), mat=MAT["sign"])
    detail = cad.join([knob, led], "MonitorDetail")
    for part in (bezel, body, neck, detail):
        set_origin(part, L("monitor"))
    return bezel, body, neck, detail


# ---------------------------------------------------------------- 3. sandwich board
def make_board():
    p = P
    w, h, t, d = p["board_w"], p["board_h"], p["frame_t"], p["frame_d"]
    outer = cad.box("BoardFrame", (w + 2 * t, d, h + 2 * t), loc=L("board"), bevel=6, mat=MAT["paint"])
    cad.subtract(outer, cad.box("_inner", (w, d + 40, h), loc=L("board")))
    bottom = -(h / 2 + t)                    # z of the frame's lower edge (relative to the board centre)
    front_len = p["board_cz"] + bottom        # distance from that edge down to the floor
    for sx in (-1, 1):
        x = sx * (w / 2 + t / 2)
        leg = cad.box("_leg", (p["leg"], p["leg"], front_len + 2), loc=L("board", x, 0, bottom - front_len / 2 + 1))
        cad.union(outer, leg)
        run = 420.0; length = math.hypot(run, front_len)
        brace = cad.box("_brace", (p["leg"], p["leg"], length), loc=L("board", x, d / 2 + run / 2 - 10, bottom - front_len / 2 + 2),
                        rot=(math.degrees(math.atan2(run, front_len)), 0, 0))
        cad.union(outer, brace)
    set_origin(outer, L("board"))
    return outer


# ---------------------------------------------------------------- 4. clipboard
def make_clipboard():
    p = P
    board = cad.box("ClipboardBoard", (p["clip_w"], p["clip_t"], p["clip_h"]), loc=L("clip"), bevel=6, mat=MAT["board"])
    clip = cad.box("_clip", (440, 36, 150), loc=L("clip", 0, -p["clip_t"] / 2 - 18, p["clip_h"] / 2 - 90), bevel=5, mat=MAT["steel"])
    ring = cad.cylinder("_ring", 34, 36, segments=12, axis="Y", loc=L("clip", 0, -p["clip_t"] / 2 - 18, p["clip_h"] / 2 + 5), mat=MAT["steel"])
    # board and clip stay two shells of one object; clip faces get steel
    cad.assign(board, MAT["board"])
    joined = cad.join([board, clip, ring], "ClipboardBoard")
    cad.assign_faces(joined, MAT["steel"], lambda c, n: c.z > L("clip")[2] + 540 and c.y < L("clip")[1] - p["clip_t"] / 2 - 1)
    pole = cad.cylinder("ClipboardPole", 30, 1350, segments=12, loc=L("clip", 0, 45, -875), mat=MAT["dark"])
    foot = cad.cylinder("_foot", 300, 40, segments=16, loc=L("clip", 0, 45, -1530))
    cad.union(pole, foot)
    set_origin(joined, L("clip")); set_origin(pole, L("clip"))
    return joined, pole


# ---------------------------------------------------------------- 5. exit door
def make_door():
    p = P
    slab = cad.box("ExitDoor", (p["door_w"], p["door_t"], p["door_h"]), loc=L("door", 0, 0, p["door_h"] / 2), bevel=8, mat=MAT["door"])
    jamb_x = p["door_w"] / 2 + p["case_w"] / 2
    for sx in (-1, 1):
        cad.union(slab, cad.box("_jamb", (p["case_w"], p["case_d"], p["door_h"] + 100), loc=L("door", sx * jamb_x, 0, (p["door_h"] + 100) / 2)))
    cad.union(slab, cad.box("_header", (p["door_w"] + 2 * p["case_w"], p["case_d"], p["case_w"]), loc=L("door", 0, 0, p["door_h"] + 50)))
    brackets = [cad.box("_bk", (50, 60, 60), loc=L("door", sx * 400, -p["door_t"] / 2 - 30, 1050), mat=MAT["chrome"]) for sx in (-1, 1)]
    bar = cad.cylinder("_bar", 22, 900, segments=12, axis="X", loc=L("door", 0, -p["door_t"] / 2 - 38, 1050), mat=MAT["chrome"])
    pushbar = cad.union(brackets[0], brackets[1], bar); pushbar.name = "ExitBar"
    sign = cad.box("ExitSign", (700, 120, 250), loc=L("door", 0, 0, p["door_h"] + 100 + 125), bevel=6, mat=MAT["sign"])
    for part in (slab, pushbar, sign):
        set_origin(part, L("door"))
    return slab, pushbar, sign


# ---------------------------------------------------------------- 6. ceiling light
def make_fixture():
    p = P
    housing = cad.box("LightHousing", (p["fix_l"], p["fix_w"], p["fix_h"]), loc=L("fixture"), bevel=4, mat=MAT["housing"])
    cad.subtract(housing, cad.box("_in", (p["fix_l"] - 40, p["fix_w"] - 40, p["fix_h"] - 10), loc=L("fixture", 0, 0, -10)))
    for sx in (-1, 1):
        cad.union(housing, cad.box("_socket", (40, 60, 40), loc=L("fixture", sx * (p["tube_l"] / 2 + 20), 0, 0)))
    tube = cad.cylinder("LightTube", p["tube_r"], p["tube_l"], segments=12, axis="X", loc=L("fixture", 0, 0, -5), mat=MAT["tube"])
    set_origin(housing, L("fixture")); set_origin(tube, L("fixture"))
    return housing, tube


# ---------------------------------------------------------------- 7. interview desk
def make_interview_desk():
    p = P
    top = cad.box("InterviewDeskTop", (p["iv_w"], p["iv_d"], p["iv_t"]), loc=L("iv", 0, 0, p["iv_h"] + p["iv_t"] / 2), bevel=8, mat=MAT["laminate"])
    ped_x = p["iv_w"] / 2 - p["ped_w"] / 2
    body = None
    for sx in (-1, 1):
        ped = cad.box("_ped", (p["ped_w"], p["ped_d"], p["iv_h"]), loc=L("iv", sx * ped_x, 0, p["iv_h"] / 2), bevel=5, mat=MAT["wood"])
        body = ped if body is None else cad.union(body, ped)
        for k in range(3):
            z = 117 + k * 233
            cad.union(body, cad.box("_drawer", (880, 20, 200), loc=L("iv", sx * ped_x, -p["ped_d"] / 2 - 10, z), bevel=3))
            cad.union(body, cad.box("_handle", (220, 25, 25), loc=L("iv", sx * ped_x, -p["ped_d"] / 2 - 32.5, z + 40), bevel=3))
    cad.union(body, cad.box("_modesty", (p["iv_w"] - 2 * p["ped_w"], 30, 600), loc=L("iv", 0, p["ped_d"] / 2 - 35, 400)))
    body.name = "InterviewDeskBody"; cad.assign(body, MAT["wood"])
    set_origin(top, L("iv")); set_origin(body, L("iv"))
    return top, body


cad.collection("Office kit")
desk_body, desk_top = make_desk()
bezel, mon_body, neck, detail = make_monitor()
board = make_board()
clipboard, pole = make_clipboard()
door, pushbar, exit_sign = make_door()
housing, tube = make_fixture()
iv_top, iv_body = make_interview_desk()

# ---------------------------------------------------------------- checks
def span(obj, axis):
    lo, hi = cad.bbox(obj)
    return lo[axis], hi[axis]


def rests_on(upper, lower, axis=2, tol=0.05):
    """Exact bounding-box adjacency (the mesh inside-test reports false positives on large coplanar contacts)."""
    assert abs(span(upper, axis)[0] - span(lower, axis)[1]) < tol, f"{upper.name} must rest exactly on {lower.name}"


def stays_clear(a, b, axis, tol=0.05):
    assert span(a, axis)[1] <= span(b, axis)[0] + tol, f"{a.name} must not reach into {b.name}"


cad.expect(desk_body, dims=(P["desk_w"], P["desk_d"], P["desk_h"]), tol=0.5)
cad.expect(desk_top, dims=(P["top_w"], P["top_d"], P["top_t"]), tol=0.5)
rests_on(desk_top, desk_body)
assert abs(span(desk_top, 1)[0] - (span(desk_body, 1)[0] - 350)) < 0.5, "the desk top must overhang the front by 350 mm"
cad.expect(bezel, dims=(P["mon_w"], P["bezel_d"], P["mon_h"]), tol=0.5)
stays_clear(bezel, mon_body, 1)
assert abs(span(bezel, 1)[1] - span(mon_body, 1)[0]) < 0.05
assert span(neck, 1)[0] >= span(bezel, 1)[1] - 0.05, "the monitor stand must stay behind the bezel face"
cad.expect_contact(bezel, detail, tol=0.5)
cad.expect(clipboard, dims=(P["clip_w"], None, 1489), tol=0.5)
cad.expect(door, dims=(P["door_w"] + 2 * P["case_w"], None, P["door_h"] + 100), tol=1.0)
assert span(housing, 2)[0] < span(tube, 2)[0] and span(tube, 2)[1] < span(housing, 2)[1] - 10, "the tube must sit inside the housing"
cad.expect(iv_top, dims=(P["iv_w"], P["iv_d"], P["iv_t"]), tol=0.5)
rests_on(iv_top, iv_body)
cad.expect_no_overlap(desk_body, iv_body)

cad.render_hints(bg="dark", hdri="city", hdri_saturation=0.2, hdri_rotation=40)
cad.save("model")
