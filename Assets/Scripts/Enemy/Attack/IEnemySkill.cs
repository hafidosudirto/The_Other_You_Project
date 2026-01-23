using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IEnemySkill
{
    float SkillRange { get; }
    bool IsActive { get; }
    void Trigger();
}

