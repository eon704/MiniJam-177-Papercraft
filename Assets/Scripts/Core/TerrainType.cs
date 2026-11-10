public enum TerrainType
{
    Empty,
    Default,
    Start,
    End,
    Water,
    Stone,
    Fire,
    Volcano,
    Lava,
    Ice,
    /// <summary>Кувшинка: островок на воде. Журавль и лягушка встают, лодка причаливает; тонет (становится водой), когда с неё уходят.</summary>
    LilyPad,
    /// <summary>Песок (Океан): суша, на которую приливом наступает вода.</summary>
    Sand
}