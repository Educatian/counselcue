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
                    if (IsActorCoreSkin(material))
                    {
                        // Warm, lifted skin: a slight warm tint, softer cavity occlusion, less oily
                        // sheen and a faint warm self-glow from the albedo standing in for the
                        // subsurface scattering the Standard shader lacks.
                        material.SetColor("_Color", new Color(1.07f, 1.0f, 0.95f));
                        SetFloat(material, "_OcclusionStrength", 0.5f);
                        SetFloat(material, "_GlossMapScale", 0.55f);
                        material.SetTexture("_EmissionMap", material.mainTexture);
                        material.SetColor("_EmissionColor", new Color(0.11f, 0.075f, 0.055f));
                        material.EnableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                    else if (materialName.Contains("head"))
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

        private static bool IsActorCoreSkin(Material material)
        {
            return material.HasProperty("_MainTex") && material.mainTexture != null &&
                   material.mainTexture.name == "Albedo" && material.name.StartsWith("M_", System.StringComparison.Ordinal);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
