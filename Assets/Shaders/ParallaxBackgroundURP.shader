Shader "Custom/ParallaxBackgroundURP"
{
    Properties
    {
        _MainTex ("Background Texture", 2D) = "white" {}
        _FarTint ("Far Layer Tint", Color) = (0.05, 0.08, 0.15, 1.0)
        _MidTint ("Mid Layer Tint", Color) = (0.12, 0.18, 0.28, 0.8)
        _BottomTint ("Abyss Void Color (Bottom)", Color) = (0.02, 0.03, 0.06, 1.0)
        _TopTint ("Cavern Sky Color (Top)", Color) = (0.08, 0.13, 0.22, 1.0)
        _TileScale1 ("Far Layer Scale", Vector) = (2.5, 2.5, 0, 0)
        _TileScale2 ("Mid Layer Scale", Vector) = (4.0, 4.0, 0, 0)
        _Offset1 ("Far Layer Offset", Vector) = (0, 0, 0, 0)
        _Offset2 ("Mid Layer Offset", Vector) = (0, 0, 0, 0)
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.45
        _AmbientPulse ("Ambient Pulse Speed", Float) = 0.8
        _AmbientPulseIntensity ("Ambient Pulse Intensity", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Background" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Pass
        {
            Name "Unlit"
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _FarTint;
                float4 _MidTint;
                float4 _BottomTint;
                float4 _TopTint;
                float4 _TileScale1;
                float4 _TileScale2;
                float4 _Offset1;
                float4 _Offset2;
                float _VignetteStrength;
                float _AmbientPulse;
                float _AmbientPulseIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Far background layer sampling
                float2 farUV = input.uv * _TileScale1.xy + _Offset1.xy;
                half4 farTex = _MainTex.Sample(sampler_MainTex, farUV);
                half3 farCol = farTex.rgb * _FarTint.rgb;

                // 2. Mid background layer sampling (scaled and shifted)
                float2 midUV = input.uv * _TileScale2.xy + _Offset2.xy;
                half4 midTex = _MainTex.Sample(sampler_MainTex, midUV);
                half3 midCol = midTex.rgb * _MidTint.rgb;

                // 3. Multi-layer composite (mid layer adds subtle architectural detail onto far layer)
                half3 finalColor = farCol + midCol * _MidTint.a;

                // 4. Vertical atmospheric gradient (fade into deep abyss void at bottom, soft haze towards top)
                half3 vertAtmosphere = lerp(_BottomTint.rgb, _TopTint.rgb, saturate(input.uv.y));
                finalColor *= (vertAtmosphere * 2.0);

                // 5. Breathing ambient pulse
                float pulse = 1.0 + sin(_Time.y * _AmbientPulse) * _AmbientPulseIntensity;
                finalColor *= pulse;

                // 6. Cinematic radial vignette around screen borders
                float2 vigCoord = input.uv - 0.5;
                float vigDist = length(vigCoord * float2(1.0, 1.2)); // slightly taller vignette
                float vignette = 1.0 - smoothstep(0.35, 0.75, vigDist) * _VignetteStrength;
                finalColor *= vignette;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
