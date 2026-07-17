using System.IO;
using System.Linq;
using AdieLab.AffectCounsel;
using UnityEditor;
using UnityEditor.Animations;
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
        private const string ControllerPath = "Assets/Animations/CounselingClient.controller";

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

        private static readonly Color HudGlass = new Color(0.035f, 0.055f, 0.052f, 0.88f);
        private static readonly Color HudGlassStrong = new Color(0.030f, 0.047f, 0.045f, 0.94f);
        private static readonly Color HudMint = new Color(0.66f, 0.88f, 0.76f, 1f);
        private static readonly Color HudText = new Color(0.95f, 0.97f, 0.95f, 1f);
        private static readonly Color HudMuted = new Color(0.73f, 0.79f, 0.75f, 1f);
        private static readonly Color HudGold = new Color(0.97f, 0.78f, 0.48f, 1f);
        private static readonly Color PaperCard = new Color(0.975f, 0.958f, 0.925f, 0.96f);
        private static readonly Color Ink = new Color(0.105f, 0.13f, 0.12f, 1f);
        private static readonly Color TealAction = new Color(0.20f, 0.48f, 0.37f, 1f);

        [MenuItem("Tools/Affect Counsel/Build Korean Counseling Room")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Materials");
            EnsureFolder(MaterialRoot);
            EnsureFolder("Assets/Animations");
            CreateMaterials();
            RuntimeAnimatorController controller = CreateAnimatorController();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.65f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.35f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.13f, 0.10f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.80f, 0.74f, 0.65f);
            RenderSettings.fogDensity = 0.003f;

            Transform environment = new GameObject("KoreanCounselingRoom_Environment").transform;
            BuildArchitecture(environment);
            BuildFurniture(environment);
            BuildDecor(environment);
            Camera camera = BuildCameraAndLights();
            ClientAvatarController client = BuildClient(camera.transform, controller);
            UiReferences ui = BuildUi();

            GameObject runtime = new GameObject("AffectCounsel_Runtime");
            WebcamSignalMonitor webcam = runtime.AddComponent<WebcamSignalMonitor>();
            FacialActionUnitMonitor actionUnits = runtime.AddComponent<FacialActionUnitMonitor>();
            GptRealtimeConversationEngine realtime = runtime.AddComponent<GptRealtimeConversationEngine>();
            CounselingSessionController session = runtime.AddComponent<CounselingSessionController>();
            runtime.AddComponent<DemoCaptureController>();
            WireWebcam(webcam, ui);
            WireActionUnits(actionUnits, ui);
            WireSession(session, client, webcam, actionUnits, realtime, ui);

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
            Directory.CreateDirectory("Builds/AffectCounselDemo");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/AffectCounselDemo/AffectCounsel.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildPipeline.BuildPlayer(options);
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
            for (int i = 0; i < 6; i++)
            {
                CreateCube($"WoodBlind_{i:00}", new Vector3(-2.97f, 1.04f + i * 0.29f, 0.78f), new Vector3(0.035f, 0.025f, 2.14f), oak, parent);
            }

            CreateCube("AcousticPanel_01", new Vector3(-1.22f, 1.85f, 3.47f), new Vector3(0.72f, 1.18f, 0.08f), sage, parent);
            CreateCube("AcousticPanel_02", new Vector3(-0.42f, 1.85f, 3.47f), new Vector3(0.72f, 1.18f, 0.08f), teal, parent);
            CreateCube("AcousticPanel_03", new Vector3(0.38f, 1.85f, 3.47f), new Vector3(0.72f, 1.18f, 0.08f), paper, parent);
        }

        private static void BuildFurniture(Transform parent)
        {
            GameObject rug = CreateCube("WovenRug", new Vector3(0f, 0.015f, -0.02f), new Vector3(3.45f, 0.025f, 3.58f), paper, parent);
            for (int i = -4; i <= 4; i++)
            {
                CreateCube($"RugThread_{i + 4:00}", new Vector3(i * 0.35f, 0.032f, 0f), new Vector3(0.015f, 0.008f, 3.34f), sage, rug.transform);
            }

            CreateChair("ClientChair", new Vector3(0f, 0f, 1.30f), 180f, teal, parent, true);
            CreateChair("CounselorChair", new Vector3(0.78f, 0f, -1.92f), 14f, sage, parent, false);

            CreateCylinder("CoffeeTableTop", new Vector3(0.02f, 0.43f, -0.02f), new Vector3(0.47f, 0.045f, 0.47f), oak, parent);
            CreateCylinder("CoffeeTableStem", new Vector3(0.02f, 0.24f, -0.08f), new Vector3(0.09f, 0.23f, 0.09f), darkOak, parent);
            CreateCylinder("CoffeeTableBase", new Vector3(0.02f, 0.04f, -0.08f), new Vector3(0.30f, 0.035f, 0.30f), darkOak, parent);

            CreateCube("TissueBox", new Vector3(-0.20f, 0.58f, -0.02f), new Vector3(0.23f, 0.13f, 0.14f), paper, parent);
            CreateCube("Tissue", new Vector3(-0.20f, 0.69f, -0.02f), new Vector3(0.045f, 0.10f, 0.025f), warmWhite, parent);
            CreateCylinder("ClientTeaCup", new Vector3(0.24f, 0.59f, 0.08f), new Vector3(0.075f, 0.10f, 0.075f), warmWhite, parent);
            CreateCube("CounselorNotebook", new Vector3(0.25f, 0.565f, -0.30f), new Vector3(0.27f, 0.022f, 0.18f), charcoal, parent);
            CreateBookcase(new Vector3(2.53f, 0f, 1.82f), parent);
        }

        private static void BuildDecor(Transform parent)
        {
            CreatePlant(new Vector3(-2.30f, 0f, 2.48f), 1.08f, parent);
            CreatePlant(new Vector3(2.54f, 1.35f, 1.78f), 0.44f, parent);

            CreateCube("ArtworkFrame", new Vector3(1.58f, 1.92f, 3.47f), new Vector3(1.18f, 0.92f, 0.07f), darkOak, parent);
            CreateCube("ArtworkCanvas", new Vector3(1.58f, 1.92f, 3.40f), new Vector3(1.04f, 0.78f, 0.03f), paper, parent);
            CreateSphere("ArtworkSun", new Vector3(1.34f, 2.05f, 3.36f), new Vector3(0.18f, 0.18f, 0.035f), brass, parent);
            CreateCube("ArtworkHill", new Vector3(1.72f, 1.76f, 3.35f), new Vector3(0.66f, 0.18f, 0.02f), sage, parent);

            CreateCylinder("FloorLampStand", new Vector3(2.44f, 0.72f, -0.18f), new Vector3(0.025f, 0.72f, 0.025f), brass, parent);
            CreateCylinder("FloorLampBase", new Vector3(2.44f, 0.05f, -0.18f), new Vector3(0.20f, 0.035f, 0.20f), brass, parent);
            CreateCylinder("FloorLampShade", new Vector3(2.44f, 1.48f, -0.18f), new Vector3(0.24f, 0.28f, 0.24f), paper, parent);

            GameObject lamp = new GameObject("FloorLampWarmLight");
            lamp.transform.SetParent(parent);
            lamp.transform.position = new Vector3(2.44f, 1.42f, -0.18f);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.44f);
            light.intensity = 1.8f;
            light.range = 3.8f;
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
            sun.intensity = 0.74f;
            sun.shadows = LightShadows.Soft;
            daylight.transform.rotation = Quaternion.Euler(34f, 128f, 0f);

            GameObject ceiling = new GameObject("SoftCeilingFill");
            Light fill = ceiling.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.86f, 0.68f);
            fill.intensity = 1.22f;
            fill.range = 6.2f;
            fill.shadows = LightShadows.Soft;
            ceiling.transform.position = new Vector3(0f, 2.72f, 0.35f);

            GameObject softbox = new GameObject("ClientFaceSoftbox");
            Light face = softbox.AddComponent<Light>();
            face.type = LightType.Spot;
            face.color = new Color(1f, 0.82f, 0.68f);
            face.intensity = 1.45f;
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
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            GameObject canvasObject = new GameObject("CounselingHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
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

            RectTransform cameraCard = CreatePanel("CameraCard", canvas.transform, new Vector2(-26f, -26f), new Vector2(326f, 178f), new Vector2(1f, 1f), panelSprite, HudGlass);
            CreateAccentBar("SignalAccent", cameraCard, 178f, HudMint);
            refs.webcamPreview = CreateRawImage("WebcamPreview", cameraCard, new Vector2(16f, -16f), new Vector2(100f, 74f), panelSprite);
            refs.webcamStatus = CreateText("WebcamStatus", "웹캠 준비 중", cameraCard, new Vector2(132f, -16f), new Vector2(174f, 42f), font, 14, HudMint, FontStyle.Bold);
            CreateText("Privacy", "영상 미저장 · 기기 내 처리", cameraCard, new Vector2(132f, -60f), new Vector2(174f, 24f), font, 12, HudText, FontStyle.Normal);
            refs.auStatus = CreateText("AuStatus", "AU 분석 대기 · 선택 기능", cameraCard, new Vector2(16f, -104f), new Vector2(290f, 24f), font, 13, HudMint, FontStyle.Bold);
            refs.allianceLabel = CreateText("Alliance", "안전 38 · 경계 62 · 공개 25", cameraCard, new Vector2(16f, -138f), new Vector2(290f, 26f), font, 13, HudGold, FontStyle.Bold);

            RectTransform speechCard = CreatePanel("ClientSpeechCard", canvas.transform, new Vector2(26f, 170f), new Vector2(540f, 112f), new Vector2(0f, 0f), panelSprite, PaperCard);
            CreateAccentBar("ClientAccent", speechCard, 112f, TealAction);
            CreateText("ClientName", "내담자  ·  김지혜, 32세", speechCard, new Vector2(24f, -12f), new Vector2(492f, 22f), font, 14, new Color(0.27f, 0.39f, 0.34f), FontStyle.Bold);
            refs.clientLine = CreateText("ClientLine", "요즘 회사에 가려고 하면 숨이 막히는 것 같아요.", speechCard, new Vector2(24f, -40f), new Vector2(492f, 58f), font, 18, Ink, FontStyle.Normal);

            RectTransform inputCard = CreatePanel("CounselorInputCard", canvas.transform, new Vector2(0f, 16f), new Vector2(1040f, 116f), new Vector2(0.5f, 0f), panelSprite, HudGlassStrong);
            CreateAccentBar("InputAccent", inputCard, 116f, HudMint);
            refs.feedbackLabel = CreateText("Feedback", "감정을 반영하고 내담자가 의미를 더 말할 수 있도록 응답해 보세요.", inputCard, new Vector2(22f, -8f), new Vector2(996f, 24f), font, 15, HudText, FontStyle.Normal);
            refs.input = CreateInputField(inputCard, new Vector2(22f, -38f), new Vector2(786f, 58f), font, panelSprite);
            refs.sendButton = CreateButton(inputCard, new Vector2(824f, -38f), new Vector2(194f, 58f), "응답하기", font, panelSprite);
            return refs;
        }

        private static RuntimeAnimatorController CreateAnimatorController()
        {
            if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState idle = machine.AddState("Idle");
            idle.motion = LoadClip("Assets/ThirdParty/MicrosoftRocketbox/Animations/f_sit_table_idle_neutral_01.max.fbx");
            AnimatorState waiting = machine.AddState("Waiting");
            waiting.motion = LoadClip("Assets/ThirdParty/MicrosoftRocketbox/Animations/f_sit_table_idle_waiting_01.max.fbx");
            AnimatorState thoughtful = machine.AddState("Thoughtful");
            thoughtful.motion = LoadClip("Assets/ThirdParty/MicrosoftRocketbox/Animations/f_sit_table_gestic_thoughtful.max.fbx");
            AnimatorState talk = machine.AddState("Talk");
            talk.motion = thoughtful.motion ?? idle.motion;
            machine.defaultState = idle;
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
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

        private static void WireSession(CounselingSessionController session, ClientAvatarController client, WebcamSignalMonitor webcam, FacialActionUnitMonitor actionUnits, GptRealtimeConversationEngine realtime, UiReferences ui)
        {
            SerializedObject serialized = new SerializedObject(session);
            serialized.FindProperty("client").objectReferenceValue = client;
            serialized.FindProperty("webcam").objectReferenceValue = webcam;
            serialized.FindProperty("actionUnits").objectReferenceValue = actionUnits;
            serialized.FindProperty("realtimeEngine").objectReferenceValue = realtime;
            serialized.FindProperty("counselorInput").objectReferenceValue = ui.input;
            serialized.FindProperty("sendButton").objectReferenceValue = ui.sendButton;
            serialized.FindProperty("clientLine").objectReferenceValue = ui.clientLine;
            serialized.FindProperty("sessionStatus").objectReferenceValue = ui.sessionStatus;
            serialized.FindProperty("feedbackLabel").objectReferenceValue = ui.feedbackLabel;
            serialized.FindProperty("allianceLabel").objectReferenceValue = ui.allianceLabel;
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
            cream = MaterialAsset("CreamWall", new Color(0.82f, 0.76f, 0.66f), 0.04f);
            warmWhite = MaterialAsset("WarmWhite", new Color(0.92f, 0.88f, 0.80f), 0.08f);
            oak = MaterialAsset("LightOak", new Color(0.58f, 0.38f, 0.20f), 0.28f);
            darkOak = MaterialAsset("DarkOak", new Color(0.22f, 0.12f, 0.07f), 0.30f);
            sage = MaterialAsset("Sage", new Color(0.28f, 0.42f, 0.35f), 0.08f);
            teal = MaterialAsset("TealFabric", new Color(0.16f, 0.30f, 0.30f), 0.04f);
            charcoal = MaterialAsset("Charcoal", new Color(0.07f, 0.08f, 0.075f), 0.18f);
            brass = MaterialAsset("Brass", new Color(0.72f, 0.48f, 0.18f), 0.62f, 0.55f);
            leaf = MaterialAsset("Leaf", new Color(0.12f, 0.30f, 0.16f), 0.16f);
            paper = MaterialAsset("Paper", new Color(0.88f, 0.83f, 0.73f), 0.08f);
            windowGlow = MaterialAsset("WindowGlow", new Color(0.68f, 0.82f, 0.84f), 0.18f);
            windowGlow.EnableKeyword("_EMISSION");
            windowGlow.SetColor("_EmissionColor", new Color(0.22f, 0.34f, 0.36f));
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
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static InputField CreateInputField(Transform parent, Vector2 position, Vector2 size, Font font, Sprite sprite)
        {
            RectTransform root = CreatePanel("CounselorInput", parent, position, size, new Vector2(0f, 1f), sprite, new Color(0.97f, 0.96f, 0.92f, 1f));
            InputField input = root.gameObject.AddComponent<InputField>();
            Text text = CreateText("Text", string.Empty, root, new Vector2(18f, -10f), new Vector2(size.x - 36f, size.y - 20f), font, 18, Ink, FontStyle.Normal);
            Text placeholder = CreateText("Placeholder", "상담자의 응답을 입력하세요…", root, new Vector2(18f, -10f), new Vector2(size.x - 36f, size.y - 20f), font, 17, new Color(0.44f, 0.49f, 0.46f), FontStyle.Normal);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.MultiLineSubmit;
            return input;
        }

        private static Button CreateButton(Transform parent, Vector2 position, Vector2 size, string label, Font font, Sprite sprite)
        {
            RectTransform root = CreatePanel("SendButton", parent, position, size, new Vector2(0f, 1f), sprite, TealAction);
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
            Text text = CreateText("Label", label, root, Vector2.zero, size, font, 18, Color.white, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
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
            public InputField input;
            public Button sendButton;
            public Text clientLine;
            public Text sessionStatus;
            public Text feedbackLabel;
            public Text allianceLabel;
            public Text webcamStatus;
            public Text auStatus;
            public RawImage webcamPreview;
        }
    }
}
