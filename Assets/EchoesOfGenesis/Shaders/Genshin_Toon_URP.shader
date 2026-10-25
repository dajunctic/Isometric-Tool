Shader "EchoesOfGenesis/Genshin_Toon_URP"
{
    Properties
    {
        [Header(Main Character Color)]
        [MainTexture] _BaseMap ("Base Color Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)

        [Header(Genshin Cel Shading)]
        _ShadeColor ("1st Shade (Shadow Tint)", Color) = (0.75, 0.7, 0.8, 1)
        _CelStep ("Cel Shadow Step", Range(0, 1)) = 0.5
        _CelFeather ("Cel Edge Feather", Range(0.001, 0.3)) = 0.04

        [Header(Face Special Mode)]
        [Toggle(_IS_FACE)] _IsFace ("Is Face (Smooth Anime Face)", Float) = 0

        [Header(Normal Map)]
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Anime Specular and Metal)]
        _MetallicGlossMap ("Metal (R) / Gloss (A)", 2D) = "white" {}
        _Metallic ("Metallic (Metal Parts)", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecIntensity ("Specular Intensity", Range(0, 3)) = 1.0
        _SpecSize ("Specular Size", Range(0.01, 0.5)) = 0.08

        [Header(MatCap for Armor and Gold)]
        _MatCapTex ("MatCap Texture", 2D) = "black" {}
        _MatCapColor ("MatCap Tint", Color) = (1, 0.9, 0.7, 1)
        _MatCapIntensity ("MatCap Intensity", Range(0, 2)) = 1.0

        [Header(Anime Rim Light)]
        _RimColor ("Rim Light Color", Color) = (1, 1, 1, 1)
        _RimWidth ("Rim Light Width", Range(0.01, 0.5)) = 0.15
        _RimIntensity ("Rim Light Intensity", Range(0, 2)) = 0.8
        _RimMask ("Rim Mask", 2D) = "white" {}

        [Header(Inverted Hull Outline)]
        [Toggle(_ENABLE_OUTLINE)] _EnableOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.2, 0.15, 0.18, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.003

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
            float4 _ShadeColor;
            float4 _SpecColor;
            float4 _MatCapColor;
            float4 _RimColor;
            float4 _OutlineColor;

            float _CelStep;
            float _CelFeather;
            float _IsFace;
            float _BumpScale;
            float _Metallic;
            float _Smoothness;
            float _SpecIntensity;
            float _SpecSize;
            float _MatCapIntensity;
            float _RimWidth;
            float _RimIntensity;
            float _EnableOutline;
            float _OutlineWidth;
            float _Cull;
        CBUFFER_END
        ENDHLSL

        // ------------------------------------------------------------------
        // ForwardLit Pass (Genshin Impact Anime Cel Shading)
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

            #pragma shader_feature_local _IS_FACE

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
            TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_MatCapTex);          SAMPLER(sampler_MatCapTex);
            TEXTURE2D(_RimMask);            SAMPLER(sampler_RimMask);

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

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 albedo = baseTex.rgb;

                // Normal
                half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                half3 normalTS = UnpackNormalScale(normalSample, _BumpScale);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tbn));
                half3 viewDirWS = normalize(input.viewDirWS);

                // Main Light
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Genshin Cel Lighting
                half NdotL = dot(normalWS, mainLight.direction);
                half halfLambert = NdotL * 0.5 + 0.5;

                half celFactor;
                #if defined(_IS_FACE)
                    // Anime Face: Soft peachy lighting, no ugly nose/cheek dark triangles
                    celFactor = smoothstep(0.3, 0.7, halfLambert);
                #else
                    // Body/Armor/Clothes: Crisp 2-band anime shadow
                    half shadowAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                    half lightValue = halfLambert * shadowAttenuation;
                    celFactor = smoothstep(_CelStep - _CelFeather, _CelStep + _CelFeather, lightValue);
                #endif

                half3 shadowColor = albedo * _ShadeColor.rgb;
                half3 diffuse = lerp(shadowColor, albedo, celFactor) * mainLight.color;

                // Anime Specular
                half4 metalGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                half metallic = metalGloss.r * _Metallic;
                half smoothness = metalGloss.a * _Smoothness;

                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specStep = step(1.0 - _SpecSize, pow(NdotH, 32.0));
                half3 specular = specStep * _SpecIntensity * _SpecColor.rgb * mainLight.color * celFactor * (metallic + 0.3);

                // MatCap (Genshin metallic ornaments & gold armor)
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 matcapUV = normalVS.xy * 0.5 + 0.5;
                half3 matcapSample = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MatCapTex, matcapUV).rgb;
                half3 matcapRef = matcapSample * _MatCapColor.rgb * _MatCapIntensity * metallic;

                // Anime Rim Light
                half NdotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = 1.0 - NdotV;
                half rimStep = step(1.0 - _RimWidth, fresnel);
                half rimMask = SAMPLE_TEXTURE2D(_RimMask, sampler_RimMask, input.uv).r;
                half3 rimLight = rimStep * rimMask * _RimIntensity * _RimColor.rgb * mainLight.color;

                // Subtle ambient
                half3 ambient = SampleSH(normalWS) * albedo * 0.25;

                half3 finalColor = diffuse + specular + matcapRef + rimLight + ambient;
                return half4(finalColor, baseTex.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Inverted Hull Outline Pass (Anime Silhouette)
        // ------------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex OutlinePassVertex
            #pragma fragment OutlinePassFragment

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings OutlinePassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                if (_EnableOutline <= 0.5 || _OutlineWidth <= 0.0001)
                {
                    output.positionCS = float4(0, 0, 0, 0);
                    return output;
                }

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1, 0, 0, 1));

                float3 normalCS = TransformWorldToHClipDir(normalInput.normalWS);
                float4 posCS = vertexInput.positionCS;
                posCS.xy += normalize(normalCS.xy + 0.00001) * _OutlineWidth * posCS.w * 0.1;

                output.positionCS = posCS;
                return output;
            }

            half4 OutlinePassFragment(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1.0);
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
