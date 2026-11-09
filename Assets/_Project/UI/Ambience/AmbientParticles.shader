Shader "Blokki Pop/Ambient Particles"
{
    Properties { _Shape ("0 Snow, 1 Star, 2 Firefly", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            // Untagged pass supports both the project's Built-in renderer and URP.
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Shape;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=v.uv*2-1; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=abs(i.uv); float r=length(p);
                float alpha;
                if(_Shape < .5) // Soft snowflake with six fine arms, not a square texture.
                {
                    float a=atan2(i.uv.y,i.uv.x);
                    float arm=pow(abs(cos(a*3)),18)*saturate(1-r);
                    alpha=max(smoothstep(.9,.05,r)*.6,arm)*smoothstep(1,.7,r);
                }
                else if(_Shape < 1.5)
                    alpha=max(pow(saturate(1-r),4),pow(saturate(1-min(p.x,p.y)*13),2)*pow(saturate(1-max(p.x,p.y)),2));
                else alpha=pow(saturate(1-r),2.5);
                return fixed4(i.color.rgb, i.color.a*alpha);
            }
            ENDCG
        }
    }
}
