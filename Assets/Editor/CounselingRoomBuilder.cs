using System.Collections.Generic;
using System.IO;
using AdieLab.AffectCounsel;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselingRoomBuilder
    {
        private const string ScenePath = "Assets/Scenes/KoreanCounselingRoom.unity";
        private const string MaterialRoot = "Assets/Materials/Counseling";
        private const string AvatarPath = "Assets/ThirdParty/MicrosoftRocketbox/Avatars/Adults/Female_Adult_05/Export/Female_Adult_05_facial.fbx";
        private const string FontPath = "Assets/Fonts/NotoSansKR-Regular.otf";
        private const string BoldFontPath = "Assets/Fonts/NotoSansKR-Bold.otf";
        private const string CasePath = "Assets/Data/Cases/WorkplaceAnxietyCase.asset";
        private const string ArtworkTexturePath = "Assets/Art/Textures/HanjiMountainArtwork.png";
        private const string RoomAssetPackPath = "Assets/Models/CounselCue/CounselCueRoomAssetPack.fbx";

        private static Material cream;
        private static Material warmWhite;
        private static Material oak;
        private static Material darkOak;
        private static Material sage;
        private static Material teal;
        private static Material charcoal;
        private static Material brass;
        private static Material windowGlow;
        private static Material leaf;
        private static Material paper;
        private static Material artwork;
        private static Material windowView;

        [MenuItem("Tools/CounselCue/Build Korean Counseling Room")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialRoot);
            EnsureFolder("Assets/Animations");
            CreateMaterials();
            RuntimeAnimatorController controller = CounselingAnimatorFactory.Create();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CaseCatalog caseCatalog = CounselingContentFactory.CreateOrUpdate();
            CounselingCaseDefinition caseDefinition = caseCatalog.DefaultCase;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.72f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.41f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.17f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.86f, 0.81f, 0.73f);
            RenderSettings.fogDensity = 0.002f;

            Transform environment = new GameObject("KoreanCounselingRoom_Environment").transform;
            BuildArchitecture(environment);
            BuildFurniture(environment);
            BuildPremiumAssetSet(environment);
            BuildDecor(environment);
            Camera camera = BuildCameraAndLights();
            Transform lookTarget = new GameObject("CounselorEyeContactTarget").transform;
            lookTarget.SetParent(camera.transform, false);
            lookTarget.localPosition = new Vector3(0f, -0.04f, 0.18f);
            ClientAvatarHost client = BuildClient(lookTarget, controller, caseDefinition);
            UiReferences ui = BuildUi();

            GameObject runtime = new GameObject("CounselCue_Runtime");
            WebcamSignalMonitor webcam = runtime.AddComponent<WebcamSignalMonitor>();
            FacialActionUnitMonitor actionUnits = runtime.AddComponent<FacialActionUnitMonitor>();
            GptRealtimeConversationEngine realtime = runtime.AddComponent<GptRealtimeConversationEngine>();
            WebNpcConversationEngine webNpc = runtime.AddComponent<WebNpcConversationEngine>();
            CounselCueWebBridge webBridge = runtime.AddComponent<CounselCueWebBridge>();
            CounselingReflectionController reflection = runtime.AddComponent<CounselingReflectionController>();
            CounselingSessionOrchestrator orchestrator = runtime.AddComponent<CounselingSessionOrchestrator>();
            CounselingSessionController session = runtime.AddComponent<CounselingSessionController>();
            CounselingCameraZoom cameraZoom = runtime.AddComponent<CounselingCameraZoom>();
            CounselingLanguageToggle languageToggle = runtime.AddComponent<CounselingLanguageToggle>();
            ClientObservationDebugHud debugHud = runtime.AddComponent<ClientObservationDebugHud>();
            ResearchDataControls dataControls = runtime.AddComponent<ResearchDataControls>();
            runtime.AddComponent<DemoCaptureController>();
            WireWebcam(webcam, ui);
            WireActionUnits(actionUnits, ui);
            WireSession(session, orchestrator, caseDefinition, client, webcam, actionUnits, realtime, webNpc, webBridge, ui);
            WireWebExperience(webBridge, session, orchestrator, webNpc, client, ui);
            WireSessionOrchestrator(orchestrator, session, reflection, caseDefinition, caseCatalog, client, ui);
            WireReflection(reflection, orchestrator, ui);
            WireCameraZoom(cameraZoom, camera, client, ui);
            WireClientDebug(debugHud, client, ui);
            WireLanguageToggle(languageToggle, orchestrator, ui);
            WireResearchDataControls(dataControls, ui);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = runtime;
            Debug.Log($"AFFECT_COUNSEL_SCENE_BUILT {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        public static void BuildWindowsFromCommandLine()
        {
            Build();
            Directory.CreateDirectory("Builds/CounselCue");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/CounselCue/CounselCue.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildPipeline.BuildPlayer(options);
            EditorApplication.Exit(0);
        }

        public static void BuildWebGLFromCommandLine()
        {
            Build();
            Directory.CreateDirectory("Builds/WebGL");
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.template = "PROJECT:CounselCue";
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException($"CounselCue WebGL build failed: {report.summary.result}");
            }

            EditorApplication.Exit(0);
        }

        private static void BuildArchitecture(Transform parent)
        {
            CreateCube("OakFloor", new Vector3(0f, -0.08f, 0f), new Vector3(6.4f, 0.16f, 7.4f), oak, parent);
            CreateCube("BackWall", new Vector3(0f, 1.6f, 3.58f), new Vector3(6.4f, 3.2f, 0.16f), cream, parent);
            CreateCube("LeftWall", new Vector3(-3.12f, 1.6f, 0f), new Vector3(0.16f, 3.2f, 7.4f), warmWhite, parent);
            CreateCube("RightWall", new Vector3(3.12f, 1.6f, 0f), new Vector3(0.16f, 3.2f, 7.4f), warmWhite, parent);
            CreateCube("Ceiling", new Vector3(0f, 3.18f, 0f), new Vector3(6.4f, 0.12f, 7.4f), warmWhite, parent);
            CreateCube("BackBaseboard", new Vector3(0f, 0.09f, 3.45f), new Vector3(6.1f, 0.18f, 0.06f), darkOak, parent);
            CreateCube("LeftBaseboard", new Vector3(-2.99f, 0.09f, 0f), new Vector3(0.06f, 0.18f, 7.1f), darkOak, parent);
            CreateCube("RightBaseboard", new Vector3(2.99f, 0.09f, 0f), new Vector3(0.06f, 0.18f, 7.1f), darkOak, parent);

            CreateCube("Window", new Vector3(-3.01f, 1.82f, 0.78f), new Vector3(0.045f, 1.82f, 2.28f), windowGlow, parent);
            CreateCube("SheerWindow", new Vector3(-2.96f, 1.82f, 0.78f), new Vector3(0.035f, 1.74f, 2.16f), warmWhite, parent);

            CreateCube("BackWindowGlow", new Vector3(-2.14f, 1.76f, 3.46f), new Vector3(1.18f, 2.28f, 0.05f), windowView != null ? windowView : windowGlow, parent);
            CreateCurtain("LeftCurtain", -2.64f, 1.62f, 3.38f, 0.78f, parent);
            CreateCurtain("RightCurtain", 2.34f, 1.62f, 3.38f, 0.92f, parent);
        }

        private static void BuildFurniture(Transform parent)
        {
            CreateChair("ClientChair", new Vector3(0f, 0f, 1.30f), 180f, warmWhite, parent, true);
            CreateChair("CounselorChair", new Vector3(0.78f, 0f, -1.92f), 14f, sage, parent, false);
            CreateLowConsole(new Vector3(-1.78f, 0f, 3.02f), parent);
        }

        private static void BuildPremiumAssetSet(Transform parent)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(RoomAssetPackPath);
            if (source == null)
            {
                throw new System.InvalidOperationException($"CounselCue room asset pack is missing: {RoomAssetPackPath}");
            }

            Transform premiumRoot = new GameObject("CounselCue_PremiumAssets").transform;
            premiumRoot.SetParent(parent);
            GameObject packInstance = (GameObject)PrefabUtility.InstantiatePrefab(source, premiumRoot);
            packInstance.name = "AssetPack_ExtractionSource";
            PrefabUtility.UnpackPrefabInstance(packInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            ExtractAssetGroup(packInstance.transform, premiumRoot, "WovenRug_Blender", "CC_WovenRug_",
                new Vector3(0f, 0.002f, 0.22f), Quaternion.identity, new Vector3(2.55f, 1f, 3.25f));

            Vector3 sideTable = new Vector3(1.20f, 0f, 1.22f);
            ExtractAssetGroup(packInstance.transform, premiumRoot, "RoundOakTable_Blender", "CC_RoundOakTable_",
                sideTable, Quaternion.Euler(0f, -12f, 0f), Vector3.one * 0.78f);
            ExtractAssetGroup(packInstance.transform, premiumRoot, "LinenTissueBox_Blender", "CC_TissueBox_",
                sideTable + new Vector3(-0.16f, 0.60f, 0.02f), Quaternion.Euler(0f, 18f, 0f), Vector3.one * 0.45f);
            ExtractAssetGroup(packInstance.transform, premiumRoot, "CeladonTeaCup_Blender", "CC_CeladonTeaCup_",
                sideTable + new Vector3(0.20f, 0.60f, -0.02f), Quaternion.Euler(0f, -16f, 0f), Vector3.one * 0.45f);

            ExtractAssetGroup(packInstance.transform, premiumRoot, "HanjiFloorLamp_Blender", "CC_FloorLamp_",
                new Vector3(-1.34f, 0f, 2.65f), Quaternion.Euler(0f, 8f, 0f), Vector3.one * 0.92f);
            ExtractAssetGroup(packInstance.transform, premiumRoot, "CounselingBookStack_Blender", "CC_BookStack_",
                new Vector3(-1.84f, 0.64f, 3.00f), Quaternion.Euler(0f, -8f, 0f), Vector3.one * 0.64f);

            GameObject leftPlant = ExtractAssetGroup(packInstance.transform, premiumRoot, "BasketPlant_Left_Blender", "CC_BasketPlant_",
                new Vector3(-2.35f, 0f, 2.42f), Quaternion.Euler(0f, -18f, 0f), Vector3.one * 0.78f);
            GameObject rightPlant = UnityEngine.Object.Instantiate(leftPlant, premiumRoot);
            rightPlant.name = "BasketPlant_Right_Blender";
            rightPlant.transform.position = new Vector3(2.30f, 0f, 2.62f);
            rightPlant.transform.rotation = Quaternion.Euler(0f, 34f, 0f);
            rightPlant.transform.localScale = Vector3.one * 0.68f;

            GameObject artworkGroup = ExtractAssetGroup(packInstance.transform, premiumRoot, "HanjiArtwork_Blender", "CC_HanjiArtwork_",
                new Vector3(0.34f, 1.44f, 3.38f), Quaternion.identity, Vector3.one * 1.02f);
            AddArtworkCanvas(artworkGroup);
            ExtractAssetGroup(packInstance.transform, premiumRoot, "AcousticRibPanel_Blender", "CC_AcousticRibPanel_",
                new Vector3(1.72f, 1.38f, 3.37f), Quaternion.identity, new Vector3(1.25f, 1.18f, 1f));

            UnityEngine.Object.DestroyImmediate(packInstance);
            int rendererCount = premiumRoot.GetComponentsInChildren<Renderer>(true).Length;
            if (rendererCount < 50)
            {
                throw new System.InvalidOperationException($"Premium room integration expected at least 50 renderers, found {rendererCount}.");
            }

            Debug.Log($"COUNSELCUE_PREMIUM_ROOM_ASSETS_PLACED renderers={rendererCount}");
        }

        /// <summary>
        /// The imported frame renders as a dark walnut slab directly behind the client's head,
        /// which lowers face/hair contrast. A textured canvas inside the frame shows the hanji
        /// artwork (or a Higgsfield wall_artwork override) and keeps the background light.
        /// </summary>
        private static void AddArtworkCanvas(GameObject artworkGroup)
        {
            Renderer frame = null;
            foreach (Renderer renderer in artworkGroup.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains("Frame")) { frame = renderer; break; }
            }
            if (frame == null || artwork == null || artwork.mainTexture == null)
            {
                Debug.LogWarning("CounselCue artwork canvas skipped: frame renderer or artwork texture not found.");
                return;
            }

            Bounds bounds = frame.bounds;
            float side = Mathf.Min(bounds.size.x * 0.80f, bounds.size.y * 0.80f);
            GameObject canvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canvas.name = "CC_HanjiArtwork_Canvas";
            canvas.transform.SetParent(artworkGroup.transform, true);
            // Unity quads face -Z, i.e. toward the counselor camera; sit just in front of the frame.
            canvas.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - 0.006f);
            canvas.transform.rotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one;
            Vector3 lossy = canvas.transform.lossyScale;
            canvas.transform.localScale = new Vector3(side / Mathf.Max(0.0001f, lossy.x), side / Mathf.Max(0.0001f, lossy.y), 1f);
            canvas.GetComponent<MeshRenderer>().sharedMaterial = artwork;
            Object.DestroyImmediate(canvas.GetComponent<Collider>());
        }

        private static GameObject ExtractAssetGroup(
            Transform packRoot,
            Transform parent,
            string groupName,
            string namePrefix,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            List<Transform> matches = new List<Transform>();
            for (int index = 0; index < packRoot.childCount; index++)
            {
                Transform child = packRoot.GetChild(index);
                if (child.name.StartsWith(namePrefix, System.StringComparison.Ordinal)) matches.Add(child);
            }

            if (matches.Count == 0)
            {
                throw new System.InvalidOperationException($"No imported room assets matched prefix {namePrefix}.");
            }

            Renderer firstRenderer = matches[0].GetComponentInChildren<Renderer>(true);
            if (firstRenderer == null)
            {
                throw new System.InvalidOperationException($"Imported room asset {matches[0].name} has no renderer.");
            }

            Bounds bounds = firstRenderer.bounds;
            for (int index = 1; index < matches.Count; index++)
            {
                Renderer renderer = matches[index].GetComponentInChildren<Renderer>(true);
                if (renderer != null) bounds.Encapsulate(renderer.bounds);
            }

            Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent);
            group.transform.position = pivot;
            for (int index = 0; index < matches.Count; index++)
            {
                matches[index].SetParent(group.transform, true);
            }

            group.transform.position = position;
            group.transform.rotation = rotation;
            group.transform.localScale = scale;
            return group;
        }

        private static void BuildDecor(Transform parent)
        {
            GameObject lamp = new GameObject("FloorLampWarmLight");
            lamp.transform.SetParent(parent);
            lamp.transform.position = new Vector3(-1.34f, 1.58f, 2.65f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.44f);
            light.intensity = 1.55f;
            light.range = 3.4f;
            light.shadows = LightShadows.Soft;
        }
        private static Camera BuildCameraAndLights()
        {
            GameObject cameraObject = new GameObject("CounselorViewCamera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0.05f, 1.52f, -1.22f);
            camera.transform.LookAt(new Vector3(0f, 1.37f, 1.04f));
            camera.fieldOfView = 38.25f;
            camera.nearClipPlane = 0.05f;
            camera.allowHDR = true;

            GameObject daylight = new GameObject("WindowDaylight");
            Light sun = daylight.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.78f);
            sun.intensity = 0.82f;
            sun.shadows = LightShadows.Soft;
            daylight.transform.rotation = Quaternion.Euler(34f, 128f, 0f);

            GameObject ceiling = new GameObject("SoftCeilingFill");
            Light fill = ceiling.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.86f, 0.68f);
            fill.intensity = 1.08f;
            fill.range = 6.2f;
            fill.shadows = LightShadows.Soft;
            ceiling.transform.position = new Vector3(0f, 2.72f, 0.35f);

            GameObject softbox = new GameObject("ClientFaceSoftbox");
            Light face = softbox.AddComponent<Light>();
            face.type = LightType.Spot;
            face.color = new Color(1f, 0.90f, 0.84f);
            face.intensity = 1.08f;
            face.range = 5f;
            face.spotAngle = 78f;
            face.shadows = LightShadows.Soft;
            softbox.transform.position = new Vector3(-1.8f, 2.35f, -1.5f);
            softbox.transform.LookAt(new Vector3(0f, 1.25f, 1.05f));
            return camera;
        }

        private static ClientAvatarHost BuildClient(Transform lookTarget, RuntimeAnimatorController controller, CounselingCaseDefinition definition)
        {
            GameObject root = new GameObject("ClientAvatarHost");
            ClientAvatarHost host = root.AddComponent<ClientAvatarHost>();
            host.Configure(lookTarget, controller, definition.AvatarPresentation, definition.ProfileDefinition);
            host.ApplyCase(definition);
            return host;
        }

        private static UiReferences BuildUi()
        {
            UiKit.Load(AssetDatabase.LoadAssetAtPath<Font>(FontPath), AssetDatabase.LoadAssetAtPath<Font>(BoldFontPath));
            GameObject canvasObject = new GameObject("CounselingHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Transform hud = canvas.transform;
            UiReferences refs = new UiReferences();

            // Cinematic scrims keep the HUD legible without boxing in the room.
            UiKit.Scrim("TopScrim", hud, false, 230f, new Color(0.02f, 0.024f, 0.022f, 0.55f));
            UiKit.Scrim("BottomScrim", hud, true, 400f, new Color(0.02f, 0.024f, 0.022f, 0.82f));

            BuildSessionPlates(hud, refs);
            BuildObservationPanels(hud, refs);
            BuildSubtitleAndDock(hud, refs);
            BuildBriefing(hud, refs);
            BuildPause(hud, refs);
            BuildDebrief(hud, refs);

            refs.languageToggleButton = UiKit.MakeButton("LanguageToggle", hud, new Vector2(0f, -28f), new Vector2(132f, 34f),
                "한국어   <color=#FFFFFF66>EN</color>", UiKit.Variant.GhostDark, 13, 17f, UiKit.TopCenter);

            refs.activeControlCard.SetActive(false);
            refs.pauseOverlay.SetActive(false);
            refs.debriefOverlay.SetActive(false);
            return refs;
        }

        private static void BuildSessionPlates(Transform hud, UiReferences refs)
        {
            RectTransform session = UiKit.Card("SessionCard", hud, UiKit.TopLeft, new Vector2(28f, -28f), new Vector2(468f, 84f), UiTheme.Glass, 18f, 0.35f);
            UiKit.SealMark(session, new Vector2(18f, -20f), 44f);
            UiKit.Eyebrow("SessionEyebrow", session, "상담 실습  ·  1:1 초기면담", new Vector2(76f, -17f), 370f, UiTheme.Celadon);
            refs.sessionStatus = UiKit.Fit(UiKit.Label("SessionStatus", session, "직장 불안 · 관계 형성 · 1번째 교환",
                new Vector2(76f, -37f), new Vector2(374f, 30f), 19, UiTheme.OnDark, true), 13);

            RectTransform control = UiKit.Card("ActiveControlCard", hud, UiKit.TopLeft, new Vector2(28f, -124f), new Vector2(468f, 80f), UiTheme.Glass, 18f, 0.35f);
            refs.activeControlCard = control.gameObject;
            refs.timerLabel = UiKit.Label("TimerLabel", control, "15:00", new Vector2(20f, -14f), new Vector2(110f, 50f), 34, UiTheme.Amber, true);
            refs.stageLabel = UiKit.Fit(UiKit.Label("StageLabel", control, "연습 모드 · 관계 형성 · 0/10턴",
                new Vector2(134f, -17f), new Vector2(190f, 22f), 13, UiTheme.OnDark, false), 11);
            refs.turnProgressFill = UiKit.Meter("TurnProgress", control, new Vector2(134f, -48f), new Vector2(190f, 5f), UiTheme.Celadon, 0f);
            refs.pauseButton = UiKit.MakeButton("PauseSession", control, new Vector2(338f, -22f), new Vector2(60f, 36f), "일시정지", UiKit.Variant.GhostDark, 12, 10f);
            refs.endButton = UiKit.MakeButton("EndSession", control, new Vector2(404f, -22f), new Vector2(46f, 36f), "종료", UiKit.Variant.OutlineDark, 12, 10f);
        }

        private static void BuildObservationPanels(Transform hud, UiReferences refs)
        {
            RectTransform camera = UiKit.Card("CameraCard", hud, UiKit.TopRight, new Vector2(-28f, -28f), new Vector2(356f, 236f), UiTheme.Glass, 18f, 0.35f);
            refs.cameraCard = camera;
            refs.webcamPreview = UiKit.MaskedRawImage("WebcamPreview", camera, new Vector2(18f, -18f), new Vector2(124f, 92f), 12f, new Color(0.14f, 0.16f, 0.15f, 1f));
            refs.webcamStatus = UiKit.Fit(UiKit.Label("WebcamStatus", camera, "웹캠 준비 중", new Vector2(156f, -20f), new Vector2(184f, 40f), 13, UiTheme.Celadon, true, TextAnchor.UpperLeft, 1.05f), 10);
            UiKit.Fit(UiKit.Label("Privacy", camera, "영상 미저장 · 기기 내 처리", new Vector2(156f, -66f), new Vector2(184f, 36f), 13, UiTheme.OnDarkMuted), 11);
            UiKit.Hairline("CameraDivider", camera, new Vector2(18f, -124f), 320f, new Color(1f, 1f, 1f, 0.10f));
            refs.auStatus = UiKit.Fit(UiKit.Label("AuStatus", camera, "AU 분석 대기 · 선택 기능", new Vector2(18f, -133f), new Vector2(320f, 20f), 13, UiTheme.OnDarkMuted), 11);
            UiKit.Eyebrow("MeterHeading", camera, "관계 상태", new Vector2(18f, -162f), 200f, UiTheme.Celadon);
            string[] meterLabels = { "안전", "경계", "공개" };
            Color[] meterColors = { UiTheme.Celadon, UiTheme.Clay, UiTheme.Amber };
            float[] initial = { 0.38f, 0.62f, 0.25f };
            refs.meterFills = new RectTransform[3];
            refs.meterValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 18f + i * 110f;
                UiKit.Fit(UiKit.Label($"MeterLabel{i}", camera, meterLabels[i], new Vector2(x, -183f), new Vector2(64f, 20f), 13, UiTheme.OnDark), 11);
                refs.meterValues[i] = UiKit.Label($"MeterValue{i}", camera, Mathf.RoundToInt(initial[i] * 100f).ToString(),
                    new Vector2(x + 60f, -183f), new Vector2(40f, 20f), 13, meterColors[i], true, TextAnchor.UpperRight);
                refs.meterFills[i] = UiKit.Meter($"Meter{i}", camera, new Vector2(x, -208f), new Vector2(100f, 5f), meterColors[i], initial[i]);
            }
            // Numeric summary kept for the session controller and language toggle; the meters show it.
            refs.allianceLabel = UiKit.Label("Alliance", camera, "안전 38 · 경계 62 · 공개 25", new Vector2(18f, -220f), new Vector2(320f, 14f), 10, UiTheme.OnDarkMuted);
            refs.allianceLabel.gameObject.SetActive(false);

            RectTransform zoom = UiKit.Card("ZoomCard", hud, UiKit.TopRight, new Vector2(-28f, -280f), new Vector2(356f, 106f), UiTheme.Glass, 18f, 0.35f);
            UiKit.Eyebrow("ZoomEyebrow", zoom, "관찰 줌", new Vector2(18f, -16f), 110f, UiTheme.Celadon);
            refs.zoomLabel = UiKit.Label("ZoomValue", zoom, "100%", new Vector2(18f, -32f), new Vector2(110f, 26f), 20, UiTheme.Amber, true);
            refs.zoomOutButton = UiKit.MakeButton("ZoomOut", zoom, new Vector2(150f, -14f), new Vector2(44f, 36f), "−", UiKit.Variant.GhostDark, 20, 10f);
            refs.zoomInButton = UiKit.MakeButton("ZoomIn", zoom, new Vector2(200f, -14f), new Vector2(44f, 36f), "+", UiKit.Variant.GhostDark, 20, 10f);
            refs.zoomResetButton = UiKit.MakeButton("ZoomReset", zoom, new Vector2(250f, -14f), new Vector2(88f, 36f), "초기", UiKit.Variant.GhostDark, 13, 10f);
            refs.faceObservationButton = UiKit.MakeButton("FaceObservation", zoom, new Vector2(18f, -60f), new Vector2(244f, 32f), "얼굴 관찰", UiKit.Variant.Primary, 13, 10f);
            refs.faceDebugButton = UiKit.MakeButton("FaceDebugToggle", zoom, new Vector2(270f, -60f), new Vector2(68f, 32f), "진단", UiKit.Variant.OutlineDark, 12, 10f);

            RectTransform debug = UiKit.Card("FaceDebugPanel", hud, UiKit.TopRight, new Vector2(-28f, -400f), new Vector2(356f, 176f), UiTheme.Glass, 18f, 0.35f);
            refs.faceDebugPanel = debug.gameObject;
            UiKit.Fit(UiKit.Label("FaceDebugTitle", debug, "표정·시선 진단", new Vector2(18f, -14f), new Vector2(320f, 20f), 13, UiTheme.Celadon, true), 10);
            refs.faceDebugLabel = UiKit.Label("FaceDebugReadout", debug, "준비 중", new Vector2(18f, -40f), new Vector2(320f, 84f), 13, UiTheme.OnDark, false, TextAnchor.UpperLeft, 1.1f);
            refs.gazeCycleButton = UiKit.MakeButton("CycleGazeState", debug, new Vector2(18f, -130f), new Vector2(320f, 32f), "시선 상태 순환", UiKit.Variant.GhostDark, 12, 10f);
            refs.faceDebugPanel.SetActive(false);
        }

        private static void BuildSubtitleAndDock(Transform hud, UiReferences refs)
        {
            // The client's line reads like a film subtitle above the counselor's dock.
            RectTransform speech = UiKit.Card("ClientSpeechCard", hud, UiKit.BottomCenter, new Vector2(0f, 160f), new Vector2(940f, 104f), new Color(0.05f, 0.056f, 0.053f, 0.52f), 22f, 0f);
            refs.clientNameLabel = UiKit.Fit(UiKit.Label("ClientName", speech, "내담자  ·  김지혜, 32세", new Vector2(40f, -13f), new Vector2(860f, 20f), 13, UiTheme.Celadon, true, TextAnchor.UpperCenter), 10);
            refs.clientLine = UiKit.Fit(UiKit.Label("ClientLine", speech, "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.", new Vector2(40f, -38f), new Vector2(860f, 60f), 21, UiTheme.OnDark, false, TextAnchor.UpperCenter, 1.08f), 15);
            UiKit.SoftShadow(refs.clientLine);

            RectTransform dock = UiKit.Card("CounselorInputCard", hud, UiKit.BottomCenter, new Vector2(0f, 18f), new Vector2(1064f, 128f), new Color(0.07f, 0.078f, 0.074f, 0.94f), 22f, 0.45f);
            refs.inputCard = dock;
            refs.inputAccent = UiKit.Node("InputAccent", dock, new Vector2(22f, -20f), new Vector2(8f, 8f));
            UiKit.Surface(refs.inputAccent, UiTheme.Celadon, 4f);
            refs.feedbackLabel = UiKit.Fit(UiKit.Label("Feedback", dock, "감정을 반영하고 내담자가 의미를 더 말할 수 있도록 응답해 보세요.",
                new Vector2(40f, -12f), new Vector2(1004f, 24f), 15, UiTheme.OnDark), 11);
            refs.input = UiKit.TextArea("CounselorInput", dock, new Vector2(18f, -48f), new Vector2(846f, 62f), "상담자의 응답을 입력하세요…");
            refs.sendButton = UiKit.MakeButton("SendButton", dock, new Vector2(876f, -48f), new Vector2(170f, 62f), "응답하기", UiKit.Variant.Primary, 17, 16f);
        }

        private static void NoFit(Button button)
        {
            Text label = button.GetComponentInChildren<Text>();
            label.resizeTextForBestFit = false;
            label.lineSpacing = 1.05f;
        }

        private static RectTransform BuildSplitCard(string name, Transform parent, Vector2 size, out RectTransform rail)
        {
            RectTransform card = UiKit.Card(name, parent, UiKit.Center, Vector2.zero, size, UiTheme.Paper, 26f, 0.55f);
            rail = UiKit.Node($"{name}Rail", card, Vector2.zero, new Vector2(372f, size.y));
            UiKit.Surface(rail, UiTheme.GlassDeep, 26f);
            RectTransform seam = UiKit.Node("RailSeam", rail, new Vector2(346f, 0f), new Vector2(26f, size.y));
            UiKit.Surface(seam, UiTheme.GlassDeep, 0f);
            return card;
        }

        private static void BuildBriefing(Transform hud, UiReferences refs)
        {
            RectTransform overlay = UiKit.Overlay("BriefingOverlay", hud, new Color(0.02f, 0.024f, 0.022f, 0.9f));
            refs.briefingOverlay = overlay.gameObject;
            RectTransform card = BuildSplitCard("BriefingCard", overlay, new Vector2(1240f, 720f), out RectTransform rail);

            UiKit.SealMark(rail, new Vector2(36f, -38f), 46f);
            UiKit.Label("Wordmark", rail, "CounselCue", new Vector2(94f, -36f), new Vector2(240f, 32f), 21, UiTheme.Paper, true);
            UiKit.Eyebrow("BriefingEyebrow", rail, "PRACTICE STUDIO", new Vector2(94f, -68f), 240f, UiTheme.Celadon);
            UiKit.Fit(UiKit.Label("BriefingTitle", rail, "오늘 만날 내담자를\n선택하세요", new Vector2(36f, -120f), new Vector2(300f, 76f), 26, UiTheme.Paper, true, TextAnchor.UpperLeft, 1.05f), 18);
            UiKit.Eyebrow("CaseListLabel", rail, "내담자", new Vector2(36f, -210f), 300f, UiTheme.OnDarkMuted);
            refs.caseButtons = new Button[5];
            for (int i = 0; i < refs.caseButtons.Length; i++)
            {
                Button button = UiKit.MakeButton($"SelectCase{i + 1}", rail, new Vector2(22f, -232f - i * 72f), new Vector2(328f, 64f), $"사례 {i + 1}", UiKit.Variant.GhostDark, 15, 14f);
                Text label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.lineSpacing = 1.1f;
                label.rectTransform.offsetMin = new Vector2(20f, 4f);
                label.rectTransform.offsetMax = new Vector2(-16f, -4f);
                refs.caseButtons[i] = button;
            }
            UiKit.Fit(UiKit.Label("PrivacyLine", rail, "웹캠 영상은 저장되지 않으며, 표정 분석은 이 기기 안에서만 이뤄집니다. 연구·훈련용 프로토타입입니다.",
                new Vector2(36f, -616f), new Vector2(300f, 70f), 13, UiTheme.OnDarkMuted, false, TextAnchor.UpperLeft, 1.15f), 9);

            const float x = 420f;
            const float width = 772f;
            UiKit.Eyebrow("CaseBriefEyebrow", card, "사례 브리핑", new Vector2(x, -46f), 400f, UiTheme.CeladonDeep);
            refs.briefingCaseLabel = UiKit.Fit(UiKit.Label("BriefingCase", card, "직장 불안", new Vector2(x, -64f), new Vector2(580f, 54f), 34, UiTheme.Ink, true), 22);
            refs.briefingMetaLabel = UiKit.Fit(UiKit.Label("BriefingMeta", card, "김지혜  ·  32세 · 직업·성인상담", new Vector2(x, -118f), new Vector2(580f, 22f), 15, UiTheme.InkMuted), 12);
            refs.briefingPortrait = UiKit.MaskedImage("BriefingPortraitFrame", "BriefingPortrait", card, new Vector2(1024f, -46f), new Vector2(168f, 168f), 20f);
            refs.briefingPortraitCaption = UiKit.Label("BriefingPortraitCaption", card, "AI 생성 사례 일러스트", new Vector2(1024f, -218f), new Vector2(168f, 16f), 12, UiTheme.InkMuted, false, TextAnchor.UpperCenter);
            refs.briefingPortrait.gameObject.SetActive(false);
            refs.briefingPortraitCaption.gameObject.SetActive(false);
            refs.briefingBodyLabel = UiKit.Fit(UiKit.Label("BriefingBody", card, "상황\n\n이번 세션의 목표",
                new Vector2(x, -160f), new Vector2(width, 196f), 15, UiTheme.Ink, false, TextAnchor.UpperLeft, 1.18f), 11);

            UiKit.Hairline("BriefingDivider", card, new Vector2(x, -370f), width, new Color(0.118f, 0.129f, 0.122f, 0.12f));
            UiKit.Eyebrow("FullSessionLabel", card, "전체 회기  ·  15분 · 10턴", new Vector2(x, -390f), width, UiTheme.CeladonDeep);
            refs.practiceStartButton = UiKit.MakeButton("StartPractice", card, new Vector2(x, -412f), new Vector2(380f, 64f),
                "코칭 연습\n<size=12><color=#F6F1E7B3>턴마다 전달 피드백을 받습니다</color></size>", UiKit.Variant.Primary, 17, 14f);
            refs.evaluationStartButton = UiKit.MakeButton("StartEvaluation", card, new Vector2(x + 392f, -412f), new Vector2(380f, 64f),
                "평가 모드\n<size=12><color=#6B6F69>피드백은 세션이 끝난 뒤 공개됩니다</color></size>", UiKit.Variant.OutlineLight, 17, 14f);
            NoFit(refs.practiceStartButton);
            NoFit(refs.evaluationStartButton);
            UiKit.Eyebrow("FocusedLabel", card, "미세기술 집중연습  ·  3분 · 3턴", new Vector2(x, -500f), width, UiTheme.CeladonDeep);
            float focusWidth = (width - 24f) / 3f;
            refs.focusOneButton = UiKit.MakeButton("StartFocusOne", card, new Vector2(x, -524f), new Vector2(focusWidth, 48f), "감정 반영 연습 · 3분", UiKit.Variant.Tonal, 14, 12f);
            refs.focusTwoButton = UiKit.MakeButton("StartFocusTwo", card, new Vector2(x + focusWidth + 12f, -524f), new Vector2(focusWidth, 48f), "개방형 질문 연습 · 3분", UiKit.Variant.Tonal, 14, 12f);
            refs.focusThreeButton = UiKit.MakeButton("StartFocusThree", card, new Vector2(x + (focusWidth + 12f) * 2f, -524f), new Vector2(focusWidth, 48f), "전달 정합 연습 · 3분", UiKit.Variant.Tonal, 14, 12f);

            UiKit.Hairline("ConsentDivider", card, new Vector2(x, -600f), width, new Color(0.118f, 0.129f, 0.122f, 0.12f));
            refs.consentToggle = UiKit.Checkbox("ConsentToggle", card, new Vector2(x, -612f), new Vector2(600f, 28f),
                "연구용 로컬 기록에 동의합니다 — 응답 텍스트와 파생 신호만 이 기기에 저장 (영상 제외)", UiTheme.Ink);
            refs.deleteDataButton = UiKit.MakeButton("DeleteLocalData", card, new Vector2(x + width - 150f, -610f), new Vector2(150f, 32f), "로컬 기록 삭제", UiKit.Variant.GhostLight, 12, 10f);
            // Class use: an optional learner code and an export file for the instructor dashboard.
            refs.learnerCodeInput = UiKit.TextArea("LearnerCode", card, new Vector2(x, -652f), new Vector2(190f, 34f), "학습자 코드 (선택)");
            refs.learnerCodeInput.lineType = InputField.LineType.SingleLine;
            refs.learnerCodeInput.characterLimit = 40;
            refs.learnerCodeInput.textComponent.fontSize = 13;
            refs.learnerCodeInput.textComponent.rectTransform.anchoredPosition = new Vector2(12f, -7f);
            refs.learnerCodeInput.textComponent.rectTransform.sizeDelta = new Vector2(166f, 22f);
            Text codeHint = (Text)refs.learnerCodeInput.placeholder;
            codeHint.gameObject.name = "LearnerCodePlaceholder";
            codeHint.fontSize = 13;
            codeHint.rectTransform.anchoredPosition = new Vector2(12f, -7f);
            codeHint.rectTransform.sizeDelta = new Vector2(166f, 22f);
            refs.learnerCodeInput.GetComponent<Image>().color = UiTheme.PaperDeep;
            refs.exportDataButton = UiKit.MakeButton("ExportLocalData", card, new Vector2(x + 200f, -652f), new Vector2(150f, 34f), "기록 내보내기", UiKit.Variant.Tonal, 12, 10f);
            refs.dataStatusLabel = UiKit.Fit(UiKit.Label("DataStatus", card, "연구용 로컬 기록 꺼짐", new Vector2(x + 364f, -660f), new Vector2(width - 364f, 20f), 12, UiTheme.InkMuted), 10);
        }

        private static void BuildPause(Transform hud, UiReferences refs)
        {
            RectTransform overlay = UiKit.Overlay("PauseOverlay", hud, new Color(0.02f, 0.024f, 0.022f, 0.72f));
            refs.pauseOverlay = overlay.gameObject;
            RectTransform card = UiKit.Card("PauseCard", overlay, UiKit.Center, Vector2.zero, new Vector2(520f, 262f), UiTheme.Paper, 24f, 0.55f);
            UiKit.Eyebrow("PauseEyebrow", card, "PAUSED", new Vector2(40f, -38f), 300f, UiTheme.CeladonDeep);
            UiKit.Fit(UiKit.Label("PauseTitle", card, "세션 일시정지", new Vector2(40f, -58f), new Vector2(440f, 40f), 26, UiTheme.Ink, true), 18);
            UiKit.Label("PauseBody", card, "타이머와 상담 입력이 멈췄습니다.\n준비되면 같은 장면에서 계속 진행하세요.", new Vector2(40f, -104f), new Vector2(440f, 52f), 15, UiTheme.InkMuted, false, TextAnchor.UpperLeft, 1.15f);
            refs.resumeButton = UiKit.MakeButton("ResumeSession", card, new Vector2(40f, -176f), new Vector2(214f, 52f), "계속하기", UiKit.Variant.Primary, 16, 14f);
            refs.pauseEndButton = UiKit.MakeButton("PauseEndSession", card, new Vector2(266f, -176f), new Vector2(214f, 52f), "종료하기", UiKit.Variant.OutlineLight, 16, 14f);
        }

        private static void BuildDebrief(Transform hud, UiReferences refs)
        {
            RectTransform overlay = UiKit.Overlay("DebriefOverlay", hud, new Color(0.02f, 0.024f, 0.022f, 0.94f));
            refs.debriefOverlay = overlay.gameObject;
            RectTransform card = BuildSplitCard("DebriefCard", overlay, new Vector2(1240f, 660f), out RectTransform rail);

            UiKit.SealMark(rail, new Vector2(36f, -38f), 46f);
            UiKit.Label("DebriefWordmark", rail, "CounselCue", new Vector2(94f, -36f), new Vector2(240f, 32f), 21, UiTheme.Paper, true);
            UiKit.Eyebrow("DebriefEyebrow", rail, "REFLECT · COMPARE · RETRY", new Vector2(94f, -68f), 240f, UiTheme.Celadon);
            refs.debriefTitle = UiKit.Fit(UiKit.Label("DebriefTitle", rail, "세션 성찰 및 재연습", new Vector2(36f, -120f), new Vector2(300f, 72f), 26, UiTheme.Paper, true, TextAnchor.UpperLeft, 1.05f), 18);
            refs.debriefReport = UiKit.Fit(UiKit.Label("DebriefSummary", rail, string.Empty, new Vector2(36f, -206f), new Vector2(300f, 280f), 14, UiTheme.OnDark, false, TextAnchor.UpperLeft, 1.3f), 11);
            UiKit.Fit(UiKit.Label("DebriefDisclaimer", rail, "※ 먼저 자기평가한 뒤 시스템 근거와 비교합니다. 훈련용 피드백이며 임상평가가 아닙니다.\n연습 뒤 불편감이 남았다면 잠시 쉬고, 지도감독자나 교육 담당자와 이야기해 보세요.",
                new Vector2(36f, -506f), new Vector2(300f, 128f), 12, UiTheme.OnDarkMuted, false, TextAnchor.UpperLeft, 1.2f), 10);

            const float x = 420f;
            const float width = 772f;
            UiKit.Eyebrow("TimelineHeading", card, "장면 타임라인 · 장면을 선택하세요", new Vector2(x, -46f), width, UiTheme.CeladonDeep);
            refs.timelineButtons = new Button[10];
            float chip = (width - 4f * 12f) / 5f;
            for (int i = 0; i < refs.timelineButtons.Length; i++)
            {
                Vector2 position = new Vector2(x + (i % 5) * (chip + 12f), -72f - (i / 5) * 50f);
                refs.timelineButtons[i] = UiKit.MakeButton($"TimelineTurn{i + 1}", card, position, new Vector2(chip, 40f), $"{i + 1}턴", UiKit.Variant.GhostLight, 13, 12f);
            }
            RectTransform scene = UiKit.Node("ScenePanel", card, new Vector2(x, -184f), new Vector2(width, 272f));
            UiKit.Surface(scene, UiTheme.PaperDeep, 18f);
            refs.sceneDetailLabel = UiKit.Fit(UiKit.Label("SceneDetail", scene, "장면을 선택하면 상담자 응답과 시스템 근거가 표시됩니다.",
                new Vector2(26f, -22f), new Vector2(width - 52f, 232f), 15, UiTheme.Ink, false, TextAnchor.UpperLeft, 1.22f), 11);
            refs.assessmentStatusLabel = UiKit.Fit(UiKit.Label("AssessmentStatus", card, "먼저 자신의 판단을 선택하세요.", new Vector2(x, -474f), new Vector2(width, 20f), 13, UiTheme.CeladonDeep, true), 10);
            refs.effectiveButton = UiKit.MakeButton("AssessEffective", card, new Vector2(x, -504f), new Vector2(176f, 52f), "잘된 장면", UiKit.Variant.GhostLight, 15, 14f);
            refs.retryNeededButton = UiKit.MakeButton("AssessRetry", card, new Vector2(x + 188f, -504f), new Vector2(176f, 52f), "다시 연습 필요", UiKit.Variant.GhostLight, 15, 14f);
            refs.replayButton = UiKit.MakeButton("ReplaySelected", card, new Vector2(x + width - 172f - 12f - 212f, -504f), new Vector2(212f, 52f), "이 장면 다시 연습", UiKit.Variant.Primary, 15, 14f);
            refs.returnButton = UiKit.MakeButton("ReturnToBriefing", card, new Vector2(x + width - 172f, -504f), new Vector2(172f, 52f), "연습 경로로", UiKit.Variant.GhostLight, 15, 14f);
        }

        private static CounselingCaseDefinition CreateDefaultCaseDefinition()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Cases");
            CounselingCaseDefinition definition = AssetDatabase.LoadAssetAtPath<CounselingCaseDefinition>(CasePath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CounselingCaseDefinition>();
                AssetDatabase.CreateAsset(definition, CasePath);
            }

            string[] supportive =
            {
                "제가 요즘 계속 긴장한 채로 지냈던 것 같아요. 누군가에게 말하니 조금 정리가 되는 느낌이에요.",
                "그 말을 들으니 제가 너무 예민한 사람은 아닌 것 같아서 조금 안심돼요.",
                "회사에 들어가는 순간부터 가슴이 답답해져요. 특히 팀장님과 이야기할 때 더 심해지고요.",
                "지난주 회의에서 팀장님이 사람들 앞에서 제 실수를 지적했어요. 그 뒤로 시선이 신경 쓰여요.",
                "또 틀리면 어쩌나 싶어서 작은 일도 계속 확인해요. 결국 제가 부족한 탓 같고요.",
                "요즘은 출근 전부터 퇴사해야 하나 생각해요. 그렇지만 그만두는 것도 겁이 나요.",
                "가족에게는 걱정시킬까 봐 말하지 못했어요. 혼자 버티는 게 점점 힘들어요.",
                "제가 원하는 건 당장 답을 정하는 것보다 안전하게 일할 수 있다는 느낌인 것 같아요.",
                "지금 정리해 주신 내용을 들으니 제가 무엇 때문에 힘든지 조금 더 선명해졌어요.",
                "다음에는 불안이 올라오는 순간을 더 살펴보고, 제가 할 수 있는 작은 선택도 찾아보고 싶어요."
            };
            string[] guarded =
            {
                "글쎄요… 그냥 제가 알아서 해야 하는 문제 같기도 해요.",
                "그렇게 간단히 해결될 문제였으면 이미 했을 것 같아요.",
                "무슨 말을 해야 할지 잘 모르겠어요.",
                "그 얘기는 아직 자세히 하고 싶지 않아요.",
                "결국 제가 잘못한 것 같아서 말해도 달라질 게 있나 싶어요.",
                "퇴사 얘기까지는 하고 싶지 않아요. 너무 앞서가는 것 같아요.",
                "가족에게는 말하고 싶지 않아요. 걱정만 더할 테니까요.",
                "제가 무엇을 원하는지는 잘 모르겠어요. 그냥 덜 힘들었으면 좋겠어요.",
                "정리가 됐는지는 모르겠어요. 아직 조금 부담스러워요.",
                "오늘은 여기까지만 이야기하고 싶어요."
            };
            CounselingDisclosureStep[] ladder = new CounselingDisclosureStep[supportive.Length];
            for (int i = 0; i < ladder.Length; i++)
            {
                ladder[i] = new CounselingDisclosureStep { supportiveReply = supportive[i], guardedReply = guarded[i] };
            }

            definition.Configure(
                "workplace-anxiety-01",
                "직장 불안",
                "김지혜",
                "32세 · 초기면담",
                "최근 회사에 가려고 하면 숨이 막히고, 자신이 약한 사람인지 걱정합니다.",
                "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.\n제가 너무 약한 사람인가 싶기도 하고요.",
                900f,
                180f,
                3,
                new[]
                {
                    "관계 안전감을 형성하고 상담 구조를 안내합니다.",
                    "반영과 개방형 질문으로 경험을 탐색합니다.",
                    "해결책을 서두르지 않고 내담자의 응답 공간을 지킵니다."
                },
                ladder,
                new[]
                {
                    new CounselingFocusSkill { id = "emotion-reflection", label = "감정 반영", objective = "감정과 의미를 구체적으로 반영한다.", coachingPrompt = "감정 단어와 그 의미를 한 문장에 담아 보세요." },
                    new CounselingFocusSkill { id = "open-question", label = "개방형 질문", objective = "내담자가 경험을 확장하도록 질문한다.", coachingPrompt = "예·아니오로 끝나지 않는 질문 뒤 응답 공간을 남기세요." },
                    new CounselingFocusSkill { id = "delivery-alignment", label = "전달 정합", objective = "언어와 표정·시선의 전달을 맞춘다.", coachingPrompt = "문장 내용과 얼굴의 긴장·미소가 같은 메시지인지 확인하세요." }
                });
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private static void WireWebcam(WebcamSignalMonitor webcam, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(webcam);
            serialized.FindProperty("preview").objectReferenceValue = ui.webcamPreview;
            serialized.FindProperty("statusLabel").objectReferenceValue = ui.webcamStatus;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireActionUnits(FacialActionUnitMonitor actionUnits, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(actionUnits);
            serialized.FindProperty("statusLabel").objectReferenceValue = ui.auStatus;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireSession(CounselingSessionController session, CounselingSessionOrchestrator orchestrator, CounselingCaseDefinition caseDefinition, ClientAvatarHost client, WebcamSignalMonitor webcam, FacialActionUnitMonitor actionUnits, GptRealtimeConversationEngine realtime, WebNpcConversationEngine webNpc, CounselCueWebBridge webBridge, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(session);
            serialized.FindProperty("client").objectReferenceValue = client;
            serialized.FindProperty("webcam").objectReferenceValue = webcam;
            serialized.FindProperty("actionUnits").objectReferenceValue = actionUnits;
            serialized.FindProperty("realtimeEngine").objectReferenceValue = realtime;
            serialized.FindProperty("webNpcEngine").objectReferenceValue = webNpc;
            serialized.FindProperty("webBridge").objectReferenceValue = webBridge;
            serialized.FindProperty("sessionOrchestrator").objectReferenceValue = orchestrator;
            serialized.FindProperty("caseDefinition").objectReferenceValue = caseDefinition;
            serialized.FindProperty("counselorInput").objectReferenceValue = ui.input;
            serialized.FindProperty("sendButton").objectReferenceValue = ui.sendButton;
            serialized.FindProperty("clientLine").objectReferenceValue = ui.clientLine;
            serialized.FindProperty("sessionStatus").objectReferenceValue = ui.sessionStatus;
            serialized.FindProperty("feedbackLabel").objectReferenceValue = ui.feedbackLabel;
            serialized.FindProperty("allianceLabel").objectReferenceValue = ui.allianceLabel;
            serialized.FindProperty("relationalMeters").objectReferenceValue = CreateRelationalMeters(ui);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(session);
        }

        private static RelationalMeterHud CreateRelationalMeters(UiReferences ui)
        {
            RelationalMeterHud meters = ui.cameraCard.gameObject.AddComponent<RelationalMeterHud>();
            SerializedObject serialized = new SerializedObject(meters);
            SerializedProperty fills = serialized.FindProperty("fills");
            SerializedProperty values = serialized.FindProperty("values");
            fills.arraySize = ui.meterFills.Length;
            values.arraySize = ui.meterValues.Length;
            for (int i = 0; i < ui.meterFills.Length; i++) fills.GetArrayElementAtIndex(i).objectReferenceValue = ui.meterFills[i];
            for (int i = 0; i < ui.meterValues.Length; i++) values.GetArrayElementAtIndex(i).objectReferenceValue = ui.meterValues[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return meters;
        }

        private static void WireWebExperience(CounselCueWebBridge bridge, CounselingSessionController session, CounselingSessionOrchestrator orchestrator, WebNpcConversationEngine webNpc, ClientAvatarHost client, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(bridge);
            serialized.FindProperty("session").objectReferenceValue = session;
            serialized.FindProperty("orchestrator").objectReferenceValue = orchestrator;
            serialized.FindProperty("npcEngine").objectReferenceValue = webNpc;
            serialized.FindProperty("client").objectReferenceValue = client;
            serialized.FindProperty("unityInputCard").objectReferenceValue = ui.inputCard;
            serialized.FindProperty("unityInputAccent").objectReferenceValue = ui.inputAccent;
            serialized.FindProperty("unityInputField").objectReferenceValue = ui.input.gameObject;
            serialized.FindProperty("unitySendButton").objectReferenceValue = ui.sendButton.gameObject;
            serialized.FindProperty("unityFeedbackLabel").objectReferenceValue = ui.feedbackLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);
        }

        private static void WireSessionOrchestrator(CounselingSessionOrchestrator orchestrator, CounselingSessionController session, CounselingReflectionController reflection, CounselingCaseDefinition caseDefinition, CaseCatalog caseCatalog, ClientAvatarHost client, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(orchestrator);
            serialized.FindProperty("sessionController").objectReferenceValue = session;
            serialized.FindProperty("reflectionController").objectReferenceValue = reflection;
            serialized.FindProperty("caseDefinition").objectReferenceValue = caseDefinition;
            serialized.FindProperty("caseCatalog").objectReferenceValue = caseCatalog;
            serialized.FindProperty("clientAvatar").objectReferenceValue = client;
            SerializedProperty caseButtons = serialized.FindProperty("caseButtons");
            caseButtons.arraySize = ui.caseButtons.Length;
            for (int i = 0; i < ui.caseButtons.Length; i++) caseButtons.GetArrayElementAtIndex(i).objectReferenceValue = ui.caseButtons[i];
            serialized.FindProperty("activeControlCard").objectReferenceValue = ui.activeControlCard;
            serialized.FindProperty("briefingOverlay").objectReferenceValue = ui.briefingOverlay;
            serialized.FindProperty("pauseOverlay").objectReferenceValue = ui.pauseOverlay;
            serialized.FindProperty("debriefOverlay").objectReferenceValue = ui.debriefOverlay;
            serialized.FindProperty("timerLabel").objectReferenceValue = ui.timerLabel;
            serialized.FindProperty("stageLabel").objectReferenceValue = ui.stageLabel;
            serialized.FindProperty("briefingCaseLabel").objectReferenceValue = ui.briefingCaseLabel;
            serialized.FindProperty("briefingMetaLabel").objectReferenceValue = ui.briefingMetaLabel;
            serialized.FindProperty("turnProgressFill").objectReferenceValue = ui.turnProgressFill;
            serialized.FindProperty("briefingBodyWidthWithPortrait").floatValue = 572f;
            serialized.FindProperty("briefingBodyLabel").objectReferenceValue = ui.briefingBodyLabel;
            serialized.FindProperty("clientNameLabel").objectReferenceValue = ui.clientNameLabel;
            serialized.FindProperty("briefingPortrait").objectReferenceValue = ui.briefingPortrait;
            serialized.FindProperty("briefingPortraitCaption").objectReferenceValue = ui.briefingPortraitCaption;
            serialized.FindProperty("debriefTitle").objectReferenceValue = ui.debriefTitle;
            serialized.FindProperty("practiceStartButton").objectReferenceValue = ui.practiceStartButton;
            serialized.FindProperty("evaluationStartButton").objectReferenceValue = ui.evaluationStartButton;
            serialized.FindProperty("focusOneButton").objectReferenceValue = ui.focusOneButton;
            serialized.FindProperty("focusTwoButton").objectReferenceValue = ui.focusTwoButton;
            serialized.FindProperty("focusThreeButton").objectReferenceValue = ui.focusThreeButton;
            serialized.FindProperty("pauseButton").objectReferenceValue = ui.pauseButton;
            serialized.FindProperty("endButton").objectReferenceValue = ui.endButton;
            serialized.FindProperty("resumeButton").objectReferenceValue = ui.resumeButton;
            serialized.FindProperty("pauseEndButton").objectReferenceValue = ui.pauseEndButton;
            serialized.FindProperty("returnButton").objectReferenceValue = ui.returnButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(orchestrator);
        }

        private static void WireReflection(CounselingReflectionController reflection, CounselingSessionOrchestrator orchestrator, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(reflection);
            serialized.FindProperty("orchestrator").objectReferenceValue = orchestrator;
            serialized.FindProperty("summaryLabel").objectReferenceValue = ui.debriefReport;
            serialized.FindProperty("sceneDetailLabel").objectReferenceValue = ui.sceneDetailLabel;
            serialized.FindProperty("assessmentStatusLabel").objectReferenceValue = ui.assessmentStatusLabel;
            serialized.FindProperty("effectiveButton").objectReferenceValue = ui.effectiveButton;
            serialized.FindProperty("retryNeededButton").objectReferenceValue = ui.retryNeededButton;
            serialized.FindProperty("replayButton").objectReferenceValue = ui.replayButton;
            SerializedProperty timeline = serialized.FindProperty("timelineButtons");
            timeline.arraySize = ui.timelineButtons.Length;
            for (int i = 0; i < ui.timelineButtons.Length; i++) timeline.GetArrayElementAtIndex(i).objectReferenceValue = ui.timelineButtons[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCameraZoom(CounselingCameraZoom cameraZoom, Camera camera, ClientAvatarHost client, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(cameraZoom);
            serialized.FindProperty("targetCamera").objectReferenceValue = camera;
            serialized.FindProperty("clientAvatar").objectReferenceValue = client;
            serialized.FindProperty("zoomOutButton").objectReferenceValue = ui.zoomOutButton;
            serialized.FindProperty("zoomInButton").objectReferenceValue = ui.zoomInButton;
            serialized.FindProperty("resetButton").objectReferenceValue = ui.zoomResetButton;
            serialized.FindProperty("faceObservationButton").objectReferenceValue = ui.faceObservationButton;
            serialized.FindProperty("zoomLabel").objectReferenceValue = ui.zoomLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireClientDebug(ClientObservationDebugHud debugHud, ClientAvatarHost client, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(debugHud);
            serialized.FindProperty("client").objectReferenceValue = client;
            serialized.FindProperty("panel").objectReferenceValue = ui.faceDebugPanel;
            serialized.FindProperty("diagnosticsLabel").objectReferenceValue = ui.faceDebugLabel;
            serialized.FindProperty("toggleButton").objectReferenceValue = ui.faceDebugButton;
            serialized.FindProperty("cycleGazeButton").objectReferenceValue = ui.gazeCycleButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireResearchDataControls(ResearchDataControls controls, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(controls);
            serialized.FindProperty("consentToggle").objectReferenceValue = ui.consentToggle;
            serialized.FindProperty("deleteButton").objectReferenceValue = ui.deleteDataButton;
            serialized.FindProperty("exportButton").objectReferenceValue = ui.exportDataButton;
            serialized.FindProperty("learnerCodeInput").objectReferenceValue = ui.learnerCodeInput;
            serialized.FindProperty("statusLabel").objectReferenceValue = ui.dataStatusLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controls);
        }

        private static void WireLanguageToggle(CounselingLanguageToggle languageToggle, CounselingSessionOrchestrator orchestrator, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(languageToggle);
            serialized.FindProperty("toggleButton").objectReferenceValue = ui.languageToggleButton;
            serialized.FindProperty("orchestrator").objectReferenceValue = orchestrator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateChair(string name, Vector3 position, float yaw, Material fabric, Transform parent, bool full)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            CreateCube("Seat", new Vector3(0f, 0.48f, 0f), new Vector3(0.84f, 0.20f, 0.74f), fabric, root.transform);
            CreateCube("Back", new Vector3(0f, 0.79f, -0.30f), new Vector3(0.84f, 0.58f, 0.18f), fabric, root.transform);
            CreateCube("LeftArm", new Vector3(-0.49f, 0.72f, 0f), new Vector3(0.14f, 0.22f, 0.70f), fabric, root.transform);
            CreateCube("RightArm", new Vector3(0.49f, 0.72f, 0f), new Vector3(0.14f, 0.22f, 0.70f), fabric, root.transform);
            if (!full) return;
            CreateCube("LeftLeg", new Vector3(-0.30f, 0.22f, -0.22f), new Vector3(0.07f, 0.44f, 0.07f), darkOak, root.transform);
            CreateCube("RightLeg", new Vector3(0.30f, 0.22f, -0.22f), new Vector3(0.07f, 0.44f, 0.07f), darkOak, root.transform);
        }

        private static void CreateCurtain(string name, float centerX, float centerY, float z, float width, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            for (int i = 0; i < 7; i++)
            {
                float normalized = i / 6f - 0.5f;
                float foldZ = z - Mathf.Abs(normalized) * 0.035f + (i % 2 == 0 ? -0.025f : 0.02f);
                CreateCube(
                    $"Fold_{i:00}",
                    new Vector3(centerX + normalized * width, centerY, foldZ),
                    new Vector3(width / 6.4f, 2.82f, 0.075f),
                    sage,
                    root.transform);
            }
            CreateCylinder("CurtainRail", new Vector3(centerX, 3.03f, z + 0.02f), new Vector3(0.025f, width * 0.58f, 0.025f), darkOak, root.transform)
                .transform.rotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void CreateLowConsole(Vector3 position, Transform parent)
        {
            GameObject root = new GameObject("LowWalnutConsole");
            root.transform.SetParent(parent);
            root.transform.position = position;
            CreateCube("Body", new Vector3(0f, 0.44f, 0f), new Vector3(1.72f, 0.70f, 0.42f), oak, root.transform);
            CreateCube("Top", new Vector3(0f, 0.82f, 0f), new Vector3(1.82f, 0.07f, 0.46f), darkOak, root.transform);
            CreateCube("LeftDoor", new Vector3(-0.43f, 0.44f, -0.225f), new Vector3(0.78f, 0.58f, 0.025f), darkOak, root.transform);
            CreateCube("RightDoor", new Vector3(0.43f, 0.44f, -0.225f), new Vector3(0.78f, 0.58f, 0.025f), darkOak, root.transform);
            for (int i = -1; i <= 1; i += 2)
            {
                CreateCube($"Leg_{i}", new Vector3(i * 0.68f, 0.10f, 0f), new Vector3(0.07f, 0.20f, 0.07f), darkOak, root.transform);
            }
            CreateCylinder("CeramicVase", new Vector3(-0.48f, 0.98f, 0f), new Vector3(0.12f, 0.16f, 0.12f), paper, root.transform);
            Material[] colors = { sage, paper, brass, cream };
            for (int i = 0; i < 4; i++)
            {
                CreateCube($"CounselingBook_{i:00}", new Vector3(0.25f + i * 0.13f, 0.94f, 0f), new Vector3(0.09f, 0.24f + i % 2 * 0.04f, 0.18f), colors[i], root.transform);
            }
        }

        private static void CreateBookcase(Vector3 position, Transform parent)
        {
            GameObject root = new GameObject("LowOakBookcase");
            root.transform.SetParent(parent);
            root.transform.position = position;
            CreateCube("Body", new Vector3(0f, 0.62f, 0f), new Vector3(0.86f, 1.22f, 0.34f), oak, root.transform);
            CreateCube("Inset", new Vector3(0f, 0.68f, -0.19f), new Vector3(0.72f, 0.88f, 0.03f), charcoal, root.transform);
            CreateCube("Shelf", new Vector3(0f, 0.66f, -0.22f), new Vector3(0.72f, 0.05f, 0.28f), oak, root.transform);
            Material[] colors = { sage, brass, paper, teal, cream };
            for (int i = 0; i < 9; i++)
            {
                float row = i < 5 ? 0.32f : 0.78f;
                float column = i < 5 ? i : i - 5;
                CreateCube($"Book_{i:00}", new Vector3(-0.27f + column * 0.14f, row, -0.24f), new Vector3(0.10f, 0.30f + i % 3 * 0.04f, 0.20f), colors[i % colors.Length], root.transform);
            }
        }

        private static void CreatePlant(Vector3 position, float scale, Transform parent)
        {
            GameObject root = new GameObject("IndoorPlant");
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;
            CreateCylinder("Pot", new Vector3(0f, 0.20f, 0f), new Vector3(0.22f, 0.22f, 0.22f), paper, root.transform);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 51f * Mathf.Deg2Rad;
                Vector3 stem = new Vector3(Mathf.Cos(angle) * 0.07f, 0.62f + i % 3 * 0.10f, Mathf.Sin(angle) * 0.07f);
                CreateCylinder($"Stem_{i:00}", stem, new Vector3(0.018f, 0.40f, 0.018f), leaf, root.transform);
                CreateSphere($"Leaf_{i:00}", stem + new Vector3(Mathf.Cos(angle) * 0.16f, 0.34f, Mathf.Sin(angle) * 0.16f), new Vector3(0.14f, 0.24f, 0.08f), leaf, root.transform);
            }
        }

        private static void CreateMaterials()
        {
            cream = MaterialAsset("CreamWall", new Color(0.91f, 0.87f, 0.79f), 0.03f);
            warmWhite = MaterialAsset("WarmWhite", new Color(0.94f, 0.91f, 0.85f), 0.06f);
            oak = MaterialAsset("LightOak", new Color(0.66f, 0.48f, 0.30f), 0.24f);
            darkOak = MaterialAsset("DarkOak", new Color(0.26f, 0.15f, 0.09f), 0.27f);
            sage = MaterialAsset("Sage", new Color(0.42f, 0.50f, 0.40f), 0.05f);
            teal = MaterialAsset("TealFabric", new Color(0.22f, 0.34f, 0.32f), 0.04f);
            charcoal = MaterialAsset("Charcoal", new Color(0.07f, 0.08f, 0.075f), 0.18f);
            brass = MaterialAsset("Brass", new Color(0.72f, 0.48f, 0.18f), 0.62f, 0.55f);
            leaf = MaterialAsset("Leaf", new Color(0.12f, 0.30f, 0.16f), 0.16f);
            paper = MaterialAsset("Paper", new Color(0.82f, 0.77f, 0.68f), 0.06f);
            windowGlow = MaterialAsset("WindowGlow", new Color(0.84f, 0.88f, 0.84f), 0.12f);
            windowGlow.EnableKeyword("_EMISSION");
            windowGlow.SetColor("_EmissionColor", new Color(0.30f, 0.34f, 0.30f));

            artwork = MaterialAsset("HanjiArtwork", Color.white, 0.02f);
            Texture2D artworkTexture = HiggsfieldAssetSlots.LoadWallArtwork();
            if (artworkTexture == null) artworkTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkTexturePath);
            artwork.mainTexture = artworkTexture;
            EditorUtility.SetDirty(artwork);

            // Optional daylight view behind the back window. Emission keeps it readable as
            // "outside" without adding a light source that would change the face lighting.
            Texture2D viewTexture = HiggsfieldAssetSlots.LoadWindowView();
            windowView = null;
            if (viewTexture != null)
            {
                windowView = MaterialAsset("WindowView", new Color(0.86f, 0.86f, 0.84f), 0.05f);
                windowView.mainTexture = viewTexture;
                windowView.EnableKeyword("_EMISSION");
                windowView.SetTexture("_EmissionMap", viewTexture);
                windowView.SetColor("_EmissionColor", new Color(0.42f, 0.42f, 0.40f));
                windowView.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(windowView);
            }
        }

        private static Material MaterialAsset(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = $"{MaterialRoot}/M_{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent) => CreatePrimitive(PrimitiveType.Cube, name, position, scale, material, parent);
        private static GameObject CreateCylinder(string name, Vector3 position, Vector3 scale, Material material, Transform parent) => CreatePrimitive(PrimitiveType.Cylinder, name, position, scale, material, parent);
        private static GameObject CreateSphere(string name, Vector3 position, Vector3 scale, Material material, Transform parent) => CreatePrimitive(PrimitiveType.Sphere, name, position, scale, material, parent);

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(gameObject.GetComponent<Collider>());
            return gameObject;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }

        private sealed class UiReferences
        {
            public GameObject activeControlCard;
            public GameObject briefingOverlay;
            public GameObject pauseOverlay;
            public GameObject debriefOverlay;
            public RectTransform inputCard;
            public RectTransform inputAccent;
            public InputField input;
            public Button sendButton;
            public Button practiceStartButton;
            public Button evaluationStartButton;
            public Button focusOneButton;
            public Button focusTwoButton;
            public Button focusThreeButton;
            public Button pauseButton;
            public Button endButton;
            public Button resumeButton;
            public Button pauseEndButton;
            public Button returnButton;
            public Button effectiveButton;
            public Button retryNeededButton;
            public Button replayButton;
            public Button[] timelineButtons;
            public Button zoomOutButton;
            public Button zoomInButton;
            public Button zoomResetButton;
            public Button faceObservationButton;
            public Button faceDebugButton;
            public Button gazeCycleButton;
            public Button[] caseButtons;
            public Button languageToggleButton;
            public Text clientLine;
            public Text sessionStatus;
            public Text feedbackLabel;
            public Text allianceLabel;
            public Text zoomLabel;
            public Text timerLabel;
            public Text stageLabel;
            public Text briefingCaseLabel;
            public Text briefingBodyLabel;
            public Text briefingMetaLabel;
            public RectTransform turnProgressFill;
            public RectTransform cameraCard;
            public RectTransform[] meterFills;
            public Text[] meterValues;
            public Image briefingPortrait;
            public Toggle consentToggle;
            public Button deleteDataButton;
            public Button exportDataButton;
            public InputField learnerCodeInput;
            public Text dataStatusLabel;
            public Text briefingPortraitCaption;
            public Text clientNameLabel;
            public Text faceDebugLabel;
            public Text debriefTitle;
            public Text debriefReport;
            public Text sceneDetailLabel;
            public Text assessmentStatusLabel;
            public Text webcamStatus;
            public Text auStatus;
            public RawImage webcamPreview;
            public GameObject faceDebugPanel;
        }
    }
}
