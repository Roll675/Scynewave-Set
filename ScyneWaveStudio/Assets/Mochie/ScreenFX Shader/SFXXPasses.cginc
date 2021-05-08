#ifndef SFXX_PASSES_INCLUDED
#define SFXX_PASSES_INCLUDED

#if TRIPLANAR_PASS
v2f vert (appdata v){
    v2f o;
	UNITY_INITIALIZE_OUTPUT(v2f, o);

    o.pulseSpeed = GetPulse();
    o.cameraPos = GetCameraPos();
    o.objPos = GetObjPos();
    float objDist = distance(o.cameraPos, o.objPos);

    float maxr = lerp(_TPMaxRange, _MaxRange, _TPUseGlobal);
    float minr = lerp(_TPMinRange, _MinRange, _TPUseGlobal);
    o.globalF = smoothstep(maxr, clamp(minr, 0, maxr-0.001), objDist);
	o.fogF = GetFalloff(_FogUseGlobal, o.globalF, _FogMinRange, _FogMaxRange, o.objDist);
    v.vertex.x *= 1.4;

    float4 a = mul(unity_CameraToWorld, v.vertex);
    float4 b = mul(unity_WorldToObject, a);
    o.raycast = UnityObjectToViewPos(b).xyz * float3(-1,-1,1);
    o.raycast *= (_ProjectionParams.z / o.raycast.z);
    o.pos = UnityObjectToClipPos(b);
    o.uv = ComputeGrabScreenPos(o.pos);
    return o;
}

float4 frag (v2f i) : SV_Target {

	#if !FOG_ENABLED && !TRIPLANAR_ENABLED
		discard;
	#endif

	MirrorCheck();
	float4 col = 0;

	#if TRIPLANAR_ENABLED
		float3 tpPos = lerp(i.cameraPos, i.objPos, _TPP2O);
		float radius = GetTriplanarRadius(i, tpPos, _TPRadius, _TPFade);
		col = GetTriplanar(i, _TPTexture, _TPNoiseTex, _TPTexture_ST.xy, _TPNoiseTex_ST.xy, radius) * _TPColor;
	#endif

	#if FOG_ENABLED
		ApplyFog(i, col);
	#endif
	
	col.a *= _Opacity;
	return col;
}
#endif

#if ZOOM_PASS
v2f vert (appdata v){
    v2f o = (v2f)0;

    o.pulseSpeed = GetPulse();
    o.objPos = GetObjPos();
    o.cameraPos = GetCameraPos();
    o.objDist = distance(o.cameraPos, o.objPos);

    float maxr = lerp(_ZoomMaxRange, _MaxRange, _ZoomUseGlobal);
    float minr = lerp(_ZoomMinRange, _MinRange, _ZoomUseGlobal);
    o.globalF = smoothstep(maxr, clamp(minr, 0, maxr-0.001), o.objDist);
    o.letterbF = smoothstep(_MaxRange, clamp(_MinRange, 0, _MaxRange-0.001), o.objDist);
	o.sstF = smoothstep(_SSTMaxRange, clamp(_SSTMinRange, 0, _SSTMaxRange-0.001), o.objDist);

    v.vertex.x *= 1.4;
    float4 a = mul(unity_CameraToWorld, v.vertex);
    float4 b = mul(unity_WorldToObject, a);
    o.pos = UnityObjectToClipPos(b);
    o.uv = ComputeGrabScreenPos(o.pos);
	o.uvs = GetSSTUV(o.uv);
    o.luv = o.uv.y;

    #if ZOOM_ENABLED
		o.zoomPos = ComputeScreenPos(UnityObjectToClipPos(float4(0,0,0,1)));
		o.zoom = GetZoom(o.objPos, o.cameraPos, o.objDist, _ZoomMinRange, _ZoomStr);
        o.uv = lerp(o.uv, o.zoomPos, o.zoom * o.pulseSpeed);
	#endif

    #if ZOOM_RGB_ENABLED
		o.zoomPos = ComputeScreenPos(UnityObjectToClipPos(float4(0,0,0,1)));
	    float zoomR = GetZoom(o.objPos, o.cameraPos, o.objDist, _ZoomMinRange, _ZoomStrR);
		float zoomG = GetZoom(o.objPos, o.cameraPos, o.objDist, _ZoomMinRange, _ZoomStrG);
		float zoomB = GetZoom(o.objPos, o.cameraPos, o.objDist, _ZoomMinRange, _ZoomStrB);
        o.uvR = lerp(o.uv, o.zoomPos, zoomR * o.pulseSpeed);
        o.uvG = lerp(o.uv, o.zoomPos, zoomG * o.pulseSpeed);
        o.uvB = lerp(o.uv, o.zoomPos, zoomB * o.pulseSpeed);
    #endif

    return o;
}

float4 frag (v2f i) : SV_Target {
	// #if ZOOM_ENABLED
	// 	i.zoomPos = ComputeScreenPos(UnityObjectToClipPos(float4(0,0,0,1)));
	// 	i.zoom = GetZoom(i.objPos, i.cameraPos, i.objDist, _ZoomMinRange, _ZoomStr);
    //     i.uv = lerp(i.uv, i.zoomPos, i.zoom * i.pulseSpeed);
    // #elif ZOOM_RGB_ENABLED
	// 	i.zoomPos = ComputeScreenPos(UnityObjectToClipPos(float4(0,0,0,1)));
	//     float zoomR = GetZoom(i.objPos, i.cameraPos, i.objDist, _ZoomMinRange, _ZoomStrR);
	// 	float zoomG = GetZoom(i.objPos, i.cameraPos, i.objDist, _ZoomMinRange, _ZoomStrG);
	// 	float zoomB = GetZoom(i.objPos, i.cameraPos, i.objDist, _ZoomMinRange, _ZoomStrB);
    //     i.uvR = lerp(i.uv, i.zoomPos, zoomR * i.pulseSpeed);
    //     i.uvG = lerp(i.uv, i.zoomPos, zoomG * i.pulseSpeed);
    //     i.uvB = lerp(i.uv, i.zoomPos, zoomB * i.pulseSpeed);
    // #endif

    MirrorCheck();
    DoLetterbox(i);

	#if IMAGE_OVERLAY_DISTORTION_ENABLED
		ApplySSTAD(i);
	#endif

    if (CanLetterbox(i)) 
		return float4(0,0,0,1);

    float4 col = tex2Dproj(_ZoomGrab, i.uv);

	#if ZOOM_RGB_ENABLED
		ApplyRGBZoom(i, col.rgb);
	#endif
	
	#if IMAGE_OVERLAY_ENABLED
		ApplySST(i, col.rgb);
	#endif

    return col;
}
#endif

#endif