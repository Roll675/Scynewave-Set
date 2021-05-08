// By Mochie
Shader "Mochie/Taken" {
    Properties {

        // [Header(BASE SETTINGS)]
			[HideInInspector]_BlendMode("__mode", Float) = 0.0
			[HideInInspector]_SrcBlend("__src", Float) = 1.0
			[HideInInspector]_DstBlend ("__dst", Float) = 0.0
			[Enum(Off,0, On,1)]_ATM("", Int) = 0
			[Enum(Off,0, On,1)]_ZWrite("", Int) = 1
			_Cutoff("Cutout", Range(0,1)) = 0.5
			_Color("Global Tint", Color) = (0.8,0.95,1,1)
			[Toggle(_)]_Invert("Invert", Int) = 0
			[Toggle(_)]_Smoothstep("Smoothstep", Int) = 0
			[Enum(Off,0, Front,1, Back,2)]_Culling("Culling", Int) = 0
			[Space(5)]
			_MainTex("Main Texture", 2D) = "black" {}
			_BumpScale("Normal Strength", Float) = 0
			[NoScaleOffset]_BumpMap("Normal Map", 2D) = "bump" {}

		// [Header(LIGHTING)]
			[Toggle(_)]_EnableLighting("Enable", Int) = 1
			[Toggle(_)]_EnableSH("Spherical Harmonics", Int) = 0
			_Metallic("Metallic", Range(0,1)) = 0
			_Roughness("Roughness", Range(0,1)) = 1
			_ReflectionStr("Reflections", Range(0,1)) = 0
			[Space(5)]
			[NoScaleOffset]_MetallicMap("Metallic Map", 2D) = "white" {}
			[NoScaleOffset]_RoughnessMap("Roughness Map", 2D) = "white" {}
			[NoScaleOffset]_ReflectionMask("Reflection Mask", 2D) = "white" {}

		// [Header(RIM LIGHT)]
			_RimBrightness("Strength", Float) = 1
			_RimWidth("Width", Range(0,1)) = 0.5
			_RimEdge("Sharpness", Range(0,0.5)) = 0
			_RimGradMask("Gradient Restriction", Range(0,1)) = 1
			[Space(5)]
			[NoScaleOffset]_RimMask("Mask", 2D) = "white" {}

		// [Header(EMISSION)]
			[Toggle(_)]_EmissInvert("Invert", Int) = 1
			_EmissStr("Strength", Float) = 1
			_EmissPow("Exponent", Float) = 1
			_EmissGradMasking("Gradient Restriction", Range(0,1)) = 0.99
			[Space(5)]
			[NoScaleOffset]_EmissTex("Ambient Occlusion", 2D) = "black" {}
			[NoScaleOffset]_EmissGradMask("Restriction Mask", 2D) = "white" {}

		// [Header(GRADIENT)]
			[Toggle(_)]_EnableGradient("Enable", Int) = 1
			[Toggle(_)]_GradientInvert("Invert Axis", Int) = 0
			[Enum(X,0, Y,1, Z,2)]_GradientAxis("Axis", Int) = 1
			_GradientScale("Noise Scale", Vector) = (5,5,5,0)
			_GradientBrightness("Brightness", Float) = 1
			_GradientHeightMax("Top Position", Float) = 0
			_GradientHeightMin("Bottom Position", Float) = -1
			_GradientSpeed("Scroll Speed", Float) = 0.01
			_GradientContrast("Contrast", Range(0,2)) = 1.5
			[Space(5)]
			[NoScaleOffset]_GradientMask("Mask", 2D) = "white" {}

		// [Header(NOISE PATCHES)]
			[Toggle(_)]_EnableNoise("Enable", Int) = 1
			_NoiseScale("Scale", Vector) = (50,50,0,0)
			_NoiseBrightness("Brightness", Float) = 1
            _NoiseCutoff("Cutoff", Range(0,1)) = 0.73
			_NoiseSmooth("Smoothing", Range(0,0.1)) = 0.01
			[Space(5)]
			[NoScaleOffset]_NoiseMask("Mask", 2D) = "white" {}

		// [Header(DISSOLVE)]
			[Toggle(_)]_EnableDissolve("Enable", Int) = 0
			_DissolveTex("Dissolve Texture (Alpha)", 2D) = "white" {}
			_DissolveAmt("Dissolve Amount", Range(0,1)) = 0
			_DissolveRimBrightness("Rim Brightness", Float) = 1
			_DissolveRimWidth("Rim Width", Float) = 2

		// [Header(OUTLINE)]
			[Toggle(_)]_EnableOutline("Enable", Int) = 0
			[Toggle(_)]_InvertedOutline("Inverted Tint", Int) = 1
			_Thickness("Thickness", Float) = 0.1
			[NoScaleOffset]_OutlineMask("Mask", 2D) = "white" {}

		[HideInInspector]_NaNLmao("", Float) = 0.0
		[HideInInspector]_TextureWidth("", Int) = 0
		[HideInInspector]_TextureHeight("", Int) = 0
		[HideInInspector]_WeightNormal("", Float) = 0
		[HideInInspector]_WeightBold("", Float) = 0
    }

    SubShader {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Cull [_Culling]
		ZWrite [_ZWrite]
		AlphaToMask [_ATM]
		Blend [_SrcBlend] [_DstBlend]

        Pass {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#define AVATAR
			#pragma target 5.0
			#include "TakenDefines.cginc"

            v2f vert (appdata v) {
                v2f o = (v2f)0;

				o.localPos = v.vertex;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
				o.screenPos = ComputeGrabScreenPos(o.pos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.uvDis = TRANSFORM_TEX(v.uv, _DissolveTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
				o.tangent.xyz = UnityObjectToWorldDir(v.tangent.xyz);
				o.tangent.w = v.tangent.w;
				UNITY_TRANSFER_SHADOW(o, v.uv1);
				UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target {
				
				float4 texCol = tex2D(_MainTex, i.uv);
				#if defined(_ALPHATEST_ON)
					float2 screenUVs = i.screenPos.xy/(i.screenPos.w+0.0000000001);
					#if UNITY_SINGLE_PASS_STEREO
						screenUVs.x *= 2;
					#endif
					if (_BlendMode == 1)
						clip(texCol.a - _Cutoff);
					else if (_BlendMode == 2)
						clip(Dither(screenUVs, texCol.a));
				#endif
				float3 col = GetBaseColor(i, texCol.rgb);
				float3 aoEmiss = 0;
				float glowPos = 0;

				UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
				FadeShadows(i, atten);
				lighting l = GetLighting(i);
				GetMetallicWorkflow(i, col);
				TakenBRDF(i, l, col);

				ApplyGradient(i, col, glowPos);
				ApplyAOEmiss(i, l, col, aoEmiss, glowPos);
				ApplyRim(i, l, col, glowPos);
				ApplySimplexPatches(i, col, glowPos);
				ApplyDissolve(i, col);

				col = Desaturate(col) * _Color.rgb;
				float4 diffuse = float4(col, texCol.a);
				UNITY_APPLY_FOG(i.fogCoord, diffuse);
				return diffuse;
            }
            ENDCG
        }

		Pass {
			Cull Front
			AlphaToMask [_ATM]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma multi_compile_fog
			#define AVATAR
			#define OUTLINE
			#pragma target 5.0
			#include "TakenDefines.cginc"

            v2f vert (appdata v) {
                v2f o = (v2f)0;
				UNITY_BRANCH
				if (_EnableOutline == 1){
					v.vertex.xyz += 0.0001*v.normal.xyz*_Thickness;
					o.localPos = v.vertex;
					o.worldPos = mul(unity_ObjectToWorld, v.vertex);
					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv = TRANSFORM_TEX(v.uv, _MainTex);
					o.uvDis = TRANSFORM_TEX(v.uv, _DissolveTex);
					o.normal = UnityObjectToWorldNormal(v.normal);
					o.screenPos = ComputeGrabScreenPos(o.pos);
				}
				else {
					o.pos = 0.0/_NaNLmao;
				}
				UNITY_TRANSFER_SHADOW(o, v.uv1);
				UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target {
				float4 diffuse = 0;

				#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
					discard;
				#endif
				
				if (_EnableOutline == 1){
					if (tex2D(_OutlineMask, i.uv).r > 0.001){
						if (_InvertedOutline == 1){
							float4 texCol = tex2D(_MainTex, i.uv);
							float3 col = GetBaseColor(i, texCol.rgb);
							lighting l = (lighting)0;
							float3 aoEmiss = 0;
							float glowPos = 0;
							float atten = 0;
							
							ApplyGradient(i, col, glowPos);
							ApplyAOEmiss(i, l, col, aoEmiss, glowPos);
							ApplyRim(i, l, col, glowPos);

							col = (1-Desaturate(col)) * _Color.rgb;
							col = lerp(0.5, col, 0.5);
							diffuse = float4(col, texCol.a);
							ApplyTransparency(i, texCol.a);
						}
						else {
							float alpha = tex2D(_MainTex, i.uv).a;
							diffuse = float4(_Color.rgb, alpha*_Color.a);
							ApplyTransparency(i, alpha);
						}
						UNITY_APPLY_FOG(i.fogCoord, diffuse);
					}
					else discard;
				}
				else discard;

				return diffuse;
            }
            ENDCG
        }
		 Pass {
            Name "ShadowCaster"
            Tags {"RenderType"="Transparent" "Queue"="Transparent" "LightMode"="ShadowCaster"}
			AlphaToMask Off
			ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_shadowcaster
			#pragma target 5.0
			#define AVATAR
            #include "TakenDefines.cginc"

			v2f vert (appdata v) {
				v2f o = (v2f)0;

				o.pos = UnityObjectToClipPos(v.vertex);
				o.screenPos = ComputeGrabScreenPos(o.pos);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.uvDis = TRANSFORM_TEX(v.uv, _DissolveTex);

				TRANSFER_SHADOW_CASTER(o);
				return o;
			}

			float4 frag(v2f i) : SV_Target {
				float4 mainTex = tex2D(_MainTex, i.uv);
				ApplyTransparency(i, mainTex.a);
				SHADOW_CASTER_FRAGMENT(i);
			}
            ENDCG
        }
    }
	CustomEditor "TakenEditor"
}