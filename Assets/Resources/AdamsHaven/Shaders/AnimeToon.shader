// Anime cel look for the battle chibi rigs: flat two-tone shading from a fixed key direction,
// a soft rim, and an inverted-hull ink outline. Scene lights are ignored on purpose so every
// offscreen portrait rig reads the same as the painted 2D art.
Shader "AdamsHaven/AnimeToon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _ShadeColor ("Shadow tint", Color) = (0.72,0.64,0.78,1)
        _KeyDir ("Key light direction (view space, +z toward camera)", Vector) = (-0.4,0.5,0.75,0)
        _ShadeStep ("Shadow threshold", Range(-1,1)) = 0.05
        _ShadeSoft ("Shadow edge softness", Range(0.001,0.3)) = 0.03
        _RimColor ("Rim color", Color) = (1,0.95,0.9,1)
        _RimPower ("Rim power", Range(1,12)) = 5
        _RimStrength ("Rim strength", Range(0,1)) = 0.25
        _Saturation ("Saturation", Range(0,2)) = 1.05
        _OutlineColor ("Outline color", Color) = (0.12,0.08,0.1,1)
        _OutlineWidth ("Outline width", Range(0,0.02)) = 0.003
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _ShadeColor, _KeyDir, _RimColor, _OutlineColor;
            float _ShadeStep, _ShadeSoft, _RimPower, _RimStrength, _Saturation, _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Toon"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 nv : TEXCOORD1; float3 vv : TEXCOORD2; };
            V vert(A a)
            {
                V o;
                float3 ws = TransformObjectToWorld(a.pos.xyz);
                o.pos = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.nv = mul((float3x3)UNITY_MATRIX_V, TransformObjectToWorldNormal(a.n));
                o.vv = TransformWorldToView(ws);
                return o;
            }
            half4 frag(V i, bool front : SV_IsFrontFace) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.nv) * (front ? 1 : -1);
                float ndl = dot(n, normalize(_KeyDir.xyz));
                float lit = smoothstep(_ShadeStep - _ShadeSoft, _ShadeStep + _ShadeSoft, ndl);
                half3 col = c.rgb * lerp(_ShadeColor.rgb, 1, lit);
                float rim = pow(saturate(1 - abs(dot(n, normalize(-i.vv)))), _RimPower) * _RimStrength * lit;
                col += _RimColor.rgb * rim;
                float g = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(g.xxx, col, _Saturation);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert(A a)
            {
                V o;
                float3 ws = TransformObjectToWorld(a.pos.xyz);
                float3 nw = normalize(TransformObjectToWorldNormal(a.n));
                // Width in world units scaled by distance keeps the line roughly constant on screen.
                float d = length(GetCameraPositionWS() - ws);
                float w = _OutlineWidth * (unity_OrthoParams.w > 0.5 ? unity_OrthoParams.y * 2 : d);
                o.pos = TransformWorldToHClip(ws + nw * w);
                return o;
            }
            half4 frag(V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
}
