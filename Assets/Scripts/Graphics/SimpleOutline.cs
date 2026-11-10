using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RTSokak.Graphics
{
    /// <summary>
    /// Outline эффект для Built-in Render Pipeline (QuickOutline).
    /// </summary>
    [DisallowMultipleComponent]
    public class SimpleOutline : MonoBehaviour
    {
        private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

        public enum Mode
        {
            OutlineAll,
            OutlineVisible,
            OutlineHidden,
            OutlineAndSilhouette,
            SilhouetteOnly
        }

        [Header("Outline Settings")]
        [SerializeField] private Mode outlineMode = Mode.OutlineAll;
        [SerializeField] private Color outlineColor = Color.green;
        [SerializeField, Range(0f, 10f)] private float outlineWidth = 2f;

        [Header("Optional")]
        [SerializeField, Tooltip("Precompute: вычисления выполняются в редакторе и сохраняются с объектом")]
        private bool precomputeOutline = false;

        [SerializeField, HideInInspector]
        private List<Mesh> bakeKeys = new List<Mesh>();

        [SerializeField, HideInInspector]
        private List<ListVector3> bakeValues = new List<ListVector3>();

        private Renderer[] renderers;
        private Material outlineMaskMaterial;
        private Material outlineFillMaterial;
        private bool needsUpdate;

        [Serializable]
        private class ListVector3
        {
            public List<Vector3> data;
        }

        // Свойства для совместимости с Worker.cs
        public Color OutlineColor
        {
            get { return outlineColor; }
            set
            {
                outlineColor = value;
                needsUpdate = true;
                if (outlineFillMaterial != null)
                    outlineFillMaterial.SetColor("_OutlineColor", outlineColor);
            }
        }

        public float OutlineWidth
        {
            get { return outlineWidth; }
            set
            {
                outlineWidth = value;
                needsUpdate = true;
                if (outlineFillMaterial != null)
                    outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
            }
        }

        public Mode OutlineMode
        {
            get { return outlineMode; }
            set
            {
                outlineMode = value;
                needsUpdate = true;
                UpdateMaterialProperties();
            }
        }

        private void Awake()
        {
            // Mesh/Skinned only — skip LineRenderer etc.
            renderers = GetComponentsInChildren<Renderer>()
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer)
                .ToArray();

            // Загружаем или создаём материалы для outline
            LoadOutlineMaterials();

            // Загружаем или генерируем сглаженные нормали
            LoadSmoothNormals();

            // Применяем свойства материалов
            needsUpdate = true;
        }

        private void LoadOutlineMaterials()
        {
            // Built-in QuickOutline shaders (ortho preferred for RTS camera)
            Shader maskShader = Shader.Find("Custom/Outline Mask");
            Shader fillShader = Shader.Find("Custom/Outline Fill Orthographic")
                ?? Shader.Find("Custom/Outline Fill");

            if (maskShader == null || fillShader == null)
            {
                UnityEngine.Debug.LogError(
                    "[SimpleOutline] Outline shaders missing. Expected Custom/Outline Mask + Custom/Outline Fill.");
                maskShader = maskShader ?? Shader.Find("Hidden/InternalErrorShader");
                fillShader = fillShader ?? Shader.Find("Hidden/InternalErrorShader");
            }

            outlineMaskMaterial = new Material(maskShader);
            outlineFillMaterial = new Material(fillShader);

            outlineMaskMaterial.name = "OutlineMask (Instance)";
            outlineFillMaterial.name = "OutlineFill (Instance)";

            outlineMaskMaterial.renderQueue = 3000;
            outlineFillMaterial.renderQueue = 3001;
        }

        private void OnEnable()
        {
            UpdateMaterialProperties();

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials.ToList();
                materials.RemoveAll(m => m == outlineMaskMaterial || (m != null && m.name.StartsWith("OutlineMask")));
                materials.RemoveAll(m => m == outlineFillMaterial || (m != null && m.name.StartsWith("OutlineFill")));
                materials.Add(outlineMaskMaterial);
                materials.Add(outlineFillMaterial);
                renderer.sharedMaterials = materials.ToArray();
            }
        }

        private void OnDisable()
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials.ToList();
                materials.RemoveAll(m => m == outlineMaskMaterial || (m != null && m.name.StartsWith("OutlineMask")));
                materials.RemoveAll(m => m == outlineFillMaterial || (m != null && m.name.StartsWith("OutlineFill")));
                renderer.sharedMaterials = materials.ToArray();
            }
        }

        private void Update()
        {
            if (needsUpdate)
            {
                needsUpdate = false;
                UpdateMaterialProperties();
            }
        }

        private void OnDestroy()
        {
            Destroy(outlineMaskMaterial);
            Destroy(outlineFillMaterial);
        }

        private void LoadSmoothNormals()
        {
            // Получаем или генерируем сглаженные нормали
            foreach (var meshFilter in GetComponentsInChildren<MeshFilter>())
            {
                // Пропускаем если уже обработан
                if (!registeredMeshes.Add(meshFilter.sharedMesh))
                {
                    continue;
                }

                // Получаем или генерируем сглаженные нормали
                var index = bakeKeys.IndexOf(meshFilter.sharedMesh);
                var smoothNormals = (index >= 0) ? bakeValues[index].data : SmoothNormals(meshFilter.sharedMesh);

                // Сохраняем сглаженные нормали в UV3
                meshFilter.sharedMesh.SetUVs(3, smoothNormals);

                // Комбинируем submeshes
                var renderer = meshFilter.GetComponent<Renderer>();
                if (renderer != null)
                {
                    CombineSubmeshes(meshFilter.sharedMesh, renderer.sharedMaterials);
                }
            }

            // Обрабатываем SkinnedMeshRenderer
            foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!registeredMeshes.Add(skinnedMeshRenderer.sharedMesh))
                {
                    continue;
                }

                // Очищаем UV4
                skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];

                // Комбинируем submeshes
                CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
            }
        }

        private List<Vector3> SmoothNormals(Mesh mesh)
        {
            // Группируем вершины по позиции
            var groups = mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index))
                .GroupBy(pair => pair.Key);

            // Копируем нормали
            var smoothNormals = new List<Vector3>(mesh.normals);

            // Усредняем нормали для сгруппированных вершин
            foreach (var group in groups)
            {
                if (group.Count() == 1)
                {
                    continue;
                }

                // Вычисляем среднюю нормаль
                var smoothNormal = Vector3.zero;
                foreach (var pair in group)
                {
                    smoothNormal += smoothNormals[pair.Value];
                }
                smoothNormal.Normalize();

                // Присваиваем сглаженную нормаль каждой вершине
                foreach (var pair in group)
                {
                    smoothNormals[pair.Value] = smoothNormal;
                }
            }

            return smoothNormals;
        }

        private void CombineSubmeshes(Mesh mesh, Material[] materials)
        {
            // Пропускаем меши с одним submesh
            if (mesh.subMeshCount == 1)
            {
                return;
            }

            // Пропускаем если submesh count больше материалов
            if (mesh.subMeshCount > materials.Length)
            {
                return;
            }

            // Добавляем комбинированный submesh
            mesh.subMeshCount++;
            mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
        }

        private void UpdateMaterialProperties()
        {
            if (outlineMaskMaterial == null || outlineFillMaterial == null) return;

            // Устанавливаем цвет (поддержка разных шейдеров)
            if (outlineFillMaterial.HasProperty("_OutlineColor"))
            {
                outlineFillMaterial.SetColor("_OutlineColor", outlineColor);
            }
            if (outlineFillMaterial.HasProperty("_Color"))
            {
                outlineFillMaterial.SetColor("_Color", outlineColor);
            }

            // Применяем режим (только если шейдер поддерживает)
            bool hasZTest = outlineMaskMaterial.HasProperty("_ZTest") && outlineFillMaterial.HasProperty("_ZTest");
            bool hasOutlineWidth = outlineFillMaterial.HasProperty("_OutlineWidth");

            if (!hasZTest || !hasOutlineWidth)
            {
                // Простой fallback режим - просто показываем цвет
                return;
            }

            switch (outlineMode)
            {
                case Mode.OutlineAll:
                    outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                    break;

                case Mode.OutlineVisible:
                    outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                    outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                    break;

                case Mode.OutlineHidden:
                    outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                    outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                    break;

                case Mode.OutlineAndSilhouette:
                    outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                    outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                    outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                    break;

                case Mode.SilhouetteOnly:
                    outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                    outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                    outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                    break;
            }
        }
    }
}
