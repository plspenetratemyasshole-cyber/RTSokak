using UnityEngine;

namespace RTSokak.Buildings
{
    public enum BuildingType
    {
        Capital = 0,
        House = 1,
        GardenBed = 2,
        Warehouse = 3,
        Barracks = 4
    }

    /// <summary>
    /// Данные здания — задаются в BuildingManager (Inspector).
    /// </summary>
    [System.Serializable]
    public class BuildingDefinition
    {
        public BuildingType type;
        public string displayName = "Building";

        [Tooltip("Сетевой префаб здания (Building + NetworkIdentity)")]
        public GameObject prefab;

        [Tooltip("Высота preview над землёй при размещении")]
        public float previewHeight = 1f;

        [Tooltip("Время полной постройки одним юнитом (секунды)")]
        public float buildDuration = 10f;

        [Tooltip("На сколько здание уходит под землю в начале стройки")]
        public float buryDepth = 3f;

        [Tooltip("На каком расстоянии от центра останавливается строитель")]
        public float approachDistance = 4f;

        [Tooltip("Размер на сетке по X (клетки мира, как у деревьев/руды)")]
        public int sizeX = 1;

        [Tooltip("Размер на сетке по Y (это ось Z мира)")]
        public int sizeY = 1;

        public int woodCost = 100;
        public int oreCost = 100;
        public int foodCost = 0;

        [Tooltip("Сколько населения даёт здание после завершения")]
        public int populationProvided = 5;

        [Tooltip("Максимальное HP здания (настраивается в BuildingManager)")]
        public int maxHealth = 1000;

        public Vector2Int Size => new Vector2Int(Mathf.Max(1, sizeX), Mathf.Max(1, sizeY));
    }
}
