using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Mochie;

public class TakenEditor : ShaderGUI {

    public static Dictionary<Material, Toggles> foldouts = new Dictionary<Material, Toggles>();
    Toggles toggles = new Toggles(
		new string[] {
			"BASE",
			"LIGHTING",
			"RIM",
			"EMISSION",
			"GRADIENT",
			"NOISE PATCHES",
			"DISSOLVE",
			"OUTLINE",
		}
	);

	string header = "TakenHeader_Pro";
	string watermark = "Watermark_Pro";
	string patIcon = "Patreon_Icon";
	string versionLabel = "v1.0";

	GUIContent maskLabel = new GUIContent("Mask");
    GUIContent albedoLabel = new GUIContent("Albedo");
	GUIContent mirrorTexLabel = new GUIContent("Albedo (Mirror)");
    GUIContent emissTexLabel = new GUIContent("Emission Map");
    GUIContent normalTexLabel = new GUIContent("Normal");
    GUIContent metallicTexLabel = new GUIContent("Metallic");
    GUIContent roughnessTexLabel = new GUIContent("Roughness");
    GUIContent occlusionTexLabel = new GUIContent("Occlusion");
    GUIContent heightTexLabel = new GUIContent("Height");
    GUIContent reflCubeLabel = new GUIContent("Cubemap");
    GUIContent shadowRampLabel = new GUIContent("Ramp");
    GUIContent specularTexLabel = new GUIContent("Specular Map");
    GUIContent primaryMapsLabel = new GUIContent("Primary Maps");
    GUIContent detailMapsLabel = new GUIContent("Detail Maps");
	GUIContent dissolveTexLabel = new GUIContent("Dissolve Map");
	GUIContent dissolveRimTexLabel = new GUIContent("Rim Color");
	GUIContent colorLabel = new GUIContent("Color");
	GUIContent packedTexLabel = new GUIContent("Packed Texture");
	GUIContent cubemapLabel = new GUIContent("Cubemap");
	GUIContent translucTexLabel = new GUIContent("Thickness Map");
	GUIContent tintLabel = new GUIContent("Tint");
	GUIContent filteringLabel = new GUIContent("PBR Filtering");
	GUIContent smoothTexLabel = new GUIContent("Smoothness");

	// Avatar Props
	MaterialProperty _BlendMode = null;
	MaterialProperty _ATM = null;
	MaterialProperty _ZWrite = null;
	MaterialProperty _Cutoff = null;
	MaterialProperty _Color = null;
	MaterialProperty _Invert = null;
	MaterialProperty _Smoothstep = null;
	MaterialProperty _Culling = null;
	MaterialProperty _MainTex = null;
	MaterialProperty _BumpScale = null;
	MaterialProperty _BumpMap = null;

	MaterialProperty _EnableLighting = null;
	MaterialProperty _EnableSH = null;
	MaterialProperty _Metallic = null;
	MaterialProperty _Roughness = null;
	MaterialProperty _ReflectionStr = null;
	MaterialProperty _MetallicMap = null;
	MaterialProperty _RoughnessMap = null;
	MaterialProperty _ReflectionMask = null;

	MaterialProperty _RimBrightness = null;
	MaterialProperty _RimWidth = null;
	MaterialProperty _RimEdge = null;
	MaterialProperty _RimGradMask = null;
	MaterialProperty _RimMask = null;

	MaterialProperty _EmissStr = null;
	MaterialProperty _EmissPow = null;
	MaterialProperty _EmissGradMasking = null;
	MaterialProperty _EmissTex = null;
	MaterialProperty _EmissGradMask = null;
	MaterialProperty _EmissInvert = null;

	MaterialProperty _EnableGradient = null;
	MaterialProperty _GradientInvert = null;
	MaterialProperty _GradientAxis = null;
	MaterialProperty _GradientScale = null;
	MaterialProperty _GradientBrightness = null;
	MaterialProperty _GradientHeightMax = null;
	MaterialProperty _GradientHeightMin = null;
	MaterialProperty _GradientSpeed = null;
	MaterialProperty _GradientContrast = null;
	MaterialProperty _GradientMask = null;

	MaterialProperty _EnableNoise = null;
	MaterialProperty _NoiseScale = null;
	MaterialProperty _NoiseBrightness = null;
	MaterialProperty _NoiseCutoff = null;
	MaterialProperty _NoiseSmooth = null;
	MaterialProperty _NoiseMask = null;

	MaterialProperty _EnableDissolve = null;
	MaterialProperty _DissolveTex = null;
	MaterialProperty _DissolveAmt = null;
	MaterialProperty _DissolveRimBrightness = null;
	MaterialProperty _DissolveRimWidth = null;

	MaterialProperty _EnableOutline = null;
	MaterialProperty _InvertedOutline = null;
	MaterialProperty _Thickness = null;
	MaterialProperty _OutlineMask = null;

	MaterialProperty _NaNLmao = null;

    BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	bool m_FirstTimeApply = true;

    MaterialEditor m_me;
    public override void OnGUI(MaterialEditor me, MaterialProperty[] props) {
		Material mat = (Material)me.target;
		mat.DisableKeyword("_");
		if (m_FirstTimeApply) {
			m_FirstTimeApply = false;
		}

		// Find properties
        foreach (var property in GetType().GetFields(bindingFlags)){
            if (property.FieldType == typeof(MaterialProperty)){
                property.SetValue(this, FindProperty(property.Name, props));
            }
        }

		_NaNLmao.floatValue = 0.0f;

		// Check name of shader to determine if certain properties should be displayed
        bool isTransparent = _BlendMode.floatValue >= 4;
        bool isCutout = _BlendMode.floatValue > 0 && _BlendMode.floatValue < 4;

		if (!EditorGUIUtility.isProSkin){
			header = "TakenHeader";
			watermark = "Watermark";
		}

		// Add mat to foldout dictionary if it isn't in there yet
		if (!foldouts.ContainsKey(mat))
			foldouts.Add(mat, toggles);


		if (_BlendMode.floatValue == 3)
			_ATM.floatValue = 1f;
		else
			_ATM.floatValue = 0f;
		
		mat.SetShaderPassEnabled("Always", _EnableOutline.floatValue  == 1);

		// Return here to reduce editor overhead if it's not visible
		if (!me.isVisible)
			return;

        Texture2D headerTex = (Texture2D)Resources.Load(header, typeof(Texture2D));
		Texture2D watermarkTex = (Texture2D)Resources.Load(watermark, typeof(Texture2D));
		Texture2D patIconTex = (Texture2D)Resources.Load(patIcon, typeof(Texture2D));
		Texture2D resetIcon = (Texture2D)Resources.Load("ResetIcon", typeof(Texture2D));
		Texture2D collapseIcon = (Texture2D)Resources.Load("CollapseIcon", typeof(Texture2D));
		GUIContent collapseLabel = new GUIContent(collapseIcon, "Collapse all foldout tabs.");

        MGUI.CenteredTexture(headerTex, 0, 0);

		bool baseTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "BASE");
		if (MGUI.TabButton(collapseLabel, 26f)){
			for (int i = 1; i <= foldouts[mat].GetToggles().Length-1; i++)
				foldouts[mat].SetState(i, false);
		}
		MGUI.Space8();
		if (baseTab){
			MGUI.Space6();
			me.RenderQueueField();
			EditorGUI.showMixedValue = _BlendMode.hasMixedValue;
			var mode = (MGUI.BlendMode)_BlendMode.floatValue;
			EditorGUI.BeginChangeCheck();
			mode = (MGUI.BlendMode)EditorGUILayout.Popup("Blending Mode", (int)mode, Enum.GetNames(typeof(MGUI.BlendMode)));
			if (EditorGUI.EndChangeCheck()) {
				me.RegisterPropertyChangeUndo("Blending Mode");
				_BlendMode.floatValue = (float)mode;
				foreach (var obj in _BlendMode.targets){
					MGUI.SetBlendMode((Material)obj, (MGUI.BlendMode)mode);
				}
				EditorGUI.showMixedValue = false;
			}
			if (_BlendMode.floatValue == 4)
				me.ShaderProperty(_ZWrite, "ZWrite");
			me.ShaderProperty(_Culling, "Culling");
			me.ShaderProperty(_Color, "Global Tint");
			me.ShaderProperty(_Smoothstep, "Smoothstep");
			if (_BlendMode.floatValue == 1){
				MGUI.Space12();
				me.ShaderProperty(_Cutoff, "Cutout");
			}
			MGUI.Space16();
			me.TexturePropertySingleLine(new GUIContent("Main Texture"), _MainTex, _Invert);
			MGUI.TexPropLabel("Invert", 95);
			me.TexturePropertySingleLine(new GUIContent("Emission (AO)"), _EmissTex, _EmissInvert);
			MGUI.TexPropLabel("Invert", 95);
			me.TexturePropertySingleLine(new GUIContent("Normal Map"), _BumpMap, _BumpScale);
			MGUI.TexPropLabel("Strength", 107);
			MGUI.TextureSO(me, _MainTex);
		}
		MGUI.Space8();

		bool lightingTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "LIGHTING");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (lightingTab){
			MGUI.Space6();
			me.ShaderProperty(_EnableLighting, "Enable");
			MGUI.ToggleGroup(_EnableLighting.floatValue == 0);
			me.ShaderProperty(_EnableSH, "SH Shading");
			MGUI.Space6();
			me.TexturePropertySingleLine(new GUIContent("Metallic"), _MetallicMap, _Metallic);
			me.TexturePropertySingleLine(new GUIContent("Roughness"), _RoughnessMap, _Roughness);
			me.TexturePropertySingleLine(new GUIContent("Reflections"), _ReflectionMask, _ReflectionStr);
			MGUI.ToggleGroupEnd();
			MGUI.Space8();
		}

		bool rimTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "RIM");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (rimTab){
			MGUI.Space6();
			me.ShaderProperty(_RimBrightness, "Strength");
			me.ShaderProperty(_RimWidth, "Width");
			me.ShaderProperty(_RimEdge, "Sharpness");
			me.ShaderProperty(_RimGradMask, "Gradient Restriction");		
			MGUI.Space4();
			me.TexturePropertySingleLine(new GUIContent("Mask"), _RimMask);
			MGUI.Space8();
		}

		bool emissTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "EMISSION");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (emissTab){
			MGUI.Space6();
			me.ShaderProperty(_EmissStr, "Strength");
			me.ShaderProperty(_EmissPow, "Exponent");
			me.ShaderProperty(_EmissGradMasking, "Gradient Restriction");
			MGUI.Space4();
			me.TexturePropertySingleLine(new GUIContent("Restriction Mask"), _EmissGradMask);
			MGUI.Space8();
		}

		bool gradTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "GRADIENT");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (gradTab){
			MGUI.Space6();
			me.ShaderProperty(_EnableGradient, "Enable");
			MGUI.ToggleGroup(_EnableGradient.floatValue == 0);
			me.ShaderProperty(_GradientInvert, "Invert Axis");
			me.ShaderProperty(_GradientAxis, "Axis");
			MGUI.Space6();
			MGUI.Vector3Field(_GradientScale, "Noise Scale");
			me.ShaderProperty(_GradientBrightness, "Brightness");
			me.ShaderProperty(_GradientHeightMax, "End Position");
			me.ShaderProperty(_GradientHeightMin, "Start Position");
			me.ShaderProperty(_GradientSpeed, "Scroll Speed");
			me.ShaderProperty(_GradientContrast, "Contrast");
			MGUI.Space4();
			me.TexturePropertySingleLine(new GUIContent("Mask"), _GradientMask);

			MGUI.ToggleGroupEnd();
			MGUI.Space8();
		}

		bool noiseTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "NOISE PATCHES");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (noiseTab){
			MGUI.Space6();
			me.ShaderProperty(_EnableNoise, "Enable");
			MGUI.Space6();
			MGUI.ToggleGroup(_EnableNoise.floatValue == 0);
			MGUI.Vector2Field(_NoiseScale, "Scale");
			me.ShaderProperty(_NoiseBrightness, "Brightness");
			me.ShaderProperty(_NoiseCutoff, "Cutoff");
			me.ShaderProperty(_NoiseSmooth, "Smoothing");
			MGUI.Space4();
			me.TexturePropertySingleLine(new GUIContent("Mask"), _NoiseMask);
			MGUI.ToggleGroupEnd();
			MGUI.Space8();
		}

		bool dissolveTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "DISSOLVE");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (dissolveTab){
			MGUI.Space6();
			if (isCutout || isTransparent){
				me.ShaderProperty(_EnableDissolve, "Enable");
				MGUI.Space6();
				MGUI.ToggleGroup(_EnableDissolve.floatValue == 0);
				me.TexturePropertySingleLine(new GUIContent("Dissolve Map"), _DissolveTex);
				MGUI.TextureSO(me, _DissolveTex, _DissolveTex.textureValue);
				MGUI.Space4();
				me.ShaderProperty(_DissolveAmt, "Strength");
				me.ShaderProperty(_DissolveRimBrightness, "Rim Brightness");
				me.ShaderProperty(_DissolveRimWidth, "Rim Width");
				MGUI.ToggleGroupEnd();
			}
			else MGUI.CenteredText("REQUIRES NON-OPAQUE BLEND MODE", 10, 0,0);
			MGUI.Space8();
		}

		bool outlineTab = Foldouts.DoFoldout(foldouts, mat, me, 1, "OUTLINE");
		if (MGUI.TabButton(resetIcon, 26f)){}

		MGUI.Space8();
		if (outlineTab){
			MGUI.Space6();
			if (!isTransparent){
				me.ShaderProperty(_EnableOutline, "Enable");
				MGUI.ToggleGroup(_EnableOutline.floatValue == 0);
				me.ShaderProperty(_InvertedOutline, "Invert Tint");
				MGUI.Space6();
				me.ShaderProperty(_Thickness, "Thickness");
				MGUI.Space4();
				me.TexturePropertySingleLine(new GUIContent("Mask"), _OutlineMask);
				MGUI.ToggleGroupEnd();
			}
			else MGUI.CenteredText("REQUIRES NON-TRANSPARENT BLEND MODE", 10, 0, 0);
			MGUI.Space8();
		}
		GUILayout.Space(15);

		MGUI.CenteredTexture(watermarkTex, 0, 0);
		float buttonSize = 24.0f;
		float xPos = 53.0f;
		GUILayout.Space(-buttonSize);
		if (MGUI.LinkButton(patIconTex, buttonSize, buttonSize, xPos)){
			Application.OpenURL("https://www.patreon.com/mochieshaders");
		}
		GUILayout.Space(buttonSize);
		MGUI.VersionLabel(versionLabel, 12,-16,-20);
    }

	void SetKeyword(Material m, string keyword, bool state) {
		if (state)
			m.EnableKeyword(keyword);
		else
			m.DisableKeyword(keyword);
	}

	public override void AssignNewShaderToMaterial(Material mat, Shader oldShader, Shader newShader) {
		m_FirstTimeApply = true;
		if (mat.HasProperty("_Emission"))
			mat.SetColor("_EmissionColor", mat.GetColor("_Emission"));
		base.AssignNewShaderToMaterial(mat, oldShader, newShader);
		MGUI.SetBlendMode(mat, (MGUI.BlendMode)mat.GetFloat("_BlendMode"));
	}
}