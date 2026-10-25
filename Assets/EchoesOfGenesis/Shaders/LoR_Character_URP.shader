Shader "EchoesOfGenesis/LoR_Character_URP"
{
    Properties
    {
        [Header(Base Color)]
        [MainTexture] _BaseMap ("Base Map (RGB) Alpha (A)", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)

        [Header(Color Tint Mask)]
        _ColorMask ("Color Mask (R)", 2D) = "black" {}
        _ColorAdjust ("Adjust Color Tint", Color) = (1, 1, 1, 1)

        [Header(Normal Map)]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Stylized Shading)]
        _ShadowTint ("Shadow Tint Color", Color) = (0.35, 0.32, 0.45, 1)
        _ShadowThreshold ("Shadow Threshold", Range(-1, 1)) = 0.05
        _ShadowSoftness ("Shadow Softness", Range(0.001, 1)) = 0.35
        _ShadowMask ("Baked Shadow / Line Mask (R)", 2D) = "white" {}

        [Header(Metallic and Specular)]
        _MetallicGlossMap ("Metallic (R) / Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _SpecularColor ("Specular Tint", Color) = (1, 0.95, 0.85, 1)
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 1.0
        _SpecularSize ("Specular Sharpness", Range(1, 128)) = 32

        [Header(MatCap Reflection)]
        _MatCapTex ("MatCap Texture", 2D) = "black" {}
        _MatCapColor ("MatCap Tint", Color) = (1, 1, 1, 1)
        _MatCapIntensity ("MatCap Intensity", Range(0, 3)) = 1.0
        _MatCapMask ("MatCap / Ref Mask (R)", 2D) = "white" {}

        [Header(Rim Lighting LoR Signature)]
        _RimColor ("Rim Color", Color) = (1, 0.85, 0.5, 1)
        _RimPower ("Rim Power", Range(0.5, 10)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 5)) = 1.5
        _RimMask ("Rim Mask (R)", 2D) = "white" {}

        [Header(Emission)]
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)

        [Header(Render State)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ColorAdjust;
            float4 _ShadowTint;
            float4 _SpecularColor;
            float4 _MatCapColor;
            float4 _RimColor;
            float4 _EmissionColor;

            float _BumpScale;
            float _ShadowThreshold;
            float _ShadowSoftness;
            float _Metallic;
            float _Smoothness;
            float _SpecularIntensity;
            float _SpecularSize;
            float _MatCapIntensity;
            float _RimPower;
            float _RimIntensity;
            float _Cull;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------------
        // ForwardLit Pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                float3 viewDirWS    : TEXCOORD5;
            };

            TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ColorMask);          SAMPLER(sampler_ColorMask);
            TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ShadowMask);        SAMPLER(sampler_ShadowMask);
            TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_MatCapTex);          SAMPLER(sampler_MatCapTex);
            TEXTURE2D(_MatCapMask);         SAMPLER(sampler_MatCapMask);
            TEXTURE2D(_RimMask);            SAMPLER(sampler_RimMask);
            TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                return output;
            }

            half3 ComputeStylizedLight(Light light, half3 normalWS, half3 viewDirWS, half3 albedo, half roughness, half shadowMask)
            {
                half NdotL = dot(normalWS, light.direction);
                half halfLambert = NdotL * 0.5 + 0.5;

                half lightThreshold = saturate((halfLambert - _ShadowThreshold) / max(_ShadowSoftness, 0.001));
                half attenuation = light.distanceAttenuation * light.shadowAttenuation * shadowMask;
                half directFactor = saturate(lightThreshold * attenuation);

                half3 shadowCol = albedo * _ShadowTint.rgb;
                half3 diffuse = lerp(shadowCol, albedo, directFactor) * light.color;

                half3 halfDir = normalize(light.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specPower = max(_SpecularSize * (1.0 - roughness * 0.8), 2.0);
                half specFactor = pow(NdotH, specPower) * _SpecularIntensity * (1.0 - roughness);
                specFactor = smoothstep(0.2, 0.8, specFactor);
                half3 specular = specFactor * _SpecularColor.rgb * light.color * directFactor;

                return diffuse + specular;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // Color adjust
                half colorMask = SAMPLE_TEXTURE2D(_ColorMask, sampler_ColorMask, input.uv).r;
                albedo.rgb = lerp(albedo.rgb, albedo.rgb * _ColorAdjust.rgb, colorMask);

                // Normal
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tbn));
                half3 viewDirWS = normalize(input.viewDirWS);

                // Metallic, Smoothness, ShadowMask
                half4 metalGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic = metalGloss.r * _Metallic;
                half smoothness = metalGloss.a * _Smoothness;
                half roughness = 1.0 - smoothness;
                half shadowMask = SAMPLE_TEXTURE2D(_ShadowMask, sampler_ShadowMask, input.uv).r;

                // Ambient SH (indirect light in shadow)
                half3 ambient = SampleSH(normalWS) * albedo.rgb * _ShadowTint.rgb * 0.5;

                // Main Light
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 lighting = ComputeStylizedLight(mainLight, normalWS, viewDirWS, albedo.rgb, roughness, shadowMask);

                // Additional Lights
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    lighting += ComputeStylizedLight(addLight, normalWS, viewDirWS, albedo.rgb, roughness, shadowMask);
                }
                #endif

                // MatCap Reflection (Only applies to metallic surfaces e.g. armor, buckles, weapons)
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 matcapUV = normalVS.xy * 0.5 + 0.5;
                half3 matcapSample = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MatCapTex, matcapUV).rgb;
                half matcapMask = SAMPLE_TEXTURE2D(_MatCapMask, sampler_MatCapMask, input.uv).r;
                half matcapFactor = matcapMask * metallic * _MatCapIntensity;
                half3 matcapTerm = matcapSample * _MatCapColor.rgb * matcapFactor;

                // LoR Natural Rim Light (Backlight modulated, not uniform 360 glow)
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 1.0 - NdotV;
                half rimFactor = pow(fresnel, _RimPower) * _RimIntensity;
                half rimMask = SAMPLE_TEXTURE2D(_RimMask, sampler_RimMask, input.uv).r;
                half backlight = saturate(dot(-normalWS, mainLight.direction) * 0.5 + 0.5);
                half3 rimTerm = rimFactor * rimMask * _RimColor.rgb * (mainLight.color * backlight + half3(0.2, 0.2, 0.2));

                // Emission
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                half3 finalRGB = lighting + ambient + matcapTerm + rimTerm + emission;
                return half4(finalRGB, albedo.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // ShadowCaster Pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float3 _LightDirection;
            float3 _LightPosition;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(output.positionCS);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // DepthOnly Pass
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
