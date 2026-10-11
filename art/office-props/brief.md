# office-props - design brief

## Request
> "Overall, the assets should look more complete and similar to Buckshot Roulette. Put an order of magnitude more effort into the assets. The game should look polished." (BREATHE.EXE iteration 2)
> Rooms must be distinctive: sleek in safe mode, weathered and moldy in horror mode (the game recolours and re-textures these props per mode).

## Purpose and deliverables
- Purpose: real-time game props (Unity 6 URP, low-poly, hard-edged, small bevels). No printing rules; keep the advisor review gate.
- Deliverables: `model.blend`, review renders, one FBX per part (`scripts/export_unity.py`).
- Unity assigns materials by the **slot name suffix** (`<Part>_main`, `_dark`, `_metal`, `_glow`, `_accent`), so each part only uses those five names.

## Decisions
| Question | Answer |
|---|---|
| Style | Low-poly grimy hybrid, Buckshot Roulette-like: hard edges, chunky silhouettes, readable at 480p |
| Scope | The furniture and fittings that fill nine rooms: reception/open plan/break room/archive/atrium/conference/HR/server room/interview suite |
| Detail budget | Each prop 200-3000 triangles; real recesses, handles, vents, drawers; no flat boxes |

## Frame of reference
- Units mm, +Z up, FRONT faces -Y. Every part origin is at floor level, centred on its footprint (wall props: centre of the back face; doors: the hinge edge at floor level).
- Blender (x, y, z) -> Unity (x, z, y); front -Y -> -Z.

## Parts
| Part | Size (mm, x y z) | Slots used |
|---|---|---|
| OfficeChair | 620 x 620 x 1000 | main fabric (seat, back), dark (arms, star base, casters), metal (gas lift) |
| ConferenceChair | 520 x 540 x 900 | main (seat, back), metal (4-leg frame), dark (feet) |
| FilingCabinet | 460 x 620 x 1330 | main (body, drawer fronts), dark (seams), metal (handles, label holders) |
| ServerRack | 600 x 1000 x 2050 | dark (frame), metal (units), glow (LEDs), accent (cables) |
| VendingMachine | 900 x 800 x 1850 | main (body), dark (recess, flap), metal (keypad), glow (display), accent (products) |
| WaterCooler | 340 x 405 x 1310 | main (cabinet), dark (top), metal (cup holder, taps), accent (bottle) |
| Copier | 760 x 690 x 1075 | main (body), dark (lid, feeder), metal (buttons), glow (screen) |
| Fridge | 700 x 700 x 1850 | main (shell), dark (seam, feet), metal (handles), accent (magnets) |
| Sofa | 1900 x 850 x 820 | main (fabric), dark (feet) |
| PottedPlant | 500 x 500 x 1200 | main (pot), dark (soil), accent (leaves) |
| MonitorLCD | 560 x 220 x 450 | dark (bezel, stand), glow (screen), metal (neck) |
| KeyboardMouse | 430 x 190 x 30 | main (keys), dark (case) |
| DeskLamp | 160 x 300 x 400 | dark (base), metal (arm), main (shade) |
| WallClock | 420 x 50 x 420 (origin: back-face centre) | main (face), dark (rim, hands) |
| TrashBin | 300 x 300 x 400 | dark (bin), metal (rim) |
| BoxStack | 520 x 420 x 620 | main (cardboard), accent (tape), dark (label) |
| ShelfUnit | 1000 x 420 x 1900 | metal (uprights, shelves), dark (braces) |
| Microwave | 500 x 380 x 300 | main (shell), dark (door glass), glow (display), metal (handle) |
| CoffeeMaker | 260 x 300 x 380 | dark (body), main (carafe base), metal (plate) |
| Whiteboard | 1500 x 60 x 1000 (wall mount, origin back-face centre) | main (board), metal (frame, tray), accent (markers) |
| DoorSlab | 920 x 44 x 2000 (origin: hinge edge at floor level, slab extends +x) | main (panel), metal (lever handle, hinges), dark (inset panels) |
| DoorFrame | 1100 x 120 x 2100 (opening 920 wide, origin: floor centre) | main (frame), metal (strike plate, hinges) |

## Requirements (the reviewer checks these)
- [R1] OfficeChair is a swivel chair: a padded seat, a tall backrest on a post, two armrests with pads, a gas-lift column and a five-star base with five casters.
- [R2] ConferenceChair has a seat, a backrest and a four-leg metal frame with four feet; no casters.
- [R3] FilingCabinet has four drawer fronts separated by dark seams, a handle and label holder on each drawer, and short feet.
- [R4] ServerRack is a 600 mm wide open-frame rack with four posts and at least eight stacked rack units whose faces have vent slots and LED lights, a cable bundle on top.
- [R5] VendingMachine has a recessed glass window showing at least five shelves of products, a lit display, a keypad of at least six buttons and a dispenser flap near the bottom.
- [R6] WaterCooler shows a cabinet, a top with two taps, a cup holder and a blue upside-down bottle on top.
- [R7] Copier has a body, a document feeder lid on top, at least two paper trays with handles and a tilted control panel with a lit screen and buttons.
- [R8] Fridge is a tall two-door fridge-freezer with a seam, two vertical handles and feet.
- [R9] Sofa is a two-seat sofa with arms, a backrest, two seat cushions and four feet.
- [R10] PottedPlant has a tapered pot, a soil disc and at least seven leaves in different directions.
- [R11] MonitorLCD has a thin bezel, a screen panel, a neck and a base; KeyboardMouse has a keyboard with at least four rows of keys and a mouse.
- [R12] DeskLamp, WallClock (with 12 hour marks and two hands), TrashBin (with a rim) and BoxStack (three taped boxes) read as those objects.
- [R13] ShelfUnit has four uprights, at least four shelves with lips and diagonal cross braces.
- [R14] Microwave has a door window, a handle and a display; CoffeeMaker has a carafe on a warming plate; Whiteboard has a frame, a pen tray and three markers.
- [R15] DoorSlab has four raised/inset panels, a lever handle and two hinges; DoorFrame has two jambs, a header and a strike plate, and its opening is at least 900 mm wide.
- [R16] Every part's materials use only the six slot name suffixes (`_main`, `_dark`, `_metal`, `_glow`, `_accent`, `_glass`) and its origin is as listed in the Parts table.

## Notes for reviewers
- Parts are intentionally multi-shell objects (touching or slightly overlapping shells joined into one mesh); inspect does not treat shells of the same object as interference.
- `cad inspect` can report false PENETRATES for large coplanar contacts; the build asserts use exact bounding boxes.

## Revision log
- r1: first build
- r2: ServerRack side panels removed (open frame); VendingMachine gets a glass pane (`_glass`); DeskLamp arms/shade rebuilt so the shade points down and forward.
