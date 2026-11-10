namespace RTSokak.Buildings
{
    /// <summary>Общий контракт очереди тренировки (столица / казарма).</summary>
    public interface IUnitTrainingQueue
    {
        int QueueCount { get; }
        float GetSlotProgress(int index);
        int GetSlotPercent(int index);
        event System.Action OnQueueChanged;
        void RequestCancelSlot(int index);
    }
}
