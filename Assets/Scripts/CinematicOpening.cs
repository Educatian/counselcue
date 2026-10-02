using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// A short letterboxed opening before the briefing: an establishing shot of the counseling
    /// room, a quiet profile of today's client, then a push over the counselor's shoulder into
    /// the first-person view. Skippable with the button, a click, Space, Enter or Escape; plays
    /// once per page load.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CinematicOpening : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private CounselingCameraZoom cameraZoom;
        [SerializeField] private ClientAvatarHost client;
        [SerializeField] private CounselorBodyController counselor;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private Font font;
        [SerializeField] private Font boldFont;
        [SerializeField] private bool playOnStart = true;

        private static bool playedThisLoad;

        /// <summary>Set by editor tooling (review capture) to keep the opening from auto-playing.</summary>
        public static bool SuppressAutoPlay;

        private Canvas overlay;
        private RectTransform topBar, bottomBar;
        private Image fade;
        private Text title, tagline, caption;
        private Button skip;
        private bool skipping;
        private CanvasGroup hudGroup;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float homeFieldOfView;

        public bool IsPlaying { get; private set; }
        private float startedAt;

        // Safety net: never leave the learner behind a black letterbox if a shot fails.
        private void Update()
        {
            if (IsPlaying && !finishing && Time.realtimeSinceStartup - startedAt > 18f)
            {
                Debug.LogWarning("CinematicOpening watchdog: finishing a stalled opening.");
                StopAllCoroutines();
                skipping = true;
                Finish();
            }
        }

        private bool finishing;

        public void Configure(Camera configuredCamera, CounselingCameraZoom zoom, ClientAvatarHost host, CounselorBodyController body, Canvas hud, Font regular, Font bold)
        {
            viewCamera = configuredCamera; cameraZoom = zoom; client = host; counselor = body; hudCanvas = hud; font = regular; boldFont = bold;
        }

        private IEnumerator Start()
        {
            // Review captures and repeat visits within one page load go straight to the briefing.
            if (!playOnStart || playedThisLoad || SuppressAutoPlay) yield break;
            yield return null;
            yield return Play();
        }

        public IEnumerator Play()
        {
            if (viewCamera == null || IsPlaying) yield break;
            playedThisLoad = true;
            IsPlaying = true;
            finishing = false;
            startedAt = Time.realtimeSinceStartup;
            skipping = false;
            homePosition = viewCamera.transform.position;
            homeRotation = viewCamera.transform.rotation;
            homeFieldOfView = viewCamera.fieldOfView;
            if (cameraZoom != null) cameraZoom.enabled = false;
            BuildOverlay();
            if (hudCanvas != null)
            {
                hudGroup = hudCanvas.gameObject.GetOrAddComponent<CanvasGroup>();
                hudGroup.alpha = 0f;
                hudGroup.blocksRaycasts = false;
            }
            if (counselor != null) counselor.HeadHidden = false;

            Vector3 body, face;
            if (client == null || !client.TryGetObservationAnchors(out body, out face))
            {
                body = new Vector3(0f, 1.05f, 1.25f);
                face = new Vector3(0f, 1.3f, 1.2f);
            }
            Vector3 clientForward = Vector3.ProjectOnPlane(homePosition - face, Vector3.up).normalized;
            Vector3 clientRight = Vector3.Cross(Vector3.up, clientForward);

            // Shot 1 — the room: a slow, high establishing move toward the client.
            SetFade(1f);
            StartCoroutine(FadeText(title, 0.7f, 1.2f, 3.2f));
            StartCoroutine(FadeText(tagline, 1.3f, 1.2f, 2.6f));
            yield return Shot(
                homePosition + new Vector3(1.35f, 0.95f, -1.35f), homePosition + new Vector3(1.0f, 0.75f, -0.95f),
                body, body + Vector3.up * 0.05f, 40f, 36f, 4.4f, fadeIn: 1.1f);
            if (skipping) { Finish(); yield break; }

            // Shot 2 — today's client, in quiet profile.
            yield return Dip();
            StartCoroutine(FadeText(caption, 0.35f, 0.9f, 2.4f));
            yield return Shot(
                face + clientForward * 0.95f + clientRight * 0.62f - Vector3.up * 0.02f,
                face + clientForward * 0.8f + clientRight * 0.5f,
                face, face - Vector3.up * 0.01f, 28f, 26f, 3.8f);
            if (skipping) { Finish(); yield break; }

            // Shot 3 — over the counselor's shoulder, pushing into the first-person view.
            yield return Dip();
            Vector3 back = -Vector3.ProjectOnPlane(homeRotation * Vector3.forward, Vector3.up).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, -back);
            Vector3 start = homePosition + back * 0.75f + side * 0.32f + Vector3.up * 0.22f;
            StartCoroutine(Letterbox(2.6f, 1.2f));
            yield return Shot(start, homePosition, body, body, 40f, homeFieldOfView, 3.6f, pushToHome: true);
            Finish();
        }

        private IEnumerator Shot(Vector3 from, Vector3 to, Vector3 lookFrom, Vector3 lookTo, float fovFrom, float fovTo, float duration, float fadeIn = 0.35f, bool pushToHome = false)
        {
            float t = 0f;
            while (t < duration && !skipping)
            {
                t += Mathf.Max(Time.unscaledDeltaTime, 1f / 240f);
                float s = Mathf.SmoothStep(0f, 1f, t / duration);
                Vector3 position = Vector3.Lerp(from, to, s);
                Quaternion look = Quaternion.LookRotation(Vector3.Lerp(lookFrom, lookTo, s) - position, Vector3.up);
                if (pushToHome) look = Quaternion.Slerp(look, homeRotation, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t / duration)));
                viewCamera.transform.SetPositionAndRotation(position, look);
                viewCamera.fieldOfView = Mathf.Lerp(fovFrom, fovTo, s);
                SetFade(1f - Mathf.Clamp01(t / fadeIn));
                // Hide the counselor's head as the camera passes into it.
                if (pushToHome && counselor != null) counselor.HeadHidden = Vector3.Distance(position, homePosition) < 0.4f;
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0)) skipping = true;
                yield return null;
            }
        }

        private IEnumerator Dip()
        {
            float t = 0f;
            while (t < 0.28f && !skipping) { t += Time.unscaledDeltaTime; SetFade(t / 0.28f); yield return null; }
        }

        private IEnumerator FadeText(Text text, float delay, float fadeTime, float hold)
        {
            if (text == null) yield break;
            float t = -delay;
            float total = fadeTime * 2f + hold;
            while (t < total && !skipping)
            {
                t += Time.unscaledDeltaTime;
                float a = t < 0f ? 0f : t < fadeTime ? t / fadeTime : t < fadeTime + hold ? 1f : 1f - (t - fadeTime - hold) / fadeTime;
                SetAlpha(text, a);
                yield return null;
            }
            SetAlpha(text, 0f);
        }

        private IEnumerator Letterbox(float delay, float duration)
        {
            float t = -delay;
            while (t < duration && !skipping)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                SetBars(1f - s);
                yield return null;
            }
        }

        private void Finish()
        {
            if (finishing) return;
            finishing = true;
            viewCamera.transform.SetPositionAndRotation(homePosition, homeRotation);
            viewCamera.fieldOfView = homeFieldOfView;
            if (cameraZoom != null) cameraZoom.enabled = true;
            if (counselor != null) counselor.HeadHidden = true;
            StartCoroutine(RevealHud());
        }

        private IEnumerator RevealHud()
        {
            float t = 0f;
            SetBars(0f);
            if (skip != null) skip.gameObject.SetActive(false);
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Clamp01(t / 0.6f);
                SetFade(skipping ? 1f - s : 0f);
                if (hudGroup != null) hudGroup.alpha = s;
                yield return null;
            }
            if (hudGroup != null) { hudGroup.alpha = 1f; hudGroup.blocksRaycasts = true; }
            if (overlay != null) Destroy(overlay.gameObject);
            IsPlaying = false;
        }

        private void BuildOverlay()
        {
            GameObject root = new GameObject("CinematicOverlay", typeof(RectTransform));
            overlay = root.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 500;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            fade = Panel("Fade", root.transform, Vector2.zero, Vector2.one, Color.black);
            topBar = Panel("TopBar", root.transform, new Vector2(0f, 1f), Vector2.one, Color.black).rectTransform;
            bottomBar = Panel("BottomBar", root.transform, Vector2.zero, new Vector2(1f, 0f), Color.black).rectTransform;
            SetBars(1f);

            title = Label("Title", root.transform, "CounselCue", boldFont, 64, new Vector2(0.5f, 0.2f), new Vector2(900f, 90f));
            tagline = Label("Tagline", root.transform, "마음을 듣는 연습 · 상담 훈련 스튜디오", font, 22, new Vector2(0.5f, 0.2f), new Vector2(900f, 40f));
            tagline.rectTransform.anchoredPosition = new Vector2(0f, -62f);
            caption = Label("Caption", root.transform, "오늘, 한 사람의 이야기를 듣습니다.", font, 26, new Vector2(0.5f, 0.12f), new Vector2(1200f, 50f));
            SetAlpha(title, 0f); SetAlpha(tagline, 0f); SetAlpha(caption, 0f);

            GameObject skipObject = new GameObject("SkipCinematic", typeof(RectTransform), typeof(Image), typeof(Button));
            skipObject.transform.SetParent(root.transform, false);
            RectTransform skipRect = (RectTransform)skipObject.transform;
            skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 0f);
            skipRect.pivot = new Vector2(1f, 0f);
            skipRect.anchoredPosition = new Vector2(-40f, 36f);
            skipRect.sizeDelta = new Vector2(150f, 44f);
            skipObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            skip = skipObject.GetComponent<Button>();
            skip.onClick.AddListener(() => skipping = true);
            Text skipLabel = Label("Label", skipRect, "건너뛰기  ›", font, 17, new Vector2(0.5f, 0.5f), new Vector2(150f, 44f));
            skipLabel.rectTransform.anchorMin = Vector2.zero;
            skipLabel.rectTransform.anchorMax = Vector2.one;
            skipLabel.rectTransform.sizeDelta = Vector2.zero;
            skipLabel.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void SetBars(float amount)
        {
            float height = 0.11f * amount;
            if (topBar != null) { topBar.anchorMin = new Vector2(0f, 1f - height); topBar.anchorMax = Vector2.one; }
            if (bottomBar != null) { bottomBar.anchorMin = Vector2.zero; bottomBar.anchorMax = new Vector2(1f, height); }
        }

        private void SetFade(float alpha)
        {
            if (fade != null) fade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        }

        private static void SetAlpha(Text text, float alpha)
        {
            if (text == null) return;
            Color c = text.color;
            c.a = Mathf.Clamp01(alpha);
            text.color = c;
        }

        private static Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(string name, Transform parent, string value, Font font, int size, Vector2 anchor, Vector2 box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = box;
            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.965f, 0.945f, 0.906f, 1f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
