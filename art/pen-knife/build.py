"""
pen-knife - build script.  Run:  cad build build.py
Pen axis is +Z (barrel), the extended blade runs to -Z. Every dimension lives in P.
Low-poly game asset: 16-segment round parts, flat shading.
"""
import os
import sys

sys.path.insert(0, os.environ.get("CADLIB_DIR", "/home/anon/.claude/skills/blender-cad/scripts"))
import cadlib as cad  # noqa: E402

# ---------------------------------------------------------------- parameters (mm)
P = dict(
    seg=16,
    barrel_len=126.0, nose_r=6.2, body_r=7.0, taper_len=24.0, bore_r=5.6, cap_hole_r=3.2, cap_z=124.5,
    collar_z0=24.0, collar_h=5.0, collar_r=7.6,
    grip_z0=29.0, grip_h=38.0, grip_r=7.5, grip_groove_n=12, grip_groove_r=0.7,
    clip_w=5.0,
    plunger_z0=100.0, plunger_stem_r=2.8, plunger_top=134.0, plunger_head_r=4.0, plunger_head_top=139.0,
    blade_tip_z=-100.0, blade_top_z=100.0, blade_w=9.0, blade_t=2.2, blade_edge_t=0.14, blade_s_start=-50.0,
)

cad.new_scene(units="mm")

# ---------------------------------------------------------------- materials
MAT = dict(
    barrel=cad.mat("plastic_matte", name="Navy plastic", color="#1c3f8f", roughness=0.55),
    chrome=cad.mat("chrome", name="Chrome trim", roughness=0.18),
    rubber=cad.mat("rubber", name="Grip rubber", color="#15161a"),
    red=cad.mat("plastic_glossy", name="Red click button", color="#c9262b", roughness=0.35),
    blade=cad.mat("steel_brushed", name="Blade steel", color="#c9ced6", roughness=0.3),
)


# ---------------------------------------------------------------- parts
def make_barrel():
    L, R, r0 = P["barrel_len"], P["body_r"], P["nose_r"]
    prof = [
        (r0, 0.0), (R, P["taper_len"]), (R, L - 6.0), (R - 1.0, L), (P["cap_hole_r"], L),
        (P["cap_hole_r"], P["cap_z"]), (P["bore_r"], P["cap_z"]), (P["bore_r"], 0.0),
    ]
    return cad.revolve("Barrel", prof, segments=P["seg"], mat=MAT["barrel"], smooth=False)


def make_collar():
    c = cad.tube("Collar", P["collar_r"], P["body_r"], P["collar_h"], segments=P["seg"],
                 loc=(0, 0, P["collar_z0"] + P["collar_h"] / 2), mat=MAT["chrome"])
    cad.set_smooth(c, None)
    return c


def make_grip():
    g = cad.tube("Grip", P["grip_r"], P["body_r"], P["grip_h"], segments=P["seg"],
                 loc=(0, 0, P["grip_z0"] + P["grip_h"] / 2), mat=MAT["rubber"])
    cut = cad.cylinder("_groove", P["grip_groove_r"], P["grip_h"] + 2.0, segments=6, axis="Z",
                       loc=(P["grip_r"] + 0.3, 0, P["grip_z0"] + P["grip_h"] / 2))
    cad.polar_array(cut, P["grip_groove_n"])
    cad.subtract(g, cut)
    cad.set_smooth(g, None)
    return g


def make_clip():
    # silhouette in (y, z): inner edge hugs the barrel (y=body_r), curls toward it at the bottom
    R = P["body_r"]
    pts = [(R, 119.0), (R + 1.2, 119.0), (R + 1.2, 80.0), (R + 0.7, 74.0), (R + 0.1, 71.9), (R + 0.3, 70.8),
           (R + 1.5, 73.0), (R + 2.4, 80.0), (R + 2.4, 121.5), (R, 121.5)]
    clip = cad.extrude("Clip", pts, P["clip_w"], rot=(90, 0, 90), mat=MAT["chrome"])
    cad.move(clip, -P["clip_w"] / 2, 0, 0)
    return clip


def make_plunger():
    r, rh = P["plunger_stem_r"], P["plunger_head_r"]
    prof = [(0, P["plunger_z0"]), (r, P["plunger_z0"]), (r, P["plunger_top"]), (rh, P["plunger_top"]),
            (rh, P["plunger_head_top"] - 0.8), (rh - 0.8, P["plunger_head_top"]), (0, P["plunger_head_top"])]
    p = cad.revolve("Plunger", prof, segments=P["seg"], mat=MAT["chrome"], smooth=False)
    cad.assign_faces(p, MAT["red"], lambda c, n: c.z > P["plunger_top"] + 0.05)
    return p


def make_blade():
    tip, top, W, t, te, s0 = (P[k] for k in ("blade_tip_z", "blade_top_z", "blade_w", "blade_t", "blade_edge_t", "blade_s_start"))
    hw = W / 2
    L = -s0 + tip * -1 - (-s0)  # unused helper, kept for readability

    def section(z):
        s = 0.0 if z >= s0 else min(1.0, (s0 - z) / (s0 - tip))
        edge_y = -hw * (1 - s ** 1.6)
        spine_y = hw * (1 - (max(0.0, s - 0.55) / 0.45) ** 2)
        if spine_y - edge_y < 0.35:  # keep the tip a real (tiny) quad
            mid = (spine_y + edge_y) / 2
            spine_y, edge_y = mid + 0.175, mid - 0.175
        ts = t / 2 * (1 - 0.65 * s * s)
        tedge = te * (1 - 0.5 * s)
        return [(ts, spine_y, z), (-ts, spine_y, z), (-tedge, edge_y, z), (tedge, edge_y, z)]

    zs = [tip, tip + 2, tip + 8, tip + 14, tip + 20, tip + 26, tip + 32, tip + 38, tip + 44, tip + 50, s0, top]
    blade = cad.loft("Blade", [section(z) for z in zs], mat=MAT["blade"])
    cad.set_smooth(blade, None)
    return blade


# ---------------------------------------------------------------- assembly
cad.collection("Pen-knife")
barrel = make_barrel()
collar = make_collar()
grip = make_grip()
clip = make_clip()
plunger = make_plunger()
blade = make_blade()

# ---------------------------------------------------------------- checks (what the brief promised)
cad.expect(barrel, dims=(2 * P["body_r"], 2 * P["body_r"], P["barrel_len"]), tol=0.1)
cad.expect(blade, dims=(P["blade_t"], P["blade_w"], P["blade_top_z"] - P["blade_tip_z"]), tol=0.1)
cad.expect_contact(blade, plunger)
cad.expect_clearance(blade, barrel, 0.5)
cad.expect_clearance(plunger, barrel, 0.25)
cad.expect_no_overlap(collar, grip)
cad.expect_no_overlap(clip, grip)
# Unity animation contract: Blade scales along the pen axis from the nose centre; Plunger translates ~4 mm.
for _o in (blade, plunger):
    assert all(abs(v) < 1e-6 for v in _o.location), f"{_o.name} origin must be at the nose centre, got {tuple(_o.location)}"
cad.expect_contact(collar, barrel)
cad.expect_contact(grip, barrel)

cad.explode_dir(blade, (0, 0, -1))
cad.explode_dir(plunger, (0, 0, 1))
cad.explode_dir(clip, (0, 1, 0))
cad.explode_dir(grip, (0, -1, 0))
cad.render_hints(bg="dark", hdri="city", hdri_saturation=0.15, hdri_rotation=60)

cad.save("model")
