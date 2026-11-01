Shader "Custom/SpriteOutlineURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _OutlineColor ("Outline Color", Color) = (2.2, 0.7, 0.08, 1.0)
        _OutlineThickness ("Outline Thickness", Range(0, 10)) = 2.5
        _OutlinePulseSpeed ("Pulse Speed", Float) = 12.0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteOutline"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineThickness;
                float _OutlinePulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 mainTexColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 col = mainTexColor * input.color;

                if (col.a > 0.08)
                {
                    // Тело спрайта с легким огненным оттенком
                    return col;
                }

                // Проверка соседних пикселей для рисования контура
                float2 offset = _OutlineThickness * _MainTex_TexelSize.xy;
                float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset.x, 0)).a;
                float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(offset.x, 0)).a;
                float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, offset.y)).a;
                float a4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, offset.y)).a;

                float a5 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset.x, offset.y) * 0.707).a;
                float a6 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(-offset.x, offset.y) * 0.707).a;
                float a7 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(offset.x, -offset.y) * 0.707).a;
                float a8 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(offset.x, offset.y) * 0.707).a;

                float maxA = max(max(max(a1, a2), max(a3, a4)), max(max(a5, a6), max(a7, a8)));

                if (maxA > 0.08)
                {
                    float pulse = 1.0 + sin(_Time.y * _OutlinePulseSpeed) * 0.3;
                    float4 fireOutline = _OutlineColor * pulse;
                    fireOutline.a = saturate(maxA * _OutlineColor.a);
                    return fireOutline;
                }

                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }
}
