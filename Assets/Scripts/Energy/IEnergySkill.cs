public interface IEnergySkill
{
    float EnergyCost { get; }
    bool PayEnergyInSkillBase { get; }
}

public interface ISkillCooldownInfo
{
    bool HasCooldown { get; }
    float CooldownDuration { get; }
    float CooldownRemaining { get; }
    bool IsCooldownReady { get; }
}

public interface ISkillReadinessInfo
{
    bool IsSkillBusy { get; }
    bool IsSkillReady { get; }
}
