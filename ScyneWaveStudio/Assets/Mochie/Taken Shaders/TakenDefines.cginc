#include "UnityPBSLighting.cginc"
#include "Autolight.cginc"
#include "../Common/Utilities.cginc"
#include "../Common/Noise.cginc"
#include "../Common/Color.cginc"
float _NaNLmao;

#if defined(AVATAR)
sampler2D _MainTex, _EmissTex, _BumpMap, _DissolveTex;
sampler2D _NoiseMask, _GradientMask, _RimMask, _ShadowMask, _EmissGradMask, _ReflectionMask, _OutlineMask;
sampler2D _RoughnessMap, _MetallicMap;
float4 _MainTex_ST, _DissolveTex_ST, _Color;
float3 _GradientScale;
float2 _NoiseScale;
float _BumpScale, _Roughness, _Metallic, _ReflectionStr, _Cutoff;
float _NoiseBrightness, _NoiseSmooth, _NoiseCutoff;
float _GradientHeightMax, _GradientHeightMin, _GradientBrightness;
float _GradientContrast, _GradientSpeed;
float _RimBrightness, _RimWidth, _RimEdge, _RimGradMask;
float _EmissPow, _EmissStr, _EmissGradMasking;
float _DissolveAmt, _DissolveRimBrightness, _DissolveRimWidth;
float _Thickness;
int _Invert, _GradientInvert, _Smoothstep, _GradientAxis, _InvertedOutline;
int _EnableRim, _EnableNoise, _EnableGradient, _EnableLighting, _EnableDissolve, _EnableSH, _EnableOutline;
int _BlendMode;

// Outputs
float3 specularTint;
float metallic;
float roughness;
float smoothness;
float omr;
float atten;

struct appdata {
	float4 vertex : POSITION;
	float4 uv : TEXCOORD0;
	float4 uv1 : TEXCOORD1;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
};

struct v2f {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 normal : TEXCOORD1;
	float4 localPos : TEXCOORD2;
	float3 worldPos : TEXCOORD3;
	float3 binormal : TEXCOORD4;
	float4 tangent : TANGENT;
	float2 uvDis : TEXCOORD6;
	float4 screenPos : TEXCOORD7;
	UNITY_SHADOW_COORDS(8)
	UNITY_FOG_COORDS(9)
};

struct lighting {
	float NdotL;
	float NdotV;
	float NdotH;
	float LdotH;
	float VdotL;
	float3 lightCol;
	float3 viewDir;
	float3 lightDir;
	float3 reflectionDir;
	float3 halfVector;
	float3 normal;
	float3 binormal;
	bool lightEnv;
};

#include "TakenLighting.cginc"
#endif

#if defined(CORRUPTION)

sampler2D _StarTex;
float4 _Color, _RimColor;
float2 _StarTexScale;
float _MaxRotation, _Threshold;
float _Radius, _RimWidth, _RimPower;
float _SimplexScale;
float _RimBrightness, _StarBrightness;
// int _IsRotating;
float _Rotation; //, _Blur;

struct appdata {
	float4 vertex : POSITION;
	float4 uv : TEXCOORD0;
};

struct v2f{
	float4 pos : SV_POSITION;
	float4 uv : TEXCOORD0;
	float3 raycast : TEXCOORD1;
	float3 worldPos : TEXCOORD2;
	float3 objPos : TEXCOORD3;
	bool isReflection : TEXCOORD4;
};

struct Ray {
	float3 origin; // origin of ray
	float3 dir; // direction of ray (unit length assumed)
};

static const float4 kernel10[10] = {
	float4(0.1094615,0.5242077,0.4103162,0.5811607),
	float4(0.618497,0.7541848,0.7961841,0.5251553),
	float4(0.0268888,0.597996,0.6776286,0.8250506),
	float4(0.1856688,0.6066927,0.3694414,0.9334932),
	float4(0.0181179,0.7472348,0.6624749,0.1957627),
	float4(0.8664547,0.6637973,0.910411,0.6891307),
	float4(0.3513095,0.5644325,0.4566466,0.7382287),
	float4(0.7508639,0.6730916,0.2798718,0.3423366),
	float4(0.9299719,0.9815703,0.7703365,0.3945645),
	float4(0.0650494,0.3470043,0.1590463,0.772713)
};

#endif

#include "TakenFunctions.cginc"