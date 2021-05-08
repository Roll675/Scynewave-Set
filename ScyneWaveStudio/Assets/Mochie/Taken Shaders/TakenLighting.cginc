float3 BoxProjection(float3 dir, float3 pos, float4 cubePos, float3 boxMin, float3 boxMax){
    #if UNITY_SPECCUBE_BOX_PROJECTION
        UNITY_BRANCH
        if (cubePos.w > 0){
            float3 factors = ((dir > 0 ? boxMax : boxMin) - pos) / dir;
            float scalar = min(min(factors.x, factors.y), factors.z);
            dir = dir * scalar + (pos - cubePos);
        }
    #endif
    return dir;
}

float3 GetReflections(v2f i, lighting l){
	float3 reflDir = l.reflectionDir;
    float3 baseReflDir = reflDir;
    reflDir = BoxProjection(reflDir, i.worldPos, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
    float4 envSample0 = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflDir, roughness * UNITY_SPECCUBE_LOD_STEPS);
    float3 p0 = DecodeHDR(envSample0, unity_SpecCube0_HDR);
    float interpolator = unity_SpecCube0_BoxMin.w;
    UNITY_BRANCH
    if (interpolator < 0.99999){
        float3 refDirBlend = BoxProjection(baseReflDir, i.worldPos, unity_SpecCube1_ProbePosition, unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax);
        float4 envSample1 = UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(unity_SpecCube1, unity_SpecCube0, refDirBlend, roughness * UNITY_SPECCUBE_LOD_STEPS);
        float3 p1 = DecodeHDR(envSample1, unity_SpecCube1_HDR);
        p0 = lerp(p1, p0, interpolator);
    }
    return p0;
}

float3 ShadeSH9(float3 normal){
	return max(0, ShadeSH9(float4(normal,1)));
}

float3 FresnelLerp(float3 specCol, float3 grazingTerm, float NdotV){
    float t = Pow5(1 - NdotV);
    return lerp(specCol, grazingTerm, t);
}

float3 GetNormalDir(v2f i, lighting l){
	float3 normal = normalize(i.normal);
	UNITY_BRANCH
	if (_BumpScale > 0){
		float3 normalMap = UnpackScaleNormal(tex2D(_BumpMap, i.uv.xy), _BumpScale);
		normal = normalize(normalMap.x * i.tangent + normalMap.y * i.binormal + normalMap.z * i.normal);
	}
	return normal;
}

void FadeShadows (v2f i, inout float atten) {
    #if HANDLE_SHADOWS_BLENDING_IN_GI
        float viewZ = dot(_WorldSpaceCameraPos - i.worldPos, UNITY_MATRIX_V[2].xyz);
        float shadowFadeDistance = UnityComputeShadowFadeDistance(i.worldPos, viewZ);
        float shadowFade = UnityComputeShadowFade(shadowFadeDistance);
        atten = saturate(atten + shadowFade);
    #endif
}

float3 GetLightColor(lighting l){
	float3 directCol = 0;
	float3 indirectCol = 0;
	float3 lightCol = 1;
	if (_EnableLighting == 1){
		directCol = _LightColor0;
		indirectCol = lerp(float3(unity_SHAr.w, unity_SHAg.w, unity_SHAb.w), ShadeSH9(l.normal), _EnableSH);
		if (!l.lightEnv){
			directCol = indirectCol * 0.6;
			indirectCol *= 0.5;
		}
		lightCol = directCol + indirectCol;
	}
	return lightCol;
}


float GetRoughness(float smoothness){
	float rough = 1-smoothness;
    rough *= 1.7-0.7*rough;
    return rough;
}

float3 GetMetallicWorkflow(v2f i, inout float3 col){
	UNITY_BRANCH
	if (_EnableLighting == 1){
		metallic = tex2D(_MetallicMap, i.uv) * _Metallic;
		roughness = tex2D(_RoughnessMap, i.uv) * _Roughness;
		smoothness = 1-roughness;
		roughness = GetRoughness(smoothness);
		specularTint = lerp(unity_ColorSpaceDielectricSpec.rgb, col, metallic);
		omr = unity_ColorSpaceDielectricSpec.a - metallic * unity_ColorSpaceDielectricSpec.a;
		col = lerp(col, col*omr, _ReflectionStr);
	}
	return col;
}

void TakenBRDF(v2f i, lighting l, inout float3 col){
	UNITY_BRANCH
	if (_EnableLighting == 1){
		float3 reflCol = GetReflections(i, l);
		float percepRough = 1-smoothness;
		float brdfRoughness = percepRough * percepRough;
		brdfRoughness = max(brdfRoughness, 0.002);

		float3 reflections = 0;
		if (_ReflectionStr > 0){
			float surfaceReduction = 1.0 / (brdfRoughness*brdfRoughness + 1.0);
			float grazingTerm = saturate(smoothness + (1-omr));
			reflections = surfaceReduction * reflCol * FresnelLerp(specularTint, grazingTerm, l.NdotV) * _ReflectionStr * tex2D(_ReflectionMask, i.uv).r;
		}

		col *= l.lightCol;
		col += reflections;
	}
}

lighting GetLighting(v2f i){
	lighting l = (lighting)0;

	l.normal = GetNormalDir(i, l);
	l.viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
	l.binormal = cross(i.normal, i.tangent.xyz) * (i.tangent.w * unity_WorldTransformParams.w);
	l.VdotL = abs(dot(l.viewDir, l.normal));

	l.lightEnv = any(_WorldSpaceLightPos0);
	l.reflectionDir = reflect(-l.viewDir, l.normal);
	l.NdotV = abs(dot(l.normal, l.viewDir));
	l.lightCol = GetLightColor(l);

	return l;
}