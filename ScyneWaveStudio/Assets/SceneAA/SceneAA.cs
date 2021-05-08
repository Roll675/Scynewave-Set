#if UNITY_EDITOR
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
class SceneAA
{
	private static readonly AccessTools.FieldRef<SceneView, RenderTexture> m_SceneTargetTextureRef =
		AccessTools.FieldRefAccess<SceneView, RenderTexture>("m_SceneTargetTexture");

	private static readonly MethodInfo IsSceneCameraDeferred = AccessTools.Method(typeof(SceneView), "IsSceneCameraDeferred");

	private static readonly MethodInfo GetCameraRect = AccessTools.Method(typeof(Handles), "GetCameraRect");

	static SceneAA()
	{
		MethodInfo target = AccessTools.Method(typeof(SceneView), "CreateCameraTargetTexture");
		MethodInfo prefix = AccessTools.Method(typeof(SceneAA), "Prefix");
		var harmonyInstance = new Harmony("blueamulet.sceneaa");
		harmonyInstance.Patch(target, new HarmonyMethod(prefix));
	}

	public static bool Prefix(SceneView __instance, ref Rect cameraRect, ref bool hdr)
	{
		int msaa = Mathf.Max(1, QualitySettings.antiAliasing);
		if ((bool)IsSceneCameraDeferred.Invoke(__instance, new object[] {}) || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal)
		{
			msaa = 1;
		}
		RenderTextureFormat format = (hdr && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
		RenderTexture m_SceneTargetTexture = m_SceneTargetTextureRef(__instance);
		if (m_SceneTargetTexture != null)
		{
			if (m_SceneTargetTexture.format != format || m_SceneTargetTexture.antiAliasing != msaa)
			{
				Object.DestroyImmediate(m_SceneTargetTexture);
				m_SceneTargetTexture = null;
			}
		}
		Rect actualCameraRect = (Rect)GetCameraRect.Invoke(null, new object[] { cameraRect });
		int width = (int)actualCameraRect.width;
		int height = (int)actualCameraRect.height;
		if (m_SceneTargetTexture == null)
		{
			m_SceneTargetTexture = new RenderTexture(0, 0, 24, format, RenderTextureReadWrite.sRGB)
			{
				name = "SceneView RT",
				antiAliasing = msaa,
				hideFlags = HideFlags.HideAndDontSave
			};
			m_SceneTargetTextureRef(__instance) = m_SceneTargetTexture;
		}
		if (m_SceneTargetTexture.width != width || m_SceneTargetTexture.height != height)
		{
			m_SceneTargetTexture.Release();
			m_SceneTargetTexture.width = width;
			m_SceneTargetTexture.height = height;
		}
		m_SceneTargetTexture.Create();
		return false;
	}
}
#endif