using UnityEngine;

namespace RTSokak.Terrain
{
    /// <summary>
    /// Простой скрипт для визуализации травы
    /// Добавь этот компонент на пустой GameObject и он создаст простую модель травы
    /// </summary>
    public class SimpleGrass : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private Color grassColor = new Color(0.3f, 0.7f, 0.3f);
        [SerializeField] private float height = 0.5f;
        [SerializeField] private float width = 0.3f;

        private void Start()
        {
            CreateGrassMesh();
        }

        private void CreateGrassMesh()
        {
            // Создаём простую крестообразную модель травы (2 плоскости)
            GameObject meshObj = new GameObject("GrassMesh");
            meshObj.transform.SetParent(transform);
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localRotation = Quaternion.identity;

            MeshFilter meshFilter = meshObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = meshObj.AddComponent<MeshRenderer>();

            // Создаём mesh
            Mesh mesh = new Mesh();
            
            // 2 плоскости крест-накрест (8 вершин)
            Vector3[] vertices = new Vector3[]
            {
                // Первая плоскость
                new Vector3(-width/2, 0, 0), new Vector3(width/2, 0, 0),
                new Vector3(-width/2, height, 0), new Vector3(width/2, height, 0),
                
                // Вторая плоскость (перпендикулярно)
                new Vector3(0, 0, -width/2), new Vector3(0, 0, width/2),
                new Vector3(0, height, -width/2), new Vector3(0, height, width/2)
            };

            // UV координаты
            Vector2[] uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1),
                
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1)
            };

            // Треугольники (2 плоскости, по 2 треугольника каждая, двусторонние)
            int[] triangles = new int[]
            {
                // Первая плоскость (передняя сторона)
                0, 2, 1,  1, 2, 3,
                // Первая плоскость (задняя сторона)
                1, 2, 0,  3, 2, 1,
                
                // Вторая плоскость (передняя сторона)
                4, 6, 5,  5, 6, 7,
                // Вторая плоскость (задняя сторона)
                5, 6, 4,  7, 6, 5
            };

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;

            // Создаём простой материал
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = grassColor;
            mat.SetFloat("_Glossiness", 0.1f); // Матовая поверхность
            meshRenderer.material = mat;
        }
    }
}
