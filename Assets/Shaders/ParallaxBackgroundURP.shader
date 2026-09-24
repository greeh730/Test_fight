Shader "Custom/ParallaxBackgroundURP"
{
    Properties
    {
        _MainTex ("Background Texture", 2D) = "white" {}
        _WallTint ("Wall Tint", Color) = (0.95, 0.95, 1.0, 1.0)
        _VoidTopColor ("Top Void / Sky Color", Color) = (0.09, 0.09, 0.14, 1.0)
        _VoidBottomColor ("Bottom Void / Abyss Color", Color) = (0.02, 0.02, 0.04, 1.0)
        _TileScale ("Tile Scale (X: Tiling, Y: Height Scale)", Vector) = (1.2, 1.0, 0, 0)
        _Offset ("Parallax Offset", Vector) = (0, 0, 0, 0)
        _BottomFadeHeight ("Bottom Fade In Height (Screen %)", Range(0, 0.7)) = 0.35
        _TopFadeHeight ("Top Fade Out Height (Screen %)", Range(0, 0.7)) = 0.35
        _SideFadeWidth ("Side Fade Width (Screen %)", Range(0, 0.4)) = 0.10
        _FadeSmoothness ("Fade Smoothness Power", Range(0.5, 3.0)) = 1.0
        _VignetteStrength ("Screen Vignette Strength", Range(0, 1)) = 0.30
        _AmbientPulse ("Ambient Pulse Speed", Float) = 0.5
        _AmbientPulseIntensity ("Ambient Pulse Intensity", Range(0, 0.2)) = 0.03
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
                float4 screenPos    : TEXCOORD1;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            CBUFFER_START(UnityPerMaterial)
                float4 _WallTint;
                float4 _VoidTopColor;
                float4 _VoidBottomColor;
                float4 _TileScale;
                float4 _Offset;
                float _BottomFadeHeight;
                float _TopFadeHeight;
                float _SideFadeWidth;
                float _FadeSmoothness;
                float _VignetteStrength;
                float _AmbientPulse;
                float _AmbientPulseIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Точные экранные координаты вьюпорта (0..1 от нижнего левого до верхнего правого пикселя экрана)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // 2. Вычисление UV стены фона с учетом смещения параллакса
                float2 wallUV = input.uv * _TileScale.xy + _Offset.xy;

                // 3. Семплирование спрайта Background_2 (горизонтальный тайлинг без артефактов)
                half4 wallTex = _MainTex.Sample(sampler_MainTex, wallUV);
                half3 wallCol = wallTex.rgb * _WallTint.rgb;

                // 4. Вертикальное экранное затемнение краев ("по краям fade in и fade out по высоте")
                // Плавное появление снизу (Fade In из глубокой тьмы бездны)
                float bottomFade = smoothstep(0.0, _BottomFadeHeight, screenUV.y);
                
                // Плавное затухание сверху (Fade Out в темноту пещеры)
                float topFade = smoothstep(1.0, 1.0 - _TopFadeHeight, screenUV.y);
                
                float vertFade = saturate(bottomFade * topFade);
                if (_FadeSmoothness != 1.0)
                {
                    vertFade = pow(vertFade, _FadeSmoothness);
                }

                // 5. Боковое мягкое затемнение по границам экрана
                float leftFade = smoothstep(0.0, _SideFadeWidth, screenUV.x);
                float rightFade = smoothstep(1.0, 1.0 - _SideFadeWidth, screenUV.x);
                float sideFade = saturate(leftFade * rightFade);

                float totalFade = vertFade * sideFade;

                // 6. Цвет окружающего градиента бездны
                half3 voidColor = lerp(_VoidBottomColor.rgb, _VoidTopColor.rgb, saturate(screenUV.y));

                // 7. Плавное смешивание стены с тьмой по краям (Fade In снизу / Fade Out сверху)
                half3 finalColor = lerp(voidColor, wallCol, totalFade);

                // 8. Мягкое живое дыхание атмосферы бездны
                float pulse = 1.0 + sin(_Time.y * _AmbientPulse) * _AmbientPulseIntensity;
                finalColor *= pulse;

                // 9. Кинематографичная мягкая радиальная виньетка
                float2 vigCoord = screenUV - 0.5;
                float vigDist = length(vigCoord * float2(1.0, 1.25));
                float vignette = 1.0 - smoothstep(0.35, 0.8, vigDist) * _VignetteStrength;
                finalColor *= vignette;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
