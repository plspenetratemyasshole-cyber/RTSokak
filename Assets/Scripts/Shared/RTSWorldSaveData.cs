using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTSokak.Shared
{
    [Serializable]
    public class RTSWorldSaveData
    {
        public int version = 2;
        public int worldSeed;
        public float timeOfDay = 0.25f; // Время суток (0.0 - 1.0)
        public List<PlayerSaveData> players = new List<PlayerSaveData>();
        public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
        public List<UnitSaveData> units = new List<UnitSaveData>();
        public List<ResourceSaveData> resources = new List<ResourceSaveData>();
        public List<GardenBedSaveData> gardenBeds = new List<GardenBedSaveData>();
    }

    [Serializable]
    public class PlayerSaveData
    {
        public int playerIndex;
        public string ipAddress;      // IP адрес клиента для переподключения (legacy)
        public string nickname;       // Ник игрока из аккаунта (основной идентификатор)
        public string accountEmail;   // Email аккаунта для дополнительной проверки
        public int wood;
        public int ore;
        public int food;
        public int currentPopulation;
        public int maxPopulation;
        public bool hasCapital;
        /// <summary>Мэр проиграл (столица/армия уничтожены) — при входе сразу UnitWaiting.</summary>
        public bool mayorDefeated;

        // Камера игрока (в сейве мира — переживает рестарт сервера)
        public bool hasCameraView;
        public float cameraX;
        public float cameraY;
        public float cameraZ;
        public float cameraZoom = 20f;
        public float cameraYaw;
    }

    [Serializable]
    public class BuildingSaveData
    {
        public int buildingType;
        public int ownerPlayerIndex;
        public string ownerNickname; // главный идентификатор владельца
        public Vector3 finalPosition;
        public float buildProgress;
        public bool isComplete;
        public float buryDepth;
        public float buildDuration;
        public int spentWood;
        public int spentOre;
        public int spentFood;
        public int populationProvided;
        public bool populationGranted;
        public int gridOriginX;
        public int gridOriginZ;
        public int gridSizeX;
        public int gridSizeZ;
        public int capitalQueueCount;
        public float capitalActiveProgress;
        public int barracksQueueCount;
        public float barracksActiveProgress;
    }

    [Serializable]
    public class UnitSaveData
    {
        public string unitKind; // Worker / Warrior / Archer
        public int ownerPlayerIndex;
        public string ownerNickname; // главный идентификатор владельца
        public Vector3 position;
        public Vector3 eulerAngles;
        public int currentHealth;
        public int maxHealth;

        // Inventory (Worker)
        public int inventoryWood;
        public int inventoryOre;
        public int inventoryFood;

        // Movement
        public Vector3 destination;
        public bool hasDestination;

        // Worker task
        public int workerState; // WorkerState
        public int profession;
        public int harvestResourceType;
        public int harvestGridX = int.MinValue;
        public int harvestGridZ = int.MinValue;
        public Vector3 lastHarvestPoint;
        public bool resumeHarvestAfterDeposit;
        public int resumeHarvestType;
        public bool resumeFarmingAfterDeposit;
        public bool useLastHarvestPointForSearch;

        // Building / deposit target
        public Vector3 targetBuildingPos;
        public int targetBuildingType = -1;

        // Farming
        public Vector3 targetGardenPos;
        public int farmingPlotIndex = -1;
        public bool farmingHarvestIntent;
        public Vector3 farmingNavDestination;

        // Combat (any RTSUnit)
        public int combatState;
        public Vector3 attackTargetPos;
        public string attackTargetKind;
        public int attackTargetOwnerIndex = -1;
    }

    [Serializable]
    public class ResourceSaveData
    {
        public int resourceType;
        public int gridX;
        public int gridZ;
        public int amount;
        public Vector3 position;
        public Vector3 eulerAngles;
    }

    [Serializable]
    public class GardenBedSaveData
    {
        public int buildingNetIdHint;
        public Vector3 buildingPosition;
        public string plotStatesJson;
    }
}
