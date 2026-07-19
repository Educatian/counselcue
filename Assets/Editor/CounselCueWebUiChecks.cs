using AdieLab.AffectCounsel;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    public static class CounselCueWebUiChecks
    {
        [MenuItem("Tools/CounselCue/Run Web UI Checks")]
        public static void RunFromCommandLine()
        {
            GameObject card = new GameObject("CounselorInputCard_Test", typeof(RectTransform));
            GameObject accent = new GameObject("InputAccent_Test", typeof(RectTransform));
            GameObject input = new GameObject("Input_Test");
            GameObject send = new GameObject("Send_Test");
            accent.transform.SetParent(card.transform);
            input.transform.SetParent(card.transform);
            send.transform.SetParent(card.transform);

            WebGlHudLayout.ApplyBrowserInputLayout(
                card.GetComponent<RectTransform>(),
                accent.GetComponent<RectTransform>(),
                input,
                send);

            Require(!input.activeSelf, "Unity input field must be hidden when browser input is active.");
            Require(!send.activeSelf, "Unity send button must be hidden when browser input is active.");
            Require(Mathf.Approximately(card.GetComponent<RectTransform>().sizeDelta.y, 44f), "Feedback card must collapse to 44 pixels.");
            Require(Mathf.Approximately(accent.GetComponent<RectTransform>().sizeDelta.y, 44f), "Feedback accent must match the collapsed card.");
            Object.DestroyImmediate(card);

            CounselingRoomBuilder.Build();
            CounselCueWebBridge bridge = Object.FindAnyObjectByType<CounselCueWebBridge>();
            Require(bridge != null, "Generated scene must contain the WebGL bridge.");
            SerializedObject serializedBridge = new SerializedObject(bridge);
            Require(serializedBridge.FindProperty("unityInputCard").objectReferenceValue != null, "WebGL bridge input card reference is missing.");
            Require(serializedBridge.FindProperty("unityInputAccent").objectReferenceValue != null, "WebGL bridge accent reference is missing.");
            Require(serializedBridge.FindProperty("unityInputField").objectReferenceValue != null, "WebGL bridge input field reference is missing.");
            Require(serializedBridge.FindProperty("unitySendButton").objectReferenceValue != null, "WebGL bridge send button reference is missing.");

            Debug.Log("COUNSELCUE_WEB_UI_CHECKS_PASS");
            EditorApplication.Exit(0);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.InvalidOperationException(message);
        }
    }
}