Shader "Enlighten/Cartoon Brick World"
{
    Properties
    {
        _BrickColor ("Brick Color", Color) = (0.8,0.5,0.35,1)
        _MortarColor ("Mortar Color", Color) = (0.65,0.6,0.5,1)
        _BrickWidth ("Brick Width (world metres)", Range(0.08,1.5)) = 0.4
        _BrickHeight ("Brick Height (world metres)", Range(0.04,0.75)) = 0.2
        _MortarWidth ("Joint Width (metres)", Range(0.001,0.04)) = 0.012
        _CornerRadius ("Corner Radius (metres)", Range(0,0.04)) = 0.014
        _BevelWidth ("Soft Bevel (metres)", Range(0.001,0.04)) = 0.015
        _Variation ("Brick Color Variation", Range(0,0.3)) = 0.08
        _RowOffset ("Alternating Row Offset", Range(0,1)) = 0.5
        _Smoothness ("Smoothness", Range(0,1)) = 0.22
        _ProjectionSharpness ("Projection Blend Sharpness", Range(2,16)) = 8
        _WorldOffset ("World Pattern Offset (metres)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BrickColor, _MortarColor, _WorldOffset;
            float _BrickWidth, _BrickHeight, _MortarWidth, _CornerRadius;
            float _BevelWidth, _Variation, _RowOffset, _Smoothness, _ProjectionSharpness;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 lightmapUV : TEXCOORD1;
            float2 dynamicLightmapUV : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 fogAndLight : TEXCOORD2;
            DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD4;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings BrickVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.fogAndLight = half4(ComputeFogFactor(p.positionCS.z), VertexLighting(p.positionWS, output.normalWS));
            OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
            OUTPUT_SH(output.normalWS, output.vertexSH);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
            return output;
        }

        // Rounded running-bond bricks in metres, deliberately independent of mesh UVs and scale.
        half3 PlanarBricks(float2 position)
        {
            float2 size = max(float2(_BrickWidth, _BrickHeight), 0.001);
            float row = floor(position.y / size.y);
            position.x += (row - 2.0 * floor(row * 0.5)) * size.x * _RowOffset;
            float2 cell = floor(position / size);
            float2 local = (frac(position / size) - 0.5) * size;
            float joint = min(_MortarWidth, min(size.x,size.y) * 0.4);
            float2 halfSize = size * 0.5 - joint * 0.5;
            float radius = min(_CornerRadius, min(halfSize.x,halfSize.y) * 0.8);
            float2 q = abs(local) - halfSize + radius;
            float distanceToEdge = length(max(q,0.0)) + min(max(q.x,q.y),0.0) - radius;
            float aa = max(fwidth(distanceToEdge), 0.0001);
            float mask = 1.0 - smoothstep(-aa, aa, distanceToEdge);
            float bevel = smoothstep(0.0, max(_BevelWidth,0.0001), -distanceToEdge);
            float random = frac(sin(dot(cell, float2(127.1,311.7))) * 43758.5453);
            half3 brick = _BrickColor.rgb * (1.0 + (random - 0.5) * 2.0 * _Variation);
            // Broad, clean bevel shading instead of noisy photographic detail.
            float upperLip = saturate(local.y / size.y + 0.5);
            brick *= lerp(0.76 + 0.28 * upperLip, 1.0, bevel);
            brick *= 1.0 + 0.035 * local.y / size.y;
            half3 color = lerp(_MortarColor.rgb, brick, mask);
            // Fade sub-pixel rows to their mean to reduce distant moire.
            float footprint = max(length(ddx(position / size)), length(ddy(position / size)));
            float coverage = saturate((size.x-joint)*(size.y-joint)/(size.x*size.y));
            return lerp(color, lerp(_MortarColor.rgb, _BrickColor.rgb, coverage), smoothstep(0.4,1.2,footprint));
        }
        half3 BrickAlbedo(float3 positionWS, half3 normalWS)
        {
            float3 p = positionWS + _WorldOffset.xyz;
            float3 weights = pow(abs(normalWS), _ProjectionSharpness);
            weights /= max(dot(weights, float3(1,1,1)), 0.0001);
            return PlanarBricks(p.zy) * weights.x + PlanarBricks(p.xz) * weights.y + PlanarBricks(p.xy) * weights.z;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BrickVertex
            #pragma fragment BrickFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            half4 BrickFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = NormalizeNormalPerPixel(input.normalWS);
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                data.fogCoord = input.fogAndLight.x;
                data.vertexLighting = input.fogAndLight.yzw;
                #ifdef DYNAMICLIGHTMAP_ON
                    data.bakedGI = SAMPLE_GI(input.lightmapUV, input.dynamicLightmapUV, input.vertexSH, data.normalWS);
                #else
                    data.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, data.normalWS);
                #endif
                data.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = BrickAlbedo(input.positionWS, data.normalWS);
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = BrickVertex(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirection = normalize(_LightPosition - output.positionWS);
                #else
                    float3 lightDirection = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }
            half4 ShadowFragment(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BrickVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BrickVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFragment(Varyings input) : SV_Target
            {
                half3 n = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex BrickMetaVertex
            #pragma fragment BrickMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            Varyings BrickMetaVertex(Attributes input)
            {
                Varyings output = BrickVertex(input);
                output.positionCS = MetaVertexPosition(input.positionOS, input.lightmapUV, input.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
                return output;
            }
            half4 BrickMetaFragment(Varyings input) : SV_Target
            {
                MetaInput meta = (MetaInput)0;
                meta.Albedo = BrickAlbedo(input.positionWS, normalize(input.normalWS));
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
