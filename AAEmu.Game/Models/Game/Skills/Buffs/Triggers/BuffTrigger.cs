﻿using System;

using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Units;

using NLog;

namespace AAEmu.Game.Models.Game.Skills.Buffs.Triggers;

public class BuffTrigger
{
    protected static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    protected Buff _buff;
    protected readonly BaseUnit _owner;
    public BuffTriggerTemplate Template { get; set; }
    public virtual void Execute(object sender, EventArgs eventArgs)
    {
        var args = eventArgs as OnTimeoutArgs;
        Logger.Trace("Buff[{0}] {1} executed. Applying {2}[{3}]!", _buff?.Template?.BuffId, GetType().Name, Template.Effect.GetType().Name, Template.Effect.Id);
        //Template.Effect.Apply()

        if (_owner is not Unit) // if doodads
        {
            //Logger.Warn("Owner is a Doodad");
            var target0 = _buff?.Owner;
            var source0 = _buff?.Owner;

            if (Template.UseOriginalSource)
            {
                source0 = _buff?.Caster;
            }

            if (Template.EffectOnSource)
            {
                target0 = source0;
            }

            if (Template.TargetBuffTagId != 0)
            {
                if (target0 != null && !target0.Buffs.CheckBuffTag(Template.TargetBuffTagId))
                    return;
            }
            if (Template.TargetNoBuffTagId != 0)
            {
                if (target0 != null && target0.Buffs.CheckBuffTag(Template.TargetNoBuffTagId))
                    return;
            }

            if (target0 == null) { return; }
            Template.Effect.Apply(source0, new SkillCasterUnit(_owner.ObjId), target0, new SkillCastUnitTarget(target0.ObjId), new CastBuff(_buff),
                new EffectSource(_buff?.Skill), // TODO : EffectSource Type trigger 
                null, DateTime.UtcNow);
            return;
        }

        //Logger.Warn("Owner is a Unit");
        var target = _buff?.Owner;
        var source = (Unit)_buff?.Owner;

        if (Template.UseOriginalSource)
        {
            source = _buff?.Caster;
        }

        if (Template.EffectOnSource)
        {
            target = source;
        }

        if (Template.TargetBuffTagId != 0)
        {
            if (target != null && !target.Buffs.CheckBuffTag(Template.TargetBuffTagId))
                return;
        }
        if (Template.TargetNoBuffTagId != 0)
        {
            if (target != null && target.Buffs.CheckBuffTag(Template.TargetNoBuffTagId))
                return;
        }

        if (target == null) { return; }
        Template.Effect.Apply(source, new SkillCasterUnit(_owner.ObjId), target, new SkillCastUnitTarget(target.ObjId), new CastBuff(_buff),
            new EffectSource(_buff?.Skill), // TODO : EffectSource Type trigger 
            null, DateTime.UtcNow);
    }

    public BuffTrigger(Buff buff, BuffTriggerTemplate template)
    {
        _buff = buff;
        _owner = _buff.Owner;
        Template = template;
    }
}
