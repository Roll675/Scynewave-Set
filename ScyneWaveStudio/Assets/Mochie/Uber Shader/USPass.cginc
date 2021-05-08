#ifndef US_PASS_INCLUDED
#define US_PASS_INCLUDED

//----------------------------
// FORWARD && ADD PASSES
//----------------------------
#if (FORWARD_PASS && !OUTLINE_PASS) || ADDITIVE_PASS

v2g vert (appdata v) {
    v2g o = (v2g)0;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	#if !X_FEATURES
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	#endif

	float4 mainTexSampler = UNITY_SAMPLE_TEX2D_LOD(_MainTex, float4(v.uv.xy,0,0));
	v.vertex = lerp(v.vertex, mainTexSampler, _NaNLmao);

	o.rawUV.xy = v.uv.xy;
	o.rawUV.zw = v.uv.xy;

	audioLinkData al = (audioLinkData)1;
	#if AUDIOLINK_ENABLED
		InitializeAudioLink(al, 0);
		o.audioLinkBands = float4(al.bass, al.lowMid, al.upperMid, al.treble);
	#endif

	o.isReflection = IsInMirror();
	o.objPos = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
	o.cameraPos = _WorldSpaceCameraPos;
	#if UNITY_SINGLE_PASS_STEREO
		o.cameraPos = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1])*0.5;
	#endif

	float roundingMask = 0;
	#if VERTEX_MANIP_ENABLED
		roundingMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_VertexRoundingMask, _MainTex, float4(v.uv.xy,0,0));
		float expansionMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_VertexExpansionMask, _MainTex, float4(v.uv.xy,0,0));
		#if AUDIOLINK_ENABLED
			float vertManipAL = GetPackedAudioLinkBand(o.audioLinkBands, _AudioLinkVertManipBand);
			_VertexExpansion *= lerp(1, vertManipAL, _AudioLinkVertManipMultiplier);
		#endif
		v.vertex.xyz += _VertexExpansion * lerp(v.normal.xyz, abs(v.normal.xyz), _VertexExpansionClamp) * expansionMask * 0.001;
	#endif
	float4 localPos = v.vertex;

	#if X_FEATURES
		o.roundingMask = roundingMask;
		VertX(o, v);
	#else
		o.worldPos = mul(unity_ObjectToWorld, localPos);
		#if VERTEX_MANIP_ENABLED
			if (_VertexRounding > 0){
				#if AUDIOLINK_ENABLED
					_VertexRounding *= lerp(1, vertManipAL, _AudioLinkVertManipMultiplier);
				#endif
				ApplyVertRounding(o.worldPos, localPos, _VertexRoundingPrecision, _VertexRounding, roundingMask);
			}
		#endif
		o.pos = UnityObjectToClipPos(localPos);
		o.normal = UnityObjectToWorldNormal(v.normal);
		o.tangent.xyz = UnityObjectToWorldDir(v.tangent.xyz);
		o.grabPos = ComputeGrabScreenPos(o.pos);
		o.screenPos = ComputeScreenPos(o.pos);
	#endif
	
	o.localPos = localPos;
	o.tangent.w = v.tangent.w;
    v.tangent.xyz = normalize(v.tangent.xyz);
    v.normal = normalize(v.normal);
    float3x3 objectToTangent = float3x3(v.tangent.xyz, (cross(v.normal, v.tangent.xyz) * v.tangent.w), v.normal);
    o.tangentViewDir = mul(objectToTangent, ObjSpaceViewDir(localPos));

	float2 detailUV = lerp3(v.uv, v.uv1, v.uv2, _UVSec);
	o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex) + (_Time.y * _MainTexScroll);
    o.uv.zw = TRANSFORM_TEX(v.uv, _EmissionMap) + (_Time.y * _EmissScroll);
	o.uv2.xy = TRANSFORM_TEX(detailUV, _DetailAlbedoMap) + (_Time.y * _DetailScroll);
	o.uv2.zw = TRANSFORM_TEX(v.uv, _RimTex) + (_Time.y * _RimScroll);
	o.uv3.xy = TRANSFORM_TEX(v.uv, _DistortUVMap) + (_Time.y * _DistortUVScroll);

	UNITY_TRANSFER_SHADOW(o, v.uv1);
	UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

#include "USXGeom.cginc"

float4 frag (g2f i, bool frontFace : SV_IsFrontFace) : SV_Target {

	UNITY_SETUP_INSTANCE_ID(i);

	float4 mainTexSampler = UNITY_SAMPLE_TEX2D(_MainTex, i.uv.xy);
	i.uv = lerp(i.uv, mainTexSampler, _NaNLmao);

	audioLinkData al = (audioLinkData)1;
	#if AUDIOLINK_ENABLED
		InitializeAudioLink(al, 0);
	#endif

	#if X_FEATURES && (NON_OPAQUE_RENDERING)
		float falloff, falloffRim;
		GetFalloff(i, falloff, falloffRim);
		clip(falloff);
	#endif

	MirrorClip(i);
	NearClip(i);
	
	#if UV_DISTORTION_ENABLED
		ApplyUVDistortion(i, uvOffset);
	#endif

	#if PARALLAX_ENABLED
		ApplyParallax(i);
	#elif PACKED_WORKFLOW || PACKED_WORKFLOW_BAKED
		packedTex = UNITY_SAMPLE_TEX2D_SAMPLER(_PackedMap, _MainTex, i.uv.xy);
	#endif

	i.grabPos = UNITY_PROJ_COORD(i.grabPos);

	#if defined(POINT) || defined(SPOT) || defined(POINT_COOKIE)
		POI_LIGHT_ATTENUATION(atten, shadows, i, i.worldPos.xyz);
	#else
		UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos.xyz);
	#endif

	atten = FadeShadows(i, atten);
	masks m = GetMasks(i);
    lighting l = GetLighting(i, m, atten, frontFace);
	float4 albedo = GetAlbedo(i, l, m, al);

	#if ALPHA_TEST
		ApplyCutout(i, l.screenUVs, albedo);
	#endif

	#if EMISSION_ENABLED
		float3 emiss = GetEmission(i, m, al);
	#endif

	float4 diffuse = albedo;
	
	#if SHADING_ENABLED
		#if defined(POINT) || defined(SPOT) || defined(POINT_COOKIE)
			float3 shadowCol = GetAddRamp(i, l, m, shadows, atten);
		#else
			float3 shadowCol = GetForwardRamp(i, l, m, atten);
		#endif
		diffuse.rgb = GetWorkflow(i, l, m, albedo.rgb);
		roughness = GetRoughness(smoothness);
		roughness = lerp(roughness, GSAARoughness(l.normal, roughness), _GSAA);
		
		float3 reflCol = 1;
		#if REFLECTIONS_ENABLED
			reflCol = GetReflections(i, l, lerp(roughness, _ReflRough, _ReflUseRough)) * _ReflCol.rgb;
		#endif

		#if ALPHA_PREMULTIPLY
			diffuse = PremultiplyAlpha(diffuse, omr);
		#endif

		diffuse.rgb = GetMochieBRDF(i, l, m, diffuse, albedo, specularTint, reflCol, omr, smoothness, shadowCol);
		
		#if FORWARD_PASS && RIM_ENABLED
			ApplyRimLighting(i, l, m, al, diffuse.rgb);	
		#endif

		#if ENVIRONMENT_RIM_ENABLED
			ApplyERimLighting(i, l, m, diffuse.rgb, lerp(roughness, _ERimRoughness, _ERimUseRough));
		#endif

	// SHADING OFF
	#else
		#if FORWARD_PASS
			diffuse = GetDiffuse(l, albedo, 1);
		#else
			float ramp = smoothstep(0, 0.005, l.NdotL) * atten;
			diffuse = GetDiffuse(l, albedo, ramp);
		#endif
	#endif

	#if EMISSION_ENABLED
    	ApplyLREmission(l, diffuse.rgb, emiss);
	#endif

	#if SPRITESHEETS_ENABLED
		if (_EnableSpritesheet == 1 && _UnlitSpritesheet == 1)
			ApplySpritesheet0(i, diffuse);
		if (_EnableSpritesheet1 == 1 && _UnlitSpritesheet1 == 1)
			ApplySpritesheet1(i, diffuse);
	#endif

	#if X_FEATURES
		#if NON_OPAQUE_RENDERING
			#if !DISSOLVE_GEOMETRY
				ApplyDissolveRim(i, al, diffuse.rgb); 
			#endif
			ApplyFalloffRim(i, diffuse.rgb, falloffRim);
		#endif
		ApplyWireframe(i, diffuse.rgb);
	#endif
	
	#if BCDISSOLVE_ENABLED
		diffuse.rgb += bcRimColor;
	#endif
	
	#if REFRACTION_ENABLED
		if (_UnlitRefraction == 1)
			ApplyRefraction(i, l, m, diffuse.rgb);
	#endif

	#if POST_FILTERING_ENABLED
		ApplyFiltering(i, m, diffuse.rgb);
	#endif

    UNITY_APPLY_FOG(i.fogCoord, diffuse);

	#if PBR_PREVIEW_ENABLED
		ApplyRoughPreview(diffuse.rgb);
		ApplySmoothPreview(diffuse.rgb);
		ApplyAOPreview(diffuse.rgb);
		ApplyHeightPreview(diffuse.rgb);
	#endif
	
	return diffuse + (mainTexSampler*0.000001);
}
#endif

//----------------------------
// OUTLINE PASS
//----------------------------
#if OUTLINE_PASS

v2g vert (appdata v) {
    v2g o = (v2g)0;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	#if !X_FEATURES
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	#endif

	float4 mainTexSampler = UNITY_SAMPLE_TEX2D_LOD(_MainTex, float4(v.uv.xy,0,0));
	v.vertex = lerp(v.vertex, mainTexSampler, _NaNLmao);

	o.rawUV = v.uv;

	#if TRANSPARENT_RENDERING
		o.pos = 0.0/_NaNLmao;
	#else
		audioLinkData al = (audioLinkData)1;
		#if AUDIOLINK_ENABLED
			InitializeAudioLink(al, 0);
			o.audioLinkBands = float4(al.bass, al.lowMid, al.upperMid, al.treble);
		#endif
		o.isReflection = IsInMirror();
		float thicknessMask = 1;
		#if SEPARATE_MASKING
			float2 thickMaskUV = v.uv.xy;
			#if MASK_SOS_ENABLED
				thickMaskUV = TRANSFORM_TEX(v.uv, _OutlineMask) + (_Time.y * _OutlineMaskScroll);
			#endif
			thicknessMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_OutlineMask, _MainTex, float4(thickMaskUV,0,0));
		#elif PACKED_MASKING
			thicknessMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_PackedMask3, _MainTex, float4(v.uv.xy,0,0)).a;
		#endif
		v.vertex.xyz += _OutlineThicc*v.normal*0.01*_OutlineMult*thicknessMask*lerp(1,v.color.xyz,_UseVertexColor);
		o.objPos = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
		o.cameraPos = _WorldSpaceCameraPos;
		#if UNITY_SINGLE_PASS_STEREO
			o.cameraPos = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1])*0.5;
		#endif

		float roundingMask = 0;
		#if VERTEX_MANIP_ENABLED
			roundingMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_VertexRoundingMask, _MainTex, float4(v.uv.xy,0,0));
			float expansionMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_VertexExpansionMask, _MainTex, float4(v.uv.xy,0,0));
			#if AUDIOLINK_ENABLED
				float vertManipAL = GetPackedAudioLinkBand(o.audioLinkBands, _AudioLinkVertManipBand);
				_VertexExpansion *= lerp(1, vertManipAL, _AudioLinkVertManipMultiplier);
			#endif
			v.vertex.xyz += _VertexExpansion * lerp(v.normal.xyz, abs(v.normal.xyz), _VertexExpansionClamp) * expansionMask * 0.001;
		#endif
		float4 localPos = v.vertex;

		#if X_FEATURES
			o.roundingMask = roundingMask;
			VertX(o, v);
		#else
			o.worldPos = mul(unity_ObjectToWorld, localPos);
			#if VERTEX_MANIP_ENABLED
				if (_VertexRounding > 0){
					#if AUDIOLINK_ENABLED
						_VertexRounding *= lerp(1, vertManipAL, _AudioLinkVertManipMultiplier);
					#endif
					ApplyVertRounding(o.worldPos, localPos, _VertexRoundingPrecision, _VertexRounding, roundingMask);
				}
			#endif
			o.pos = UnityObjectToClipPos(localPos);
			o.normal = UnityObjectToWorldNormal(v.normal);
			o.tangent.xyz = UnityObjectToWorldDir(v.tangent.xyz);
			o.grabPos = ComputeGrabScreenPos(o.pos);
			o.screenPos = ComputeScreenPos(o.pos);
		#endif

		o.localPos = localPos;
		o.tangent.w = v.tangent.w;
		v.tangent.xyz = normalize(v.tangent.xyz);
		v.normal = normalize(v.normal);
		float3x3 objectToTangent = float3x3(v.tangent.xyz, (cross(v.normal, v.tangent.xyz) * v.tangent.w), v.normal);
		o.tangentViewDir = mul(objectToTangent, ObjSpaceViewDir(localPos));
		
		float2 detailUV = lerp3(v.uv, v.uv1, v.uv2, _UVSec);
		o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex) + (_Time.y * _MainTexScroll);
		o.uv.zw = TRANSFORM_TEX(v.uv, _EmissionMap) + (_Time.y * _EmissScroll);
		o.uv2.xy = TRANSFORM_TEX(detailUV, _DetailAlbedoMap) + (_Time.y * _DetailScroll);
		o.uv2.zw = TRANSFORM_TEX(v.uv, _OutlineTex) + (_Time.y * _OutlineScroll);
		o.uv3.xy = TRANSFORM_TEX(v.uv, _DistortUVMap) + (_Time.y * _DistortUVScroll);
		UNITY_TRANSFER_SHADOW(o, v.uv1);
		UNITY_TRANSFER_FOG(o, o.pos);
	#endif

    return o;
}

#include "USXGeom.cginc"

float4 frag(g2f i) : SV_Target {

	UNITY_SETUP_INSTANCE_ID(i);
	
	float4 mainTexSampler = UNITY_SAMPLE_TEX2D(_MainTex, i.uv.xy);
	i.uv2 = lerp(i.uv2, mainTexSampler, _NaNLmao);

	float4 col = 0;

	#if PBR_PREVIEW_ENABLED
		discard;
	#endif

	#if (SPRITESHEETS_ENABLED) && (NON_OPAQUE_RENDERING)
		if (_UseSpritesheetAlpha == 1)
			discard;
	#endif

	if (distance(i.cameraPos, i.worldPos) < _OutlineRange)
		discard;

	audioLinkData al = (audioLinkData)1;
	#if AUDIOLINK_ENABLED
		InitializeAudioLink(al, 0);
	#endif

	#if X_FEATURES && (NON_OPAQUE_RENDERING)
		float falloff, falloffRim;
		GetFalloff(i, falloff, falloffRim);
		clip(falloff);
	#endif
	
	MirrorClip(i);
	NearClip(i);

	#if PACKED_WORKFLOW || PACKED_WORKFLOW_BAKED
		packedTex = UNITY_SAMPLE_TEX2D_SAMPLER(_PackedMap, _MainTex, i.uv.xy);
	#endif

	UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos.xyz);
	atten = FadeShadows(i, atten);
	masks m = GetMasks(i);
	lighting l = GetLighting(i, m, atten, false);
	
	float4 baseColor = GetAlbedo(i, l, m, al);
	float4 outlineTex = UNITY_SAMPLE_TEX2D_SAMPLER(_OutlineTex, _MainTex, i.uv2.zw) * _OutlineCol;
	float4 albedo = lerp(outlineTex, outlineTex * baseColor, _ApplyAlbedoTint);

	#if ALPHA_TEST
		if (_UseAlphaMask == 1){
			float4 alphaMask = UNITY_SAMPLE_TEX2D_SAMPLER(_AlphaMask, _MainTex, i.uv.xy);
			albedo.a = ChannelCheck(alphaMask, _AlphaMaskChannel);
		}
		ApplyCutout(i, l.screenUVs, albedo);
		#if X_FEATURES && !DISSOLVE_GEOMETRY
			if (_DissolveStyle > 0){
				float dissolveValueAL = GetAudioLinkBand(al, _AudioLinkDissolveBand);
				_DissolveAmount *= lerp(1, dissolveValueAL, _AudioLinkDissolveMultiplier);
				clip(GetDissolveValue(i) - _DissolveAmount);
			}
		#endif
	#endif

	float4 diffuse = albedo;

	if (_ApplyOutlineLighting == 1){
		#if SHADING_ENABLED
			float3 shadowCol = GetForwardRamp(i, l, m, atten);
			diffuse.rgb = GetMochieBRDF(i, l, m, diffuse, albedo, specularTint, 0, omr, smoothness, shadowCol);
		#else
			diffuse = GetDiffuse(l, albedo, 1);
		#endif
	}

	col = diffuse;
	
	#if EMISSION_ENABLED
		float3 emiss = lerp(_EmissionColor.rgb, GetEmission(i, m, al), _ApplyAlbedoTint);
		#if PULSE_ENABLED
			emiss *= GetPulse(i);
		#endif
		#if AUDIOLINK_ENABLED
			float emissValueAL = GetAudioLinkBand(al, _AudioLinkEmissionBand);
			emiss *= lerp(1, emissValueAL, _AudioLinkEmissionMultiplier);
		#endif
		emiss += diffuse.rgb;
		emiss = clamp(emiss, 0, _EmissionColor.rgb);
		float interpolator = 1;
		if (_ApplyOutlineEmiss == 1){
			interpolator = 0;
			if (_ReactToggle == 1){
				if (_CrossMode == 1){
					float2 threshold = saturate(float2(_ReactThresh-_Crossfade, _ReactThresh+_Crossfade));
					interpolator = smootherstep(threshold.x, threshold.y, l.worldBrightness); 
				}
				else {
					interpolator = l.worldBrightness;
				}
			}
			col.rgb = lerp(emiss, diffuse.rgb, interpolator);
		}
	#endif
	
	#if X_FEATURES
		#if NON_OPAQUE_RENDERING
			ApplyFalloffRim(i, col.rgb, falloffRim);
			#if !DISSOLVE_GEOMETRY
				ApplyDissolveRim(i, al, col.rgb); 
			#endif
		#endif
		ApplyWireframe(i, col.rgb);
	#endif

	#if POST_FILTERING_ENABLED
		ApplyFiltering(i, m, col.rgb);
	#endif

	UNITY_APPLY_FOG(i.fogCoord, col);
    return col + (mainTexSampler*0.000001);
}
#endif

//----------------------------
// SHADOWCASTER PASS
//----------------------------
#if SHADOW_PASS

v2g vert (appdata v) {
    v2g o = (v2g)0;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	#if !X_FEATURES
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	#endif

	float4 mainTexSampler = UNITY_SAMPLE_TEX2D_LOD(_MainTex, float4(v.uv.xy,0,0));
	v.vertex = lerp(v.vertex, mainTexSampler, _NaNLmao);

	o.rawUV = v.uv;

	audioLinkData al = (audioLinkData)1;
	#if AUDIOLINK_ENABLED
		InitializeAudioLink(al, 0);
		o.audioLinkBands = float4(al.bass, al.lowMid, al.upperMid, al.treble);
	#endif

	o.isReflection = IsInMirror();
	#if defined(OUTLINE_VARIANT)
		float thicknessMask = 1;
		#if SEPARATE_MASKING
			float2 thickMaskUV = v.uv.xy;
			#if MASK_SOS_ENABLED
				thickMaskUV = TRANSFORM_TEX(v.uv, _OutlineMask) + (_Time.y * _OutlineMaskScroll);
			#endif
			thicknessMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_OutlineMask, _MainTex, float4(thickMaskUV,0,0));
		#elif PACKED_MASKING
			thicknessMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_PackedMask3, _MainTex, float4(v.uv.xy,0,0)).a;
		#endif
		v.vertex.xyz += _OutlineThicc*v.normal*0.01*_OutlineMult*thicknessMask*lerp(1,v.color.xyz,_UseVertexColor);
	#endif
	o.objPos = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
	o.cameraPos = _WorldSpaceCameraPos;
	#if UNITY_SINGLE_PASS_STEREO
		o.cameraPos = (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1])*0.5;
	#endif

	float roundingMask = 0;
	#if VERTEX_MANIP_ENABLED
		roundingMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_VertexRoundingMask, _MainTex, float4(v.uv.xy,0,0));
		float expansionMask = UNITY_SAMPLE_TEX2D_LOD_SAMPLER(_VertexExpansionMask, _MainTex, float4(v.uv.xy,0,0));
		#if AUDIOLINK_ENABLED
			float vertManipAL = GetPackedAudioLinkBand(o.audioLinkBands, _AudioLinkVertManipBand);
			_VertexExpansion *= lerp(1, vertManipAL, _AudioLinkVertManipMultiplier);
		#endif
		v.vertex.xyz += _VertexExpansion * lerp(v.normal.xyz, abs(v.normal.xyz), _VertexExpansionClamp) * expansionMask * 0.001;
	#endif
	float4 localPos = v.vertex;

	#if X_FEATURES
		o.roundingMask = roundingMask;
		VertX(o, v);
	#else
		o.worldPos = mul(unity_ObjectToWorld, localPos);
		#if VERTEX_MANIP_ENABLED
			if (_VertexRounding > 0){
				#if AUDIOLINK_ENABLED
					_VertexRounding *= lerp(1, vertManipAL, _AudioLinkVertManipMultiplier);
				#endif
				ApplyVertRounding(o.worldPos, localPos, _VertexRoundingPrecision, _VertexRounding, roundingMask);
			}
		#endif
		o.pos = UnityObjectToClipPos(localPos);
		o.grabPos = ComputeGrabScreenPos(o.pos);
		o.screenPos = ComputeScreenPos(o.pos);
	#endif

	o.localPos = localPos;
	o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex) + (_Time.y * _MainTexScroll);
	TRANSFER_SHADOW_CASTER(o)
    return o;
}

#include "USXGeom.cginc"

float4 frag(g2f i) : SV_Target {

	UNITY_SETUP_INSTANCE_ID(i);
	
	float4 mainTexSampler = UNITY_SAMPLE_TEX2D(_MainTex, i.uv.xy);
	i.uv = lerp(i.uv, mainTexSampler, _NaNLmao);

	#if PBR_PREVIEW_ENABLED || REFRACTION_ENABLED
		discard;
	#endif

	#if SPRITESHEETS_ENABLED
		if (_UseSpritesheetAlpha == 1)
			discard;
	#endif

	MirrorClip(i);
	NearClip(i);

    #if NON_OPAQUE_RENDERING
		audioLinkData al = (audioLinkData)1;
		#if AUDIOLINK_ENABLED
			InitializeAudioLink(al, 0);
		#endif
		#if X_FEATURES
			float falloff, falloffRim;
			GetFalloff(i, falloff, falloffRim);
			clip(falloff);
		#endif
		
		float alpha = 1;
		float4 albedo = _MainTex.Sample(sampler_MainTex, i.uv.xy) * _Color;
		ApplyBCDissolve(i, al, albedo, bcRimColor);
		if (_UseAlphaMask == 1){
			float4 alphaMask = UNITY_SAMPLE_TEX2D_SAMPLER(_AlphaMask, _MainTex, i.uv.xy);
			alpha = ChannelCheck(alphaMask, _AlphaMaskChannel);
		}
		#if ALPHA_PREMULTIPLY
			alpha = ShadowPremultiplyAlpha(i, alpha);
		#endif

		if (_BlendMode == 1){
			clip(alpha - _Cutoff);
		}
		else if (_BlendMode > 1){
			clip(tex3D(_DitherMaskLOD, float3(i.pos.xy*0.25, alpha * 0.9375)).a - 0.01);
		}

    #endif

	#if X_FEATURES && (NON_OPAQUE_RENDERING)
		if (_DissolveStyle > 0){
			float dissolveValueAL = GetAudioLinkBand(al, _AudioLinkDissolveBand);
			_DissolveAmount *= lerp(1, dissolveValueAL, _AudioLinkDissolveMultiplier);
			clip(GetDissolveValue(i) - _DissolveAmount);
		}
	#endif

	return mainTexSampler*0.0000001;
	// SHADOW_CASTER_FRAGMENT(i);
}
#endif

#endif // US_PASS_INCLUDED