using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace IsometricTool.Editor
{
    public class IsometricSpriteBaker : EditorWindow
    {
        public GameObject characterTarget;
        public Camera isoCamera;
        public AnimationClip animationClip;
        public int framesPerDirection = 8;
        public int textureResolution = 256;
        public string outputDirectory = "Assets/Sprites/Isometric";
        public bool generateSpriteSheet = true;

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

        public static readonly float[] DirectionAngles = new float[]
        {
            225f, // South
            270f, // SouthWest
            315f, // West
            0f,   // NorthWest
            45f,  // North
            90f,  // NorthEast
            135f, // East
            180f  // SouthEast
        };

        [MenuItem("Tools/Isometric/Open Sprite Baker Window")]
        public static void OpenWindow()
        {
            GetWindow<IsometricSpriteBaker>("Isometric Sprite Baker");
        }

        public bool bakeAllDirections = true;

        private void OnGUI()
        {
            GUILayout.Label("Isometric 8-Direction Sprite Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            characterTarget = (GameObject)EditorGUILayout.ObjectField("Character Target", characterTarget, typeof(GameObject), true);
            isoCamera = (Camera)EditorGUILayout.ObjectField("Isometric Camera", isoCamera, typeof(Camera), true);
            animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);

            framesPerDirection = EditorGUILayout.IntSlider("Frames per Direction", framesPerDirection, 4, 60);
            textureResolution = EditorGUILayout.IntPopup("Resolution", textureResolution, 
                new[] { "128 x 128 (Mobile / Minions)", "256 x 256 (Standard In-game)", "512 x 512 (Boss / High-res)", "1024 x 1024 (Character Select / Close-up)", "2048 x 2048 (Ultra HQ)" }, 
                new[] { 128, 256, 512, 1024, 2048 });

            bakeAllDirections = EditorGUILayout.Toggle("Bake All 8 Directions", bakeAllDirections);
            outputDirectory = EditorGUILayout.TextField("Output Folder", outputDirectory);
            generateSpriteSheet = EditorGUILayout.Toggle("Generate Sprite Sheet", generateSpriteSheet);

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake 8-Direction Sprites", GUILayout.Height(36)))
            {
                if (characterTarget == null || isoCamera == null || animationClip == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign Character Target, Camera and Animation Clip.", "OK");
                    return;
                }
                string result = Bake(characterTarget, isoCamera, animationClip, framesPerDirection, textureResolution, outputDirectory, generateSpriteSheet, bakeAllDirections);
                EditorUtility.DisplayDialog("Bake Finished", result, "OK");
            }
        }

        public static string Bake(GameObject target, Camera cam, AnimationClip clip, int frameCount, int resolution, string outFolder, bool makeSheet, bool allDirections = true)
        {
            if (target == null || cam == null || clip == null) return "Missing inputs";

            Animator animator = target.GetComponent<Animator>();
            if (animator == null) return "Target has no Animator";

            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);

            foreach (var smr in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            string animName = clip.name;
            string clipFolder = Path.Combine(outFolder, animName).Replace('\\', '/');
            if (!Directory.Exists(clipFolder)) Directory.CreateDirectory(clipFolder);

            // Setup RenderTexture
            RenderTexture rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            RenderTexture prevRT = cam.targetTexture;
            cam.targetTexture = rt;

            Quaternion originalRotation = target.transform.rotation;
            Vector3 originalPosition = target.transform.position;

            int dirCount = allDirections ? 8 : 1;
            Texture2D[,] frames = new Texture2D[dirCount, frameCount];
            float clipLength = clip.length;

            PlayableGraph graph = PlayableGraph.Create("EditorBakeGraph");
            var animOutput = AnimationPlayableOutput.Create(graph, "EditorBakeOutput", animator);
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            PlayableOutputExtensions.SetSourcePlayable(animOutput, clipPlayable);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            try
            {
                for (int dir = 0; dir < dirCount; dir++)
                {
                    string dirName = DirectionNames[dir];
                    float angle = DirectionAngles[dir];
                    string dirFolder = Path.Combine(clipFolder, dirName).Replace('\\', '/');
                    if (!Directory.Exists(dirFolder)) Directory.CreateDirectory(dirFolder);

                    // Set character facing direction
                    target.transform.rotation = Quaternion.Euler(0f, angle, 0f);

                    for (int f = 0; f < frameCount; f++)
                    {
                        double time = ((double)f / frameCount) * clipLength;

                        clipPlayable.SetTime(time);
                        graph.Evaluate();

                        if (headBone != null)
                        {
                            foreach (var c in target.GetComponentsInChildren<Transform>(true))
                            {
                                if (c.name == "Constraint_Head")
                                {
                                    c.position = headBone.position;
                                    c.rotation = headBone.rotation;
                                }
                            }
                        }

                        cam.Render();

                        RenderTexture.active = rt;
                        Texture2D frameTex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
                        frameTex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                        frameTex.Apply();
                        RenderTexture.active = null;

                        frames[dir, f] = frameTex;

                        byte[] pngBytes = frameTex.EncodeToPNG();
                        string framePath = Path.Combine(dirFolder, $"frame_{f:D2}.png").Replace('\\', '/');
                        File.WriteAllBytes(framePath, pngBytes);
                    }
                }

                if (makeSheet)
                {
                    int sheetWidth = resolution * frameCount;
                    int sheetHeight = resolution * dirCount;
                    Texture2D sheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.ARGB32, false);

                    Color[] clearPixels = new Color[sheetWidth * sheetHeight];
                    sheet.SetPixels(clearPixels);

                    for (int dir = 0; dir < dirCount; dir++)
                    {
                        int row = (dirCount - 1) - dir; // Row 0 is bottom in Unity Texture2D
                        for (int f = 0; f < frameCount; f++)
                        {
                            Color[] fPixels = frames[dir, f].GetPixels();
                            sheet.SetPixels(f * resolution, row * resolution, resolution, resolution, fPixels);
                        }
                    }
                    sheet.Apply();

                    string sheetName = allDirections ? $"{animName}_8Dir_SpriteSheet.png" : $"{animName}_Select_SpriteSheet.png";
                    string sheetPath = Path.Combine(clipFolder, sheetName).Replace('\\', '/');
                    File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
                    DestroyImmediate(sheet);
                }
            }
            finally
            {
                graph.Destroy();
                cam.targetTexture = prevRT;
                target.transform.rotation = originalRotation;
                target.transform.position = originalPosition;
                DestroyImmediate(rt);

                for (int d = 0; d < dirCount; d++)
                {
                    for (int f = 0; f < frameCount; f++)
                    {
                        if (frames[d, f] != null) DestroyImmediate(frames[d, f]);
                    }
                }
            }

            AssetDatabase.Refresh();
            IsometricCaptureStudio.ConfigureImportSettings(clipFolder);

            return $"Bake completed! Output saved to: {clipFolder}";
        }
    }
}
