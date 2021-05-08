#if defined(PLATFORM)
#include "../Common/Noise.cginc"
float GetSimplex3DPlatform(float2 coords){
	float3 c = float3(coords * 10, _Time.y*0.5);
	float n = 0;
	float a = 0.5;

	[unroll(4)]
	for (int i = 0; i < 4; i++){
		n += a*Simplex3D(c);
		c = c * 2 + float(i)*0.01;
		a *= 0.5;
	}
	
	return (n + 1) * 0.5;
}
#endif

#if defined(CORRUPTION)

// Pool
float GetSimplex4DPool(float3 coords, float scale){
	float4 c = float4(coords * scale, _Time.y*0.1);
	float n = 0;
	float a = 0.5;

	[unroll(6)]
	for (int i = 0; i < 6; i++){
		n += a*Simplex4D(c);
		c = c * 2 + float(i)*0.01;
		a *= 0.5;
	}
	
	return (n + 1) * 0.5;
}

sampler2D_float _CameraDepthTexture;
float3 wPos;
void GetDepth(v2f i, out float3 wPos){
	float depth = Linear01Depth(DecodeFloatRG(tex2Dproj(_CameraDepthTexture, i.uv)));
	i.raycast *= (_ProjectionParams.z / i.raycast.z);
	float4 vPos = float4(i.raycast * depth, 1);
	wPos = mul(unity_CameraToWorld, vPos).xyz;
}

float GetSphere(const float r, const Ray ray){
	float3 oc = ray.origin;
	float dotDirOC = dot(oc, ray.dir);
	float root = dotDirOC * dotDirOC - (dot(oc, oc) - r * r);
	if (root < EPS){
		return -1.0;
	}
	return sqrt(root)-dotDirOC;
}

float3x3 AngleAxis3x3(float angle, float3 axis){
    float c, s;
    sincos(angle, s, c);

    float t = 1 - c;
    float x = axis.x;
    float y = axis.y;
    float z = axis.z;

    return float3x3(
        t * x * x + c,      t * x * y - s * z,  t * x * z + s * y,
        t * x * y + s * z,  t * y * y + c,      t * y * z - s * x,
        t * x * z - s * y,  t * y * z + s * x,  t * z * z + c
    );
}

void ApplyTwitch(v2f i, inout float3 axis){
	// UNITY_BRANCH
	// if (_IsRotating > 0)
	// 	axis.xz = mul(GetRotationMatrix(GetNoise(axis.xz)*_Blur*4), axis.xz);
	float2 rot = mul(GetRotationMatrix(_Rotation), axis.xz);
	axis = float3(rot.x, axis.y, rot.y);
}

float GetStars(v2f i, Ray cameraRay){
	const float sphere = GetSphere(1737000, cameraRay);
	float movement =  (_Time.y*0.01);
	float3 normal = normalize((cameraRay.origin + sphere * cameraRay.dir));
	float newField = 0;

	if (sphere > 0){
		[unroll(4)]
		for (uint j = 0; j < 4; j++){
			const float4 dotPos = kernel10[j+1];
			const float3x3 rot = AngleAxis3x3(movement, normalize(dotPos.yzw));
			ApplyTwitch(i, normal);
			normal = mul(rot, normal);
			const float u = (atan2(normal.x, normal.z) / UNITY_TWO_PI + 0.5)*2.0;
			const float v = asin(normal.y) / UNITY_PI + 0.5;
			float2 uv = float2(u,v)*_StarTexScale;
			newField += tex2Dlod(_StarTex, float4(uv,0,0));
		}
	}
	return newField * _StarBrightness;
}
#endif

#if defined(AVATAR)

// Leg gradient
float GetSimplex4DLegs(float3 uv, float3 scale, float speed){
	float4 c = float4(uv * scale, _Time.y * speed);
	float n = 0.5 * Simplex4D(c);
	c *= 2;
	n += 0.25 * Simplex4D(c);
	return (n + 1) * 0.5;
}

// Patches
float GetSimplex3DPatch(float2 coords, float2 scale){
	float3 c = float3(coords * scale, _Time.y*0.5);
	float n = 0;
	float a = 0.5;

	[unroll(3)]
	for (int i = 0; i < 3; i++){
		n += a*Simplex3D(c);
		c = c * 2 + float(i)*0.01;
		a *= 0.5;
	}
	
	return (n + 1) * 0.5;
}

float3 GetBaseColor(v2f i, float3 texCol){
	float3 col = texCol;
	col = lerp(col, 1-col, _Invert);
	if (_Smoothstep == 1){
		col.r = smootherstep(0,1,smootherstep(0,1,col.r));
		col.g = smootherstep(0,1,smootherstep(0,1,col.g));
		col.b = smootherstep(0,1,smootherstep(0,1,col.b));
	}
	return col;
}

void ApplyRim(v2f i, lighting l, inout float3 col, float glowPos){
	if (_RimBrightness > 0){
		float rimMask = tex2D(_RimMask, i.uv).r;
		if (rimMask > 0.001){
			_RimWidth = lerp(_RimWidth, 0.95, glowPos);
			float rim = pow((1-l.VdotL), (1-_RimWidth) * 10);
			rim = smootherstep(_RimEdge, (1-_RimEdge), rim) * rimMask;
			rim = lerp(rim, lerp(rim*0.1, rim, glowPos), _RimGradMask);
			col += rim * _RimBrightness;
		}
	}
}

void ApplyAOEmiss(v2f i, lighting l, inout float3 col, inout float3 aoEmiss, float glowPos){
	aoEmiss = 1-tex2D(_EmissTex, i.uv).g;
	if (_Smoothstep == 1)
		aoEmiss = smootherstep(0,1,aoEmiss);
	aoEmiss = saturate(pow(aoEmiss, _EmissPow));
	aoEmiss = lerp(aoEmiss, lerp(aoEmiss*0.01, aoEmiss, glowPos), tex2D(_EmissGradMask, i.uv).r*_EmissGradMasking);
	// aoEmiss *= saturate(smoothstep(2,0,(l.lightCol.r + l.lightCol.b + l.lightCol.g)/3.0));
	col += aoEmiss * _EmissStr;
}

void ApplySimplexPatches(v2f i, inout float3 col, float glowPos){
	if (_EnableNoise == 1){
		float noiseMask = tex2D(_NoiseMask, i.uv).r;
		if (noiseMask > 0.001){
			float noise = GetSimplex3DPatch(i.uv, _NoiseScale);
			noise = smootherstep(_NoiseSmooth,0,noise-(1-_NoiseCutoff));
			noise = lerp(noise*0.5, noise*1.5, glowPos);
			float3 noiseCol = noise * _NoiseBrightness;
			col = lerp(col, noiseCol, noise * noiseMask);
		}
	}
}

void ApplyGradient(v2f i, inout float3 col, inout float glowPos){
	if (_EnableGradient == 1){
		float gradMask = tex2D(_GradientMask, i.uv).r;
		if (gradMask > 0.001){
			float3 gradPos = 0;
			float glowPosInterp = 0;
			float gradNoise = 1;

			if (_GradientAxis == 0){
				gradPos = float3(i.localPos.x - (_Time.y*_GradientSpeed), i.localPos.yz);
				glowPosInterp = lerp(i.localPos.x, 1-i.localPos.x, _GradientInvert);
			}
			else if (_GradientAxis == 1){
				gradPos = float3(i.localPos.x, i.localPos.y - (_Time.y*_GradientSpeed), i.localPos.z);
				glowPosInterp = lerp(i.localPos.y, 1-i.localPos.y, _GradientInvert);
			}
			else {
				gradPos = float3(i.localPos.xy, i.localPos.z - (_Time.y*_GradientSpeed));
				glowPosInterp = lerp(i.localPos.z, 1-i.localPos.z, _GradientInvert);
			}
			
			glowPos = smootherstep(_GradientHeightMax, _GradientHeightMin, glowPosInterp);
			
			#if !defined(OUTLINE)
				if (_GradientContrast > 0)
					gradNoise = GetSimplex4DLegs(gradPos, _GradientScale, 0.5);
				gradNoise = lerp(lerp(0.5,gradNoise,_GradientContrast),gradNoise,gradPos);
				gradNoise *= glowPos;
				gradNoise = saturate(gradNoise *_GradientBrightness);
				col += gradNoise;
			#endif
		}
	}
}

void ApplyDissolve(v2f i, inout float3 col){
	#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
		if (_EnableDissolve == 1){
			float dissolveTex = tex2D(_DissolveTex, i.uvDis).r;
			float dissolveStr = dissolveTex - _DissolveAmt;
			clip(dissolveStr);
			float rim = dissolveStr;
			float rimWidth = abs(_DissolveRimWidth) * 0.005;
			float3 rimCol = max(0,(rimWidth-rim)/rimWidth) * _Color.rgb * _DissolveRimBrightness;
			col += rimCol;
		}
	#endif
}

void ApplyTransparency(v2f i, float alpha){
	#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
		float2 screenUVs = i.screenPos.xy/(i.screenPos.w+0.0000000001);
		#if UNITY_SINGLE_PASS_STEREO
			screenUVs.x *= 2;
		#endif
		
		#if defined(_ALPHATEST_ON)
			if (_BlendMode == 1)
				clip(alpha - _Cutoff);
			#if defined(UNITY_PASS_SHADOWCASTER)
				else if (_BlendMode >= 2)
					clip(Dither(screenUVs, alpha));
			#elif defined(OUTLINE)
				else if (_BlendMode == 2)
					clip(Dither(screenUVs, alpha));
			#endif
		#else
			clip(Dither(screenUVs, alpha));
		#endif

		
		if (_EnableDissolve == 1)
			clip(tex2D(_DissolveTex, i.uvDis).r - _DissolveAmt);
	#endif
}
#endif