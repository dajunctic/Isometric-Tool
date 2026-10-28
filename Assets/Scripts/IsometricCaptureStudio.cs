using System;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsometricTool
{
    public class IsometricCaptureStudio : MonoBehaviour
    {
        [Header("Capture Setup")]
        public GameObject characterTarget;
        public Camera isoCamera;
        public AnimationClip animationClip;

        [Header("Bake Settings")]
        [Range(4, 32)]
        public int framesPerDirection = 8;
        public int resolution = 256;
        public string outputFolder = "Assets/Sprites/Isometric";
        public bool createCombinedSpriteSheet = true;

        [Header("Isometric 8 Directions")]
        public static readonly string[] DirectionNames = new string[]
        {
            "00_South",       // Nhìn xuống dưới (thẳng vào camera)
            "01_SouthWest",   // Xuống trái
            "02_West",        // Sang trái
            "03_NorthWest",   // Lên trái
            "04_North",       // Lên trên (quay lưng)
            "05_NorthEast",   // Lên phải
            "06_East",        // Sang phải
            "07_SouthEast"    // Xuống phải
        };

        // Rotation angles for character (clockwise from South)
        public static readonly float[] DirectionAngles = new float[]
        {
            225f, // South (faces camera directly)
            270f, // SouthWest
            315f, // West
            0f,   // NorthWest
            45f,  // North (faces away)
            90f,  // NorthEast
            135f, // East
            180f  // SouthEast
        };

        public string Bake()
        {
            if (characterTarget == null || isoCamera == null || animationClip == null)
            {
                Debug.LogError("[IsometricCaptureStudio] Missing required references!");
                return "Missing references";
            }

            Animator animator = characterTarget.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("[IsometricCaptureStudio] Character Target must have an Animator!");
                return "No animator";
            }

            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);

            // Ensure all SMRs are updated
            foreach (var smr in characterTarget.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            string animName = animationClip.name;
            string targetDir = Path.Combine(outputFolder, animName).Replace('\\', '/');
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            // Setup RenderTexture
            RenderTexture rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            RenderTexture prevRT = isoCamera.targetTexture;
            isoCamera.targetTexture = rt;

            Quaternion originalRot = characterTarget.transform.rotation;
            Vector3 originalPos = characterTarget.transform.position;

            Texture2D[,] frameMatrix = new Texture2D[8, framesPerDirection];
            float clipLength = animationClip.length;

            // Setup PlayableGraph for Humanoid evaluation
            PlayableGraph graph = PlayableGraph.Create("StudioBakeGraph");
            var animOutput = AnimationPlayableOutput.Create(graph, "BakeOutput", animator);
            var clipPlayable = AnimationClipPlayable.Create(graph, animationClip);
            PlayableOutputExtensions.SetSourcePlayable(animOutput, clipPlayable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            try
            {
                for (int d = 0; d < 8; d++)
                {
                    string dirName = DirectionNames[d];
                    float yaw = DirectionAngles[d];
                    string dirPath = Path.Combine(targetDir, dirName).Replace('\\', '/');
                    if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                    // Face direction
                    characterTarget.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                    for (int f = 0; f < framesPerDirection; f++)
                    {
                        double sampleTime = ((double)f / framesPerDirection) * clipLength;

                        // Evaluate pose
                        PlayableExtensions.SetTime(clipPlayable, sampleTime);
                        graph.Evaluate();

                        // Align head constraints if present
                        if (headBone != null)
                        {
                            foreach (var c in characterTarget.GetComponentsInChildren<Transform>(true))
                            {
                                if (c.name == "Constraint_Head")
                                {
                                    c.position = headBone.position;
                                    c.rotation = headBone.rotation;
                                }
                            }
                        }

                        // Render frame
                        isoCamera.Render();

                        // Read frame pixels
                        RenderTexture.active = rt;
                        Texture2D frameTex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
                        frameTex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                        frameTex.Apply();
                        RenderTexture.active = null;

                        frameMatrix[d, f] = frameTex;

                        // Save PNG file
                        byte[] pngData = frameTex.EncodeToPNG();
                        string filePath = Path.Combine(dirPath, $"frame_{f:D2}.png").Replace('\\', '/');
                        File.WriteAllBytes(filePath, pngData);
                    }
                }

                // Generate Combined 8-Direction Sprite Sheet
                if (createCombinedSpriteSheet)
                {
                    int sheetW = resolution * framesPerDirection;
                    int sheetH = resolution * 8;
                    Texture2D sheet = new Texture2D(sheetW, sheetH, TextureFormat.ARGB32, false);

                    Color[] blank = new Color[sheetW * sheetH];
                    sheet.SetPixels(blank);

                    for (int d = 0; d < 8; d++)
                    {
                        int row = 7 - d; // In Texture2D, y=0 is bottom row
                        for (int f = 0; f < framesPerDirection; f++)
                        {
                            Color[] pixels = frameMatrix[d, f].GetPixels();
                            sheet.SetPixels(f * resolution, row * resolution, resolution, resolution, pixels);
                        }
                    }
                    sheet.Apply();

                    string sheetFile = Path.Combine(targetDir, $"{animName}_8Dir_Sheet.png").Replace('\\', '/');
                    File.WriteAllBytes(sheetFile, sheet.EncodeToPNG());
                    DestroyImmediate(sheet);
                }
            }
            finally
            {
                graph.Destroy();
                isoCamera.targetTexture = prevRT;
                characterTarget.transform.rotation = originalRot;
                characterTarget.transform.position = originalPos;
                DestroyImmediate(rt);

                for (int d = 0; d < 8; d++)
                {
                    for (int f = 0; f < framesPerDirection; f++)
                    {
                        if (frameMatrix[d, f] != null) DestroyImmediate(frameMatrix[d, f]);
                    }
                }
            }

#if UNITY_EDITOR
            AssetDatabase.Refresh();
            ConfigureImportSettings(targetDir);
#endif

            Debug.Log($"[IsometricCaptureStudio] Successfully baked 8 directions ({8 * framesPerDirection} sprites) to: {targetDir}");
            return targetDir;
        }

#if UNITY_EDITOR
        public static void ConfigureImportSettings(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
        }
#endif
    }
}
