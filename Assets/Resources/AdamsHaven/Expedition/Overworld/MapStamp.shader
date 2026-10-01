// Scattered map stamps (canopy, undergrowth, rocks, props). Canopy thins out around the party so they
// stay visible under the trees. uv2 = the stamp's foot point (where it meets the ground), uv3.x = 1 if it may fade.
Shader "AdamsHaven/MapStamp"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Party ("Party position (xy) and fade radius (z, w)", Vector) = (0, 0, 1.2, 3.2)
        _Target ("Preview target (xy) and fade radius (z, w)", Vector) = (-99, -99, 0.8, 2.2)
        _FadeTo ("Faded alpha", Float) = 0.25
        _Road ("Worn road", 2D) = "black" {}
        _Map ("Map origin (xy) and size (zw)", Vector) = (0, 0, 64, 40)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _Road;
            float4 _Party, _Target, _Map;
            float _FadeTo;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float2 foot : TEXCOORD1; float2 flags : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 foot : TEXCOORD1; float cut : TEXCOORD2; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color;
                o.foot = float3(mul(unity_ObjectToWorld, float4(v.foot, 0, 1)).xy, v.flags.x);
                // Trees and brush standing on walked trail are cleared away (rocks stay: flags.y = 0).
                float road = tex2Dlod(_Road, float4((o.foot.xy - _Map.xy) / _Map.zw, 0, 0)).r;
                o.cut = v.flags.y * smoothstep(0.25, 0.45, road);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.a *= 1 - i.cut;
                if (i.foot.z > 0.5)
                {
                    float fade = smoothstep(_Party.z, _Party.w, distance(i.foot.xy, _Party.xy));
                    fade = min(fade, smoothstep(_Target.z, _Target.w, distance(i.foot.xy, _Target.xy)));
                    c.a *= lerp(_FadeTo, 1, fade);
                }
                return c;
            }
            ENDCG
        }
    }
}
