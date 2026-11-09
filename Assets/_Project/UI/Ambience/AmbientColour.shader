Shader "Blokki Pop/Ambient Colour"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite geometry only", 2D) = "white" {}
        _Top ("Top", Color) = (.25,.25,.45,1)
        _Bottom ("Bottom", Color) = (.45,.4,.6,1)
        _Glow ("Gentle light", Color) = (.6,.7,1,1)
        _GlowStrength ("Light strength", Range(0,.15)) = .035
        _AmbientTime ("Paused game time", Float) = 0
        _Reveal ("Radial refresh", Range(0,1)) = 1
        _Aspect ("Surface aspect", Float) = 1
        _RingWidth ("Refresh ring width", Float) = .045
        _RingGlow ("Refresh ring glow", Float) = .9
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            // Untagged = Always in Built-in and SRPDefaultUnlit in URP.
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Top, _Bottom, _Glow;
            float _GlowStrength, _AmbientTime;
            float _Reveal, _Aspect, _RingWidth, _RingGlow;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float t=smoothstep(0,1,i.uv.y);
                float light=pow(saturate(.5+.5*sin(i.uv.x*3+i.uv.y*4+_AmbientTime*.12)),3);
                fixed3 colour=lerp(_Bottom.rgb,_Top.rgb,t)+_Glow.rgb*light*_GlowStrength;
                float2 delta=(i.uv-.5)*float2(_Aspect,1);
                float extent=length(float2(_Aspect,1))*.5;
                float radius=lerp(-_RingWidth,extent+_RingWidth,saturate(_Reveal));
                float distance=length(delta);
                float cover=1-smoothstep(radius-_RingWidth,radius+_RingWidth,distance);
                float ring=(1-smoothstep(0,_RingWidth,abs(distance-radius)))*step(.001,_Reveal)*(1-step(.999,_Reveal));
                colour+=_Glow.rgb*ring*_RingGlow;
                return fixed4(colour*i.color.rgb,i.color.a*cover);
            }
            ENDCG
        }
    }
}
