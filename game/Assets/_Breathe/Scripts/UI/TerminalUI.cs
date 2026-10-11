using System;
using UnityEngine;
using UnityEngine.UI;

namespace Breathe
{
    /// <summary>Resolution-independent menus and first-person HUD.</summary>
    public sealed class TerminalUI
    {
        public static readonly Color Ink = Hex("15234B"), Paper = Hex("F4F0CB"), Muted = Hex("62636E"), Teal = Hex("2E60B5"), Red = Hex("DC3B43"), Night = Hex("190C13"), Pale = Hex("F1EACB");
        readonly Font font;
        /// <summary>Pixel fonts assigned from GameAssets at startup (Silkscreen Bold for headings, VT323 for body). Null means the built-in font.</summary>
        public static Font HeadFont, BodyFont;
        const float HeadScale = .74f, BodyScale = 1.38f;
        readonly Sprite disc;
        readonly Image matte;
        readonly RectTransform[] bars=new RectTransform[4];
        public readonly RectTransform Root;
        /// <summary>Widget kit only (no screen overlay): used to draw text and buttons on world-space canvases mounted on 3D props.</summary>
        public static TerminalUI Kit() => new TerminalUI();
        TerminalUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            disc = MakeDisc();
        }
        static Sprite MakeDisc()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Generated disc" };
            var pixels = new Color[64*64];
            for (int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x] = new Color(1,1,1,Mathf.Clamp01(31f-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))));
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0,0,64,64), new Vector2(.5f,.5f));
        }
        public TerminalUI(Transform parent)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            texture.name = "Generated 2D circle";
            var pixels = new Color[64*64];
            for (int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x] = new Color(1,1,1,Mathf.Clamp01(31f-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))));
            texture.SetPixels(pixels); texture.Apply();
            disc = Sprite.Create(texture, new Rect(0,0,64,64), new Vector2(.5f,.5f));
            var go = new GameObject("BREATHE 2D interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            // Keep the authored 16:9 frame centered and fully visible, including narrow windows.
            var matteObject=new GameObject("Window matte",typeof(RectTransform),typeof(Image));
            matte=matteObject.GetComponent<Image>();
            var matteRect=matteObject.GetComponent<RectTransform>(); matteRect.SetParent(go.transform,false);
            matteRect.anchorMin=Vector2.zero; matteRect.anchorMax=Vector2.one;
            matteRect.offsetMin=matteRect.offsetMax=Vector2.zero;
            matte.color=Hex("101019"); matte.raycastTarget=false;
            for(int i=0;i<4;i++)
            {
                var bar=new GameObject("Letterbox "+i,typeof(RectTransform),typeof(Image));
                bars[i]=bar.GetComponent<RectTransform>(); bars[i].SetParent(go.transform,false);
                bar.GetComponent<Image>().color=Hex("101019"); bar.GetComponent<Image>().raycastTarget=false;
            }
            var frame=new GameObject("Safe frame",typeof(RectTransform));
            Root=frame.GetComponent<RectTransform>(); Root.SetParent(go.transform,false);
            Root.anchorMin=Root.anchorMax=Root.pivot=new Vector2(.5f,.5f);
            Root.sizeDelta=new Vector2(1600,900); Root.anchoredPosition=Vector2.zero;
        }
        /// <summary>Test hook: render full-frame screenshots without the letterbox.</summary>
        public static Rect? ViewportOverride;
        public static Rect Viewport(float width=0,float height=0)
        {
            if(ViewportOverride.HasValue) return ViewportOverride.Value;
            float aspect=(width>0?width:Screen.width)/Mathf.Max(1,height>0?height:Screen.height), frame=16f/9f;
            if(aspect>frame) { float w=frame/aspect; return new Rect((1-w)/2,0,w,1); }
            float h=aspect/frame; return new Rect(0,(1-h)/2,1,h);
        }
        public void SetWorldVisible(bool visible)
        {
            matte.enabled=!visible;
            var bounds=Root.GetComponentInParent<Canvas>().pixelRect;
            var view=Viewport(bounds.width,bounds.height);
            Vector2[] min={Vector2.zero,new Vector2(view.xMax,0),new Vector2(view.xMin,0),new Vector2(view.xMin,view.yMax)};
            Vector2[] max={new Vector2(view.xMin,1),Vector2.one,new Vector2(view.xMax,view.yMin),new Vector2(view.xMax,1)};
            for(int i=0;i<4;i++) { bars[i].anchorMin=min[i]; bars[i].anchorMax=max[i]; bars[i].offsetMin=bars[i].offsetMax=Vector2.zero; }
        }
        public void Clear()
        {
            for(int i=Root.childCount-1;i>=0;i--) { var obj=Root.GetChild(i).gameObject; obj.SetActive(false); UnityEngine.Object.Destroy(obj); }
        }
        public void Dispose() { if (disc) { UnityEngine.Object.Destroy(disc.texture); UnityEngine.Object.Destroy(disc); } }
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value, out var c); return c; }
        public static RectTransform Position(GameObject go, Transform parent, float x,float y,float w,float h)
        {
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0,1);
            rt.anchoredPosition = new Vector2(x,-y); rt.sizeDelta = new Vector2(w,h); return rt;
        }
        public Image Box(float x,float y,float w,float h,Color color, Transform parent=null, string name="Panel")
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image)); Position(go,parent??Root,x,y,w,h);
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return image;
        }
        public Image Circle(float x,float y,float size,Color color,Transform parent=null)
        { var image=Box(x,y,size,size,color,parent,"Circle"); image.sprite=disc; return image; }
        public Text Label(string value,float x,float y,float w,float h,int size,Color color,bool bold=false,Transform parent=null,TextAnchor align=TextAnchor.UpperLeft)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text)); Position(go,parent??Root,x,y,w,h);
            var text=go.GetComponent<Text>(); var chosen=bold?HeadFont:BodyFont; bool pixel=chosen!=null;
            text.font=pixel?chosen:font; text.text=value; text.color=color;
            text.fontSize=pixel?Mathf.Max(8,Mathf.RoundToInt(size*(bold?HeadScale:BodyScale))):size; text.fontStyle=bold&&!pixel?FontStyle.Bold:FontStyle.Normal;
            text.resizeTextForBestFit=true; text.resizeTextMinSize=Mathf.Max(8,Mathf.RoundToInt(text.fontSize*.5f)); text.resizeTextMaxSize=text.fontSize;
            text.alignment=align; text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate; text.raycastTarget=false;
            text.supportRichText=false; return text;
        }
        public Button Button(string value,float x,float y,float w,float h,Action click,bool primary=false,bool dark=false,Transform parent=null,int fontSize=21)
        {
            var image=Box(x,y,w,h,primary?Teal:dark?Hex("253638"):Hex("DDDCCF"),parent,"Button: "+value);
            image.raycastTarget=true;
            Box(0,0,w,3,primary?Hex("91B7F1"):Hex("FFFDE8"),image.transform);
            Box(0,0,3,h,primary?Hex("91B7F1"):Hex("FFFDE8"),image.transform);
            Box(0,h-3,w,3,Hex("3D3B46"),image.transform);
            Box(w-3,0,3,h,Hex("3D3B46"),image.transform);
            var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            var colors=button.colors; colors.highlightedColor=new Color(.86f,.92f,.88f); colors.pressedColor=new Color(.7f,.78f,.72f); button.colors=colors;
            Label(value,18,0,w-36,h,fontSize,primary||dark?Paper:Ink,true,image.transform,TextAnchor.MiddleLeft);
            button.onClick.AddListener(()=>click()); return button;
        }
        public void Rule(float x,float y,float w,Color color) => Box(x,y,w,1,color);
    }
}
