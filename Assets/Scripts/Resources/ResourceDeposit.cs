using System.Collections.Generic;
using UnityEngine;

namespace RTSokak.Resources
{
    /// <summary>
    /// Описывает залежь ресурсов (лес или месторождение руды)
    /// </summary>
    [System.Serializable]
    public class ResourceDeposit
    {
        public ResourceType type;
        public Vector2Int centerPosition; // Центр залежи на grid
        public int minSize = 5;  // Минимальный размер залежи
        public int maxSize = 15; // Максимальный размер залежи
        public float density = 0.6f; // Плотность размещения (0-1)

        /// <summary>
        /// Генерирует позиции ресурсов в этой залежи
        /// </summary>
        public List<Vector2Int> GeneratePositions(System.Random random)
        {
            List<Vector2Int> positions = new List<Vector2Int>();
            
            int size = random.Next(minSize, maxSize + 1);
            int radius = size / 2;

            // Генерируем ресурсы в круге вокруг центра
            for (int x = -radius; x <= radius; x++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    // Проверяем что точка внутри круга
                    float distance = Mathf.Sqrt(x * x + z * z);
                    if (distance <= radius)
                    {
                        // Используем плотность для вероятности размещения
                        if (random.NextDouble() < density)
                        {
                            Vector2Int pos = new Vector2Int(
                                centerPosition.x + x,
                                centerPosition.y + z
                            );
                            positions.Add(pos);
                        }
                    }
                }
            }

            return positions;
        }
    }

    /// <summary>
    /// Конфигурация генерации ресурсов
    /// </summary>
    [System.Serializable]
    public class ResourceGenerationConfig
    {
        [Header("World Settings")]
        public int worldSize = 100; // Размер мира в grid координатах
        
        [Header("Forest Settings")]
        [Tooltip("Number of forest deposits. With maxForestSize=1 → that many trees. Large size explodes totals.")]
        public int forestCount = 5;
        public int minForestSize = 8;
        public int maxForestSize = 20;
        [Range(0f, 1f)]
        public float forestDensity = 0.7f;

        [Header("Ore Deposit Settings")]
        [Tooltip("Number of ore deposits. Keep maxOreSize small if count is high (100+).")]
        public int oreDepositCount = 3;
        public int minOreSize = 5;
        public int maxOreSize = 12;
        [Range(0f, 1f)]
        public float oreDensity = 0.5f;

        [Header("Prefab Settings")]
        public GameObject treePrefab;
        public float treeYOffset = 0f;
        
        public GameObject orePrefab;
        public float oreYOffset = 0f;
    }
}
