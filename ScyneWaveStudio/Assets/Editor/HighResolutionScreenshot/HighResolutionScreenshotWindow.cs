using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HighResolutionScreenshot
{
    public class HighResolutionScreenshotWindow : EditorWindow
    {
        private enum FileFormats
        {
            Png,
            Jpg,
            Exr,
            Tga
        }
        
        private Vector2Int resolution = new Vector2Int(1920, 1080);
        private int resolutionScale;

        private bool useSceneViewCamera;
        private Camera targetCamera;
        private bool keepTransparent = true;

        private string savePath;
        private FileFormats fileFormats;
        
        [MenuItem("Tools/Screenshot")]
        private static void ShowWindow()
        {
            GetWindow<HighResolutionScreenshotWindow>("Screenshot");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Target Camera", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                EditorGUI.BeginDisabledGroup(this.fileFormats == FileFormats.Jpg || this.fileFormats == FileFormats.Exr);
                this.keepTransparent = EditorGUILayout.Toggle("Keep Transparent", this.keepTransparent);
                EditorGUI.EndDisabledGroup();
                
                this.useSceneViewCamera = EditorGUILayout.Toggle("Use SceneView Camera", this.useSceneViewCamera);

                EditorGUI.BeginDisabledGroup(this.useSceneViewCamera);
                this.targetCamera = (Camera)EditorGUILayout.ObjectField("Camera", this.targetCamera, typeof(Camera), true);
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            var scaledResolution = this.resolution * this.resolutionScale;
            EditorGUILayout.LabelField($"Resolution ({scaledResolution.x} x {scaledResolution.y})", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                this.resolution = EditorGUILayout.Vector2IntField("Resolution (Width x Height)", this.resolution);
                this.resolutionScale = EditorGUILayout.IntSlider("Scale", this.resolutionScale, 1, 10);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Save Folder Path", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                EditorGUILayout.BeginHorizontal();
                this.savePath = EditorGUILayout.TextField("", this.savePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    this.savePath = GetSaveFolderPath("Select Save Path");
                }
                EditorGUILayout.EndHorizontal();

                this.fileFormats = (FileFormats) EditorGUILayout.EnumPopup("Extension", this.fileFormats);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Take Screenshot", GUILayout.Height(60)))
            {
                this.TakeScreenshot();
            }
        }

        private void TakeScreenshot()
        {
            var scaledResolution = this.resolution * this.resolutionScale;
            var renderTexture = RenderTexture.GetTemporary(scaledResolution.x, scaledResolution.y, 24);
            var renderCamera = this.useSceneViewCamera ? SceneView.lastActiveSceneView.camera : this.targetCamera;

            var beforeClearFlag = renderCamera.clearFlags;
            if (this.keepTransparent &&  this.fileFormats != FileFormats.Jpg && this.fileFormats != FileFormats.Exr)
            {
                renderCamera.clearFlags = CameraClearFlags.Color;
            }

            var beforeHDR = renderCamera.allowHDR;
            if (this.fileFormats == FileFormats.Exr)
            {
                renderCamera.allowHDR = true;
            }

            renderCamera.targetTexture = renderTexture;

            var format = this.GetTextureFormat();

            var texture = new Texture2D(scaledResolution.x, scaledResolution.y, format, false);
            
            renderCamera.Render();
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, scaledResolution.x, scaledResolution.y), 0, 0);

            renderCamera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);

            if (this.keepTransparent && this.fileFormats != FileFormats.Jpg && this.fileFormats != FileFormats.Exr)
            {
                renderCamera.clearFlags = beforeClearFlag;
            }
            
            if (this.fileFormats == FileFormats.Exr)
            {
                renderCamera.allowHDR = beforeHDR;
            }

            var data = this.GetEncodingData(texture);

            var fileName =
                $"{this.savePath}/screenshot_{scaledResolution.x}x{scaledResolution.y}_{DateTime.Now:yyyyMMddHHmmss}.{this.fileFormats.ToString().ToLower()}";
            File.WriteAllBytes(fileName, data);
            Application.OpenURL(fileName);
        }

        private byte[] GetEncodingData(Texture2D texture)
        {
            var data = new byte[] { };
            switch (this.fileFormats)
            {
                case FileFormats.Png:
                    data = texture.EncodeToPNG();
                    break;
                case FileFormats.Jpg:
                    data = texture.EncodeToJPG();
                    break;
                case FileFormats.Exr:
                    data = texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);
                    break;
                case FileFormats.Tga:
                    data = texture.EncodeToTGA();
                    break;
            }

            return data;
        }

        private TextureFormat GetTextureFormat()
        {
            var format = TextureFormat.ARGB32;
            switch (this.fileFormats)
            {
                case FileFormats.Png:
                    format = this.keepTransparent ? TextureFormat.ARGB32 : TextureFormat.RGB24;
                    break;
                case FileFormats.Jpg:
                    format = TextureFormat.RGB24;
                    break;
                case FileFormats.Exr:
                    format = TextureFormat.RGBAFloat;
                    break;
                case FileFormats.Tga:
                    format = this.keepTransparent ? TextureFormat.ARGB32 : TextureFormat.RGB24;
                    break;
            }

            return format;
        }

        private static string GetSaveFolderPath(string title)
        {
            var savePath = EditorUtility.OpenFolderPanel(title, GetCurrentDirectory(), "");
            return savePath;
        }

        private static string GetCurrentDirectory()
        {
            const BindingFlags flag = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var asm = Assembly.Load("UnityEditor.dll");
            var typeProjectBrowser = asm.GetType("UnityEditor.ProjectBrowser");
            var projectBrowserWindow = GetWindow(typeProjectBrowser);
            return (string)typeProjectBrowser.GetMethod("GetActiveFolderPath", flag)?.Invoke(projectBrowserWindow, null); 
        }
    }
}