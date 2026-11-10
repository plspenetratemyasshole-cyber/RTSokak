namespace RTSokak.Network
{
    /// <summary>Роль локального / сетевого игрока в мире.</summary>
    public enum PlayerRole : byte
    {
        /// <summary>Ещё не выбрал Mayor / Unit.</summary>
        Choosing = 0,
        /// <summary>Мэр выбирает точку поселения (Enter).</summary>
        MayorPlacing = 1,
        /// <summary>Мэр управляет поселением (RTS).</summary>
        MayorPlaying = 2,
        /// <summary>Ждёт, пока мэр назначит юнита; спектатор.</summary>
        UnitWaiting = 3,
        /// <summary>Управляет конкретным юнитом (FPS).</summary>
        UnitPlaying = 4
    }
}
