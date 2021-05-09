// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Reds mirror master shader"
{
	Properties
	{
		[HideInInspector][HDR]_ReflectionTex1("_ReflectionTex1", 2D) = "white" {}
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		[HideInInspector][HDR]_ReflectionTex0("_ReflectionTex0", 2D) = "white" {}
		_Color0("Color 0", Color) = (0,0,0,0)
		_ReflectionStrength("ReflectionStrength", Range( 0 , 2)) = 0.82
		_Cutouttexture("Cutout texture", 2D) = "black" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "Transparent+0" }
		Cull Back
		Blend SrcAlpha OneMinusSrcAlpha
		CGPROGRAM
		#include "UnityCG.cginc"
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf StandardSpecular keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
		};

		uniform float4 _Color0;
		uniform float _ReflectionStrength;
		uniform sampler2D _ReflectionTex1;
		uniform sampler2D _ReflectionTex0;
		uniform sampler2D _Cutouttexture;
		uniform float4 _Cutouttexture_ST;
		uniform float _Cutoff = 0.5;


		float stereoEyeIndex6( float In0 )
		{
			return unity_StereoEyeIndex;
		}


		float4 ComputeNonStereoScreenPos3( float4 In0 )
		{
			float4 o = In0 * 0.5f;
			o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w;
			o.zw = In0.zw;
			return o;
		}


		void surf( Input i , inout SurfaceOutputStandardSpecular o )
		{
			float In06 = 0.0;
			float localstereoEyeIndex6 = stereoEyeIndex6( In06 );
			float3 ase_vertex3Pos = mul( unity_WorldToObject, float4( i.worldPos , 1 ) );
			float4 unityObjectToClipPos2 = UnityObjectToClipPos( ase_vertex3Pos );
			float4 In03 = unityObjectToClipPos2;
			float4 localComputeNonStereoScreenPos3 = ComputeNonStereoScreenPos3( In03 );
			float2 temp_output_9_0 = ( (localComputeNonStereoScreenPos3).xy / (localComputeNonStereoScreenPos3).w );
			float4 ifLocalVar8 = 0;
			if( localstereoEyeIndex6 > 0.0 )
				ifLocalVar8 = tex2D( _ReflectionTex1, temp_output_9_0 );
			else if( localstereoEyeIndex6 == 0.0 )
				ifLocalVar8 = tex2D( _ReflectionTex0, temp_output_9_0 );
			o.Albedo = ( ( _Color0 * _ReflectionStrength ) * ifLocalVar8 ).rgb;
			o.Alpha = 1;
			float2 uv_Cutouttexture = i.uv_texcoord * _Cutouttexture_ST.xy + _Cutouttexture_ST.zw;
			clip( tex2D( _Cutouttexture, uv_Cutouttexture, float2( 0,0 ), float2( 0,0 ) ).r - _Cutoff );
		}

		ENDCG
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=15301
222;92;1144;601;1939.06;500.8223;2.515374;True;False
Node;AmplifyShaderEditor.PosVertexDataNode;1;-688.5,-294;Float;False;0;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.UnityObjToClipPosHlpNode;2;-431.4919,-287.4801;Float;False;1;0;FLOAT3;0,0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CustomExpressionNode;3;-238.2469,-271.2541;Float;False;float4 o = In0 * 0.5f@$o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w@$o.zw = In0.zw@$return o@;4;False;1;True;In0;FLOAT4;0,0,0,0;In;ComputeNonStereoScreenPos;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ComponentMaskNode;7;69.5,-257;Float;False;True;True;False;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.ComponentMaskNode;4;-569.1998,35.00001;Float;False;False;False;False;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;9;-284.2,196.3;Float;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CustomExpressionNode;6;-433.5,-106;Float;False;return unity_StereoEyeIndex@;1;False;1;True;In0;FLOAT;0;In;stereoEyeIndex;True;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;18;17.02802,-408.076;Float;False;Property;_ReflectionStrength;ReflectionStrength;4;0;Create;True;0;0;False;0;0.82;0;0;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;17;126.028,-654.0759;Float;False;Property;_Color0;Color 0;3;0;Create;True;0;0;False;0;0,0,0,0;0,0,0,0;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;15;58.42308,199.3195;Float;True;Property;_ReflectionTex0;_ReflectionTex0;2;2;[HideInInspector];[HDR];Create;True;0;0;False;0;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;16;60.25409,12.5582;Float;True;Property;_ReflectionTex1;_ReflectionTex1;0;2;[HideInInspector];[HDR];Create;True;0;0;False;0;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ConditionalIfNode;8;390.5,-141;Float;False;False;5;0;FLOAT;0;False;1;FLOAT;0;False;2;COLOR;0,0,0,0;False;3;COLOR;0,0,0,0;False;4;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;19;402.3505,-418.2454;Float;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.DesaturateOpNode;26;479.2388,352.1703;Float;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;20;633.7502,-285.0498;Float;False;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;22;-507.5432,575.6605;Float;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;11,11;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.WorldNormalVector;23;227.542,508.2501;Float;False;False;1;0;FLOAT3;0,0,1;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;27;493.3002,190.4658;Float;False;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode;21;-213.7355,465.2883;Float;True;Property;_TextureSample0;Texture Sample 0;5;0;Create;True;0;0;False;0;823b25a053ecbda49bda2fe86062e3c7;823b25a053ecbda49bda2fe86062e3c7;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;28;2426.616,-340.9788;Float;True;Property;_Cutouttexture;Cutout texture;6;0;Create;True;0;0;True;0;None;None;True;0;False;black;Auto;False;Object;-1;Derivative;Texture2D;6;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;14;2927.67,-586.7422;Float;False;True;2;Float;ASEMaterialInspector;0;0;StandardSpecular;Reds mirror master shader;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;0;False;0;Custom;0.5;True;True;0;True;TransparentCutout;;Transparent;All;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;-1;False;-1;-1;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;1;-1;-1;-1;0;0;0;False;0;0;0;False;-1;-1;0;False;-1;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;2;0;1;0
WireConnection;3;0;2;0
WireConnection;7;0;3;0
WireConnection;4;0;3;0
WireConnection;9;0;7;0
WireConnection;9;1;4;0
WireConnection;15;1;9;0
WireConnection;16;1;9;0
WireConnection;8;0;6;0
WireConnection;8;2;16;0
WireConnection;8;3;15;0
WireConnection;19;0;17;0
WireConnection;19;1;18;0
WireConnection;26;0;23;0
WireConnection;20;0;19;0
WireConnection;20;1;8;0
WireConnection;23;0;21;0
WireConnection;27;2;26;0
WireConnection;21;1;22;0
WireConnection;14;0;20;0
WireConnection;14;10;28;0
ASEEND*/
//CHKSM=FEA257A69CF5BCBCE4925FF725A614619DC0317D