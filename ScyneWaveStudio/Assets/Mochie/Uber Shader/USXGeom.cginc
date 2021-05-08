#ifndef USX_GEOM_INCLUDED
#define USX_GEOM_INCLUDED

#if X_FEATURES

#if CLONES_ENABLED
[instance(9)]
[maxvertexcount(3)]
void geom(triangle v2g i[3], inout TriangleStream<g2f> tristream, uint instanceID : SV_GSInstanceID, uint primID : SV_PrimitiveID){

	#if SHADOW_PASS
		if (_Screenspace == 1)
			return;
	#endif

	g2f o = (g2f)0;

	DEFAULT_UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i[0]);

	float4 offset = GetCloneCoords(instanceID);
	_Visibility = saturate(_Visibility * offset.w);
	uint check = step(0.00001, _Visibility*any(offset.xyz))*instanceID;
	
	if (instanceID != check)
		return;

	float3 edgeA = i[1].worldPos - i[2].worldPos;
	float3 edgeB = i[2].worldPos - i[0].worldPos;
	float3 edgeC = i[0].worldPos - i[1].worldPos;
	float3 edges = float3(length(edgeC), length(edgeA), length(edgeB));
	float3 normalDir = normalize(cross(edgeA, edgeB));

	float3 bCoords[3];
	GetBarycentricCoords(bCoords, edges);

	float wfStr = lerp(0, _WFVisibility, _WireframeToggle);
	float wfOpac = lerp(0, _WFFill, _WireframeToggle);

	[unroll(3)]
	for (uint j = 0; j < 3; j++){

		float3 wPos = i[j].worldPos;
		wPos += offset.xyz;
		if (instanceID != 0){
			float3 nDir = lerp(normalDir, abs(normalDir), _SaturateEP);
			wPos += (((1/_Visibility)-1) * nDir) * _EntryPos;
		}

		if (_ShatterToggle == 1){
			float dist = distance(wPos, i[j].cameraPos);
			float objDist = distance(i[j].objPos, i[j].cameraPos);
			float shatterAmt = smoothstep(_ShatterMax, _ShatterMin, dist)*_ShatterSpread;
			if (_ShatterClones == 1){
				if (instanceID != 0){
					if (dist < _ShatterCull)
						return;
					#if OUTLINE_PASS
						if (shatterAmt > 0)
							return;
					#endif

					wPos += shatterAmt*normalDir;
				}
			}
			else {
				if (dist < _ShatterCull)
					return;
				#if OUTLINE_PASS
					if (shatterAmt > 0)
						return;
				#endif

				wPos += shatterAmt*normalDir;	
			}
		}
		
		#if DISSOLVE_GEOMETRY
			float dissolveValueAL = 1;
			#if AUDIOLINK_ENABLED
				dissolveValueAL = GetPackedAudioLinkBand(i[j].audioLinkbands, _AudioLinkDissolveBand);
			#endif
			float scanLine = 0;
			float axisPos = 0;
			if (_GeomDissolveAxis > 2){
				float3 direction = normalize(_DissolvePoint1 - _DissolvePoint0);
				scanLine = dot(direction, i[j].localPos) + _GeomDissolveAmount;
			}
			else {
				if (_GeomDissolveAxis == 0)
					axisPos = i[j].localPos.x;
				else if (_GeomDissolveAxis == 1)
					axisPos = i[j].localPos.y;
				else
					axisPos = i[j].localPos.z;
				axisPos = lerp(axisPos, 1-axisPos, _GeomDissolveAxisFlip);
				scanLine = lerp(_GeomDissolveAmount, 1-_GeomDissolveAmount, _GeomDissolveAxisFlip);
			}
			float scanLineOffset = _GeomDissolveWidth * 0.5;
			float boundLower = scanLine - scanLineOffset;
			float boundUpper = scanLine + scanLineOffset;
			float interp = smootherstep(boundLower, boundUpper, axisPos);
			#if AUDIOLINK_ENABLED
				interp *= lerp(1, dissolveValueAL, _AudioLinkDissolveMultiplier);
			#endif
			float wfInterp = smootherstep(boundLower - _GeomDissolveWidth*1.25, boundUpper, axisPos);
			float opacInterp = smootherstep(boundLower - scanLine*1.5, boundUpper, axisPos);
			wfStr = saturate(wfStr * lerp(1, wfInterp, _GeomDissolveWireframe));
			wfOpac = saturate(wfOpac * lerp(1, opacInterp, _GeomDissolveWireframe));

			if (_DissolveClones == 1){
				if (instanceID != 0){
					#if OUTLINE_PASS
						if (interp > 0)
							return;
					#endif

					wPos += lerp(0, lerp(normalDir, abs(normalDir), _GeomDissolveClamp) * _GeomDissolveSpread, interp);

					if (axisPos > boundUpper+_GeomDissolveClip)
						return;
				}
			}
			else {
				#if OUTLINE_PASS
					if (interp > 0)
						return;
				#endif

				float3 offsetDir = lerp(normalDir, abs(normalDir), _GeomDissolveClamp);
				float3 offsetPos = lerp(0, offsetDir * _GeomDissolveSpread, interp);
				wPos += offsetPos;
				
				if (any(offsetPos) && (primID % _GeomDissolveFilter != 0))
					return;

				if (axisPos > boundUpper+_GeomDissolveClip)
					return;
			}
		#endif
		
		if (_GlitchToggle == 1){
			float noise = GetNoise(wPos.xy*frac(_Time.y));
			if (_GlitchClones == 1){
				if (instanceID != 0){
					wPos.xyz += ((noise*_GlitchIntensity) * normalDir) * (noise > 0.99999-_GlitchFrequency);
					wPos.xyz += ((noise*_Instability) * normalDir);
				}
			}
			else {
				wPos.xyz += ((noise*_GlitchIntensity) * normalDir) * (noise > 0.99999-_GlitchFrequency);
				wPos.xyz += ((noise*_Instability) * normalDir);	
			}
		}

		o.pos = UnityWorldToClipPos(wPos);
		o.rawUV = i[j].rawUV;
		o.uv = i[j].uv;
		o.uv1 = i[j].uv1;
		o.uv2 = i[j].uv2;
		o.uv3 = i[j].uv3;
		o.worldPos = float4(wPos, i[j].worldPos.w);
		o.binormal = i[j].binormal;
		o.tangentViewDir = i[j].tangentViewDir;
		o.cameraPos = i[j].cameraPos;
		o.objPos = i[j].objPos;
		o.bCoords = bCoords[j];
		o.WFStr = wfStr;
		o.wfOpac = wfOpac;
		o.instID = instanceID;
		o.screenPos = i[j].screenPos;
		o.isReflection = i[j].isReflection;
		o.localPos = i[j].localPos;
		
		o.tangent = i[j].tangent;
		o.normal = i[j].normal;
		
        #if defined(SHADOWS_SCREEN) || (defined(SHADOWS_DEPTH) && defined(SPOT)) || defined(SHADOWS_CUBE)
			UNITY_TRANSFER_SHADOW(o, i[j].pos);
        #endif
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		UNITY_TRANSFER_FOG(o, o.pos);
		tristream.Append(o);
	}   
	tristream.RestartStrip();
}

#else // CLONES_ENABLED

[maxvertexcount(3)]
void geom(triangle v2g i[3], inout TriangleStream<g2f> tristream, uint instanceID : SV_GSInstanceID, uint primID : SV_PrimitiveID){

	#if SHADOW_PASS
		if (_Screenspace == 1)
			return;
	#endif

	g2f o = (g2f)0;

	DEFAULT_UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i[0]);

	float3 edgeA = i[1].worldPos - i[2].worldPos;
	float3 edgeB = i[2].worldPos - i[0].worldPos;
	float3 edgeC = i[0].worldPos - i[1].worldPos;
	float3 edges = float3(length(edgeC), length(edgeA), length(edgeB));
	float3 normalDir = normalize(cross(edgeA, edgeB));

	float3 bCoords[3];
	GetBarycentricCoords(bCoords, edges);

	float wfStr = lerp(0, _WFVisibility, _WireframeToggle);
	float wfOpac = lerp(0, _WFFill, _WireframeToggle);

	[unroll(3)]
	for (uint j = 0; j < 3; j++){

		float3 wPos = i[j].worldPos;

		if (_ShatterToggle == 1){
			float dist = distance(wPos, i[j].cameraPos);
			float objDist = distance(i[j].objPos, i[j].cameraPos);
			if (dist < _ShatterCull)
				return;
			float shatterAmt = smoothstep(_ShatterMax, _ShatterMin, dist)*_ShatterSpread;

			#if OUTLINE_PASS
				if (shatterAmt > 0)
					return;
			#endif

			wPos += shatterAmt*normalDir;
		}
		
		#if DISSOLVE_GEOMETRY
			float dissolveValueAL = 1;
			#if AUDIOLINK_ENABLED
				dissolveValueAL = GetPackedAudioLinkBand(i[j].audioLinkBands, _AudioLinkDissolveBand);
			#endif
			float scanLine = 0;
			float axisPos = 0;
			if (_GeomDissolveAxis > 2){
				float3 direction = normalize(_DissolvePoint1 - _DissolvePoint0);
				scanLine = dot(direction, i[j].localPos) + _GeomDissolveAmount;
			}
			else {
				if (_GeomDissolveAxis == 0)
					axisPos = i[j].localPos.x;
				else if (_GeomDissolveAxis == 1)
					axisPos = i[j].localPos.y;
				else
					axisPos = i[j].localPos.z;
				axisPos = lerp(axisPos, 1-axisPos, _GeomDissolveAxisFlip);
				scanLine = lerp(_GeomDissolveAmount, 1-_GeomDissolveAmount, _GeomDissolveAxisFlip);
			}
			float scanLineOffset = _GeomDissolveWidth * 0.5;
			float boundLower = scanLine - scanLineOffset;
			float boundUpper = scanLine + scanLineOffset;
			float interp = smootherstep(boundLower, boundUpper, axisPos);
			#if AUDIOLINK_ENABLED
				interp *= lerp(1, dissolveValueAL, _AudioLinkDissolveMultiplier);
			#endif
			#if OUTLINE_PASS
				if (interp > 0)
					return;
			#endif

			float3 offsetDir = lerp(normalDir, abs(normalDir), _GeomDissolveClamp);
			float3 offsetPos = lerp(0, offsetDir * _GeomDissolveSpread, interp);
			wPos += offsetPos;
			
			if (any(offsetPos) && (primID % _GeomDissolveFilter != 0))
				return;

			if (axisPos > boundUpper + _GeomDissolveClip)
				return;

			float wfInterp = smootherstep(boundLower - _GeomDissolveWidth*1.25, boundUpper, axisPos);
			float opacInterp = smootherstep(boundLower - scanLine*1.25, boundUpper, axisPos);
			wfStr = saturate(wfStr * lerp(1, wfInterp, _GeomDissolveWireframe));
			wfOpac = saturate(wfOpac * lerp(1, opacInterp, _GeomDissolveWireframe));
		#endif
		
		if (_GlitchToggle == 1){
			float noise = GetNoise(wPos.xy*frac(_Time.y));
			wPos.xyz += ((noise*_GlitchIntensity) * normalDir) * (noise > 0.99999-_GlitchFrequency);
			wPos.xyz += ((noise*_Instability) * normalDir);
		}

		o.pos = UnityWorldToClipPos(wPos);
		o.rawUV = i[j].rawUV;
		o.uv = i[j].uv;
		o.uv1 = i[j].uv1;
		o.uv2 = i[j].uv2;
		o.uv3 = i[j].uv3;
		o.worldPos = float4(wPos, i[j].worldPos.w);
		o.binormal = i[j].binormal;
		o.tangentViewDir = i[j].tangentViewDir;
		o.cameraPos = i[j].cameraPos;
		o.objPos = i[j].objPos;
		o.bCoords = bCoords[j];
		o.WFStr = wfStr;
		o.wfOpac = wfOpac;
		o.instID = instanceID;
		o.grabPos = i[j].grabPos;
		o.screenPos = i[j].screenPos;
		o.isReflection = i[j].isReflection;
		o.localPos = i[j].localPos;
		
		o.tangent = i[j].tangent;
		o.normal = i[j].normal;
		
        #if defined(SHADOWS_SCREEN) || (defined(SHADOWS_DEPTH) && defined(SPOT)) || defined(SHADOWS_CUBE)
			UNITY_TRANSFER_SHADOW(o, i[j].pos);
        #endif
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		UNITY_TRANSFER_FOG(o, o.pos);
		tristream.Append(o);
	}   
	tristream.RestartStrip();
}
#endif // CLONES_ENABLED
#endif // X_FEATURES
#endif // USX_GEOM_INCLUDED