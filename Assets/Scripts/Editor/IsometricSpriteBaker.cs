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
        [Header("Setup")]
        public GameObject characterTarget;
        public Camera isoCamera;
        public AnimationClip animationClip;

        [Header("Settings")]
        public int framesPerDirection = 8;
        public int textureResolution = 256;
        public bool bakeAllDirections = true;
        public string outputDirectory = "Assets/Sprites/Isometric";
        public bool generateSpriteSheet = true;

        [Header("Preview State")]
        public int previewDirection = 0;
        public bool isPlaying = true;
        public bool previewStepped = true;
        public float playbackSpeed = 1.0f;
        private double previewTimer = 0.0;
        private double lastUpdateTime = 0.0;
        private RenderTexture previewRT;
        private PlayableGraph previewGraph;
        private AnimationClipPlayable previewClipPlayable;
        private AnimationClip lastGraphClip;
        private GameObject lastGraphTarget;

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

        public static readonly string[] DirectionShortNames = new string[]
        {
            "South (Down)", "SW", "West (Left)", "NW", "North (Up)", "NE", "East (Right)", "SE"
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
            var win = GetWindow<IsometricSpriteBaker>("Isometric Sprite Baker");
            win.minSize = new Vector2(840, 750);
            win.Show();
        }

        private void OnEnable()
        {
            // Auto-find targets if empty
            if (characterTarget == null) characterTarget = GameObject.Find("P09_Female_Character");
            if (isoCamera == null)
            {
                var camObj = GameObject.Find("Isometric_Camera");
                if (camObj != null) isoCamera = camObj.GetComponent<Camera>();
            }
            if (animationClip == null)
            {
                animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/DoubleL/Demo/Anim/OneHand_Up_Run_F_InPlace.anim");
            }

            lastUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DestroyPreviewGraph();
            if (previewRT != null)
            {
                DestroyImmediate(previewRT);
                previewRT = null;
            }
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            double dt = now - lastUpdateTime;
            lastUpdateTime = now;

            if (isPlaying && animationClip != null)
            {
                previewTimer += dt * playbackSpeed;
                if (previewTimer > animationClip.length)
                {
                    previewTimer %= animationClip.length;
                }
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("Isometric 8-Direction Sprite Baker & Preview Studio", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            // LEFT PANEL: Configurations (width: 440)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(440));
            GUILayout.Label("Configuration & Export Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            characterTarget = (GameObject)EditorGUILayout.ObjectField("Character Target", characterTarget, typeof(GameObject), true);
            isoCamera = (Camera)EditorGUILayout.ObjectField("Isometric Camera", isoCamera, typeof(Camera), true);
            animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);

            EditorGUILayout.Space(6);
            framesPerDirection = EditorGUILayout.IntSlider("Frames per Direction", framesPerDirection, 4, 32);
            textureResolution = EditorGUILayout.IntPopup("Resolution", textureResolution,
                new[] { "128 x 128 (Mobile / Minions)", "256 x 256 (Standard In-game)", "512 x 512 (Boss / High-res)", "1024 x 1024 (Character Select / Close-up)", "2048 x 2048 (Ultra HQ)" },
                new[] { 128, 256, 512, 1024, 2048 });

            bakeAllDirections = EditorGUILayout.Toggle("Bake All 8 Directions", bakeAllDirections);
            generateSpriteSheet = EditorGUILayout.Toggle("Generate Sprite Sheet", generateSpriteSheet);
            outputDirectory = EditorGUILayout.TextField("Output Folder", outputDirectory);

            EditorGUILayout.Space(12);

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
            if (GUILayout.Button("Bake Sprites (100% Transparent Alpha)", GUILayout.Height(40)))
            {
                if (characterTarget == null || isoCamera == null || animationClip == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign Character Target, Camera and Animation Clip.", "OK");
                }
                else
                {
                    DestroyPreviewGraph();
                    string result = Bake(characterTarget, isoCamera, animationClip, framesPerDirection, textureResolution, outputDirectory, generateSpriteSheet, bakeAllDirections);
                    EditorUtility.DisplayDialog("Bake Finished", result, "OK");
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                $"Total frames to export: {(bakeAllDirections ? 8 : 1) * framesPerDirection}\n" +
                $"Estimated Sheet Size: {(bakeAllDirections ? framesPerDirection * textureResolution : framesPerDirection * textureResolution)} x {(bakeAllDirections ? 8 * textureResolution : textureResolution)} px\n" +
                $"Format: 32-bit PNG with sub-pixel transparent alpha (Calibrated Difference Matte).", MessageType.Info);

            EditorGUILayout.EndVertical();

            // RIGHT / BOTTOM PANEL: Realtime Animation Preview
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Interactive Animation Preview", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // Direction Selection Buttons
            GUILayout.Label("Facing Direction:", EditorStyles.miniBoldLabel);
            int newDir = GUILayout.SelectionGrid(previewDirection, DirectionShortNames, 4);
            if (newDir != previewDirection)
            {
                previewDirection = newDir;
            }

            EditorGUILayout.Space(6);

            // Controls
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(isPlaying ? "Pause" : "Play", GUILayout.Width(60)))
            {
                isPlaying = !isPlaying;
            }
            previewStepped = EditorGUILayout.ToggleLeft("Stepped (Frame Preview)", previewStepped, GUILayout.Width(170));
            playbackSpeed = EditorGUILayout.Slider("Speed", playbackSpeed, 0.1f, 2.0f);
            EditorGUILayout.EndHorizontal();

            // Scrubber
            if (animationClip != null)
            {
                float clipLen = animationClip.length;
                float currentVal = (float)previewTimer;
                float newVal = EditorGUILayout.Slider("Timeline", currentVal, 0f, clipLen);
                if (Mathf.Abs(newVal - currentVal) > 0.001f)
                {
                    previewTimer = newVal;
                    isPlaying = false;
                }

                int currentFrame = Mathf.Clamp(Mathf.FloorToInt((float)(previewTimer / clipLen) * framesPerDirection), 0, framesPerDirection - 1);
                GUILayout.Label($"Current Frame: {currentFrame + 1} / {framesPerDirection} (Time: {previewTimer:F2}s / {clipLen:F2}s)", EditorStyles.miniLabel);
            }

            // Preview Texture Box
            Rect previewRect = GUILayoutUtility.GetRect(320, 320, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreviewTexture(previewRect);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPreviewTexture(Rect rect)
        {
            if (characterTarget == null || isoCamera == null || animationClip == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
                GUI.Label(rect, "Assign Character, Camera & Animation to preview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Checkerboard background
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));

            int rtSize = 384;
            if (previewRT == null || previewRT.width != rtSize || previewRT.height != rtSize)
            {
                if (previewRT != null) DestroyImmediate(previewRT);
                previewRT = new RenderTexture(rtSize, rtSize, 24, RenderTextureFormat.ARGB32);
                previewRT.antiAliasing = 4;
            }

            SetupPreviewGraph();

            // Calculate sample time
            double sampleTime = previewTimer;
            if (previewStepped && framesPerDirection > 0 && animationClip.length > 0)
            {
                int frameIdx = Mathf.Clamp(Mathf.FloorToInt((float)(previewTimer / animationClip.length) * framesPerDirection), 0, framesPerDirection - 1);
                sampleTime = ((double)frameIdx / framesPerDirection) * animationClip.length;
            }

            // Sample animation
            previewClipPlayable.SetTime(sampleTime);
            previewGraph.Evaluate();

            // Rotate character to preview direction
            Quaternion origRot = characterTarget.transform.rotation;
            characterTarget.transform.rotation = Quaternion.Euler(0f, DirectionAngles[previewDirection], 0f);

            // Update head constraints
            Animator anim = characterTarget.GetComponent<Animator>();
            Transform headBone = anim != null ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
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

            // Render camera to previewRT
            RenderTexture prevTarget = isoCamera.targetTexture;
            Color prevBg = isoCamera.backgroundColor;
            CameraClearFlags prevClear = isoCamera.clearFlags;

            isoCamera.targetTexture = previewRT;
            isoCamera.backgroundColor = new Color(0.14f, 0.14f, 0.16f, 1f);
            isoCamera.clearFlags = CameraClearFlags.SolidColor;
            isoCamera.Render();

            isoCamera.targetTexture = prevTarget;
            isoCamera.backgroundColor = prevBg;
            isoCamera.clearFlags = prevClear;
            characterTarget.transform.rotation = origRot;

            // Draw to GUI
            float size = Mathf.Min(rect.width, rect.height) - 10;
            Rect centerRect = new Rect(rect.x + (rect.width - size) * 0.5f, rect.y + (rect.height - size) * 0.5f, size, size);
            GUI.DrawTexture(centerRect, previewRT, ScaleMode.ScaleToFit);
        }

        private void SetupPreviewGraph()
        {
            if (previewGraph.IsValid() && lastGraphClip == animationClip && lastGraphTarget == characterTarget)
            {
                return;
            }

            DestroyPreviewGraph();

            Animator animator = characterTarget.GetComponent<Animator>();
            if (animator == null) return;

            previewGraph = PlayableGraph.Create("StudioPreviewGraph");
            var animOutput = AnimationPlayableOutput.Create(previewGraph, "PreviewOut", animator);
            previewClipPlayable = AnimationClipPlayable.Create(previewGraph, animationClip);
            PlayableOutputExtensions.SetSourcePlayable(animOutput, previewClipPlayable);
            previewGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            lastGraphClip = animationClip;
            lastGraphTarget = characterTarget;
        }

        private void DestroyPreviewGraph()
        {
            if (previewGraph.IsValid())
            {
                previewGraph.Destroy();
            }
            lastGraphClip = null;
            lastGraphTarget = null;
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

            // Double RenderTextures for Calibrated Difference Matte (100% Perfect Transparent Alpha)
            RenderTexture rtBlack = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            RenderTexture rtWhite = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rtBlack.antiAliasing = 4;
            rtWhite.antiAliasing = 4;

            RenderTexture prevRT = cam.targetTexture;
            Color prevBg = cam.backgroundColor;
            CameraClearFlags prevClear = cam.clearFlags;

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

            Texture2D readTexBlack = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
            Texture2D readTexWhite = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);

            try
            {
                cam.clearFlags = CameraClearFlags.SolidColor;

                for (int dir = 0; dir < dirCount; dir++)
                {
                    string dirName = DirectionNames[dir];
                    float angle = DirectionAngles[dir];
                    string dirFolder = Path.Combine(clipFolder, dirName).Replace('\\', '/');
                    if (!Directory.Exists(dirFolder)) Directory.CreateDirectory(dirFolder);

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

                        // Pass 1: Render on Black
                        cam.backgroundColor = Color.black;
                        cam.targetTexture = rtBlack;
                        cam.Render();

                        RenderTexture.active = rtBlack;
                        readTexBlack.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                        readTexBlack.Apply();

                        // Pass 2: Render on White
                        cam.backgroundColor = Color.white;
                        cam.targetTexture = rtWhite;
                        cam.Render();

                        RenderTexture.active = rtWhite;
                        readTexWhite.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                        readTexWhite.Apply();
                        RenderTexture.active = null;

                        // Calibrate background delta from corner
                        Color bgB = readTexBlack.GetPixel(0, 0);
                        Color bgW = readTexWhite.GetPixel(0, 0);
                        float bgDiff = ((bgW.r - bgB.r) + (bgW.g - bgB.g) + (bgW.b - bgB.b)) / 3.0f;
                        if (bgDiff < 0.001f) bgDiff = 0.8f;

                        // Compute true alpha and un-premultiply color
                        Color[] pixB = readTexBlack.GetPixels();
                        Color[] pixW = readTexWhite.GetPixels();
                        Color[] outPix = new Color[resolution * resolution];

                        for (int i = 0; i < outPix.Length; i++)
                        {
                            float diff = ((pixW[i].r - pixB[i].r) + (pixW[i].g - pixB[i].g) + (pixW[i].b - pixB[i].b)) / 3.0f;
                            float alpha = Mathf.Clamp01(1.0f - (diff / bgDiff));

                            if (alpha <= 0.01f)
                            {
                                outPix[i] = new Color(0, 0, 0, 0);
                            }
                            else
                            {
                                float r = Mathf.Clamp01(pixB[i].r / alpha);
                                float g = Mathf.Clamp01(pixB[i].g / alpha);
                                float b = Mathf.Clamp01(pixB[i].b / alpha);
                                outPix[i] = new Color(r, g, b, alpha);
                            }
                        }

                        Texture2D frameTex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
                        frameTex.SetPixels(outPix);
                        frameTex.Apply();

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
                        int row = (dirCount - 1) - dir;
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
                cam.backgroundColor = prevBg;
                cam.clearFlags = prevClear;
                target.transform.rotation = originalRotation;
                target.transform.position = originalPosition;

                DestroyImmediate(rtBlack);
                DestroyImmediate(rtWhite);
                DestroyImmediate(readTexBlack);
                DestroyImmediate(readTexWhite);

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

            return $"Bake completed! 100% Transparent Alpha Sprites saved to: {clipFolder}";
        }
    }
}
