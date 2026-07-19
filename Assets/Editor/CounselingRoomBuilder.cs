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
        private const string FontPath = "Assets/Fonts/NotoSansKR-VF.ttf";
        private const string CasePath = "Assets/Data/Cases/WorkplaceAnxietyCase.asset";
        private const string ArtworkTexturePath = "Assets/Art/Textures/HanjiMountainArtwork.png";
        private const string RoomAssetPackPath = "Assets/Models/CounselCue/CounselCueRoomAssetPack.fbx";
        private const string UiButtonSpritePath = "Assets/ThirdParty/Kenney/UI/button_rectangle_depth_flat.png";
        private const string UiPanelSpritePath = "Assets/ThirdParty/Kenney/UI/input_rectangle.png";
        private const string UiDividerSpritePath = "Assets/ThirdParty/Kenney/UI/divider_edges.png";

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
        private static Sprite uiButtonSprite;
        private static Sprite uiInputSprite;
        private static Sprite uiDividerSprite;

        private static readonly Color HudGlass = new Color(0.035f, 0.055f, 0.052f, 0.88f);
        private static readonly Color HudGlassStrong = new Color(0.030f, 0.047f, 0.045f, 0.94f);
        private static readonly Color HudMint = new Color(0.66f, 0.88f, 0.76f, 1f);
        private static readonly Color HudText = new Color(0.95f, 0.97f, 0.95f, 1f);
        private static readonly Color HudMuted = new Color(0.73f, 0.79f, 0.75f, 1f);
        private static readonly Color HudGold = new Color(0.97f, 0.78f, 0.48f, 1f);
        private static readonly Color PaperCard = new Color(0.975f, 0.958f, 0.925f, 0.96f);
        private static readonly Color Ink = new Color(0.105f, 0.13f, 0.12f, 1f);
        private static readonly Color TealAction = new Color(0.20f, 0.48f, 0.37f, 1f);

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
            CounselingCaseDefinition caseDefinition = CreateDefaultCaseDefinition();
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
            ClientAvatarController client = BuildClient(camera.transform, controller);
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
            runtime.AddComponent<DemoCaptureController>();
            WireWebcam(webcam, ui);
            WireActionUnits(actionUnits, ui);
            WireSession(session, orchestrator, caseDefinition, client, webcam, actionUnits, realtime, webNpc, webBridge, ui);
            WireWebExperience(webBridge, session, orchestrator, webNpc, client);
            WireSessionOrchestrator(orchestrator, session, reflection, caseDefinition, ui);
            WireReflection(reflection, orchestrator, ui);
            WireCameraZoom(cameraZoom, camera, ui);
            WireLanguageToggle(languageToggle, ui);

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

            CreateCube("BackWindowGlow", new Vector3(-2.14f, 1.76f, 3.46f), new Vector3(1.18f, 2.28f, 0.05f), windowGlow, parent);
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

            ExtractAssetGroup(packInstance.transform, premiumRoot, "HanjiArtwork_Blender", "CC_HanjiArtwork_",
                new Vector3(0.34f, 1.44f, 3.38f), Quaternion.identity, Vector3.one * 1.02f);
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
            face.color = new Color(1f, 0.82f, 0.68f);
            face.intensity = 1.34f;
            face.range = 5f;
            face.spotAngle = 78f;
            face.shadows = LightShadows.Soft;
            softbox.transform.position = new Vector3(-1.8f, 2.35f, -1.5f);
            softbox.transform.LookAt(new Vector3(0f, 1.25f, 1.05f));
            return camera;
        }

        private static ClientAvatarController BuildClient(Transform lookTarget, RuntimeAnimatorController controller)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
            GameObject root;
            if (source == null)
            {
                root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                root.name = "ClientFallback";
                root.GetComponent<MeshRenderer>().sharedMaterial = teal;
                Debug.LogWarning($"Rocketbox avatar was not imported: {AvatarPath}");
            }
            else
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                root.name = "Client_Jihye_Rocketbox";
            }

            root.transform.position = new Vector3(0f, 0.08f, 1.02f);
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Animator animator = root.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            ClientAvatarController client = root.AddComponent<ClientAvatarController>();
            SerializedObject serialized = new SerializedObject(client);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("lookTarget").objectReferenceValue = lookTarget;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return client;
        }

        private static UiReferences BuildUi()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            uiButtonSprite = LoadUiSprite(UiButtonSpritePath, new Vector4(28f, 28f, 28f, 34f));
            uiInputSprite = LoadUiSprite(UiPanelSpritePath, new Vector4(24f, 24f, 24f, 24f));
            uiDividerSprite = LoadUiSprite(UiDividerSpritePath, new Vector4(8f, 0f, 8f, 0f));
            Sprite panelSprite = uiInputSprite ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject canvasObject = new GameObject("CounselingHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = true;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            UiReferences refs = new UiReferences();
            RectTransform sessionCard = CreatePanel("SessionCard", canvas.transform, new Vector2(26f, -26f), new Vector2(354f, 82f), new Vector2(0f, 1f), panelSprite, HudGlass);
            CreateAccentBar("SessionAccent", sessionCard, 82f, HudMint);
            CreateText("SessionEyebrow", "상담 실습  ·  1:1 초기면담", sessionCard, new Vector2(22f, -11f), new Vector2(308f, 22f), font, 14, HudMint, FontStyle.Bold);
            refs.sessionStatus = CreateText("SessionStatus", "불안 사례 · 초기면담 · 1번째 교환", sessionCard, new Vector2(22f, -38f), new Vector2(310f, 30f), font, 19, HudText, FontStyle.Bold);

            RectTransform controlCard = CreatePanel("ActiveControlCard", canvas.transform, new Vector2(26f, -122f), new Vector2(354f, 80f), new Vector2(0f, 1f), panelSprite, HudGlass);
            refs.activeControlCard = controlCard.gameObject;
            CreateAccentBar("ControlAccent", controlCard, 80f, HudGold);
            refs.stageLabel = CreateText("StageLabel", "연습 모드 · 관계 형성 · 0/10턴", controlCard, new Vector2(18f, -8f), new Vector2(318f, 25f), font, 13, HudText, FontStyle.Bold);
            refs.timerLabel = CreateText("TimerLabel", "15:00", controlCard, new Vector2(18f, -38f), new Vector2(82f, 28f), font, 20, HudGold, FontStyle.Bold);
            refs.pauseButton = CreateButton("PauseSession", controlCard, new Vector2(108f, -34f), new Vector2(104f, 36f), "일시정지", font, panelSprite, 13);
            refs.endButton = CreateButton("EndSession", controlCard, new Vector2(220f, -34f), new Vector2(112f, 36f), "종료", font, panelSprite, 13);

            RectTransform cameraCard = CreatePanel("CameraCard", canvas.transform, new Vector2(-26f, -26f), new Vector2(326f, 178f), new Vector2(1f, 1f), panelSprite, HudGlass);
            CreateAccentBar("SignalAccent", cameraCard, 178f, HudMint);
            refs.webcamPreview = CreateRawImage("WebcamPreview", cameraCard, new Vector2(16f, -16f), new Vector2(100f, 74f), panelSprite);
            refs.webcamStatus = CreateText("WebcamStatus", "웹캠 준비 중", cameraCard, new Vector2(132f, -16f), new Vector2(174f, 42f), font, 14, HudMint, FontStyle.Bold);
            CreateText("Privacy", "영상 미저장 · 기기 내 처리", cameraCard, new Vector2(132f, -60f), new Vector2(174f, 24f), font, 13, HudText, FontStyle.Normal);
            refs.auStatus = CreateText("AuStatus", "AU 분석 대기 · 선택 기능", cameraCard, new Vector2(16f, -104f), new Vector2(290f, 24f), font, 13, HudMint, FontStyle.Bold);
            refs.allianceLabel = CreateText("Alliance", "안전 38 · 경계 62 · 공개 25", cameraCard, new Vector2(16f, -138f), new Vector2(290f, 26f), font, 13, HudGold, FontStyle.Bold);

            RectTransform zoomCard = CreatePanel("ZoomCard", canvas.transform, new Vector2(-26f, -218f), new Vector2(326f, 58f), new Vector2(1f, 1f), panelSprite, HudGlass);
            CreateAccentBar("ZoomAccent", zoomCard, 58f, HudGold);
            CreateText("ZoomEyebrow", "관찰 줌", zoomCard, new Vector2(16f, -7f), new Vector2(82f, 20f), font, 12, HudMuted, FontStyle.Bold);
            refs.zoomLabel = CreateText("ZoomValue", "100%", zoomCard, new Vector2(16f, -27f), new Vector2(82f, 24f), font, 16, HudGold, FontStyle.Bold);
            refs.zoomOutButton = CreateButton("ZoomOut", zoomCard, new Vector2(108f, -8f), new Vector2(55f, 42f), "−", font, panelSprite, 21);
            refs.zoomInButton = CreateButton("ZoomIn", zoomCard, new Vector2(169f, -8f), new Vector2(55f, 42f), "+", font, panelSprite, 21);
            refs.zoomResetButton = CreateButton("ZoomReset", zoomCard, new Vector2(230f, -8f), new Vector2(78f, 42f), "초기", font, panelSprite, 14);

            RectTransform speechCard = CreatePanel("ClientSpeechCard", canvas.transform, new Vector2(26f, 170f), new Vector2(540f, 112f), new Vector2(0f, 0f), panelSprite, PaperCard);
            CreateAccentBar("ClientAccent", speechCard, 112f, TealAction);
            CreateText("ClientName", "내담자  ·  김지혜, 32세", speechCard, new Vector2(24f, -12f), new Vector2(492f, 22f), font, 14, new Color(0.27f, 0.39f, 0.34f), FontStyle.Bold);
            refs.clientLine = CreateText("ClientLine", "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.", speechCard, new Vector2(24f, -40f), new Vector2(492f, 58f), font, 18, Ink, FontStyle.Normal);

            RectTransform inputCard = CreatePanel("CounselorInputCard", canvas.transform, new Vector2(0f, 16f), new Vector2(1040f, 116f), new Vector2(0.5f, 0f), panelSprite, HudGlassStrong);
            CreateAccentBar("InputAccent", inputCard, 116f, HudMint);
            refs.feedbackLabel = CreateText("Feedback", "감정을 반영하고 내담자가 의미를 더 말할 수 있도록 응답해 보세요.", inputCard, new Vector2(22f, -8f), new Vector2(996f, 24f), font, 15, HudText, FontStyle.Normal);
            refs.input = CreateInputField(inputCard, new Vector2(22f, -38f), new Vector2(786f, 58f), font, panelSprite);
            refs.sendButton = CreateButton("SendButton", inputCard, new Vector2(824f, -38f), new Vector2(194f, 58f), "응답하기", font, panelSprite, 18);

            RectTransform briefingRoot = CreateOverlayRoot("BriefingOverlay", canvas.transform);
            refs.briefingOverlay = briefingRoot.gameObject;
            RectTransform briefingCard = CreatePanel("BriefingCard", briefingRoot, Vector2.zero, new Vector2(980f, 720f), new Vector2(0.5f, 0.5f), panelSprite, PaperCard);
            briefingCard.anchoredPosition = Vector2.zero;
            CreateAccentBar("BriefingAccent", briefingCard, 720f, TealAction);
            CreateText("BriefingEyebrow", "COUNSELCUE · PRACTICE PATH", briefingCard, new Vector2(42f, -28f), new Vector2(890f, 24f), font, 13, TealAction, FontStyle.Bold);
            Text briefingTitle = CreateText("BriefingTitle", "오늘의 연습 경로 선택", briefingCard, new Vector2(42f, -62f), new Vector2(890f, 50f), font, 31, Ink, FontStyle.Bold);
            briefingTitle.verticalOverflow = VerticalWrapMode.Overflow;
            refs.briefingCaseLabel = CreateText("BriefingCase", "직장 불안 · 김지혜, 32세 · 초기면담", briefingCard, new Vector2(42f, -116f), new Vector2(890f, 30f), font, 17, new Color(0.27f, 0.39f, 0.34f), FontStyle.Bold);
            string briefingBody =
                "상황\n최근 회사에 가려고 하면 숨이 막히고, 자신이 약한 사람인지 걱정합니다.\n\n" +
                "이번 세션의 목표\n1. 관계 안전감을 형성하고 상담 구조를 안내합니다.\n" +
                "2. 반영과 개방형 질문으로 경험을 탐색합니다.\n" +
                "3. 해결책을 서두르지 않고 내담자의 응답 공간을 지킵니다.\n\n" +
                "15분 · 목표 10턴 · 웹캠 원본 미저장";
            refs.briefingBodyLabel = CreateText("BriefingBody", briefingBody, briefingCard, new Vector2(42f, -158f), new Vector2(890f, 236f), font, 16, Ink, FontStyle.Normal);
            CreateText("FullSessionLabel", "전체 회기 · 15분 / 목표 10턴", briefingCard, new Vector2(42f, -402f), new Vector2(890f, 24f), font, 14, TealAction, FontStyle.Bold);
            refs.practiceStartButton = CreateButton("StartPractice", briefingCard, new Vector2(42f, -434f), new Vector2(430f, 56f), "코칭 연습 시작", font, panelSprite, 17);
            refs.evaluationStartButton = CreateButton("StartEvaluation", briefingCard, new Vector2(500f, -434f), new Vector2(432f, 56f), "평가 모드 시작", font, panelSprite, 17);
            CreateDivider("PracticePathDivider", briefingCard, new Vector2(42f, -500f), new Vector2(890f, 4f), new Color(0.34f, 0.47f, 0.40f, 0.55f));
            CreateText("FocusedLabel", "미세기술 집중연습 · 시작값 3분 / 목표 3턴", briefingCard, new Vector2(42f, -512f), new Vector2(890f, 24f), font, 14, TealAction, FontStyle.Bold);
            refs.focusOneButton = CreateButton("StartFocusOne", briefingCard, new Vector2(42f, -548f), new Vector2(280f, 54f), "감정 반영 · 3분", font, panelSprite, 15);
            refs.focusTwoButton = CreateButton("StartFocusTwo", briefingCard, new Vector2(347f, -548f), new Vector2(280f, 54f), "개방형 질문 · 3분", font, panelSprite, 15);
            refs.focusThreeButton = CreateButton("StartFocusThree", briefingCard, new Vector2(652f, -548f), new Vector2(280f, 54f), "전달 정합 · 3분", font, panelSprite, 15);
            CreateText("PrivacyLine", "웹캠 원본 미저장 · 집중연습 시간은 사용자 연구로 조정할 시작값입니다.", briefingCard, new Vector2(42f, -628f), new Vector2(890f, 30f), font, 13, new Color(0.38f, 0.43f, 0.40f), FontStyle.Normal);

            RectTransform pauseRoot = CreateOverlayRoot("PauseOverlay", canvas.transform, 0.56f);
            refs.pauseOverlay = pauseRoot.gameObject;
            RectTransform pauseCard = CreatePanel("PauseCard", pauseRoot, Vector2.zero, new Vector2(560f, 260f), new Vector2(0.5f, 0.5f), panelSprite, PaperCard);
            pauseCard.anchoredPosition = new Vector2(-390f, 0f);
            CreateText("PauseTitle", "세션 일시정지", pauseCard, new Vector2(38f, -36f), new Vector2(484f, 44f), font, 27, Ink, FontStyle.Bold);
            CreateText("PauseBody", "타이머와 상담 입력이 멈췄습니다.\n준비되면 같은 장면에서 계속 진행하세요.", pauseCard, new Vector2(38f, -94f), new Vector2(484f, 66f), font, 16, Ink, FontStyle.Normal);
            refs.resumeButton = CreateButton("ResumeSession", pauseCard, new Vector2(38f, -176f), new Vector2(234f, 52f), "계속하기", font, panelSprite, 16);
            refs.pauseEndButton = CreateButton("PauseEndSession", pauseCard, new Vector2(288f, -176f), new Vector2(234f, 52f), "종료하기", font, panelSprite, 16);

            RectTransform debriefRoot = CreateOverlayRoot("DebriefOverlay", canvas.transform, 1f);
            refs.debriefOverlay = debriefRoot.gameObject;
            RectTransform debriefCard = CreatePanel("DebriefCard", debriefRoot, Vector2.zero, new Vector2(1120f, 760f), new Vector2(0.5f, 0.5f), panelSprite, PaperCard);
            debriefCard.anchoredPosition = Vector2.zero;
            CreateAccentBar("DebriefAccent", debriefCard, 760f, TealAction);
            CreateText("DebriefEyebrow", "REFLECT · COMPARE · RETRY", debriefCard, new Vector2(46f, -28f), new Vector2(1028f, 24f), font, 13, TealAction, FontStyle.Bold);
            refs.debriefTitle = CreateText("DebriefTitle", "세션 성찰 및 재연습", debriefCard, new Vector2(46f, -62f), new Vector2(1028f, 44f), font, 30, Ink, FontStyle.Bold);
            refs.debriefReport = CreateText("DebriefSummary", string.Empty, debriefCard, new Vector2(46f, -116f), new Vector2(1028f, 104f), font, 15, Ink, FontStyle.Normal);
            CreateDivider("ReflectionDivider", debriefCard, new Vector2(46f, -214f), new Vector2(1028f, 4f), new Color(0.34f, 0.47f, 0.40f, 0.55f));
            CreateText("TimelineHeading", "장면 타임라인 · 장면을 선택하세요", debriefCard, new Vector2(46f, -226f), new Vector2(1028f, 24f), font, 14, TealAction, FontStyle.Bold);
            refs.timelineButtons = new Button[10];
            for (int i = 0; i < refs.timelineButtons.Length; i++)
            {
                float x = 46f + (i % 5) * 204f;
                float y = -258f - (i / 5) * 46f;
                refs.timelineButtons[i] = CreateButton($"TimelineTurn{i + 1}", debriefCard, new Vector2(x, y), new Vector2(190f, 38f), $"{i + 1}턴", font, panelSprite, 12);
            }
            refs.sceneDetailLabel = CreateText("SceneDetail", "장면을 선택하면 상담자 응답과 시스템 근거가 표시됩니다.", debriefCard, new Vector2(46f, -360f), new Vector2(1028f, 142f), font, 15, Ink, FontStyle.Normal);
            refs.assessmentStatusLabel = CreateText("AssessmentStatus", "먼저 자신의 판단을 선택하세요.", debriefCard, new Vector2(46f, -510f), new Vector2(1028f, 28f), font, 14, new Color(0.27f, 0.39f, 0.34f), FontStyle.Bold);
            refs.effectiveButton = CreateButton("AssessEffective", debriefCard, new Vector2(46f, -548f), new Vector2(210f, 50f), "잘된 장면", font, panelSprite, 15);
            refs.retryNeededButton = CreateButton("AssessRetry", debriefCard, new Vector2(274f, -548f), new Vector2(230f, 50f), "다시 연습 필요", font, panelSprite, 15);
            refs.replayButton = CreateButton("ReplaySelected", debriefCard, new Vector2(522f, -548f), new Vector2(260f, 50f), "이 장면 다시 연습", font, panelSprite, 15);
            refs.returnButton = CreateButton("ReturnToBriefing", debriefCard, new Vector2(800f, -548f), new Vector2(274f, 50f), "연습 경로로", font, panelSprite, 15);
            CreateText("DebriefDisclaimer", "※ 먼저 자기평가한 뒤 시스템 근거와 비교합니다. 훈련용 피드백이며 임상평가가 아닙니다.", debriefCard, new Vector2(46f, -620f), new Vector2(1028f, 36f), font, 13, new Color(0.38f, 0.43f, 0.40f), FontStyle.Normal);

            refs.languageToggleButton = CreateButton("LanguageToggle", canvas.transform, new Vector2(735f, -22f), new Vector2(130f, 38f), "UI: EN", font, panelSprite, 13);

            refs.activeControlCard.SetActive(false);
            refs.pauseOverlay.SetActive(false);
            refs.debriefOverlay.SetActive(false);
            return refs;
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

        private static void WireSession(CounselingSessionController session, CounselingSessionOrchestrator orchestrator, CounselingCaseDefinition caseDefinition, ClientAvatarController client, WebcamSignalMonitor webcam, FacialActionUnitMonitor actionUnits, GptRealtimeConversationEngine realtime, WebNpcConversationEngine webNpc, CounselCueWebBridge webBridge, UiReferences ui)
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
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(session);
        }

        private static void WireWebExperience(CounselCueWebBridge bridge, CounselingSessionController session, CounselingSessionOrchestrator orchestrator, WebNpcConversationEngine webNpc, ClientAvatarController client)
        {
            SerializedObject serialized = new SerializedObject(bridge);
            serialized.FindProperty("session").objectReferenceValue = session;
            serialized.FindProperty("orchestrator").objectReferenceValue = orchestrator;
            serialized.FindProperty("npcEngine").objectReferenceValue = webNpc;
            serialized.FindProperty("client").objectReferenceValue = client;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);
        }

        private static void WireSessionOrchestrator(CounselingSessionOrchestrator orchestrator, CounselingSessionController session, CounselingReflectionController reflection, CounselingCaseDefinition caseDefinition, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(orchestrator);
            serialized.FindProperty("sessionController").objectReferenceValue = session;
            serialized.FindProperty("reflectionController").objectReferenceValue = reflection;
            serialized.FindProperty("caseDefinition").objectReferenceValue = caseDefinition;
            serialized.FindProperty("activeControlCard").objectReferenceValue = ui.activeControlCard;
            serialized.FindProperty("briefingOverlay").objectReferenceValue = ui.briefingOverlay;
            serialized.FindProperty("pauseOverlay").objectReferenceValue = ui.pauseOverlay;
            serialized.FindProperty("debriefOverlay").objectReferenceValue = ui.debriefOverlay;
            serialized.FindProperty("timerLabel").objectReferenceValue = ui.timerLabel;
            serialized.FindProperty("stageLabel").objectReferenceValue = ui.stageLabel;
            serialized.FindProperty("briefingCaseLabel").objectReferenceValue = ui.briefingCaseLabel;
            serialized.FindProperty("briefingBodyLabel").objectReferenceValue = ui.briefingBodyLabel;
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

        private static void WireCameraZoom(CounselingCameraZoom cameraZoom, Camera camera, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(cameraZoom);
            serialized.FindProperty("targetCamera").objectReferenceValue = camera;
            serialized.FindProperty("zoomOutButton").objectReferenceValue = ui.zoomOutButton;
            serialized.FindProperty("zoomInButton").objectReferenceValue = ui.zoomInButton;
            serialized.FindProperty("resetButton").objectReferenceValue = ui.zoomResetButton;
            serialized.FindProperty("zoomLabel").objectReferenceValue = ui.zoomLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireLanguageToggle(CounselingLanguageToggle languageToggle, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(languageToggle);
            serialized.FindProperty("toggleButton").objectReferenceValue = ui.languageToggleButton;
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
            Texture2D artworkTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtworkTexturePath);
            artwork.mainTexture = artworkTexture;
            EditorUtility.SetDirty(artwork);
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

        private static Sprite LoadUiSprite(string path, Vector4 border)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool requiresImport = importer.textureType != TextureImporterType.Sprite ||
                                  importer.spriteImportMode != SpriteImportMode.Single ||
                                  importer.spriteBorder != border ||
                                  importer.mipmapEnabled ||
                                  importer.wrapMode != TextureWrapMode.Clamp;
            if (requiresImport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, Vector2 anchor, Sprite sprite, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            Shadow shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -3f);
            return rect;
        }

        private static void CreateAccentBar(string name, Transform parent, float height, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(5f, height);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CreateDivider(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = gameObject.GetComponent<Image>();
            image.sprite = uiDividerSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
        }

        private static Text CreateText(string name, string value, Transform parent, Vector2 position, Vector2 size, Font font, int fontSize, Color color, FontStyle style)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = gameObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignByGeometry = true;
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static InputField CreateInputField(Transform parent, Vector2 position, Vector2 size, Font font, Sprite sprite)
        {
            RectTransform root = CreatePanel("CounselorInput", parent, position, size, new Vector2(0f, 1f), uiInputSprite ?? sprite, new Color(0.98f, 0.97f, 0.94f, 1f));
            InputField input = root.gameObject.AddComponent<InputField>();
            Text text = CreateText("Text", string.Empty, root, new Vector2(18f, -10f), new Vector2(size.x - 36f, size.y - 20f), font, 18, Ink, FontStyle.Normal);
            Text placeholder = CreateText("Placeholder", "상담자의 응답을 입력하세요…", root, new Vector2(18f, -10f), new Vector2(size.x - 36f, size.y - 20f), font, 17, new Color(0.44f, 0.49f, 0.46f), FontStyle.Normal);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.MultiLineSubmit;
            return input;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 position, Vector2 size, string label, Font font, Sprite sprite, int fontSize)
        {
            RectTransform root = CreatePanel(name, parent, position, size, new Vector2(0f, 1f), uiButtonSprite ?? sprite, TealAction);
            Button button = root.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.90f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.58f, 0.63f, 0.60f, 0.72f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.10f;
            button.colors = colors;
            Text text = CreateText("Label", label, root, Vector2.zero, size, font, fontSize, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static RectTransform CreateOverlayRoot(string name, Transform parent, float alpha = 0.76f)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.023f, alpha);
            return rect;
        }

        private static RawImage CreateRawImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            RectTransform frame = CreatePanel($"{name}Frame", parent, position, size, new Vector2(0f, 1f), sprite, new Color(0.15f, 0.18f, 0.17f, 1f));
            GameObject rawObject = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            RectTransform rect = rawObject.GetComponent<RectTransform>();
            rect.SetParent(frame, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            RawImage raw = rawObject.GetComponent<RawImage>();
            raw.color = new Color(0.35f, 0.40f, 0.38f, 1f);
            return raw;
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
            public Text debriefTitle;
            public Text debriefReport;
            public Text sceneDetailLabel;
            public Text assessmentStatusLabel;
            public Text webcamStatus;
            public Text auStatus;
            public RawImage webcamPreview;
        }
    }
}
