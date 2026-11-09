using UnityEngine;
using UnityEngine.UI;

/// <summary>Editable vector UI shapes. No textures, runtime GameObjects or layout writes.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PopHomeGraphic : MaskableGraphic
{
    public enum Kind { Rounded, Circle, Play, Chevron, Lock, Gear, Calendar, Palette, Blocks, Speaker, Haptics, Crown, Pause, Close, Rows, Home }
    [SerializeField] private Kind shape;
    [SerializeField, Min(0)] private float cornerRadius = 35;
    [SerializeField, Range(.01f,.15f)] private float stroke = .045f;
    [SerializeField] private Color bottomTint = Color.white;
    [SerializeField] private bool verticalGradient;
    public Kind Shape { get => shape; set { shape = value; SetVerticesDirty(); } }
    public float Radius { get => cornerRadius; set { cornerRadius = value; SetVerticesDirty(); } }
    public Color BottomTint { get => bottomTint; set { bottomTint = value; verticalGradient=true; SetVerticesDirty(); } }
    private VertexHelper mesh;
    private Rect box;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        mesh = vh; mesh.Clear(); box = rectTransform.rect;
        switch (shape)
        {
            case Kind.Rounded: FillRounded(); break;
            case Kind.Circle: Disc(Vector2.zero, .5f); break;
            case Kind.Play: Triangle(new(-.22f,-.34f),new(.35f,0),new(-.22f,.34f)); break;
            case Kind.Chevron: Line(new(-.15f,-.3f),new(.16f,0)); Line(new(.16f,0),new(-.15f,.3f)); break;
            case Kind.Lock: Outline(-.29f,-.35f,.29f,.12f); Arc(new(0,.1f),.21f,0,180); Line(new(-.21f,.1f),new(-.21f,.23f)); Line(new(.21f,.1f),new(.21f,.23f)); Disc(new(0,-.1f),.04f); break;
            case Kind.Gear:
                for(int i=0;i<32;i++) { float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16; float r=i%4<2?.43f:.34f,s=(i+1)%4<2?.43f:.34f; Line(new(Mathf.Cos(a)*r,Mathf.Sin(a)*r),new(Mathf.Cos(b)*s,Mathf.Sin(b)*s)); }
                Arc(Vector2.zero,.14f,0,360); break;
            case Kind.Calendar: Outline(-.36f,-.38f,.36f,.28f); Line(new(-.36f,.08f),new(.36f,.08f)); Line(new(-.19f,.18f),new(-.19f,.42f)); Line(new(.19f,.18f),new(.19f,.42f)); for(int i=0;i<6;i++)Disc(new((i%3-1)*.19f,-.1f-i/3*.17f),.025f); break;
            case Kind.Palette: Arc(Vector2.zero,.38f,0,360); for(int i=0;i<4;i++){float a=(45+i*75)*Mathf.Deg2Rad;Disc(new(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.24f),.045f);} break;
            case Kind.Blocks: Outline(-.36f,-.36f,-.03f,-.03f); Outline(.03f,-.36f,.36f,-.03f); Outline(-.36f,.03f,-.03f,.36f); break;
            case Kind.Speaker: Line(new(-.32f,-.14f),new(-.32f,.14f)); Line(new(-.32f,.14f),new(-.1f,.14f)); Line(new(-.1f,.14f),new(.1f,.33f)); Line(new(.1f,.33f),new(.1f,-.33f)); Line(new(.1f,-.33f),new(-.1f,-.14f)); Line(new(-.1f,-.14f),new(-.32f,-.14f)); Arc(new(.1f,0),.27f,-60,60); break;
            case Kind.Haptics: Outline(-.18f,-.35f,.18f,.35f); Line(new(-.36f,-.2f),new(-.4f,0)); Line(new(-.4f,0),new(-.36f,.2f)); Line(new(.36f,-.2f),new(.4f,0)); Line(new(.4f,0),new(.36f,.2f)); break;
            case Kind.Crown:
                Vector2[] crown = { new(-.36f,-.18f), new(-.43f,.25f), new(-.16f,.06f), new(0,.38f), new(.16f,.06f), new(.43f,.25f), new(.36f,-.18f) };
                for(int i=0;i<crown.Length;i++) Triangle(new(0,-.14f),crown[i],crown[(i+1)%crown.Length]);
                FillBox(-.33f,-.34f,.33f,-.23f); break;
            case Kind.Pause: FillBox(-.25f,-.33f,-.08f,.33f); FillBox(.08f,-.33f,.25f,.33f); break;
            case Kind.Close: Line(new(-.27f,-.27f),new(.27f,.27f)); Line(new(-.27f,.27f),new(.27f,-.27f)); break;
            case Kind.Rows: for(int i=0;i<3;i++) FillBox(-.35f,-.30f+i*.24f,.35f,-.17f+i*.24f); break;
            case Kind.Home: Triangle(new(-.42f,.03f),new(0,.42f),new(.42f,.03f)); FillBox(-.29f,-.36f,-.06f,.05f); FillBox(.06f,-.36f,.29f,.05f); FillBox(-.29f,-.10f,.29f,.05f); break;
        }
    }
    private Vector2 Point(Vector2 p) => box.center + Vector2.Scale(p,box.size);
    private void Add(Vector2 p) { float y=Mathf.InverseLerp(box.yMin,box.yMax,p.y); mesh.AddVert(p,(Color32)(verticalGradient?Color.Lerp(bottomTint,color,y):color),Vector2.zero); }
    private void Triangle(Vector2 a,Vector2 b,Vector2 c) { int n=mesh.currentVertCount;Add(Point(a));Add(Point(b));Add(Point(c));mesh.AddTriangle(n,n+1,n+2); }
    private void Line(Vector2 a,Vector2 b)
    {
        Vector2 p=Point(a),q=Point(b),normal=new Vector2(-(q-p).y,(q-p).x).normalized*Mathf.Min(box.width,box.height)*stroke*.5f;
        int n=mesh.currentVertCount;Add(p-normal);Add(p+normal);Add(q+normal);Add(q-normal);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);
    }
    private void Outline(float l,float b,float r,float t) { Line(new(l,b),new(l,t));Line(new(l,t),new(r,t));Line(new(r,t),new(r,b));Line(new(r,b),new(l,b)); }
    private void FillBox(float l,float b,float r,float t) { Triangle(new(l,b),new(l,t),new(r,t)); Triangle(new(l,b),new(r,t),new(r,b)); }
    private void Arc(Vector2 center,float radius,float from,float to)
    { int count=Mathf.Max(8,Mathf.CeilToInt((to-from)/12));for(int i=0;i<count;i++){float a=Mathf.Lerp(from,to,i/(float)count)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1f)/count)*Mathf.Deg2Rad;Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);} }
    private void Disc(Vector2 center,float radius)
    { for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Triangle(center,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius);} }
    private void FillRounded()
    {
        float r=Mathf.Min(cornerRadius,Mathf.Min(box.width,box.height)*.5f);Add(box.center);
        for(int corner=0;corner<4;corner++)for(int step=0;step<=8;step++)
        { float angle=(corner*90+step*90f/8)*Mathf.Deg2Rad;Vector2 center=new(corner==0||corner==3?box.xMax-r:box.xMin+r,corner<2?box.yMax-r:box.yMin+r);Add(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r); }
        for(int i=1;i<mesh.currentVertCount;i++)mesh.AddTriangle(0,i,i==mesh.currentVertCount-1?1:i+1);
    }
}
