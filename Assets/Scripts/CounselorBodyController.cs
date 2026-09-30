using System;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// The learner's own body in the counselor's chair: seated with the right leg crossed over the
    /// left, a notepad resting on the crossed knee, the left hand steadying it and the right hand
    /// holding a pen. The head is hidden because the camera is its eyes. While the learner types,
    /// the pen writes and the view glances down at the notes (see <see cref="CounselingCameraZoom"/>),
    /// so the counselor's arms and crossed legs come into the first-person view; the view returns
    /// to the client when typing stops or the response is sent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CounselorBodyController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private CounselingCameraZoom cameraZoom;
        [SerializeField] private Transform chair;
        [SerializeField, Range(0f, 30f)] private float glancePitch = 22f;
        [SerializeField, Range(0f, 20f)] private float glanceFieldOfView = 12f;
        [SerializeField] private float glanceHoldSeconds = 1.3f;

        private Animator animator;
        private Transform hips, head, neck;
        private Transform leftUpperLeg, leftLowerLeg, leftFoot, rightUpperLeg, rightLowerLeg, rightFoot;
        private Transform leftUpperArm, leftLowerArm, leftHand, rightUpperArm, rightLowerArm, rightHand;
        private Transform notepad, pen;
        private int settleFrames = 4;
        private bool aligned;
        private bool headHidden = true;
        private float lastTyping = -99f;
        private float glance;
        private float glanceVelocity;
        private float writePhase;
        private Vector3 headScale = Vector3.one;

        /// <summary>Raised by the session controller whenever the counselor's input text changes.</summary>
        public static event Action Typing;
        /// <summary>Raised when a response is sent; the view returns to the client.</summary>
        public static event Action Submitted;

        public static void NotifyTyping() => Typing?.Invoke();
        public static void NotifySubmitted() => Submitted?.Invoke();

        public bool HeadHidden
        {
            get => headHidden;
            set { headHidden = value; ApplyHead(); }
        }

        public void Configure(Camera configuredCamera, CounselingCameraZoom zoom, Transform counselorChair)
        {
            viewCamera = configuredCamera;
            cameraZoom = zoom;
            chair = counselorChair;
        }

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) { enabled = false; return; }
            hips = Bone(HumanBodyBones.Hips);
            head = Bone(HumanBodyBones.Head);
            neck = Bone(HumanBodyBones.Neck);
            leftUpperLeg = Bone(HumanBodyBones.LeftUpperLeg); leftLowerLeg = Bone(HumanBodyBones.LeftLowerLeg); leftFoot = Bone(HumanBodyBones.LeftFoot);
            rightUpperLeg = Bone(HumanBodyBones.RightUpperLeg); rightLowerLeg = Bone(HumanBodyBones.RightLowerLeg); rightFoot = Bone(HumanBodyBones.RightFoot);
            leftUpperArm = Bone(HumanBodyBones.LeftUpperArm); leftLowerArm = Bone(HumanBodyBones.LeftLowerArm); leftHand = Bone(HumanBodyBones.LeftHand);
            rightUpperArm = Bone(HumanBodyBones.RightUpperArm); rightLowerArm = Bone(HumanBodyBones.RightLowerArm); rightHand = Bone(HumanBodyBones.RightHand);
            if (head != null) headScale = head.localScale;
            BuildProps();
        }

        private Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        private void OnEnable()
        {
            Typing += OnTyping;
            Submitted += OnSubmitted;
        }

        private void OnDisable()
        {
            Typing -= OnTyping;
            Submitted -= OnSubmitted;
            if (cameraZoom != null) cameraZoom.SetGlance(0f, 0f);
        }

        private void OnTyping() => lastTyping = Time.unscaledTime;
        private void OnSubmitted() => lastTyping = -99f;

        private void BuildProps()
        {
            notepad = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            notepad.name = "CounselorNotepad";
            Destroy(notepad.GetComponent<Collider>());
            notepad.SetParent(transform, false);
            notepad.localScale = new Vector3(0.22f, 0.012f, 0.30f);
            Material paper = new Material(Shader.Find("Standard")) { color = new Color(0.95f, 0.93f, 0.87f) };
            paper.SetFloat("_Glossiness", 0.12f);
            notepad.GetComponent<Renderer>().sharedMaterial = paper;
            Transform cover = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            cover.name = "Cover";
            Destroy(cover.GetComponent<Collider>());
            cover.SetParent(notepad, false);
            cover.localPosition = new Vector3(0f, -0.6f, 0f);
            cover.localScale = new Vector3(1.04f, 0.5f, 1.03f);
            Material board = new Material(Shader.Find("Standard")) { color = new Color(0.23f, 0.29f, 0.26f) };
            cover.GetComponent<Renderer>().sharedMaterial = board;
            for (int i = 0; i < 7; i++)
            {
                Transform ruled = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                ruled.name = "Line";
                Destroy(ruled.GetComponent<Collider>());
                ruled.SetParent(notepad, false);
                ruled.localPosition = new Vector3(0.02f, 0.52f, 0.3f - i * 0.1f);
                ruled.localScale = new Vector3(i % 3 == 2 ? 0.55f : 0.8f, 0.05f, 0.012f);
                ruled.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.36f, 0.42f, 0.5f) };
            }

            pen = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            pen.name = "CounselorPen";
            Destroy(pen.GetComponent<Collider>());
            pen.SetParent(transform, false);
            pen.localScale = new Vector3(0.009f, 0.07f, 0.009f);
            pen.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.08f, 0.09f, 0.1f) };
        }

        private void LateUpdate()
        {
            if (animator == null || viewCamera == null) return;
            if (settleFrames > 0) { settleFrames--; return; }
            if (!aligned) Align();

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);

            // Right leg crossed over the left: knee over knee, shin hanging across the left shin.
            Vector3 leftKnee = leftLowerLeg.position;
            float thigh = Vector3.Distance(leftUpperLeg.position, leftLowerLeg.position);
            Vector3 rightAnkle = leftKnee - right * (0.14f * thigh / 0.45f) + forward * 0.12f - up * (0.34f * thigh / 0.45f);
            Vector3 rightKneePole = leftKnee + up * 0.25f + forward * 0.25f + right * 0.05f;
            HumanTwoBoneIk.Solve(rightUpperLeg, rightLowerLeg, rightFoot, rightAnkle, rightKneePole);

            // Notepad on the crossed knee, tilted toward the eyes.
            Vector3 kneeTop = rightLowerLeg.position + up * 0.07f;
            Vector3 padCentre = kneeTop - forward * 0.08f + right * 0.02f + up * 0.03f;
            Vector3 toEye = (viewCamera.transform.position - padCentre).normalized;
            Quaternion padRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, toEye).normalized, Vector3.Slerp(up, toEye, 0.35f));
            notepad.SetPositionAndRotation(padCentre, padRotation);

            Vector3 padUp = padRotation * Vector3.up;
            Vector3 padRight = padRotation * Vector3.right;
            Vector3 padForward = padRotation * Vector3.forward;

            // Left hand steadies the pad's left edge; right hand writes across the page.
            bool typing = Time.unscaledTime - lastTyping < glanceHoldSeconds;
            if (typing) writePhase += Time.unscaledDeltaTime * 9f;
            Vector3 write = typing
                ? padRight * (Mathf.Sin(writePhase * 0.23f) * 0.05f) + padForward * (Mathf.Sin(writePhase) * 0.008f - 0.02f) + padUp * (Mathf.Abs(Mathf.Sin(writePhase * 1.7f)) * 0.006f)
                : Vector3.zero;
            Vector3 leftTarget = padCentre - padRight * 0.13f + padForward * 0.02f + padUp * 0.02f;
            Vector3 rightTarget = padCentre + padRight * 0.04f - padForward * 0.05f + padUp * 0.035f + write;
            HumanTwoBoneIk.Solve(leftUpperArm, leftLowerArm, leftHand, leftTarget, leftUpperArm.position - right * 0.25f - up * 0.25f + forward * 0.05f);
            HumanTwoBoneIk.Solve(rightUpperArm, rightLowerArm, rightHand, rightTarget, rightUpperArm.position + right * 0.25f - up * 0.25f + forward * 0.02f);
            OrientHand(leftHand, leftLowerArm, padUp);
            OrientHand(rightHand, rightLowerArm, padUp);

            // Pen held between the right hand's fingers, tip on the page.
            Vector3 penTip = rightTarget + padForward * 0.06f - padUp * 0.03f;
            Vector3 penTop = rightHand.position + up * 0.05f - forward * 0.01f;
            pen.position = Vector3.Lerp(penTip, penTop, 0.5f);
            pen.rotation = Quaternion.FromToRotation(Vector3.up, (penTop - penTip).normalized);
            pen.localScale = new Vector3(0.009f, Vector3.Distance(penTip, penTop) * 0.5f, 0.009f);

            ApplyHead();
            UpdateGlance(typing);
        }

        private static void OrientHand(Transform hand, Transform forearm, Vector3 surfaceUp)
        {
            // Keep the hand in line with the forearm but let the palm face the page.
            Vector3 along = (hand.position - forearm.position).normalized;
            Quaternion aim = Quaternion.LookRotation(along, surfaceUp);
            Quaternion current = Quaternion.LookRotation(along, hand.rotation * Vector3.up);
            hand.rotation = Quaternion.Slerp(Quaternion.identity, aim * Quaternion.Inverse(current), 0.35f) * hand.rotation;
        }

        private void Align()
        {
            aligned = true;
            Transform leftEye = Bone(HumanBodyBones.LeftEye);
            Transform rightEye = Bone(HumanBodyBones.RightEye);
            Vector3 eyes = leftEye != null && rightEye != null ? Vector3.Lerp(leftEye.position, rightEye.position, 0.5f) : head.position + Vector3.up * 0.08f;
            // The camera is the counselor's eyes: move the seated body so its eyes sit just behind it.
            Vector3 camera = viewCamera.transform.position - Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized * 0.05f;
            transform.position += camera - eyes;
            if (chair != null)
            {
                Vector3 seat = hips.position - transform.forward * 0.06f;
                chair.position = new Vector3(seat.x, chair.position.y, seat.z);
            }
        }

        private void ApplyHead()
        {
            if (head == null) return;
            head.localScale = headHidden ? headScale * 0.0001f : headScale;
        }

        private void UpdateGlance(bool typing)
        {
            if (cameraZoom == null) return;
            bool allowed = typing && cameraZoom.TargetFieldOfView > CounselingCameraZoom.CloseFieldOfView + 1f;
            glance = Mathf.SmoothDamp(glance, allowed ? 1f : 0f, ref glanceVelocity, allowed ? 0.35f : 0.28f, Mathf.Infinity, Time.unscaledDeltaTime);
            cameraZoom.SetGlance(glance * glancePitch, glance * glanceFieldOfView);
        }
    }
}
