using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AdieLab.AffectCounsel
{
    /// <summary>
    /// Review-time probe of a client's face: how far the jaw bone sits from its bind pose and which
    /// blendshapes are non-zero. Used by <see cref="ReviewCaptureRunner"/> to diagnose open mouths.
    /// </summary>
    public static class FaceDiagnostics
    {
        public static void Append(string path, string label, Component root)
        {
            var text = new StringBuilder();
            text.AppendLine($"== {label}");
            Animator animator = root.GetComponentsInChildren<Animator>(false).FirstOrDefault(a => a.isHuman && a.isActiveAndEnabled);
            if (animator == null) { text.AppendLine("no active humanoid animator"); File.AppendAllText(path, text.ToString()); return; }
            text.AppendLine($"avatar={animator.avatar?.name} model={animator.gameObject.name}");
            Transform jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            text.AppendLine($"humanoid jaw bone={(jaw == null ? "<unmapped>" : jaw.name)}");
            foreach (SkinnedMeshRenderer renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                // Jaw and head in bind pose (from the skin's bind matrices) vs. now.
                if (jaw != null && head != null)
                {
                    int j = System.Array.IndexOf(renderer.bones, jaw);
                    int h = System.Array.IndexOf(renderer.bones, head);
                    if (j >= 0 && h >= 0 && j < mesh.bindposes.Length && h < mesh.bindposes.Length)
                    {
                        Matrix4x4 jawBind = renderer.transform.localToWorldMatrix * mesh.bindposes[j].inverse;
                        Matrix4x4 headBind = renderer.transform.localToWorldMatrix * mesh.bindposes[h].inverse;
                        Quaternion bindRelative = Quaternion.Inverse(headBind.rotation) * jawBind.rotation;
                        Quaternion nowRelative = Quaternion.Inverse(head.rotation) * jaw.rotation;
                        Vector3 euler = (Quaternion.Inverse(bindRelative) * nowRelative).eulerAngles;
                        text.AppendLine($"  [{renderer.name}] jaw vs bind: {Quaternion.Angle(bindRelative, nowRelative):F1} deg (euler {Wrap(euler.x):F1},{Wrap(euler.y):F1},{Wrap(euler.z):F1})");
                    }
                }
                var active = new List<string>();
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    float weight = renderer.GetBlendShapeWeight(i);
                    if (weight > 0.5f) active.Add($"{mesh.GetBlendShapeName(i)}={weight:F0}");
                }
                if (mesh.blendShapeCount > 0 && label == "case1")
                {
                    var names = new List<string>();
                    for (int i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
                    text.AppendLine($"  [{renderer.name}] all: {string.Join(" ", names)}");
                }
                if (mesh.blendShapeCount > 0)
                    text.AppendLine($"  [{renderer.name}] shapes={mesh.blendShapeCount} nonzero: {string.Join(", ", active.Take(24))}");
            }
            File.AppendAllText(path, text.ToString());
        }

        private static float Wrap(float angle) => angle > 180f ? angle - 360f : angle;
    }
}
