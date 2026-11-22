namespace Game.Gameplay.Zones
{
    /// <summary>Начался переход: экран загрузки сейчас закроет картинку. Хорошее место, чтобы закрыть окна.</summary>
    public readonly struct ZoneLeavingEvent
    {
        public readonly string FromZoneId;
        public readonly string ToZoneId;
        public ZoneLeavingEvent(string from, string to) { FromZoneId = from; ToZoneId = to; }
    }

    /// <summary>Новая сцена загружена, игрок уже стоит на точке прибытия (экран загрузки ещё гаснет).</summary>
    public readonly struct ZoneEnteredEvent
    {
        public readonly string ZoneId;
        public readonly string PreviousZoneId;
        public ZoneEnteredEvent(string zoneId, string previous) { ZoneId = zoneId; PreviousZoneId = previous; }
    }
}
