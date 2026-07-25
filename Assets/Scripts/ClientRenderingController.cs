using UnityEngine;

namespace AdieLab.AffectCounsel
{
    [DisallowMultipleComponent]
    public sealed class ClientRenderingController : MonoBehaviour
    {
        public void ApplyReadableFaceMaterials()
        {
            if (!Application.isPlaying) return;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = materials[index];
                    string materialName = material.name.ToLowerInvariant();
                    if (materialName.Contains("head"))
                    {
                        SetFloat(material, "_Smoothness", 0.22f);
                        SetFloat(material, "_Glossiness", 0.22f);
                        SetFloat(material, "_BumpScale", 0.82f);
                    }
                    else if (materialName.Contains("eye") || renderer.name.ToLowerInvariant().Contains("eye"))
                    {
                        SetFloat(material, "_Smoothness", 0.78f);
                        SetFloat(material, "_Glossiness", 0.78f);
                    }
                }
                renderer.materials = materials;
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
