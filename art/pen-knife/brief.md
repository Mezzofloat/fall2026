# pen-knife - design brief

## Request
> "Create an asset for the pen-knife that the chaser holds. Overall upgrade the assets by using blender and the CAD skill (modified for 3d/2.5d assets though, no 3d printing elements obviously but keep the advisor checks)."
> Context: BREATHE.EXE, a first-person office horror (Baldi's Basics x Buckshot Roulette). Mr. Vale, the interviewer, clicks a pen and it becomes a knife.

## Purpose and deliverables
- Purpose: **real-time game asset** (Unity 6 URP). Not for printing: no wall/print rules.
- Deliverables: `model.blend`, review renders, `knife.fbx` (one object per part, Z-up pen axis) for Unity.
- Animation contract: `Blade` and `Plunger` are separate objects. In Unity the click is: Blade scales along the pen axis (Z) from ~0.01 to 1 about the nose-centre origin (its tail stays hidden in the bore), and the Plunger translates ~4 mm. The blade is longer than the barrel, so it cannot retract by translation.

## Decisions confirmed by the user
| Question | Answer |
|---|---|
| How Vale looks | Flat billboard sprite, with this real 3D knife attached to his hand |
| Asset scope | Hero set (this is the first piece) |
| Style | Low-poly grimy hybrid: hard-edged facets, saturated corporate colours |

## Assumptions (user can override)
| Item | Value | Reason |
|---|---|---|
| Real-world scale | Stylised chunky click pen, 126 mm barrel (oversized on purpose for game readability) | Reads as an ordinary pen first |
| Blade state modelled | **Extended** (100 mm out of the nose); retracted is a translation of Blade+Plunger | One asset, animated in Unity |
| Facets | 16 segments on round parts, flat shading | Low-poly look |
| Colours | Corporate navy barrel, red click button, chrome trim, brushed steel blade | Corporate-friendly by day, turns red under horror lighting |
| Grime | Colour variation only; Unity adds dirt textures later | Keeps the model clean |

## Frame of reference
- Units mm. Pen axis = **Z**. Origin at the centre of the nose opening (barrel z=0). Barrel runs +Z, extended blade runs -Z (tip at z=-100). Clip on the +Y side. The cutting edge is on the -Y side of the blade.

## Parts
| Part | Strategy | Key dimensions (mm) | Material | Interfaces |
|---|---|---|---|---|
| Barrel | hollow revolve, 16 seg | 14 dia, z 0..126, nose r6.2 tapering to r7.0 at z24, bore r5.6, cap hole r3.2 | navy matte plastic | holds Grip, Collar, Clip; bore passes Blade and Plunger |
| Collar | tube | r7.0..7.6, z 24..29 | chrome | sits on Barrel |
| Grip | tube + 12 grooves | r7.0..7.5, z 29..67 | black rubber | sits on Barrel |
| Clip | extruded silhouette | 4.2 wide, from z 121.5 down to 62.8, curled tip | chrome | attached to Barrel at the top |
| Plunger | revolve | stem r2.2 z100..134, head r3.2 to z138.5 | chrome stem, red head | passes through the cap hole |
| Blade | loft | 9.0 wide x 2.2 thick x 200 long, z -100..100 | brushed steel | passes nose bore; top touches Plunger |

## Requirements (the reviewer checks these)
- [R1] The pen has a hollow barrel about 14 mm across and about 126 mm long, tapering at the nose, with a visible open bore at the nose that the blade passes through.
- [R2] A single slim, flat-ground, drop-point blade (about 9 mm wide, 2.2 mm thick), sharp edge on one side, with a pointed tip, protrudes about 100 mm from the nose.
- [R3] The blade runs inside the barrel bore with at least 0.5 mm clearance and does not intersect the barrel.
- [R4] A click plunger with a visibly red head stands proud of the top of the barrel, and its stem passes through a hole in the cap with clearance.
- [R5] A chrome pocket clip is attached at the top of the barrel on one side and curls in at its lower end.
- [R6] A black rubber grip sleeve with at least 8 grooves sits on the lower barrel, plus a chrome collar ring between nose and grip.
- [R7] The asset is separate named objects: Barrel, Collar, Grip, Clip, Plunger, Blade (6 total).
- [R8] The top of the blade touches the bottom of the plunger (the mechanism is continuous) and no other parts interpenetrate.
- [R10] Blade and Plunger objects have their origin at the nose centre (0,0,0); the clip curl sits above the grip (no interpenetration with Grip).
- [R9] Style: low-poly faceted look (visible flat facets on the barrel), reads as an ordinary corporate pen first and a knife second. Corporate navy, red button, chrome and steel.

## Checks encoded in build.py
- dims of Barrel, Blade; blade/plunger contact; blade/barrel clearance; plunger/barrel clearance.

## Revision log
- r1: first build
- r2: pen made chunkier and blade widened (9 x 2.2 x 200) because r1's 6 mm blade read as an ice pick; light review background (steel was invisible on dark).
- r3: advisor round 1 REJECT fixes: clip curl moved above grip (z>=70.8), groove cutters kept inside the sleeve wall, animation contract changed to blade scale + plunger click, origin assert added.
- r4: grip grooves now run through both ends so all 12 are visible as notches in the top view (R6 was unverifiable).
