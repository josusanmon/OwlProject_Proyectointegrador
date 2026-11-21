// Tonemapping AgX para URP (lo usa AgXRendererFeature).
// Basado en la aproximacion polinomial de AgX de Benjamin Wrensch (2023),
// que reproduce la curva de Troy Sobotka usada en Blender 4.
Shader "Hidden/OwlProject/AgX"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "AgX"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // x: multiplicador de exposicion, y: saturacion, z: contraste
            float4 _AgXParams;
            float _AgXLook;

            // Matriz de entrada ("inset") de AgX, espacio lineal Rec.709/sRGB.
            static const float3x3 AgXInset = float3x3(
                0.842479062253094,  0.0423282422610123, 0.0423756549057051,
                0.0784335999999992, 0.878468636469772,  0.0784336,
                0.0792237451477643, 0.0791661274605434, 0.879142973793104);

            // Inversa ("outset").
            static const float3x3 AgXOutset = float3x3(
                 1.19687900512017,  -0.0528968517574562, -0.0529716355144438,
                -0.0980208811401368, 1.15190312990417,   -0.0980434501171241,
                -0.0990297440797205,-0.0989611768448433,  1.15107367264116);

            static const float AgXMinEV = -12.47393;
            static const float AgXMaxEV = 4.026069;

            // Sigmoide de contraste de AgX (polinomio de 6o grado).
            float3 AgXContraste(float3 x)
            {
                float3 x2 = x * x;
                float3 x4 = x2 * x2;
                return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4
                     - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
            }

            float3 AgX(float3 c)
            {
                // Las matrices estan escritas por columnas (como en GLSL): mul(v, M).
                c = mul(c, AgXInset);
                c = clamp(log2(max(c, 1e-10)), AgXMinEV, AgXMaxEV);
                c = (c - AgXMinEV) / (AgXMaxEV - AgXMinEV);
                return AgXContraste(c);
            }

            float3 AgXLookAplicar(float3 c, float look)
            {
                const float3 lw = float3(0.2126, 0.7152, 0.0722);
                float3 slope = 1.0;
                float3 power = 1.0;
                float sat = 1.0;

                if (look > 1.5)      { slope = float3(1.0, 0.9, 0.5); power = 0.8;  sat = 0.8; } // Golden
                else if (look > 0.5) { power = 1.35; sat = 1.4; }                                 // Punchy

                // Contraste y saturacion extra del Volume
                power *= _AgXParams.z;
                sat *= _AgXParams.y;

                c = pow(max(c * slope, 0.0), power);
                float luma = dot(c, lw);
                return luma + sat * (c - luma);
            }

            float3 AgXEotf(float3 c)
            {
                c = mul(c, AgXOutset);
                // La curva de AgX entrega valores "para pantalla" (gamma 2.2);
                // URP espera color lineal y codifica sRGB al final.
                return pow(saturate(c), 2.2);
            }

            // Ruido para evitar bandas al pasar a 8 bits.
            float RuidoGradiente(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                float3 c = max(color.rgb, 0.0) * _AgXParams.x;
                c = AgX(c);
                c = AgXLookAplicar(c, _AgXLook);
                c += (RuidoGradiente(input.positionCS.xy) - 0.5) / 255.0;
                c = AgXEotf(c);

                return half4(c, color.a);
            }
            ENDHLSL
        }
    }
}
