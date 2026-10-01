Shader "UI/SilverbrookDungeonMist"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mist artwork", 2D) = "white" {}
        _Discovery ("Discovery", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };
            sampler2D _MainTex, _Discovery;
            fixed4 _Color;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.worldPosition = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.color = v.color * _Color; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float discovery = tex2D(_Discovery, i.uv).r;
                float2 drift = float2(sin(_Time.y * 0.035), cos(_Time.y * 0.027)) * 0.022;
                fixed3 cloud = lerp(tex2D(_MainTex, i.uv).rgb, tex2D(_MainTex, saturate(i.uv + drift)).rgb, 0.45);
                // Unknown cells stay fully opaque; atmospheric animation never leaks hidden topology.
                float alpha = lerp(1, 0.28, smoothstep(0, 0.5, discovery)) * (1 - smoothstep(0.5, 1, discovery));
                fixed4 color = fixed4(cloud * lerp(0.38, 0.25, discovery), alpha) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
