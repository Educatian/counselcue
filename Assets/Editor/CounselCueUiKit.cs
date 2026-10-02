using AdieLab.AffectCounsel;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// Editor-side factory for CounselCue's interface: rounded surfaces with soft shadows,
    /// consistent button variants and type styles built on <see cref="UiTheme"/> tokens and
    /// the procedural sprites in Assets/Art/UI.
    /// </summary>
    internal static class UiKit
    {
        private const string SpriteRoot = "Assets/Art/UI/";
        private const float SpriteRadius = 32f;

        public static Font Font;
        /// <summary>Static bold cut; avoids Unity's synthetic emboldening, which smears small Hangul.</summary>
        public static Font BoldFont;
        public static Sprite Round;
        public static Sprite Ring;
        public static Sprite Shadow;
        public static Sprite Gradient;
        public static Sprite Vignette;
        public static Sprite Seal;

        public enum Variant { Primary, Tonal, GhostDark, GhostLight, OutlineLight, OutlineDark }

        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        public static void Load(Font font, Font boldFont)
        {
            Font = font;
            BoldFont = boldFont != null ? boldFont : font;
            Round = LoadSprite("ui_round.png", new Vector4(32f, 32f, 32f, 32f));
            Ring = LoadSprite("ui_ring.png", new Vector4(32f, 32f, 32f, 32f));
            Shadow = LoadSprite("ui_shadow.png", new Vector4(72f, 72f, 72f, 72f));
            Gradient = LoadSprite("ui_gradient_v.png", Vector4.zero);
            Vignette = LoadSprite("ui_vignette.png", Vector4.zero);
            Seal = LoadSprite("ui_seal.png", Vector4.zero);
        }

        private static Sprite LoadSprite(string file, Vector4 border)
        {
            string path = SpriteRoot + file;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"CounselCue UI sprite missing: {path}");
                return null;
            }
            bool reimport = importer.textureType != TextureImporterType.Sprite ||
                            importer.spriteImportMode != SpriteImportMode.Single ||
                            importer.spriteBorder != border ||
                            importer.mipmapEnabled ||
                            importer.wrapMode != TextureWrapMode.Clamp ||
                            importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (reimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---- Layout primitives -------------------------------------------------------------

        /// <summary>A rect whose anchor and pivot sit on the same corner, positioned from it.</summary>
        public static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Node(string name, Transform parent, Vector2 position, Vector2 size) =>
            Node(name, parent, TopLeft, position, size);

        public static RectTransform Stretch(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Center;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static Image Surface(RectTransform rect, Color color, float radius, Sprite sprite = null)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite ?? Round;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = radius <= 0f ? 100f : SpriteRadius / radius;
            image.color = color;
            image.raycastTarget = false;
            if (radius <= 0f) image.sprite = null;
            return image;
        }

        /// <summary>
        /// A card: an empty root holding a soft shadow and a rounded surface, so content added
        /// to the root draws above both and hiding the root hides the shadow too.
        /// </summary>
        public static RectTransform Card(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, float radius, float shadowAlpha)
        {
            RectTransform root = Node(name, parent, anchor, position, size);
            if (shadowAlpha > 0f && Shadow != null)
            {
                RectTransform shadow = Stretch("Shadow", root, new Vector2(-40f, -52f), new Vector2(40f, 28f));
                Image shadowImage = shadow.gameObject.AddComponent<Image>();
                shadowImage.sprite = Shadow;
                shadowImage.type = Image.Type.Sliced;
                shadowImage.color = new Color(0f, 0f, 0f, shadowAlpha);
                shadowImage.raycastTarget = false;
            }
            RectTransform surface = Stretch("Surface", root, Vector2.zero, Vector2.zero);
            Image image = Surface(surface, color, radius);
            image.raycastTarget = true;
            return root;
        }

        public static RectTransform Overlay(string name, Transform parent, Color tint)
        {
            RectTransform root = Stretch(name, parent, Vector2.zero, Vector2.zero);
            Image image = root.gameObject.AddComponent<Image>();
            image.sprite = Vignette;
            image.type = Image.Type.Simple;
            image.color = tint;
            image.raycastTarget = true;
            return root;
        }

        /// <summary>Full-width gradient scrim at the top or bottom edge for HUD legibility.</summary>
        public static void Scrim(string name, Transform parent, bool bottom, float height, Color tint)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, bottom ? 0f : 1f);
            rect.anchorMax = new Vector2(1f, bottom ? 0f : 1f);
            rect.pivot = Center;
            rect.anchoredPosition = new Vector2(0f, bottom ? height * 0.5f : -height * 0.5f);
            rect.sizeDelta = new Vector2(0f, height);
            // The sprite is opaque at the bottom row; flip it for the top edge.
            if (!bottom) rect.localEulerAngles = new Vector3(0f, 0f, 180f);
            Image image = gameObject.AddComponent<Image>();
            image.sprite = Gradient;
            image.type = Image.Type.Simple;
            image.color = tint;
            image.raycastTarget = false;
        }

        public static RectTransform Hairline(string name, Transform parent, Vector2 position, float width, Color color)
        {
            RectTransform rect = Node(name, parent, position, new Vector2(width, 1f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static Image SealMark(Transform parent, Vector2 position, float size)
        {
            RectTransform rect = Node("Seal", parent, position, new Vector2(size, size));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Seal;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        // ---- Type ---------------------------------------------------------------------------

        public static Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color,
            bool bold = false, TextAnchor alignment = TextAnchor.UpperLeft, float lineSpacing = 1f)
        {
            RectTransform rect = Node(name, parent, position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = bold ? BoldFont : Font;
            text.fontSize = fontSize;
            text.fontStyle = bold && BoldFont == Font ? FontStyle.Bold : FontStyle.Normal;
            text.alignByGeometry = false;
            text.alignment = alignment;
            text.lineSpacing = lineSpacing;
            text.supportRichText = true;
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Noto CJK has tall line metrics; overflow keeps a single line from vanishing in a
            // snug box. Fit() switches to truncate + best-fit for text that can run long.
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Eyebrow: small bold caps line that labels a group.</summary>
        public static Text Eyebrow(string name, Transform parent, string value, Vector2 position, float width, Color color) =>
            Label(name, parent, value, position, new Vector2(width, 18f), 12, color, true);

        /// <summary>Shrinks long (e.g. English) text to fit its box instead of truncating it.</summary>
        public static Text Fit(Text text, int minSize)
        {
            if (text == null) return null;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Min(minSize, text.fontSize);
            return text;
        }

        public static void SoftShadow(Text text, float alpha = 0.55f)
        {
            UnityEngine.UI.Shadow shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = new Vector2(0f, -1.5f);
        }

        // ---- Controls -----------------------------------------------------------------------

        public static UnityEngine.UI.Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size, string label, Variant variant,
            int fontSize = 15, float radius = 12f, Vector2? anchor = null)
        {
            RectTransform root = Node(name, parent, anchor ?? TopLeft, position, size);
            bool outline = variant == Variant.OutlineLight || variant == Variant.OutlineDark;
            Color fill;
            Color text;
            switch (variant)
            {
                case Variant.Primary: fill = UiTheme.CeladonDeep; text = UiTheme.Paper; break;
                case Variant.Tonal: fill = UiTheme.CeladonSoft; text = UiTheme.CeladonDeep; break;
                case Variant.GhostDark: fill = new Color(1f, 1f, 1f, 0.08f); text = UiTheme.OnDark; break;
                case Variant.GhostLight: fill = new Color(0.118f, 0.129f, 0.122f, 0.06f); text = UiTheme.Ink; break;
                case Variant.OutlineDark: fill = new Color(1f, 1f, 1f, 0.32f); text = UiTheme.OnDark; break;
                default: fill = new Color(0.118f, 0.129f, 0.122f, 0.30f); text = UiTheme.Ink; break;
            }
            Image image = Surface(root, fill, radius, outline ? Ring : Round);
            image.raycastTarget = true;

            UnityEngine.UI.Button button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            bool translucent = variant == Variant.GhostDark || variant == Variant.GhostLight || outline;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = translucent ? new Color(1.05f, 1.05f, 1.05f, 2.2f) : new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.84f, 0.86f, 0.85f, translucent ? 2.6f : 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            RectTransform labelRect = Stretch("Label", root, new Vector2(10f, 2f), new Vector2(-10f, -2f));
            Text labelText = labelRect.gameObject.AddComponent<Text>();
            labelText.font = BoldFont;
            labelText.fontSize = fontSize;
            labelText.fontStyle = BoldFont == Font ? FontStyle.Bold : FontStyle.Normal;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.supportRichText = true;
            labelText.color = text;
            labelText.text = label;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.raycastTarget = false;
            Fit(labelText, 10);

            UiButtonState state = root.gameObject.AddComponent<UiButtonState>();
            SerializedObject serialized = new SerializedObject(state);
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("label").objectReferenceValue = labelText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        /// <summary>A slim meter; returns the fill rect whose anchorMax.x is the value.</summary>
        public static RectTransform Meter(string name, Transform parent, Vector2 position, Vector2 size, Color fill, float value)
        {
            RectTransform track = Node(name, parent, position, size);
            Surface(track, new Color(1f, 1f, 1f, 0.12f), size.y * 0.5f);
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(track, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Surface(fillRect, fill, size.y * 0.5f);
            return fillRect;
        }

        public static InputField TextArea(string name, Transform parent, Vector2 position, Vector2 size, string placeholder)
        {
            RectTransform root = Node(name, parent, position, size);
            Image surface = Surface(root, UiTheme.Paper, 16f);
            surface.raycastTarget = true;
            InputField input = root.gameObject.AddComponent<InputField>();
            input.targetGraphic = surface;
            Text text = Label("Text", root, string.Empty, new Vector2(20f, -14f), new Vector2(size.x - 40f, size.y - 24f), 18, UiTheme.Ink);
            text.supportRichText = false;
            Text hint = Label("Placeholder", root, placeholder, new Vector2(20f, -14f), new Vector2(size.x - 40f, size.y - 24f), 17, UiTheme.InkMuted);
            hint.fontStyle = FontStyle.Normal;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.MultiLineSubmit;
            input.caretColor = UiTheme.CeladonDeep;
            input.caretWidth = 2;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.498f, 0.722f, 0.627f, 0.45f);
            return input;
        }

        public static Toggle Checkbox(string name, Transform parent, Vector2 position, Vector2 size, string label, Color labelColor)
        {
            RectTransform root = Node(name, parent, position, size);
            Toggle toggle = root.gameObject.AddComponent<Toggle>();

            RectTransform box = Node("Box", root, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(20f, 20f));
            Image boxImage = Surface(box, UiTheme.Paper, 6f);
            boxImage.raycastTarget = true;
            RectTransform ring = Stretch("Ring", box, Vector2.zero, Vector2.zero);
            Surface(ring, new Color(0.118f, 0.129f, 0.122f, 0.45f), 6f, Ring);
            RectTransform check = Node("Check", box, Center, Vector2.zero, new Vector2(12f, 12f));
            Image checkImage = Surface(check, UiTheme.CeladonDeep, 3f);

            Text text = Label("ConsentLabel", root, label, new Vector2(32f, 0f), new Vector2(size.x - 32f, size.y), 13, labelColor);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = true;
            Fit(text, 10);

            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = false;
            return toggle;
        }

        /// <summary>Rounded clip that shows its child image only when one is assigned.</summary>
        public static Image MaskedImage(string frameName, string imageName, Transform parent, Vector2 position, Vector2 size, float radius)
        {
            RectTransform frame = Node(frameName, parent, position, size);
            Image maskShape = Surface(frame, Color.white, radius);
            Mask mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            maskShape.raycastTarget = false;
            RectTransform content = Stretch(imageName, frame, Vector2.zero, Vector2.zero);
            Image image = content.gameObject.AddComponent<Image>();
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        public static RawImage MaskedRawImage(string name, Transform parent, Vector2 position, Vector2 size, float radius, Color background)
        {
            RectTransform frame = Node($"{name}Frame", parent, position, size);
            Surface(frame, background, radius);
            Mask mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            RectTransform content = Stretch(name, frame, Vector2.zero, Vector2.zero);
            RawImage raw = content.gameObject.AddComponent<RawImage>();
            raw.color = new Color(0.35f, 0.38f, 0.36f, 1f);
            raw.raycastTarget = false;
            return raw;
        }
    }
}
