# office-kit - design brief

## Request
> "Overall upgrade the assets by using blender and the CAD skill (modified for 3d/2.5d assets though, no 3d printing elements obviously but keep the advisor checks)." (BREATHE.EXE: Baldi's Basics x Buckshot Roulette office horror.)
> Replace the plain cubes in the lobby (main menu) and interview room with real low-poly props. Hero set agreed with the user: desks, monitor, clipboard, board, door, light fixtures.

## Purpose and deliverables
- Purpose: real-time game props (Unity 6 URP). No printing rules.
- Deliverables: `model.blend`, review renders, one FBX per part (`cad export --format fbx --per-part`).
- UI text is drawn by Unity on world-space canvases mounted on these props (the screens, boards and plates). The models leave **flat, clear surface areas** for those canvases.

## Decisions confirmed by the user
| Question | Answer |
|---|---|
| Asset scope | Hero set (desk, monitor, clipboard, board, door, lights, interview desk) |
| Style | Low-poly grimy hybrid: hard-edged, bevelled, saturated corporate colours; flat people stay sprites |

## Assumptions
| Item | Value | Reason |
|---|---|---|
| Units / scale | Design in mm, 1 mm = 0.001 m in Unity (Unity import applies x10 of the file) | Matches the pen-knife pipeline |
| Axes | Blender -Y is the front of every prop (faces the player); +Z up. In Unity this is -Z front, +Y up | Measured mapping Blender (x,y,z) -> Unity (x,z,y) |
| Origins | See Parts table; each part's origin is where Unity positions it | Lets the game place props without offsets |
| Colour | Placeholder colours here; Unity assigns the final per-part materials and recolours them red for horror | |
| Faceting | Hard edges, small bevels (3-8 mm), round parts 12-16 segments | Low-poly look |

## Frame of reference
- Units mm. +Z up, front faces -Y. Part origins listed below.

## Parts (all separate objects)
| Part | Origin | Size (mm, x y z) | Notes |
|---|---|---|---|
| DeskBody | floor, centre of front panel | 5800 x 900 x 1000 | 29 vertical slat grooves on the front (-Y) face, recessed plinth |
| DeskTop | same origin | 6000 x 1300 x 80, bottom at z=1000, centred y=-150 | overhangs the front by 350; bevelled edge |
| MonitorBezel | centre of the front face | 1950 x 150 x 1450, aperture 1700 x 1200 | the flat screen area is the aperture |
| MonitorBody | centre of the front face (same) | CRT hump behind the bezel, y 150..950 | stepped taper, vent slots on top |
| MonitorNeck | same | stand under the body | tilt-swivel base |
| MonitorDetail | same | power knob + LED on the bezel's lower right | |
| BoardFrame | board centre | frame around a 1350 x 1550 board face, legs to the floor | A-frame, 2 front legs + 2 back braces |
| ClipboardBoard | board centre | 1100 x 30 x 1450 (flat face for UI) with a steel clip at the top | |
| ClipboardPole | board centre | pole 1550 down to the floor + round foot | |
| ExitDoor | floor, centre of the door | slab 1200 x 100 x 2200 in a casing 1400 x 150 x 2300 | |
| ExitBar | same | chrome push bar across the slab | |
| ExitSign | same | green sign box above the casing | |
| LightHousing | fixture centre | 1800 x 300 x 70, open at the bottom | end caps |
| LightTube | fixture centre | 1700 x 40 x 40 tube | separate so Unity can flicker it |
| InterviewDeskTop | floor, centre of the desk | 3600 x 1100 x 80 at z 700..780 | |
| InterviewDeskBody | same | two pedestals with 3 drawer fronts each (with handles) and a modesty panel | |

## Requirements (the reviewer checks these)
- [R1] DeskBody is a wooden counter with at least 20 visible vertical slat grooves on its front face, and DeskTop is a slab that overhangs the front by about 350 mm and sits on the body.
- [R2] The monitor reads as a chunky CRT: a bezel with a clear rectangular screen aperture (about 1700 x 1200 mm), a stepped hump body behind it, a neck stand, and a knob and LED on the bezel.
- [R3] BoardFrame is a framed sandwich board (face about 1350 x 1550 mm) standing on two front legs with two angled back braces; the face is an open flat area.
- [R4] The clipboard is a flat board (about 1100 x 1450 mm) with a steel clip at the top on a pole that reaches the floor with a round foot.
- [R5] ExitDoor is a slab in a casing with a chrome push bar across it and a separate sign box above the casing.
- [R6] The ceiling fixture is an open housing with end caps and a separate round tube inside it, and the tube does not intersect the housing.
- [R7] InterviewDesk is a slab top over two pedestals with three drawer fronts each (each with a handle) and a modesty panel; the knee space between the pedestals is open.
- [R8] The kit is exactly these separate named objects: DeskBody, DeskTop, MonitorBezel, MonitorBody, MonitorNeck, MonitorDetail, BoardFrame, ClipboardBoard, ClipboardPole, ExitDoor, ExitBar, ExitSign, LightHousing, LightTube, InterviewDeskTop, InterviewDeskBody (16).
- [R9] Within each prop the parts touch where they should (top on body, tube in housing, bar on door) and nothing interpenetrates beyond touching.
- [R10] Style: low-poly, hard edged with small bevels, dull corporate colours; reads as cheap 1990s office furniture. The front of every prop faces -Y.

## Checks encoded in build.py
- Dimension expects for the major parts; contact between top and body; tube clearance in housing.

## Revision log
- r1: first build
- r2: fixed board legs/braces sign error, clipboard clip material, monitor stand plate (was 70 mm in front of the bezel), tube z (was 5 mm into the housing top plate).

## Notes for reviewers
- `cad inspect` lists PENETRATES for DeskBody/DeskTop, MonitorBezel/MonitorBody, MonitorBody/MonitorNeck, ExitDoor/ExitBar and InterviewDeskTop/InterviewDeskBody. These are **false positives** of the inspect inside-test on large coplanar contacts (its parity ray steps 1e-4 mm, below float32 precision at coordinates in the thousands of mm). Evidence: an EXACT boolean INTERSECT of each flagged pair has volume 0.0 mm3 and 0 faces, and the build's exact bounding-box asserts (`rests_on`, `stays_clear`) pass.
- Intended multi-shell objects: MonitorDetail (knob + LED) and ClipboardBoard (board + clip + ring) are joined objects, not boolean leftovers.
- The beige back face of MonitorBody is visible through the bezel aperture in renders; in game the Unity canvas covers the aperture.
