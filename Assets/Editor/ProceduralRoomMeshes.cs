using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AdieLab.AffectCounsel.Editor
{
    /// <summary>
    /// Small, WebGL-friendly procedural meshes for the counseling room: rounded boxes (soft
    /// furniture edges catch light the way upholstery and turned wood do) and folded curtains.
    /// Meshes are saved under Assets/Models/Generated and refilled in place on every build so
    /// their GUIDs, and the scene references to them, stay stable.
    /// </summary>
    internal static class ProceduralRoomMeshes
    {
        private const string Folder = "Assets/Models/Generated";

        /// <summary>
        /// Box with rounded edges and corners. UVs are planar per face in metres, so a material's
        /// tiling is "tiles per metre" regardless of the box size.
        /// </summary>
        public static Mesh RoundedBox(string name, Vector3 size, float radius, int segments = 4)
        {
            Vector3 half = size * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.98f);
            Vector3 inner = half - Vector3.one * r;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in faceNormals)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized;
                if (u.sqrMagnitude < 0.5f) u = Vector3.right;
                Vector3 v = Vector3.Cross(n, u).normalized;
                float hu = Mathf.Abs(Vector3.Dot(half, Abs(u))), hv = Mathf.Abs(Vector3.Dot(half, Abs(v)));
                float iu = Mathf.Abs(Vector3.Dot(inner, Abs(u))), iv = Mathf.Abs(Vector3.Dot(inner, Abs(v)));
                float hn = Mathf.Abs(Vector3.Dot(half, Abs(n)));
                float[] us = Stations(hu, iu, segments), vs = Stations(hv, iv, segments);
                int start = vertices.Count;
                for (int j = 0; j < vs.Length; j++)
                {
                    for (int i = 0; i < us.Length; i++)
                    {
                        Vector3 p = n * hn + u * us[i] + v * vs[j];
                        Vector3 clamped = new Vector3(
                            Mathf.Clamp(p.x, -inner.x, inner.x),
                            Mathf.Clamp(p.y, -inner.y, inner.y),
                            Mathf.Clamp(p.z, -inner.z, inner.z));
                        Vector3 dir = p - clamped;
                        Vector3 normal = dir.sqrMagnitude > 1e-10f ? dir.normalized : n;
                        Vector3 position = clamped + normal * r;
                        vertices.Add(position);
                        normals.Add(normal);
                        uvs.Add(new Vector2(Vector3.Dot(position, u), Vector3.Dot(position, v)));
                    }
                }
                int stride = us.Length;
                for (int j = 0; j < vs.Length - 1; j++)
                {
                    for (int i = 0; i < stride - 1; i++)
                    {
                        int a = start + j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                        AddOriented(triangles, vertices, a, b, d, n);
                        AddOriented(triangles, vertices, a, d, c, n);
                    }
                }
            }
            return Store(name, vertices, normals, uvs, triangles);
        }

        /// <summary>
        /// A hanging curtain with soft folds, facing -Z (toward the counselor camera), pivot at
        /// the top centre. UVs in metres.
        /// </summary>
        public static Mesh Curtain(string name, float width, float height, float foldWidth, float depth, int seed)
        {
            System.Random random = new System.Random(seed);
            int columns = Mathf.Max(8, Mathf.CeilToInt(width / 0.02f));
            int rows = 6;
            float[] phase = new float[columns + 1];
            float drift = (float)random.NextDouble() * 6.28f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int j = 0; j <= rows; j++)
            {
                float t = j / (float)rows;
                float y = -t * height;
                // Folds open up slightly toward the hem, as gathered fabric does.
                float amp = depth * Mathf.Lerp(0.8f, 1.15f, t);
                for (int i = 0; i <= columns; i++)
                {
                    float x = -width * 0.5f + width * i / columns;
                    float fold = Mathf.Sin((x / foldWidth) * Mathf.PI * 2f + drift) + 0.35f * Mathf.Sin((x / foldWidth) * Mathf.PI * 4.3f + drift * 1.7f);
                    float z = fold * amp * 0.5f;
                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2(x, y));
                }
            }
            int stride = columns + 1;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    int a = j * stride + i, b = a + 1, c = a + stride, d = c + 1;
                    AddOriented(triangles, vertices, a, b, d, Vector3.back);
                    AddOriented(triangles, vertices, a, d, c, Vector3.back);
                }
            }
            Mesh mesh = Store(name, vertices, null, uvs, triangles);
            return mesh;
        }

        private static float[] Stations(float half, float inner, int segments)
        {
            List<float> values = new List<float>();
            for (int s = 0; s <= segments; s++) values.Add(Mathf.Lerp(-half, -inner, s / (float)segments));
            if (inner > 1e-4f)
            {
                int middle = Mathf.Max(1, Mathf.CeilToInt(inner * 2f / 0.25f));
                for (int s = 1; s < middle; s++) values.Add(Mathf.Lerp(-inner, inner, s / (float)middle));
            }
            for (int s = 0; s <= segments; s++)
            {
                float value = Mathf.Lerp(inner, half, s / (float)segments);
                if (s == 0 && inner <= 1e-4f) continue;
                values.Add(value);
            }
            return values.ToArray();
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void AddOriented(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 outward)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(face, outward) >= 0f) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
            else { triangles.Add(a); triangles.Add(c); triangles.Add(b); }
        }

        private static Mesh Store(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Models")) AssetDatabase.CreateFolder("Assets", "Models");
                AssetDatabase.CreateFolder("Assets/Models", "Generated");
            }
            string path = $"{Folder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mesh == null;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            if (normals != null) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            // Lightmap UVs so the room can bake soft bounce light and contact occlusion.
            Unwrapping.GenerateSecondaryUVSet(mesh);
            if (created) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
