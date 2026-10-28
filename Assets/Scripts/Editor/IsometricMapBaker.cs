using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;

namespace IsometricTool.Editor
{
    public class IsometricMapBaker : EditorWindow
    {
        [Header("Setup")]
        public Vector3 mapCenter = Vector3.zero;
        public float mapAreaSize = 80f;
        public float cameraPitch = 30f;
        public float cameraYaw = 45f;

        [Header("Grid & Resolution")]
        public int chunkCols = 3;
        public int chunkRows = 3;
        public int chunkResolution = 512;
        public Color backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);

        [Header("School 2: Props Library & Entities")]
        public bool bakeCleanGroundOnly = true; // Hide trees/buildings when baking ground so they become separate interactive props
        public bool bakePropsLibrary = true;    // Bake unique props (tree, house, tavern) to PNGs
        public int propResolution = 256;       // Resolution of each prop PNG

        [Header("A* Collision")]
        public bool generateCollision = true;
        public int collisionCellSizePx = 32;
        public float maxWalkableSlope = 40f;

        [Header("Output")]
        public string outputFolder = "Assets/Exports/H5_Map";

        // State
        private bool showGizmos = true;
        private Camera bakeCam;
        private Vector2 scrollPos;

        [MenuItem("Tools/Isometric/Open Map Baker Window")]
        public static void OpenWindow()
        {
            var win = GetWindow<IsometricMapBaker>("Isometric Map Baker");
            win.minSize = new Vector2(480, 680);
            win.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            FitSceneBounds();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyBakeCam();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(6);
            GUILayout.Label("Isometric Map Baker (Interactive Props & Entities)", EditorStyles.boldLabel);

            // MAP AREA
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Phạm vi bản đồ (Map Area)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fit Scene (Toàn cảnh)")) FitSceneBounds();
            if (GUILayout.Button("Focus Selection (Theo mục chọn)")) FocusSelection();
            EditorGUILayout.EndHorizontal();

            mapCenter = EditorGUILayout.Vector3Field("Tâm bản đồ (Center)", mapCenter);
            mapAreaSize = EditorGUILayout.Slider("Kích thước (Mét)", mapAreaSize, 10f, 400f);

            EditorGUILayout.BeginHorizontal();
            cameraPitch = EditorGUILayout.Slider("Góc nghiêng (Pitch)", cameraPitch, 20f, 60f);
            if (GUILayout.Button("30°", GUILayout.Width(45))) cameraPitch = 30f;
            if (GUILayout.Button("35.2°", GUILayout.Width(45))) cameraPitch = 35.264f;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            cameraYaw = EditorGUILayout.Slider("Góc xoay (Yaw)", cameraYaw, 0f, 360f);
            if (GUILayout.Button("45°", GUILayout.Width(45))) cameraYaw = 45f;
            if (GUILayout.Button("135°", GUILayout.Width(45))) cameraYaw = 135f;
            if (GUILayout.Button("225°", GUILayout.Width(45))) cameraYaw = 225f;
            if (GUILayout.Button("315°", GUILayout.Width(45))) cameraYaw = 315f;
            EditorGUILayout.EndHorizontal();

            showGizmos = EditorGUILayout.Toggle("Hiện khung lưới trong Scene", showGizmos);
            EditorGUILayout.EndVertical();

            // CHUNKS
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Nền đất (Ground Chunks)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            chunkCols = EditorGUILayout.IntSlider("Số cột (Cols)", chunkCols, 1, 8);
            chunkRows = EditorGUILayout.IntSlider("Số hàng (Rows)", chunkRows, 1, 8);
            EditorGUILayout.EndHorizontal();

            chunkResolution = EditorGUILayout.IntPopup("Độ phân giải Chunk", chunkResolution,
                new[] { "256 x 256", "512 x 512 (Chuẩn)", "1024 x 1024 (Nét)" },
                new[] { 256, 512, 1024 });

            int totalW = chunkCols * chunkResolution;
            int totalH = chunkRows * chunkResolution;
            EditorGUILayout.LabelField("Kích thước tổng:", $"{totalW} x {totalH} px ({chunkCols * chunkRows} chunks)");
            EditorGUILayout.EndVertical();

            // SCHOOL 2: PROPS & ENTITIES
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Tách Props & Tương tác (Trường phái 2)", EditorStyles.boldLabel);
            bakeCleanGroundOnly = EditorGUILayout.Toggle("Nướng nền đất sạch (Ẩn cây/nhà khi chụp đất)", bakeCleanGroundOnly);
            bakePropsLibrary = EditorGUILayout.Toggle("Xuất thư viện Props (Chụp từng cây/quán rượu ra PNG)", bakePropsLibrary);
            if (bakePropsLibrary)
            {
                propResolution = EditorGUILayout.IntPopup("Độ phân giải Prop PNG", propResolution,
                    new[] { "128 x 128", "256 x 256 (Chuẩn)", "512 x 512" },
                    new[] { 128, 256, 512 });
            }

            EditorGUILayout.Space(2);
            generateCollision = EditorGUILayout.Toggle("Quét lưới va chạm A* (Collision Grid)", generateCollision);
            if (generateCollision)
            {
                collisionCellSizePx = EditorGUILayout.IntPopup("Cỡ ô A* (Pixels)", collisionCellSizePx,
                    new[] { "16 px", "32 px (Chuẩn)", "64 px" },
                    new[] { 16, 32, 64 });
            }
            EditorGUILayout.EndVertical();

            // OUTPUT & BAKE
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Đường dẫn xuất", EditorStyles.boldLabel);
            outputFolder = EditorGUILayout.TextField("Folder:", outputFolder);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.2f, 0.85f, 0.4f);
            if (GUILayout.Button("Bake Map & Props (A - Z)", GUILayout.Height(42)))
            {
                ExecuteFullBake();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void FocusSelection()
        {
            if (Selection.activeTransform != null)
            {
                mapCenter = Selection.activeTransform.position;
            }
        }

        private void FitSceneBounds()
        {
            Terrain[] terrains = Terrain.activeTerrains;
            if (terrains != null && terrains.Length > 0)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var t in terrains)
                {
                    var pos = t.transform.position;
                    var size = t.terrainData.size;
                    minX = Mathf.Min(minX, pos.x);
                    maxX = Mathf.Max(maxX, pos.x + size.x);
                    minZ = Mathf.Min(minZ, pos.z);
                    maxZ = Mathf.Max(maxZ, pos.z + size.z);
                }

                float centerX = (minX + maxX) * 0.5f;
                float centerZ = (minZ + maxZ) * 0.5f;
                float centerY = 0f;
                foreach (var t in terrains)
                {
                    Vector3 samplePoint = new Vector3(centerX, 0f, centerZ);
                    Vector3 tPos = t.transform.position;
                    Vector3 tSize = t.terrainData.size;
                    if (samplePoint.x >= tPos.x && samplePoint.x <= tPos.x + tSize.x &&
                        samplePoint.z >= tPos.z && samplePoint.z <= tPos.z + tSize.z)
                    {
                        centerY = t.SampleHeight(samplePoint);
                        break;
                    }
                }

                mapCenter = new Vector3(centerX, centerY, centerZ);
                mapAreaSize = Mathf.Max(maxX - minX, maxZ - minZ) * 0.75f;
                return;
            }

            MeshRenderer[] mrs = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            if (mrs.Length > 0)
            {
                Bounds b = mrs[0].bounds;
                for (int i = 1; i < mrs.Length; i++) b.Encapsulate(mrs[i].bounds);
                mapCenter = b.center;
                mapAreaSize = Mathf.Max(b.size.x, b.size.z) * 0.8f;
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showGizmos) return;

            Quaternion camRot = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 camRight = camRot * Vector3.right;
            Vector3 camUp = camRot * Vector3.up;

            float chunkWidthWorld = mapAreaSize / chunkCols;
            float chunkHeightWorld = mapAreaSize / chunkRows;

            // Draw full bounding box
            Handles.color = Color.yellow;
            Vector3 topLeft = mapCenter - camRight * (mapAreaSize * 0.5f) + camUp * (mapAreaSize * 0.5f);
            Vector3 topRight = mapCenter + camRight * (mapAreaSize * 0.5f) + camUp * (mapAreaSize * 0.5f);
            Vector3 bottomRight = mapCenter + camRight * (mapAreaSize * 0.5f) - camUp * (mapAreaSize * 0.5f);
            Vector3 bottomLeft = mapCenter - camRight * (mapAreaSize * 0.5f) - camUp * (mapAreaSize * 0.5f);
            Handles.DrawPolyLine(topLeft, topRight, bottomRight, bottomLeft, topLeft);

            // Draw chunks grid
            Handles.color = new Color(0f, 0.9f, 1f, 0.6f);
            for (int c = 1; c < chunkCols; c++)
            {
                float x = -mapAreaSize * 0.5f + c * chunkWidthWorld;
                Handles.DrawLine(mapCenter + camRight * x + camUp * (mapAreaSize * 0.5f),
                                 mapCenter + camRight * x - camUp * (mapAreaSize * 0.5f));
            }
            for (int r = 1; r < chunkRows; r++)
            {
                float y = mapAreaSize * 0.5f - r * chunkHeightWorld;
                Handles.DrawLine(mapCenter - camRight * (mapAreaSize * 0.5f) + camUp * y,
                                 mapCenter + camRight * (mapAreaSize * 0.5f) + camUp * y);
            }

            Handles.color = Color.red;
            Handles.DrawWireDisc(mapCenter, Vector3.up, 2f);
        }

        private Camera GetOrCreateBakeCamera()
        {
            if (bakeCam != null) return bakeCam;

            GameObject camObj = GameObject.Find("__TempIsometricMapBakeCamera");
            if (camObj == null)
            {
                camObj = new GameObject("__TempIsometricMapBakeCamera");
                camObj.hideFlags = HideFlags.HideAndDontSave;
            }
            bakeCam = camObj.GetComponent<Camera>();
            if (bakeCam == null) bakeCam = camObj.AddComponent<Camera>();

            // Configure URP Camera Data for Real-time Shadows & Water Depth Texture
            var additionalData = camObj.GetComponent<UniversalAdditionalCameraData>();
            if (additionalData == null) additionalData = camObj.AddComponent<UniversalAdditionalCameraData>();
            additionalData.renderShadows = true;
            additionalData.renderPostProcessing = false;
            // CRITICAL FOR WATER: Enable Depth Texture & Opaque Texture so transparent water renders!
            additionalData.requiresDepthTextureOption = CameraOverrideOption.On;
            additionalData.requiresOpaqueTextureOption = CameraOverrideOption.On;

            bakeCam.orthographic = true;
            bakeCam.nearClipPlane = 0.1f;
            bakeCam.farClipPlane = 2000f;
            bakeCam.clearFlags = CameraClearFlags.SolidColor;
            bakeCam.backgroundColor = backgroundColor;
            bakeCam.cullingMask = ~0;

            return bakeCam;
        }

        private void DestroyBakeCam()
        {
            if (bakeCam != null)
            {
                bakeCam.targetTexture = null;
                DestroyImmediate(bakeCam.gameObject);
                bakeCam = null;
            }
        }

        public void ExecuteFullBake()
        {
            float safeCamDist = Mathf.Max(350f, mapAreaSize * 3.5f);

            var urpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            float origShadowDistance = 50f;
            if (urpAsset != null)
            {
                origShadowDistance = urpAsset.shadowDistance;
                urpAsset.shadowDistance = Mathf.Max(1200f, safeCamDist + mapAreaSize * 2.5f);
            }

            bool prevFog = RenderSettings.fog;
            RenderSettings.fog = false; // Disable fog for crisp colors

            try
            {
                EditorUtility.DisplayProgressBar("Bake Map", "Khởi tạo thư mục...", 0.05f);

                string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
                string fullOutDir = Path.Combine(projectRoot, outputFolder).Replace('\\', '/');
                string chunksDir = Path.Combine(fullOutDir, "chunks").Replace('\\', '/');
                string propsDir = Path.Combine(fullOutDir, "props").Replace('\\', '/');

                if (!Directory.Exists(fullOutDir)) Directory.CreateDirectory(fullOutDir);
                if (!Directory.Exists(chunksDir)) Directory.CreateDirectory(chunksDir);
                if (bakePropsLibrary && !Directory.Exists(propsDir)) Directory.CreateDirectory(propsDir);

                Camera cam = GetOrCreateBakeCamera();
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = safeCamDist * 2.5f;

                Quaternion camRot = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
                Vector3 camRight = camRot * Vector3.right;
                Vector3 camUp = camRot * Vector3.up;
                Vector3 camForward = camRot * Vector3.forward;

                float chunkWidthWorld = mapAreaSize / chunkCols;
                float chunkHeightWorld = mapAreaSize / chunkRows;
                cam.orthographicSize = chunkHeightWorld * 0.5f;

                int totalMapW = chunkCols * chunkResolution;
                int totalMapH = chunkRows * chunkResolution;

                // --- PASS 1: BAKE CLEAN GROUND CHUNKS (Hide trees/props if enabled) ---
                Terrain[] allTerrains = Terrain.activeTerrains;
                bool[] prevTerrainTreeStates = new bool[allTerrains.Length];
                List<MeshRenderer> hiddenRenderers = new List<MeshRenderer>();

                if (bakeCleanGroundOnly)
                {
                    // Hide terrain trees
                    for (int i = 0; i < allTerrains.Length; i++)
                    {
                        prevTerrainTreeStates[i] = allTerrains[i].drawTreesAndFoliage;
                        allTerrains[i].drawTreesAndFoliage = false;
                    }

                    // Hide non-ground mesh renderers (trees, houses, fences, bridge)
                    foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        if (mr.name.StartsWith("__Temp")) continue;
                        string n = mr.gameObject.name.ToLower();
                        // Keep water plane and terrain
                        if (n == "plane" || n.Contains("water") || n.Contains("terrain")) continue;

                        if (mr.enabled)
                        {
                            mr.enabled = false;
                            hiddenRenderers.Add(mr);
                        }
                    }
                }

                RenderTexture rt = new RenderTexture(chunkResolution, chunkResolution, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 4;
                Texture2D readTex = new Texture2D(chunkResolution, chunkResolution, TextureFormat.RGBA32, false);

                cam.targetTexture = rt;
                cam.backgroundColor = backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;

                int totalChunks = chunkCols * chunkRows;
                int currentChunk = 0;
                for (int r = 0; r < chunkRows; r++)
                {
                    for (int c = 0; c < chunkCols; c++)
                    {
                        currentChunk++;
                        EditorUtility.DisplayProgressBar("Bake Map", $"Render Ground [{c},{r}]...", (float)currentChunk / totalChunks * 0.4f);

                        float offsetX = (c - (chunkCols - 1) * 0.5f) * chunkWidthWorld;
                        float offsetY = ((chunkRows - 1) * 0.5f - r) * chunkHeightWorld;

                        Vector3 chunkCenterWorld = mapCenter + camRight * offsetX + camUp * offsetY;
                        cam.transform.position = chunkCenterWorld - camForward * safeCamDist;
                        cam.transform.rotation = camRot;
                        cam.Render();

                        RenderTexture.active = rt;
                        readTex.ReadPixels(new Rect(0, 0, chunkResolution, chunkResolution), 0, 0);
                        readTex.Apply();
                        RenderTexture.active = null;

                        File.WriteAllBytes(Path.Combine(chunksDir, $"chunk_{c}_{r}.png"), readTex.EncodeToPNG());
                    }
                }

                // Restore hidden objects & terrain trees
                if (bakeCleanGroundOnly)
                {
                    for (int i = 0; i < allTerrains.Length; i++)
                    {
                        allTerrains[i].drawTreesAndFoliage = prevTerrainTreeStates[i];
                    }
                    foreach (var mr in hiddenRenderers)
                    {
                        if (mr != null) mr.enabled = true;
                    }
                }

                cam.targetTexture = null;
                RenderTexture.active = null;
                DestroyImmediate(rt);
                DestroyImmediate(readTex);

                // --- PASS 2: BAKE MINIMAP ---
                EditorUtility.DisplayProgressBar("Bake Map", "Render Minimap...", 0.45f);
                int minimapRes = 1024;
                RenderTexture rtMinimap = new RenderTexture(minimapRes, minimapRes, 24, RenderTextureFormat.ARGB32);
                rtMinimap.antiAliasing = 4;
                Texture2D readMinimap = new Texture2D(minimapRes, minimapRes, TextureFormat.RGBA32, false);

                cam.targetTexture = rtMinimap;
                cam.backgroundColor = backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.orthographicSize = mapAreaSize * 0.5f;
                cam.transform.position = mapCenter - camForward * safeCamDist;
                cam.transform.rotation = camRot;
                cam.Render();

                RenderTexture.active = rtMinimap;
                readMinimap.ReadPixels(new Rect(0, 0, minimapRes, minimapRes), 0, 0);
                readMinimap.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(fullOutDir, "minimap.png"), readMinimap.EncodeToPNG());

                cam.targetTexture = null;
                DestroyImmediate(rtMinimap);
                DestroyImmediate(readMinimap);

                // --- PASS 3: BAKE PROPS LIBRARY (Chụp từng cây, quán rượu ra file PNG tách nền) ---
                Dictionary<string, PropMeta> propsLibrary = new Dictionary<string, PropMeta>();
                List<EntityInstance> entityPlacements = new List<EntityInstance>();

                if (bakePropsLibrary)
                {
                    EditorUtility.DisplayProgressBar("Bake Map", "Bake thư viện Props (PNGs)...", 0.55f);

                    // 1. Collect unique tree prototypes from terrains
                    Dictionary<int, GameObject> treePrototypePrefabs = new Dictionary<int, GameObject>();
                    foreach (var t in allTerrains)
                    {
                        var protos = t.terrainData.treePrototypes;
                        for (int i = 0; i < protos.Length; i++)
                        {
                            if (protos[i].prefab != null && !treePrototypePrefabs.ContainsKey(i))
                            {
                                treePrototypePrefabs[i] = protos[i].prefab;
                            }
                        }
                    }

                    // Bake each unique tree prototype
                    foreach (var kvp in treePrototypePrefabs)
                    {
                        GameObject prefab = kvp.Value;
                        string propKey = prefab.name;
                        if (!propsLibrary.ContainsKey(propKey))
                        {
                            PropMeta meta = BakeSingleProp(prefab, propKey, propsDir, propResolution, camRot);
                            propsLibrary[propKey] = meta;
                        }
                    }

                    // 2. Scan scene GameObjects (Props like Bridge, Fence, Tavern, Houses)
                    foreach (var mr in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        if (mr.name.StartsWith("__Temp")) continue;
                        string n = mr.gameObject.name;
                        string nl = n.ToLower();
                        if (nl == "plane" || nl.Contains("water") || nl.Contains("terrain")) continue;

                        // Check if inside map bounds
                        Vector3 worldPos = mr.bounds.center;
                        worldPos.y = mr.bounds.min.y; // Foot position
                        Vector2Int pixelPos = WorldToPixel(worldPos, mapCenter, camRight, camUp, mapAreaSize, totalMapW, totalMapH);

                        if (pixelPos.x >= 0 && pixelPos.x <= totalMapW && pixelPos.y >= 0 && pixelPos.y <= totalMapH)
                        {
                            string propKey = mr.gameObject.name.Replace(" ", "_");
                            // Strip clone suffix if any
                            if (propKey.Contains("(")) propKey = propKey.Substring(0, propKey.IndexOf("(")).TrimEnd('_');

                            if (!propsLibrary.ContainsKey(propKey))
                            {
                                PropMeta meta = BakeSingleProp(mr.gameObject, propKey, propsDir, propResolution, camRot);
                                propsLibrary[propKey] = meta;
                            }

                            bool isBuilding = nl.Contains("tavern") || nl.Contains("house") || nl.Contains("shop") || nl.Contains("bridge") || nl.Contains("castle");
                            entityPlacements.Add(new EntityInstance
                            {
                                id = "obj_" + entityPlacements.Count,
                                type = propKey,
                                x = pixelPos.x,
                                y = pixelPos.y,
                                name = mr.gameObject.name,
                                interactable = isBuilding,
                                action = isBuilding ? "Vào xem" : "Tương tác"
                            });
                        }
                    }

                    // 3. Scan terrain tree instances
                    foreach (var t in allTerrains)
                    {
                        var tData = t.terrainData;
                        Vector3 tPos = t.transform.position;
                        Vector3 tSize = tData.size;

                        for (int i = 0; i < tData.treeInstanceCount; i++)
                        {
                            TreeInstance ti = tData.GetTreeInstance(i);
                            if (treePrototypePrefabs.TryGetValue(ti.prototypeIndex, out GameObject prefab))
                            {
                                Vector3 worldPos = Vector3.Scale(ti.position, tSize) + tPos;
                                Vector2Int pixelPos = WorldToPixel(worldPos, mapCenter, camRight, camUp, mapAreaSize, totalMapW, totalMapH);

                                if (pixelPos.x >= 0 && pixelPos.x <= totalMapW && pixelPos.y >= 0 && pixelPos.y <= totalMapH)
                                {
                                    entityPlacements.Add(new EntityInstance
                                    {
                                        id = "tree_" + entityPlacements.Count,
                                        type = prefab.name,
                                        x = pixelPos.x,
                                        y = pixelPos.y,
                                        name = prefab.name,
                                        interactable = false,
                                        action = "Chặt cây"
                                    });
                                }
                            }
                        }
                    }
                }

                // --- PASS 4: COLLISION GRID ---
                int gridCols = totalMapW / collisionCellSizePx;
                int gridRows = totalMapH / collisionCellSizePx;
                int[][] grid = new int[gridRows][];

                if (generateCollision)
                {
                    EditorUtility.DisplayProgressBar("Bake Map", "Quét lưới va chạm...", 0.85f);
                    for (int gy = 0; gy < gridRows; gy++)
                    {
                        grid[gy] = new int[gridCols];
                        float normY = ((float)gy + 0.5f) / gridRows;
                        float camY = (0.5f - normY) * mapAreaSize;

                        for (int gx = 0; gx < gridCols; gx++)
                        {
                            float normX = ((float)gx + 0.5f) / gridCols;
                            float camX = (normX - 0.5f) * mapAreaSize;

                            Vector3 rayOrigin = mapCenter + camRight * camX + camUp * camY - camForward * safeCamDist;
                            Ray ray = new Ray(rayOrigin, camForward);

                            if (Physics.Raycast(ray, out RaycastHit hit, safeCamDist * 2f + 500f))
                            {
                                float slope = Vector3.Angle(hit.normal, Vector3.up);
                                string hitName = hit.collider.gameObject.name.ToLower();
                                if (slope > maxWalkableSlope || hitName.Contains("rock") || hitName.Contains("fence") || hitName.Contains("cube") || hitName.Contains("tree"))
                                {
                                    grid[gy][gx] = 1;
                                }
                                else
                                {
                                    grid[gy][gx] = 0;
                                }
                            }
                            else
                            {
                                grid[gy][gx] = 0;
                            }
                        }
                    }
                }
                else
                {
                    for (int gy = 0; gy < gridRows; gy++) grid[gy] = new int[gridCols];
                }

                // --- PASS 5: WRITE JSON & HTML ---
                EditorUtility.DisplayProgressBar("Bake Map", "Xuất file dữ liệu & HTML5...", 0.95f);
                string jsonContent = GenerateSchool2MapDataJson(totalMapW, totalMapH, gridCols, gridRows, grid, propsLibrary, entityPlacements);
                File.WriteAllText(Path.Combine(fullOutDir, "map_data.json"), jsonContent);

                string htmlContent = GenerateSchool2HtmlDemo(jsonContent);
                File.WriteAllText(Path.Combine(fullOutDir, "index.html"), htmlContent);

                // Cleanup
                DestroyBakeCam();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Bake Xong!", 
                    $"Bake hoàn tất theo Trường phái 2 (Props + Y-Sorting)!\n" +
                    $"- Kích thước: {totalMapW}x{totalMapH} px\n" +
                    $"- Thư viện Props: {propsLibrary.Count} mẫu riêng biệt\n" +
                    $"- Thực thể (Entities): {entityPlacements.Count} vị trí cắm trên map\n\n" +
                    $"Hãy mở file index.html để test tương tác click vào vật thể!", "Mở Thư Mục");
                EditorUtility.RevealInFinder(Path.Combine(fullOutDir, "index.html"));
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                DestroyBakeCam();
                Debug.LogError("[IsometricMapBaker] Lỗi: " + ex.Message + "\n" + ex.StackTrace);
                EditorUtility.DisplayDialog("Lỗi", ex.Message, "OK");
            }
            finally
            {
                RenderSettings.fog = prevFog;
                if (urpAsset != null) urpAsset.shadowDistance = origShadowDistance;
            }
        }

        private Vector2Int WorldToPixel(Vector3 worldPos, Vector3 center, Vector3 camRight, Vector3 camUp, float areaSize, int mapW, int mapH)
        {
            Vector3 delta = worldPos - center;
            float camX = Vector3.Dot(delta, camRight);
            float camY = Vector3.Dot(delta, camUp);
            float normX = (camX / areaSize) + 0.5f;
            float normY = 0.5f - (camY / areaSize);
            return new Vector2Int(Mathf.RoundToInt(normX * mapW), Mathf.RoundToInt(normY * mapH));
        }

        private PropMeta BakeSingleProp(GameObject source, string propKey, string outDir, int res, Quaternion camRot)
        {
            // Spawn temporary instance far away to isolate
            GameObject temp = Instantiate(source);
            temp.name = "__TempPropBake";
            temp.transform.position = new Vector3(5000f, 5000f, 5000f);
            temp.transform.rotation = Quaternion.identity;

            // Compute bounds
            Bounds bounds = new Bounds(temp.transform.position, Vector3.zero);
            bool hasBounds = false;
            foreach (var r in temp.GetComponentsInChildren<Renderer>())
            {
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            GameObject camObj = new GameObject("__TempPropCam");
            Camera pCam = camObj.AddComponent<Camera>();
            var pData = camObj.AddComponent<UniversalAdditionalCameraData>();
            pData.renderShadows = true;
            pData.renderPostProcessing = false;

            pCam.orthographic = true;
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            pCam.orthographicSize = maxDim * 0.75f;
            pCam.nearClipPlane = 0.1f;
            pCam.farClipPlane = 200f;
            pCam.clearFlags = CameraClearFlags.SolidColor;
            pCam.backgroundColor = new Color(0, 0, 0, 0); // Transparent

            Vector3 forward = camRot * Vector3.forward;
            camObj.transform.position = bounds.center - forward * 50f;
            camObj.transform.rotation = camRot;

            RenderTexture rt = new RenderTexture(res, res, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            pCam.targetTexture = rt;
            pCam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            tex.Apply();

            pCam.targetTexture = null;
            RenderTexture.active = null;

            string fileName = $"prop_{propKey}.png";
            File.WriteAllBytes(Path.Combine(outDir, fileName), tex.EncodeToPNG());

            // Compute foot pivot in pixel space
            Vector3 footWorld = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 vp = pCam.WorldToViewportPoint(footWorld);
            int pivotX = Mathf.Clamp(Mathf.RoundToInt(vp.x * res), 0, res);
            int pivotY = Mathf.Clamp(Mathf.RoundToInt((1f - vp.y) * res), 0, res);

            // Cleanup
            DestroyImmediate(temp);
            DestroyImmediate(camObj);
            DestroyImmediate(rt);
            DestroyImmediate(tex);

            return new PropMeta
            {
                file = "props/" + fileName,
                width = res,
                height = res,
                pivotX = pivotX,
                pivotY = pivotY
            };
        }

        [Serializable]
        public struct PropMeta
        {
            public string file;
            public int width;
            public int height;
            public int pivotX;
            public int pivotY;
        }

        [Serializable]
        public struct EntityInstance
        {
            public string id;
            public string type;
            public int x;
            public int y;
            public string name;
            public bool interactable;
            public string action;
        }

        private string GenerateSchool2MapDataJson(int totalW, int totalH, int gridCols, int gridRows, int[][] grid,
            Dictionary<string, PropMeta> library, List<EntityInstance> entities)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"mapWidth\": {totalW},");
            sb.AppendLine($"  \"mapHeight\": {totalH},");
            sb.AppendLine($"  \"chunkCols\": {chunkCols},");
            sb.AppendLine($"  \"chunkRows\": {chunkRows},");
            sb.AppendLine($"  \"chunkResolution\": {chunkResolution},");
            sb.AppendLine($"  \"collisionCellSize\": {collisionCellSizePx},");
            sb.AppendLine($"  \"gridCols\": {gridCols},");
            sb.AppendLine($"  \"gridRows\": {gridRows},");

            // Props Library
            sb.AppendLine("  \"props_library\": {");
            int libIdx = 0;
            foreach (var kvp in library)
            {
                libIdx++;
                var m = kvp.Value;
                sb.AppendLine($"    \"{kvp.Key}\": {{ \"file\": \"{m.file}\", \"width\": {m.width}, \"height\": {m.height}, \"pivotX\": {m.pivotX}, \"pivotY\": {m.pivotY} }}{(libIdx == library.Count ? "" : ",")}");
            }
            sb.AppendLine("  },");

            // Entities placement list
            sb.AppendLine("  \"entities\": [");
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                sb.Append($"    {{ \"id\": \"{e.id}\", \"type\": \"{e.type}\", \"x\": {e.x}, \"y\": {e.y}, \"name\": \"{e.name}\", \"interactable\": {(e.interactable ? "true" : "false")}, \"action\": \"{e.action}\" }}");
                sb.AppendLine(i == entities.Count - 1 ? "" : ",");
            }
            sb.AppendLine("  ],");

            // Collision grid
            sb.AppendLine("  \"grid\": [");
            for (int r = 0; r < gridRows; r++)
            {
                sb.Append("    [");
                sb.Append(string.Join(",", grid[r]));
                sb.Append(r == gridRows - 1 ? "]" : "],");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateSchool2HtmlDemo(string embeddedJson)
        {
            return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>H5 Isometric Interactive Engine (School 2)</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; }
        body { background: #111; overflow: hidden; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        #canvas { display: block; }
        #ui {
            position: absolute; top: 12px; left: 12px; background: rgba(20, 24, 30, 0.88);
            padding: 12px 16px; border-radius: 8px; color: #eee; font-size: 13px;
            backdrop-filter: blur(6px); border: 1px solid rgba(255, 255, 255, 0.1);
            box-shadow: 0 4px 15px rgba(0,0,0,0.5); pointer-events: auto; max-width: 340px;
        }
        #ui h2 { font-size: 15px; margin-bottom: 6px; color: #4ade80; }
        #ui label { display: flex; align-items: center; gap: 6px; margin: 4px 0; cursor: pointer; }
        #minimap-ui {
            position: absolute; bottom: 12px; right: 12px; width: 180px; height: 180px;
            border: 2px solid rgba(255,255,255,0.25); border-radius: 8px; overflow: hidden;
            background: #1a1a20; box-shadow: 0 4px 15px rgba(0,0,0,0.6);
        }
        #minimap-canvas { width: 100%; height: 100%; display: block; }
        
        /* Interactive Modal */
        #modal {
            display: none; position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%);
            background: #1e293b; color: #f8fafc; padding: 20px 24px; border-radius: 12px;
            border: 2px solid #38bdf8; box-shadow: 0 10px 30px rgba(0,0,0,0.8); z-index: 100;
            min-width: 280px; text-align: center;
        }
        #modal h3 { color: #38bdf8; margin-bottom: 8px; font-size: 18px; }
        #modal p { font-size: 14px; margin-bottom: 16px; color: #cbd5e1; }
        #modal button {
            background: #0284c7; color: white; border: none; padding: 8px 18px;
            border-radius: 6px; cursor: pointer; font-weight: bold; font-size: 14px;
        }
        #modal button:hover { background: #0369a1; }
    </style>
</head>
<body>
    <canvas id=""canvas""></canvas>

    <div id=""ui"">
        <h2>Isometric Interactive Engine</h2>
        <p>• <b>Kéo chuột:</b> Lia camera | <b>Cuộn:</b> Phóng to/nhỏ</p>
        <p>• <b>Click đất:</b> Di chuyển nhân vật</p>
        <p>• <b>Click Quán Rượu / Cây:</b> Tương tác sự kiện!</p>
        <hr style=""border-color: rgba(255,255,255,0.1); margin: 8px 0;"">
        <label><input type=""checkbox"" id=""chkYSort"" checked> Bật Y-Sorting (Che nhân vật khi nấp sau)</label>
        <label><input type=""checkbox"" id=""chkGrid""> Hiện Lưới Va Chạm A*</label>
        <label><input type=""checkbox"" id=""chkFoot""> Hiện Chân Đế Props (Pivot)</label>
        <p id=""status"" style=""color: #4ade80; margin-top: 6px;"">Đang tải...</p>
    </div>

    <div id=""minimap-ui"">
        <canvas id=""minimap-canvas"" width=""180"" height=""180""></canvas>
    </div>

    <div id=""modal"">
        <h3 id=""modal-title"">Tương Tác</h3>
        <p id=""modal-desc"">Nội dung</p>
        <button onclick=""closeModal()"">Đóng</button>
    </div>

    <script>
        const canvas = document.getElementById('canvas');
        const ctx = canvas.getContext('2d');
        const miniCanvas = document.getElementById('minimap-canvas');
        const miniCtx = miniCanvas.getContext('2d');

        function resize() {
            canvas.width = window.innerWidth;
            canvas.height = window.innerHeight;
        }
        window.addEventListener('resize', resize);
        resize();

        const mapData = " + embeddedJson + @";
        document.getElementById('status').innerText = `${mapData.mapWidth}x${mapData.mapHeight} px | ${mapData.entities ? mapData.entities.length : 0} Thực thể`;

        // 1. Preload Background Chunks
        let bgChunks = [];
        for (let r = 0; r < mapData.chunkRows; r++) {
            for (let c = 0; c < mapData.chunkCols; c++) {
                const img = new Image();
                img.src = `chunks/chunk_${c}_${r}.png`;
                bgChunks.push({ col: c, row: r, img: img });
            }
        }

        // 2. Preload Props Library
        const propImages = {};
        if (mapData.props_library) {
            for (const key in mapData.props_library) {
                const img = new Image();
                img.src = mapData.props_library[key].file;
                propImages[key] = img;
            }
        }

        let minimapImg = new Image();
        minimapImg.src = 'minimap.png';

        // Camera & Player State
        let zoom = 1.0;
        let camX = (canvas.width - mapData.mapWidth * zoom) / 2;
        let camY = (canvas.height - mapData.mapHeight * zoom) / 2;
        let isDragging = false, lastMouseX = 0, lastMouseY = 0;
        let hoveredEntity = null;

        const player = {
            x: mapData.mapWidth / 2,
            y: mapData.mapHeight / 2,
            targetX: mapData.mapWidth / 2,
            targetY: mapData.mapHeight / 2,
            speed: 220,
            size: 14,
            color: '#38bdf8'
        };

        // Mouse Controls
        canvas.addEventListener('mousedown', e => {
            if (e.button === 0) {
                lastMouseX = e.clientX;
                lastMouseY = e.clientY;
                isDragging = false;
            }
        });

        window.addEventListener('mousemove', e => {
            const worldX = (e.clientX - camX) / zoom;
            const worldY = (e.clientY - camY) / zoom;

            if (e.buttons === 1) {
                const dx = e.clientX - lastMouseX;
                const dy = e.clientY - lastMouseY;
                if (Math.abs(dx) > 3 || Math.abs(dy) > 3) {
                    isDragging = true;
                    camX += dx;
                    camY += dy;
                    lastMouseX = e.clientX;
                    lastMouseY = e.clientY;
                }
            } else {
                // Check Hover on Entities
                hoveredEntity = null;
                if (mapData.entities) {
                    for (let i = mapData.entities.length - 1; i >= 0; i--) {
                        const ent = mapData.entities[i];
                        const lib = mapData.props_library[ent.type];
                        if (!lib) continue;

                        const left = ent.x - lib.pivotX;
                        const top = ent.y - lib.pivotY;
                        if (worldX >= left && worldX <= left + lib.width &&
                            worldY >= top && worldY <= top + lib.height) {
                            hoveredEntity = ent;
                            break;
                        }
                    }
                }
                canvas.style.cursor = hoveredEntity ? (hoveredEntity.interactable ? 'pointer' : 'help') : 'default';
            }
        });

        canvas.addEventListener('mouseup', e => {
            if (!isDragging && e.button === 0) {
                const worldX = (e.clientX - camX) / zoom;
                const worldY = (e.clientY - camY) / zoom;

                if (hoveredEntity) {
                    // Click on Entity!
                    player.targetX = hoveredEntity.x;
                    player.targetY = hoveredEntity.y;
                    showModal(hoveredEntity.name, `Bạn vừa nhấp vào: <b>${hoveredEntity.name}</b>!<br>Hành động: [${hoveredEntity.action}]`);
                } else {
                    // Click on Ground to Move
                    player.targetX = Math.max(0, Math.min(mapData.mapWidth, worldX));
                    player.targetY = Math.max(0, Math.min(mapData.mapHeight, worldY));
                }
            }
            isDragging = false;
        });

        canvas.addEventListener('wheel', e => {
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.1 : 0.9;
            const mouseX = e.clientX;
            const mouseY = e.clientY;
            camX = mouseX - (mouseX - camX) * factor;
            camY = mouseY - (mouseY - camY) * factor;
            zoom = Math.max(0.3, Math.min(2.5, zoom * factor));
        });

        function showModal(title, text) {
            document.getElementById('modal-title').innerText = title;
            document.getElementById('modal-desc').innerHTML = text;
            document.getElementById('modal').style.display = 'block';
        }

        function closeModal() {
            document.getElementById('modal').style.display = 'none';
        }

        // Loop
        let lastTime = performance.now();
        function loop(now) {
            const dt = (now - lastTime) / 1000;
            lastTime = now;
            update(dt);
            render();
            requestAnimationFrame(loop);
        }

        function update(dt) {
            const dx = player.targetX - player.x;
            const dy = player.targetY - player.y;
            const dist = Math.hypot(dx, dy);
            if (dist > 3) {
                const moveDist = Math.min(dist, player.speed * dt);
                player.x += (dx / dist) * moveDist;
                player.y += (dy / dist) * moveDist;
            }
        }

        function render() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            ctx.save();
            ctx.translate(camX, camY);
            ctx.scale(zoom, zoom);

            const cRes = mapData.chunkResolution;
            const showGrid = document.getElementById('chkGrid').checked;
            const showFoot = document.getElementById('chkFoot').checked;
            const enableYSort = document.getElementById('chkYSort').checked;

            // 1. RENDER BACKGROUND (GROUND & WATER ONLY)
            for (const ch of bgChunks) {
                const chunkX = ch.col * cRes;
                const chunkY = ch.row * cRes;
                if (camX + (chunkX + cRes) * zoom < 0 || camX + chunkX * zoom > canvas.width ||
                    camY + (chunkY + cRes) * zoom < 0 || camY + chunkY * zoom > canvas.height) {
                    continue;
                }
                if (ch.img.complete && ch.img.naturalWidth > 0) {
                    ctx.drawImage(ch.img, chunkX, chunkY, cRes, cRes);
                }
            }

            // 2. COLLISION GRID
            if (showGrid && mapData.grid) {
                const cSize = mapData.collisionCellSize;
                for (let r = 0; r < mapData.gridRows; r++) {
                    for (let c = 0; c < mapData.gridCols; c++) {
                        if (mapData.grid[r][c] === 1) {
                            ctx.fillStyle = 'rgba(239, 68, 68, 0.35)';
                            ctx.fillRect(c * cSize, r * cSize, cSize, cSize);
                        }
                    }
                }
            }

            // 3. Y-SORTING QUEUE: ENTITIES + PLAYER
            const renderQueue = [];

            // Add visible entities
            if (mapData.entities) {
                for (const ent of mapData.entities) {
                    const lib = mapData.props_library[ent.type];
                    if (!lib) continue;
                    // Viewport culling
                    if (camX + (ent.x + lib.width) * zoom < 0 || camX + (ent.x - lib.width) * zoom > canvas.width ||
                        camY + (ent.y + lib.height) * zoom < 0 || camY + (ent.y - lib.height) * zoom > canvas.height) {
                        continue;
                    }
                    renderQueue.push({ isPlayer: false, y: ent.y, data: ent, lib: lib });
                }
            }

            // Add player
            renderQueue.push({ isPlayer: true, y: player.y });

            // Sort by foot Y
            if (enableYSort) {
                renderQueue.sort((a, b) => a.y - b.y);
            }

            // Draw all in sorted order
            for (const item of renderQueue) {
                if (item.isPlayer) {
                    // Draw Player Shadow
                    ctx.fillStyle = 'rgba(0, 0, 0, 0.45)';
                    ctx.beginPath();
                    ctx.ellipse(player.x, player.y + 2, player.size * 0.9, player.size * 0.45, 0, 0, Math.PI * 2);
                    ctx.fill();

                    // Draw Player
                    ctx.fillStyle = player.color;
                    ctx.beginPath();
                    ctx.arc(player.x, player.y - player.size * 0.8, player.size, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.strokeStyle = '#fff';
                    ctx.lineWidth = 2 / zoom;
                    ctx.stroke();
                } else {
                    const ent = item.data;
                    const lib = item.lib;
                    const img = propImages[ent.type];
                    const drawX = ent.x - lib.pivotX;
                    const drawY = ent.y - lib.pivotY;

                    if (img && img.complete) {
                        ctx.drawImage(img, drawX, drawY, lib.width, lib.height);
                    }

                    // Highlight on Hover
                    if (hoveredEntity === ent) {
                        ctx.strokeStyle = '#38bdf8';
                        ctx.lineWidth = 2 / zoom;
                        ctx.strokeRect(drawX, drawY, lib.width, lib.height);

                        // Floating Name Tag
                        ctx.fillStyle = 'rgba(0, 0, 0, 0.75)';
                        ctx.fillRect(ent.x - 50 / zoom, drawY - 24 / zoom, 100 / zoom, 20 / zoom);
                        ctx.fillStyle = '#4ade80';
                        ctx.font = `${12 / zoom}px sans-serif`;
                        ctx.textAlign = 'center';
                        ctx.fillText(ent.name, ent.x, drawY - 10 / zoom);
                        ctx.textAlign = 'left';
                    }

                    // Foot Pivot Marker
                    if (showFoot) {
                        ctx.fillStyle = '#facc15';
                        ctx.beginPath();
                        ctx.arc(ent.x, ent.y, 3 / zoom, 0, Math.PI * 2);
                        ctx.fill();
                    }
                }
            }

            // Destination Marker
            if (Math.hypot(player.targetX - player.x, player.targetY - player.y) > 5) {
                ctx.strokeStyle = '#4ade80';
                ctx.lineWidth = 2 / zoom;
                ctx.beginPath();
                ctx.arc(player.targetX, player.targetY, 8, 0, Math.PI * 2);
                ctx.stroke();
            }

            ctx.restore();
            renderMinimap();
        }

        function renderMinimap() {
            miniCtx.clearRect(0, 0, miniCanvas.width, miniCanvas.height);
            if (minimapImg.complete && minimapImg.naturalWidth > 0) {
                miniCtx.drawImage(minimapImg, 0, 0, miniCanvas.width, miniCanvas.height);
            }
            if (!mapData) return;

            const px = (player.x / mapData.mapWidth) * miniCanvas.width;
            const py = (player.y / mapData.mapHeight) * miniCanvas.height;
            miniCtx.fillStyle = '#38bdf8';
            miniCtx.beginPath();
            miniCtx.arc(px, py, 4, 0, Math.PI * 2);
            miniCtx.fill();

            const viewX = (-camX / (mapData.mapWidth * zoom)) * miniCanvas.width;
            const viewY = (-camY / (mapData.mapHeight * zoom)) * miniCanvas.height;
            const viewW = (canvas.width / (mapData.mapWidth * zoom)) * miniCanvas.width;
            const viewH = (canvas.height / (mapData.mapHeight * zoom)) * miniCanvas.height;
            miniCtx.strokeStyle = 'rgba(255, 255, 255, 0.8)';
            miniCtx.lineWidth = 2;
            miniCtx.strokeRect(viewX, viewY, viewW, viewH);
        }

        requestAnimationFrame(loop);
    </script>
</body>
</html>";
        }
    }
}
