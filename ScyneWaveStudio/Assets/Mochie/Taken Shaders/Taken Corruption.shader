Shader "Mochie/Taken Corruption Unlit Cutout" {
	// DO NOT CHANGE THE SHADER NAME
    Properties {
		[Space(8)]
		_Color("Color", Color) = (0.8,0.95,1,1)
		[Header(STARS)]
		_StarBrightness("Brightness", Float) = 2
		// [Toggle(_)]_IsRotating("Is Rotating?", Int) = 0
		// _Blur("Blur", Range(0,1)) = 0
		_Rotation("Rotation", Range(0,360)) = 183
		[NoScaleOffset]_StarTex("Star Tex", 2D) = "white" {}
		_StarTexScale("Scale", Vector) = (4,4,0,0)
		
		[Header(CORRUPTION)]
		_RimBrightness("Brightness", Float) = 7
		_RimWidth("Rim Width", Float) = 5.5
		_Radius("Pool Size", Float) = 35
		_SimplexScale("Noise Scale", Float) = 2.5

		[HideInInspector]_NaNLmao("", Float) = 0.0
		[HideInInspector]_MainTex("", 2D) = "white" {}
		[HideInInspector]_Cutoff("", Range(0,1)) = 1
    }
    SubShader {
        Tags {
			"RenderType"="AlphaTest" 
			"Queue"="AlphaTest+55"
		}
		Cull Front
		ZWrite Off
		ZTest Always
		Blend SrcAlpha OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#define CORRUPTION
			#pragma target 5.0
            #include "UnityCG.cginc"
			#include "TakenDefines.cginc"

            v2f vert (appdata v){
                v2f o = (v2f)0;
				o.isReflection = IsInMirror();
				o.objPos = mul(unity_ObjectToWorld, float4(0,0,0,1));
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.raycast = UnityObjectToViewPos(v.vertex).xyz * float3(-1,-1,1);
				o.uv = ComputeGrabScreenPos(o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target {

				if (i.isReflection)
					discard;

				GetDepth(i, wPos);
				float dist = distance(i.objPos, wPos);
				_Radius = max(0.002, _Radius*0.01);
				_RimWidth = clamp(_RimWidth*0.01, 0, _Radius-0.001);
				float noise = GetSimplex4DPool(wPos, _SimplexScale);
				float radius = smoothstep(_Radius, _Radius-_RimWidth,  dist * noise);
				float4 col = 0;

				UNITY_BRANCH
				if (radius > 0){
					Ray cameraRay;
					cameraRay.origin = _WorldSpaceCameraPos;
					cameraRay.dir = normalize(wPos - _WorldSpaceCameraPos.xyz);
					col = float4(GetStars(i, cameraRay).xxx, radius);
					col.rgb += (1-radius)*_RimBrightness;
					col.rgb *= _Color.rgb;
				}
                return col;
            }
            ENDCG
        }
    }
}