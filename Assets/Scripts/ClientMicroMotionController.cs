using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ClientMicroMotionController : MonoBehaviour
    {
        private Animator animator;
        private Transform chest;
        private Transform head;
        private float phase;
        private float nextShift;
        private Vector2 headTarget;
        private Vector2 headCurrent;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            phase = Random.Range(0f, Mathf.PI * 2f);
            nextShift = Random.Range(3.5f, 6.5f);
        }

        private void LateUpdate()
        {
            float time = Time.time + phase;
            float breath = Mathf.Sin(time * 1.18f);
            if (chest != null) chest.localRotation *= Quaternion.Euler(breath * 0.28f, 0f, 0f);

            nextShift -= Time.deltaTime;
            if (nextShift <= 0f)
            {
                headTarget = new Vector2(Random.Range(-0.7f, 0.7f), Random.Range(-1.1f, 1.1f));
                nextShift = Random.Range(3.8f, 7.2f);
            }
            headCurrent = Vector2.Lerp(headCurrent, headTarget, 1f - Mathf.Exp(-Time.deltaTime * 0.65f));
            if (head != null) head.localRotation *= Quaternion.Euler(headCurrent.x + breath * 0.10f, headCurrent.y, 0f);
        }
    }
}
