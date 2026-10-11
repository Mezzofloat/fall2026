using System.Collections.Generic;
using UnityEngine;
using static Breathe.PropFactory;

namespace Breathe
{
    /// <summary>
    /// Builds the furniture for each OfficeMap block from low-poly primitives and, where approved Blender parts exist, from those.
    /// Every model is built in a local frame: origin at the block's centre on the floor, +x along its length, +z toward its front.
    /// </summary>
    public sealed class Furnisher
    {
        readonly Theme theme; readonly PropFactory kit; readonly System.Random rng = new System.Random(77);
        public readonly List<Renderer> Screens = new List<Renderer>();
        public readonly List<(Renderer renderer, bool red)> Leds = new List<(Renderer, bool)>();
        readonly Material wood, darkWood, metal, steel, plastic, black, fabricBlue, fabricGrey, fabricRed, fabricGreen, paper, leaf, soil, cardboard, ceramic, screenGlow, ledGreen, ledRed, binderRed, binderBlue, binderYellow, chrome, marble;

        public Furnisher(Theme theme, PropFactory kit)
        {
            this.theme = theme; this.kit = kit;
            var woodS = ProcTex.Wood(TerminalUI.Hex("A8794F"), TerminalUI.Hex("8C603E"), false, 3, theme.Textures); var woodH = ProcTex.Wood(TerminalUI.Hex("A8794F"), TerminalUI.Hex("8C603E"), true, 3, theme.Textures);
            var darkS = ProcTex.Wood(TerminalUI.Hex("4B3224"), TerminalUI.Hex("3A261B"), false, 4, theme.Textures); var darkH = ProcTex.Wood(TerminalUI.Hex("4B3224"), TerminalUI.Hex("3A261B"), true, 4, theme.Textures);
            var metalS = ProcTex.Metal(TerminalUI.Hex("A9B0BA"), false, 5, theme.Textures); var metalH = ProcTex.Metal(TerminalUI.Hex("A9B0BA"), true, 5, theme.Textures);
            wood = theme.Lit("Wood", "FFFFFF", "D8C0B0", .35f, .05f, woodS, woodH, new Vector2(1, 1));
            darkWood = theme.Lit("Dark wood", "FFFFFF", "C8B0A0", .4f, .05f, darkS, darkH);
            metal = theme.Lit("Metal", "FFFFFF", "D0B8A8", .6f, .1f, metalS, metalH, null, .6f);
            steel = theme.Lit("Steel", "C9CED6", "8A7C78", .8f, .15f, null, null, null, .85f);
            plastic = theme.Lit("Plastic", "DCD6C4", "8C7C66", .3f, .05f);
            black = theme.Lit("Black plastic", "1B1D24", "0E0B0B", .35f, .1f);
            fabricBlue = theme.Lit("Fabric blue", "FFFFFF", "FFFFFF", .05f, .02f, ProcTex.Fabric(TerminalUI.Hex("3F5F8F"), false, 6, theme.Textures), ProcTex.Fabric(TerminalUI.Hex("3F5F8F"), true, 6, theme.Textures));
            fabricGrey = theme.Lit("Fabric grey", "FFFFFF", "FFFFFF", .05f, .02f, ProcTex.Fabric(TerminalUI.Hex("8D95A3"), false, 7, theme.Textures), ProcTex.Fabric(TerminalUI.Hex("8D95A3"), true, 7, theme.Textures));
            fabricRed = theme.Lit("Fabric red", "FFFFFF", "FFFFFF", .05f, .02f, ProcTex.Fabric(TerminalUI.Hex("A8333F"), false, 8, theme.Textures), ProcTex.Fabric(TerminalUI.Hex("A8333F"), true, 8, theme.Textures));
            fabricGreen = theme.Lit("Fabric green", "FFFFFF", "FFFFFF", .05f, .02f, ProcTex.Fabric(TerminalUI.Hex("3F7F63"), false, 9, theme.Textures), ProcTex.Fabric(TerminalUI.Hex("3F7F63"), true, 9, theme.Textures));
            paper = theme.Lit("Paper", "F2EFE4", "B6A98A", .1f, .02f);
            leaf = theme.Lit("Leaf", "3F9A4E", "4A5A2A", .2f, .05f);
            soil = theme.Lit("Soil", "3A2B22", "1E1612", .05f, .02f);
            cardboard = theme.Lit("Cardboard", "B58A5A", "6F5538", .08f, .02f);
            ceramic = theme.Lit("Ceramic", "F4F4F0", "A89E88", .7f, .15f);
            screenGlow = theme.Glow("Screen", "8FE3D0", "FF5A4A");
            ledGreen = theme.Glow("LED green", "39FF7E", "FF3030"); ledRed = theme.Glow("LED red", "FF4040", "FF1A1A");
            binderRed = theme.Lit("Binder red", "B33A3A", "5E2020", .15f, .03f); binderBlue = theme.Lit("Binder blue", "3A5AB3", "20305E", .15f, .03f); binderYellow = theme.Lit("Binder yellow", "D8B83A", "6E5E20", .15f, .03f);
            chrome = theme.Lit("Chrome", "E8ECF2", "9A8A86", .9f, .2f, null, null, null, 1f);
            marble = theme.Lit("Marble", "E8E8E4", "9A8E84", .6f, .1f);
        }

        Material glassMat;
        Material Pick(params Material[] m) => m[rng.Next(m.Length)];
        /// <summary>Materials for the Blender props' named slots. main = the part's body colour.</summary>
        PropSkin Skin(Material main, Material accent = null) => new PropSkin { Main = main, Dark = black, Metal = metal, Glow = screenGlow, Accent = accent ?? main, Glass = glassMat ??= theme.Glass("Prop glass", "CFEFFA", "8A7A70") };

        public GameObject Build(FurnitureBlock block, Transform parent, Vector2 roomCenter)
        {
            var r = block.Rect; bool alongX = r.width >= r.height; float length = alongX ? r.width : r.height, depth = alongX ? r.height : r.width;
            var center = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);
            Vector2 toRoom = roomCenter - center; Vector2 facing = alongX ? new Vector2(0, toRoom.y >= 0 ? 1 : -1) : new Vector2(toRoom.x >= 0 ? 1 : -1, 0);
            var root = new GameObject("Furniture: " + block.Kind); root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(center.x, 0, -center.y);
            root.transform.localRotation = Quaternion.LookRotation(new Vector3(facing.x, 0, -facing.y));
            BuildKind(block.Kind, root.transform, length, depth);
            return root;
        }


        /// <summary>Build one furniture kind in a local frame (+x along its length, +z toward its front).</summary>
        public void BuildKind(string kind, Transform t, float length, float depth)
        {
            switch (kind)
            {
                case "reception desk": Reception(t, length, depth); break;
                case "sofa": Sofa(t, length, depth); break;
                case "cubicle": Cubicle(t, length, depth); break;
                case "counter": Counter(t, length, depth); break;
                case "fridge": Fridge(t, length, depth); break;
                case "table": Table(t, length, depth, 4); break;
                case "shelves": Shelves(t, length, depth); break;
                case "planter": Planter(t, length, depth); break;
                case "conference table": Conference(t, length, depth); break;
                case "hr desk": DeskWithMonitor(t, length, depth, true); break;
                case "filing": Filing(t, length, depth); break;
                case "rack": Rack(t, length, depth); break;
                case "waiting chairs": WaitingChairs(t, length, depth); break;
                case "plant": Plant(t, new Vector3(0, 0, 0), .9f); break;
                case "vending": if (kit.Has("VendingMachine")) kit.PlaceSkinned(t, "VendingMachine", Vector3.zero, 180, Vector3.one, Skin(binderRed, binderYellow)); else Box(t, "Vending", new Vector3(0, .9f, 0), new Vector3(.9f, 1.8f, .8f), binderRed); break;
                case "cooler": if (kit.Has("WaterCooler")) kit.PlaceSkinned(t, "WaterCooler", Vector3.zero, 180, Vector3.one * 1.05f, Skin(ceramic, theme.Glass("Bottle", "4AA8FF", "5A6A70", .6f, .6f))); else Cyl(t, "Cooler", new Vector3(0, .55f, 0), .17f, 1.1f, ceramic); break;
                case "boxes": if (kit.Has("BoxStack")) kit.PlaceSkinned(t, "BoxStack", Vector3.zero, rng.Next(-20, 20), Vector3.one * 1.1f, Skin(cardboard, paper)); else Box(t, "Boxes", new Vector3(0, .3f, 0), new Vector3(.5f, .6f, .4f), cardboard); break;
                case "copier": if (kit.Has("Copier")) kit.PlaceSkinned(t, "Copier", Vector3.zero, 180, Vector3.one, Skin(ceramic)); else Box(t, "Copier", new Vector3(0, .5f, 0), new Vector3(.76f, 1f, .66f), ceramic); break;
                case "trash": if (kit.Has("TrashBin")) kit.PlaceSkinned(t, "TrashBin", Vector3.zero, 0, Vector3.one, Skin(black)); else Cyl(t, "Bin", new Vector3(0, .2f, 0), .15f, .4f, black); break;
                default: Box(t, "Block", new Vector3(0, .45f, 0), new Vector3(length * .95f, .9f, depth * .95f), wood); break;
            }
        }

        /// <summary>Place a piece of furniture anywhere (the lobby uses this): yaw turns its front (+z) to face the room.</summary>
        public Transform Place(string kind, Transform parent, Vector3 position, float yaw, float length, float depth)
        {
            var root = new GameObject("Furniture: " + kind).transform; root.SetParent(parent, false); root.localPosition = position; root.localRotation = Quaternion.Euler(0, yaw, 0);
            BuildKind(kind, root, length, depth); return root;
        }

        // ------------------------------------------------------------------ pieces
        public void Chair(Transform p, Vector3 pos, float yaw, Material fabric, bool swivel = true)
        {
            var c = new GameObject("Chair").transform; c.SetParent(p, false); c.localPosition = pos; c.localRotation = Quaternion.Euler(0, yaw, 0);
            if (!swivel && kit.Has("ConferenceChair")) { kit.PlaceSkinned(c, "ConferenceChair", Vector3.zero, 180, Vector3.one, Skin(fabric)); return; }
            if (kit.Has("OfficeChair")) { kit.PlaceSkinned(c, "OfficeChair", Vector3.zero, 180, Vector3.one, Skin(fabric, steel)); return; }
            Box(c, "Seat", new Vector3(0, .46f, 0), new Vector3(.46f, .08f, .46f), fabric);
            Box(c, "Back", new Vector3(0, .78f, -.21f), new Vector3(.44f, .5f, .07f), fabric);
            Box(c, "Armrest L", new Vector3(-.26f, .62f, 0), new Vector3(.05f, .05f, .34f), black); Box(c, "Armrest R", new Vector3(.26f, .62f, 0), new Vector3(.05f, .05f, .34f), black);
            Cyl(c, "Pole", new Vector3(0, .24f, 0), .035f, .4f, chrome);
            for (int i = 0; i < 5; i++) { float a = i * 72f; var leg = Box(c, "Star", new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * .2f, .05f, Mathf.Cos(a * Mathf.Deg2Rad) * .2f), new Vector3(.05f, .04f, .4f), black); leg.transform.localRotation = Quaternion.Euler(0, a, 0); Ball(c, "Wheel", new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * .38f, .03f, Mathf.Cos(a * Mathf.Deg2Rad) * .38f), new Vector3(.06f, .06f, .06f), black); }
        }

        void Monitor(Transform p, Vector3 pos, float yaw = 0)
        {
            var m = new GameObject("Monitor").transform; m.SetParent(p, false); m.localPosition = pos; m.localRotation = Quaternion.Euler(0, yaw, 0);
            if (kit.Has("MonitorLCD")) { kit.PlaceSkinned(m, "MonitorLCD", Vector3.zero, 180, Vector3.one * .92f, Skin(black)); return; }
            Box(m, "Base", new Vector3(0, .015f, 0), new Vector3(.22f, .03f, .18f), black); Box(m, "Neck", new Vector3(0, .12f, -.02f), new Vector3(.05f, .24f, .04f), black);
            Box(m, "Bezel", new Vector3(0, .38f, 0), new Vector3(.56f, .34f, .04f), black);
            var screen = Box(m, "Screen", new Vector3(0, .38f, .021f), new Vector3(.52f, .3f, .005f), screenGlow, false); Screens.Add(screen.GetComponent<Renderer>());
            for (int i = 0; i < 4; i++) Box(m, "Text line", new Vector3(-.12f + (i % 2) * .03f, .46f - i * .06f, .0245f), new Vector3(.22f - (i % 3) * .04f, .015f, .002f), black, false);
        }
        void Keyboard(Transform p, Vector3 pos)
        {
            if (kit.Has("KeyboardMouse")) { kit.PlaceSkinned(p, "KeyboardMouse", pos, 180, Vector3.one, Skin(plastic)); return; }
            Box(p, "Keyboard", pos, new Vector3(.42f, .02f, .15f), plastic); Box(p, "Mouse", pos + new Vector3(.32f, 0, 0), new Vector3(.06f, .025f, .1f), black);
        }
        void Papers(Transform p, Vector3 pos, int n) { for (int i = 0; i < n; i++) Box(p, "Paper", pos + new Vector3((float)(rng.NextDouble() - .5) * .1f, .005f + i * .006f, (float)(rng.NextDouble() - .5) * .1f), new Vector3(.21f, .004f, .3f), paper).transform.localRotation = Quaternion.Euler(0, (float)(rng.NextDouble() * 30 - 15), 0); }
        void Mug(Transform p, Vector3 pos) { Cyl(p, "Mug", pos + new Vector3(0, .05f, 0), .04f, .1f, ceramic); }
        void Phone(Transform p, Vector3 pos) { Box(p, "Phone", pos + new Vector3(0, .03f, 0), new Vector3(.2f, .06f, .18f), black); Box(p, "Handset", pos + new Vector3(0, .07f, 0), new Vector3(.22f, .035f, .06f), black); }

        void Plant(Transform p, Vector3 pos, float scale)
        {
            if (kit.Has("PottedPlant")) { kit.PlaceSkinned(p, "PottedPlant", pos, rng.Next(360), Vector3.one * scale * 1.05f, Skin(ceramic, leaf)); return; }
            Cyl(p, "Pot", pos + new Vector3(0, .18f * scale, 0), .22f * scale, .36f * scale, ceramic); Cyl(p, "Soil", pos + new Vector3(0, .36f * scale, 0), .2f * scale, .03f, soil);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad, h = (.6f + (i % 3) * .22f) * scale;
                var l = Ball(p, "Leaf", pos + new Vector3(Mathf.Sin(a) * .16f * scale, .36f * scale + h * .55f, Mathf.Cos(a) * .16f * scale), new Vector3(.14f, h, .06f) * scale, leaf);
                l.transform.localRotation = Quaternion.Euler(Mathf.Cos(a) * 24, i * 45, -Mathf.Sin(a) * 24);
            }
        }

        // ------------------------------------------------------------------ furniture
        void Reception(Transform p, float length, float depth)
        {
            if (kit.Has("DeskBody") && kit.Has("DeskTop"))
            {
                var s = new Vector3(length / 6.0f, 1f, depth / 1.3f);
                kit.Place(p, "DeskBody", new Vector3(0, 0, -depth * .12f), 180, s, wood); kit.Place(p, "DeskTop", new Vector3(0, 0, -depth * .12f), 180, s, black);
            }
            else { Box(p, "Body", new Vector3(0, .5f, 0), new Vector3(length, 1f, depth * .8f), wood); Box(p, "Top", new Vector3(0, 1.04f, .05f), new Vector3(length + .1f, .08f, depth), black); }
            Monitor(p, new Vector3(-length * .25f, 1.08f, -.05f)); Keyboard(p, new Vector3(-length * .25f, 1.09f, .22f)); Phone(p, new Vector3(length * .3f, 1.08f, 0)); Papers(p, new Vector3(length * .05f, 1.08f, 0), 3);
            Box(p, "Bell", new Vector3(length * .15f, 1.12f, .2f), new Vector3(.08f, .05f, .08f), chrome); Mug(p, new Vector3(-length * .05f, 1.08f, 0));
            Plant(p, new Vector3(length * .44f, 1.08f, -.05f), .4f);
        }

        void Sofa(Transform p, float length, float depth)
        {
            if (kit.Has("Sofa")) { kit.PlaceSkinned(p, "Sofa", Vector3.zero, 180, new Vector3(length / 1.9f, 1f, Mathf.Min(1.1f, depth / .85f)), Skin(fabricRed)); return; }
            Box(p, "Base", new Vector3(0, .22f, 0), new Vector3(length, .44f, depth * .9f), fabricRed); Box(p, "Back", new Vector3(0, .62f, -depth * .38f), new Vector3(length, .6f, depth * .24f), fabricRed);
        }
        void Cubicle(Transform p, float length, float depth)
        {
            // three desks side by side in a block of length 3 -> one desk per metre
            int n = Mathf.Max(1, Mathf.RoundToInt(length));
            for (int i = 0; i < n; i++)
            {
                float x = -length / 2 + .5f + i;
                var d = new GameObject("Cubicle").transform; d.SetParent(p, false); d.localPosition = new Vector3(x, 0, 0);
                Box(d, "Desk top", new Vector3(0, .74f, 0), new Vector3(.94f, .05f, depth * .86f), Pick(wood, darkWood)); Box(d, "Drawer unit", new Vector3(.3f, .36f, 0), new Vector3(.3f, .72f, depth * .8f), metal);
                Box(d, "Panel back", new Vector3(0, 1.0f, -depth * .45f), new Vector3(1.0f, 1.0f, .06f), Pick(fabricBlue, fabricGrey, fabricGreen)); Box(d, "Panel side", new Vector3(-.5f, 1.0f, 0), new Vector3(.06f, 1.0f, depth * .9f), Pick(fabricBlue, fabricGrey));
                Monitor(d, new Vector3(-.1f, .765f, -depth * .2f)); Keyboard(d, new Vector3(-.1f, .77f, depth * .1f)); Papers(d, new Vector3(.25f, .77f, depth * .22f), 2); Mug(d, new Vector3(-.4f, .765f, depth * .1f));
                Chair(d, new Vector3(-.05f, 0, depth * .62f), 180 + rng.Next(-25, 25), Pick(fabricBlue, fabricGrey, fabricGreen));
            }
        }

        void Counter(Transform p, float length, float depth)
        {
            Box(p, "Cabinets", new Vector3(0, .45f, 0), new Vector3(length, .9f, depth * .9f), wood); Box(p, "Worktop", new Vector3(0, .93f, 0), new Vector3(length + .04f, .05f, depth), marble);
            for (int i = 0; i < Mathf.RoundToInt(length); i++) { Box(p, "Door line", new Vector3(-length / 2 + .5f + i, .45f, depth * .455f), new Vector3(.01f, .8f, .01f), black); Box(p, "Handle", new Vector3(-length / 2 + .65f + i, .78f, depth * .46f), new Vector3(.12f, .02f, .02f), chrome); }
            Box(p, "Sink", new Vector3(-length * .2f, .92f, 0), new Vector3(.7f, .04f, .4f), steel); Cyl(p, "Tap", new Vector3(-length * .2f, 1.05f, -depth * .3f), .02f, .25f, chrome);
            if (kit.Has("Microwave")) kit.PlaceSkinned(p, "Microwave", new Vector3(length * .22f, .955f, 0), 180, Vector3.one, Skin(plastic));
            else Box(p, "Microwave", new Vector3(length * .22f, 1.08f, 0), new Vector3(.5f, .3f, .38f), plastic);
            if (kit.Has("CoffeeMaker")) kit.PlaceSkinned(p, "CoffeeMaker", new Vector3(length * .02f, .955f, 0), 180, Vector3.one, Skin(ceramic));
            else Box(p, "Coffee machine", new Vector3(length * .02f, 1.1f, 0), new Vector3(.24f, .34f, .26f), black);
        }
        void Fridge(Transform p, float length, float depth)
        {
            for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(length)); i++)
            {
                var pos = new Vector3(-length / 2 + .5f + i, 0, 0);
                if (kit.Has("Fridge")) { kit.PlaceSkinned(p, "Fridge", pos, 180, Vector3.one * 1.05f, Skin(ceramic, binderRed)); continue; }
                Box(p, "Fridge", pos + new Vector3(0, .95f, 0), new Vector3(.92f, 1.9f, depth * .9f), ceramic);
            }
        }
        void Table(Transform p, float length, float depth, int chairs)
        {
            Box(p, "Top", new Vector3(0, .74f, 0), new Vector3(length * .9f, .05f, depth * .9f), wood);
            for (int i = 0; i < 4; i++) Box(p, "Leg", new Vector3((i % 2 - .5f) * (length * .8f), .37f, (i / 2 - .5f) * (depth * .8f)), new Vector3(.06f, .74f, .06f), metal);
            for (int i = 0; i < chairs; i++) Chair(p, new Vector3(i % 2 == 0 ? -length * .25f : length * .25f, 0, i < 2 ? -depth * .62f : depth * .62f), i < 2 ? 0 : 180, fabricRed, false);
            Mug(p, new Vector3(0, .765f, 0)); Papers(p, new Vector3(.2f, .765f, .1f), 2);
        }

        void Shelves(Transform p, float length, float depth)
        {
            int units = Mathf.Max(1, Mathf.RoundToInt(length));
            for (int u = 0; u < units; u++)
            {
                float cx = -length / 2 + .5f + u;
                if (kit.Has("ShelfUnit")) kit.PlaceSkinned(p, "ShelfUnit", new Vector3(cx, 0, 0), 180, new Vector3(.94f, 1f, Mathf.Min(1.1f, depth / .42f)), Skin(metal));
                for (int l = 0; l < 5; l++)
                {
                    float y = .14f + l * .4f; float x = cx - .43f;
                    while (x < cx + .3f)
                    {
                        float w = .12f + (float)rng.NextDouble() * .2f, hh = .2f + (float)rng.NextDouble() * .15f; bool box = rng.Next(3) == 0;
                        Box(p, box ? "Box" : "Binder", new Vector3(x + w / 2, y + hh / 2, -.02f), new Vector3(w, hh, .28f), box ? cardboard : Pick(binderRed, binderBlue, binderYellow, paper)); x += w + .025f;
                        if (rng.Next(7) == 0) x += .12f;
                    }
                }
            }
        }
        void Planter(Transform p, float length, float depth)
        {
            Box(p, "Planter", new Vector3(0, .25f, 0), new Vector3(length * .95f, .5f, depth * .95f), marble); Box(p, "Soil", new Vector3(0, .52f, 0), new Vector3(length * .85f, .04f, depth * .85f), soil);
            for (int i = 0; i < 6; i++) Plant(p, new Vector3(-length * .35f + i * length * .14f, .5f, ((i % 2) - .5f) * depth * .4f), 1.3f);
        }

        void Conference(Transform p, float length, float depth)
        {
            Box(p, "Top", new Vector3(0, .75f, 0), new Vector3(length, .08f, depth * .9f), darkWood); Box(p, "Base", new Vector3(0, .38f, 0), new Vector3(length * .7f, .74f, depth * .3f), darkWood);
            for (int i = 0; i < 5; i++) { float x = -length / 2 + .5f + i * (length - 1f) / 4f; Chair(p, new Vector3(x, 0, -depth * .62f), 0, fabricGrey, false); Chair(p, new Vector3(x, 0, depth * .62f), 180, fabricGrey, false); }
            Box(p, "Speakerphone", new Vector3(0, .81f, 0), new Vector3(.3f, .04f, .3f), black); for (int i = 0; i < 3; i++) Papers(p, new Vector3(-length * .3f + i * length * .3f, .8f, depth * .25f), 2);
        }

        void DeskWithMonitor(Transform p, float length, float depth, bool nameplate)
        {
            Box(p, "Top", new Vector3(0, .74f, 0), new Vector3(length, .05f, depth * .9f), wood);
            for (int i = 0; i < 2; i++) Box(p, "Pedestal", new Vector3(-length / 2 + .25f + i * (length - .5f), .36f, 0), new Vector3(.4f, .72f, depth * .85f), wood);
            Monitor(p, new Vector3(0, .765f, -depth * .2f)); Keyboard(p, new Vector3(0, .77f, depth * .15f)); Papers(p, new Vector3(length * .3f, .77f, 0), 4); Mug(p, new Vector3(-length * .3f, .765f, 0)); Phone(p, new Vector3(length * .42f, .765f, depth * .2f));
            if (kit.Has("DeskLamp")) kit.PlaceSkinned(p, "DeskLamp", new Vector3(length * .36f, .765f, -depth * .2f), 200, Vector3.one, Skin(binderYellow));
            if (nameplate) Box(p, "Nameplate", new Vector3(-length * .15f, .79f, depth * .3f), new Vector3(.22f, .04f, .06f), chrome);
            Chair(p, new Vector3(0, 0, depth * .75f), 180, fabricBlue);
        }

        void Filing(Transform p, float length, float depth)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(length * 2f));
            for (int i = 0; i < n; i++)
            {
                var pos = new Vector3(-length / 2 + .25f + i * .5f, 0, 0);
                if (kit.Has("FilingCabinet")) kit.PlaceSkinned(p, "FilingCabinet", pos, 180, Vector3.one, Skin(metal));
                else Box(p, "Cabinet", pos + new Vector3(0, .65f, 0), new Vector3(.46f, 1.3f, .62f), metal);
            }
        }
        void Rack(Transform p, float length, float depth)
        {
            int n = Mathf.Max(1, Mathf.FloorToInt(length / .64f));
            for (int i = 0; i < n; i++)
            {
                var pos = new Vector3(-length / 2 + .32f + i * (length - .64f) / Mathf.Max(1, n - 1), 0, 0);
                if (kit.Has("ServerRack")) { kit.PlaceSkinned(p, "ServerRack", pos, 180, Vector3.one, Skin(black, binderYellow)); continue; }
                Box(p, "Rack", pos + new Vector3(0, 1f, 0), new Vector3(.6f, 2f, depth * .9f), black);
            }
        }
        void WaitingChairs(Transform p, float length, float depth)
        {
            for (int i = 0; i < Mathf.Max(2, Mathf.RoundToInt(length)); i++)
            {
                var pos = new Vector3(-length / 2 + .5f + i, 0, 0);
                if (kit.Has("ConferenceChair")) kit.PlaceSkinned(p, "ConferenceChair", pos, 180, Vector3.one, Skin(fabricRed)); else Chair(p, pos, 0, fabricRed);
            }
            Box(p, "Side table", new Vector3(length / 2 + .25f, .3f, 0), new Vector3(.4f, .6f, .4f), wood); Papers(p, new Vector3(length / 2 + .25f, .62f, 0), 2);
        }
    }
}
