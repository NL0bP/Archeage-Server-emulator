using System;
using System.Collections.Generic;
using System.Numerics;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.AI;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Formulas;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;

using NLog;

namespace AAEmu.Game.Models.Game.NPChar
{
    public class Npc : Unit
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public uint TemplateId { get; set; }
        public NpcTemplate Template { get; set; }
        //public Item[] Equip { get; set; }
        public NpcSpawner Spawner { get; set; }
        public override UnitCustomModelParams ModelParams => Template.ModelParams;
        public override float Scale => Template.Scale;
        public override byte RaceGender => (byte)(16 * Template.Gender + Template.Race);
        public WorldPos Pos { get; set; }
        public Quaternion Rot { get; set; }
        public Vector3 Vel { get; set; }
        public Vector3 AngVel { get; set; }
        public DateTime SpawnTime { get; set; }

        #region Attributes

        public int Str
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Str);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Str))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }

                return res;
            }
        }

        public int Dex
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Dex);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Dex))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public int Sta
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Sta);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Sta))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public int Int
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Int);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Int))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public int Spi
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Spi);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Spi))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public int Fai
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Fai);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Fai))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int MaxHp
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.MaxHealth);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.MaxHealth))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int HpRegen
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.HealthRegen);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                res += Spi / 10;
                foreach (var bonus in GetBonuses(UnitAttribute.HealthRegen))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int PersistentHpRegen
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.PersistentHealthRegen);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.PersistentHealthRegen))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int MaxMp
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.MaxMana);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.MaxMana))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int MpRegen
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.ManaRegen);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                res += Spi / 10;
                foreach (var bonus in GetBonuses(UnitAttribute.ManaRegen))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int PersistentMpRegen
        {
            get
            {
                var formula =
                    FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.PersistentManaRegen);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.PersistentManaRegen))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int Armor
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.Armor);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.Armor))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public override int MagicResistance
        {
            get
            {
                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.MagicResist);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = (int)formula.Evaluate(parameters);
                foreach (var bonus in GetBonuses(UnitAttribute.MagicResist))
                {
                    if (bonus.Template.ModifierType == UnitModifierType.Percent)
                    {
                        res += (int)(res * bonus.Value / 100f);
                    }
                    else
                    {
                        res += bonus.Value;
                    }
                }
                return res;
            }
        }

        public int KillExp
        {
            get
            {
                if (Template.NoExp)
                {
                    return 0;
                }

                var formula = FormulaManager.Instance.GetUnitFormula(UnitOwnerType.Npc, UnitFormulaKind.KillExp);
                var parameters = new Dictionary<string, double>
                {
                    ["level"] = Level,
                    ["str"] = Str,
                    ["dex"] = Dex,
                    ["sta"] = Sta,
                    ["int"] = Int,
                    ["spi"] = Spi,
                    ["fai"] = Fai,
                    ["npc_template"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcTemplate, (byte)Template.NpcTemplateId),
                    ["npc_kind"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcKind, (byte)Template.NpcKindId),
                    ["npc_grade"] =
                    FormulaManager.Instance.GetUnitVariable(formula.Id, UnitFormulaVariableType.NpcGrade, (byte)Template.NpcGradeId)
                };
                var res = formula.Evaluate(parameters);
                res *= Template.ExpMultiplier;
                res += Template.ExpAdder;
                return (int)res;
            }
        }

        #endregion

        public Npc()
        {
            Name = "";
            Ai = new NpcAi(this, 100f); //Template.AggroLinkHelpDist);
            UnitType = BaseUnitType.Npc;
        }

        public override void DoDie(Unit killer)
        {
            base.DoDie(killer);

            if (killer is Character character)
            {
                character.AddExp(KillExp, true);
                character.Quests.OnKill(this);
            }

            Spawner?.DecreaseCount(this);
        }

        public override void BroadcastPacket(GamePacket packet, bool self)
        {
            foreach (var character in WorldManager.Instance.GetAround<Character>(this))
            {
                character.SendPacket(packet);
            }
        }

        public override void AddVisibleObject(Character character)
        {
            character.SendPacket(new SCUnitStatePacket(this));
            character.SendPacket(new SCUnitPointsPacket(ObjId, Hp, Mp, HighAbilityRsc));
        }

        public override void RemoveVisibleObject(Character character)
        {
            if (character.CurrentTarget != null && character.CurrentTarget == this)
            {
                character.CurrentTarget = null;
                character.SendPacket(new SCTargetChangedPacket(character.ObjId, 0));
            }

            character.SendPacket(new SCUnitsRemovedPacket(new[] { ObjId }));
        }
    }
}
