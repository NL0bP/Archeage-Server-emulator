using System.Collections.Generic;

using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Shipyard;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCUnitStatePacket : GamePacket
    {
        private readonly Unit _unit;
        private readonly BaseUnitType _baseUnitType;
        private UnitModelPostureType _unitModelPostureType;

        public SCUnitStatePacket(Unit unit) : base(SCOffsets.SCUnitStatePacket, 5)
        {
            _unit = unit;
            switch (_unit)
            {
                case Character _:
                    _baseUnitType = BaseUnitType.Character;
                    _unitModelPostureType = UnitModelPostureType.None;
                    break;
                case Npc npc:
                    {
                        _baseUnitType = BaseUnitType.Npc;
                        _unitModelPostureType = npc.Template.AnimActionId > 0 ? UnitModelPostureType.ActorModelState : UnitModelPostureType.None;
                        break;
                    }
                case Slave _:
                    _baseUnitType = BaseUnitType.Slave;
                    _unitModelPostureType = UnitModelPostureType.TurretState; // was TurretState = 8
                    break;
                case House _:
                    _baseUnitType = BaseUnitType.Housing;
                    _unitModelPostureType = UnitModelPostureType.HouseState;
                    break;
                case Transfer _:
                    _baseUnitType = BaseUnitType.Transfer;
                    _unitModelPostureType = UnitModelPostureType.TurretState;
                    break;
                case Mount _:
                    _baseUnitType = BaseUnitType.Mate;
                    _unitModelPostureType = UnitModelPostureType.None;
                    break;
                case Shipyard _:
                    _baseUnitType = BaseUnitType.Shipyard;
                    _unitModelPostureType = UnitModelPostureType.None;
                    break;
            }
        }

        public override PacketStream Write(PacketStream stream)
        {
            #region NetUnit
            stream.WriteBc(_unit.ObjId);
            stream.Write(_unit.Name);

            #region BaseUnitType
            stream.Write((byte)_baseUnitType);
            switch (_baseUnitType)
            {
                case BaseUnitType.Character:
                    var character = (Character)_unit;
                    stream.Write(character.Id); // type(id)
                    stream.Write(0L);           // v?
                    break;
                case BaseUnitType.Npc:
                    var npc = (Npc)_unit;
                    stream.WriteBc(npc.ObjId);    // objId
                    stream.Write(npc.TemplateId); // npc templateId
                    stream.Write(0u);             // type(id)
                    stream.Write((byte)0);        // clientDriven
                    break;
                case BaseUnitType.Slave:
                    var slave = (Slave)_unit;
                    stream.Write(slave.Id);             // Id ? slave.Id
                    stream.Write(slave.TlId);           // tl
                    stream.Write(slave.TemplateId);     // templateId
                    stream.Write(slave.Summoner.ObjId); // ownerId ? slave.Summoner.ObjId
                    break;
                case BaseUnitType.Housing:
                    var house = (House)_unit;
                    var buildStep = house.CurrentStep == -1
                        ? 0
                        : -house.Template.BuildSteps.Count + house.CurrentStep;

                    stream.Write(house.TlId);       // tl
                    stream.Write(house.TemplateId); // templateId
                    stream.Write((short)buildStep); // buildstep
                    break;
                case BaseUnitType.Transfer:
                    var transfer = (Transfer)_unit;
                    stream.Write(transfer.TlId);       // tl
                    stream.Write(transfer.TemplateId); // templateId
                    break;
                case BaseUnitType.Mate:
                    var mount = (Mount)_unit;
                    stream.Write(mount.TlId);       // tl
                    stream.Write(mount.TemplateId); // teplateId
                    stream.Write(mount.OwnerId);    // characterId (masterId)
                    break;
                case BaseUnitType.Shipyard:
                    var shipyard = (Shipyard)_unit;
                    stream.Write(shipyard.Template.Id);         // type(id)
                    stream.Write(shipyard.Template.TemplateId); // type(id)
                    break;
            }
            #endregion BaseUnitType

            if (_unit.OwnerId > 0) // master
            {
                var name = NameManager.Instance.GetCharacterName(_unit.OwnerId);
                stream.Write(name ?? "");
            }
            else
            {
                stream.Write("");
            }

            stream.WritePositionBc(_unit.Position.X, _unit.Position.Y, _unit.Position.Z); // posXYZ
            stream.Write(_unit.Scale); // scale
            stream.Write(_unit.Level); // level
            stream.Write((byte)0);     // level for 3.0.3.0
            for (var i = 0; i < 4; i++)
            {
                stream.Write((sbyte)-1); // slot for 3.0.3.0
            }

            stream.Write(_unit.ModelId); // modelRef

            Inventory_Equip0(stream, _unit); // Equip character

            stream.Write(_unit.ModelParams); // CustomModel_3570

            stream.WriteBc(0);
            stream.Write(_unit.Hp * 100); // preciseHealth
            stream.Write(_unit.Mp * 100); // preciseMana

            #region AttachPoint1
            if (_unit is Transfer)
            {
                var transfer = (Transfer)_unit;
                if (transfer.BondingObjId != 0)
                {
                    stream.Write(transfer.AttachPointId);  // point
                    stream.WriteBc(transfer.BondingObjId); // point to the owner where to attach
                }
                else
                {
                    stream.Write((sbyte)-1);   // point
                }
            }
            else
            {
                stream.Write((sbyte)-1);   // point
            }
            #endregion AttachPoint1

            #region AttachPoint2
            switch (_unit)
            {
                case Character character2 when character2.Bonding == null:
                    stream.Write((sbyte)-1); // point
                    break;
                case Character character2:
                    stream.Write(character2.Bonding);
                    break;
                case Slave slave when slave.BondingObjId > 0:
                    stream.WriteBc(slave.BondingObjId);
                    break;
                case Slave _:
                case Transfer _:
                    stream.Write((sbyte)-1); // attachPoint
                    break;
                default:
                    stream.Write((sbyte)-1); // point
                    break;
            }
            #endregion AttachPoint2

            #region ModelPosture
            // TODO added that NPCs can be hunted to move their legs while moving, but if they sit or do anything they will just stand there
            if (_baseUnitType == BaseUnitType.Npc) // NPC
            {
                if (_unit is Npc npc)
                {
                    // TODO UnitModelPosture
                    if (npc.Faction.Id != 115 || npc.Faction.Id != 3) // npc.Faction.GuardHelp не агрессивные мобы
                    {
                        stream.Write((byte)_unitModelPostureType); // type // оставим это для того, чтобы NPC могли заниматься своими делами
                    }
                    else
                    {
                        stream.Write((byte)UnitModelPostureType.None); // type //для NPC на которых можно напасть и чтобы они шевелили ногами (для людей особенно)
                    }
                }
            }
            else // other
            {
                stream.Write((byte)_unitModelPostureType);
            }

            stream.Write(false); // isLooted

            switch (_unitModelPostureType)
            {
                case UnitModelPostureType.HouseState: // build
                    stream.Write(false); // flags Byte
                    break;
                case UnitModelPostureType.ActorModelState: // npc
                    var npc = _unit as Npc;
                    stream.Write(npc.Template.AnimActionId); // animId
                    stream.Write(true);                     // activate
                    break;
                case UnitModelPostureType.FarmfieldState:
                    stream.Write(0u);    // type(id)
                    stream.Write(0f);    // growRate
                    stream.Write(0);     // randomSeed
                    stream.Write(false); // flags Byte
                    break;
                case UnitModelPostureType.TurretState: // slave
                    stream.Write(0f);    // pitch
                    stream.Write(0f);    // yaw
                    break;
            }
            #endregion ModelPosture

            stream.Write(_unit.ActiveWeapon);

            switch (_unit)
            {
                case Character character:
                    {
                        stream.Write((byte)character.Skills.Skills.Count);       // learnedSkillCount
                        if (character.Skills.Skills.Count >= 0)
                        {
                            _log.Warn("Warning! character.learnedSkillCount = {0}", character.Skills.Skills.Count);
                        }
                        stream.Write((byte)character.Skills.PassiveBuffs.Count); // passiveBuffCount
                        if (character.Skills.Skills.Count >= 0)
                        {
                            _log.Warn("Warning! character.passiveBuffCount = {0}", character.Skills.PassiveBuffs.Count);
                        }
                        stream.Write(character.HighAbilityRsc);                  // highAbilityRsc

                        foreach (var skill in character.Skills.Skills.Values)
                        {
                            stream.WritePisc(skill.TemplateId);
                        }
                        foreach (var buff in character.Skills.PassiveBuffs.Values)
                        {
                            stream.WritePisc(buff.Id);
                        }
                        break;
                    }
                case Npc npc:
                    stream.Write((byte)1); // learnedSkillCount
                    stream.Write((byte)0); // passiveBuffCount
                    stream.Write(0);       // highAbilityRsc
                    stream.WritePisc(npc.Template.BaseSkillId);
                    break;
                default:
                    stream.Write((byte)0); // learnedSkillCount
                    stream.Write((byte)0); // passiveBuffCount
                    stream.Write(0);       // highAbilityRsc
                    break;
            }

            if (_baseUnitType == BaseUnitType.Housing)
            {
                stream.Write(Helpers.ConvertDirectionToRadian(_unit.Position.RotationZ)); // должно быть float
            }
            else
            {
                stream.Write(_unit.Position.RotationX);
                stream.Write(_unit.Position.RotationY);
                stream.Write(_unit.Position.RotationZ);
            }

            switch (_unit)
            {
                case Character unit:
                    stream.Write(unit.RaceGender);
                    break;
                case Npc npc:
                    stream.Write(npc.RaceGender);
                    break;
                default:
                    stream.Write(_unit.RaceGender);
                    break;
            }

            if (_unit is Character character4)
            {
                stream.WritePisc(0, 0, character4.Appellations.ActiveAppellation, 0);      // pisc
                stream.WritePisc(_unit.Faction?.Id ?? 0, _unit.Expedition?.Id ?? 0, 0, 0); // pisc
                stream.WritePisc(0, 0, 0, 0); // pisc
            }
            else
            {
                stream.WritePisc(0, 0, 0, 0); // TODO второе число больше нуля, что это за число?
                stream.WritePisc(_unit.Faction?.Id ?? 0, _unit.Expedition?.Id ?? 0, 0, 0); // pisc
                stream.WritePisc(0, 0, 0, 0); // pisc
            }

            if (_unit is Character character5)
            {
                var flags = new BitSet(16); // short

                if (character5.Invisible)
                {
                    flags.Set(5);
                }

                if (character5.IdleStatus)
                {
                    flags.Set(13);
                }

                //stream.WritePisc(0, 0); // очки чести полученные в PvP, кол-во убийств в PvP
                stream.Write(flags.ToByteArray()); // flags(ushort)
                /*
                * 0x01 - 8bit - режим боя
                * 0x04 - 6bit - невидимость?
                * 0x08 - 5bit - дуэль
                * 0x40 - 2bit - gmmode, дополнительно 7 байт
                * 0x80 - 1bit - дополнительно tl(ushort), tl(ushort), tl(ushort), tl(ushort)
                * 0x0100 - 16bit - дополнительно 3 байт (bc), firstHitterTeamId(uint)
                * 0x0400 - 14bit - надпись "Отсутсвует" под именем
                */
            }
            else if (_unit is Npc)
            {
                stream.Write((ushort)8192); // flags
            }
            else
            {
                stream.Write((ushort)0); // flags
            }

            if (_unit is Character character6)
            {
                #region read_Abilities_6300
                var activeAbilities = character6.Abilities.GetActiveAbilities();
                foreach (var ability in character6.Abilities.Values)
                {
                    stream.Write(ability.Exp);
                    stream.Write(ability.Order);
                }

                stream.Write((byte)activeAbilities.Count); // nActive
                foreach (var ability in activeAbilities)
                {
                    stream.Write((byte)ability); // active
                }
                #endregion read_Abilities_6300

                #region read_Exp_Order_6460
                foreach (var ability in character6.Abilities.Values)
                {
                    stream.Write(ability.Exp);
                    stream.Write(ability.Order);  // ability.Order
                    stream.Write(false);          // canNotLevelUp
                }

                byte nHighActive = 0;
                byte nActive = 0;
                stream.Write(nHighActive); // nHighActive
                stream.Write(nActive);     // nActive
                while (nHighActive > 0)
                {
                    while (nActive > 0)
                    {
                        stream.Write(0); // active
                        nActive--;
                    }
                    nHighActive--;
                }
                #endregion read_Exp_Order_6460

                stream.WriteBc(0);     // objId
                stream.Write((byte)0); // camp

                #region Stp
                stream.Write((byte)30);  // stp
                stream.Write((byte)60);  // stp
                stream.Write((byte)50);  // stp
                stream.Write((byte)0);   // stp
                stream.Write((byte)40);  // stp
                stream.Write((byte)100); // stp

                stream.Write((byte)7); // flags

                character6.VisualOptions.Write(stream, 0x20); // cosplay_visual
                #endregion Stp

                stream.Write(1); // premium

                #region Stats
                for (var i = 0; i < 5; i++)
                {
                    stream.Write(0); // stats
                }
                stream.Write(0); // extendMaxStats
                stream.Write(0); // applyExtendCount
                stream.Write(0); // applyNormalCount
                stream.Write(0); // applySpecialCount
                #endregion Stats

                stream.WritePisc(0, 0, 0, 0);
                stream.WritePisc(0, 0);
                stream.Write((byte)0); // accountPrivilege
            }
            #endregion NetUnit


            #region NetBuff

            // TODO: Fix the patron and auction house license buff issue
            //if (_unit is Character)
            //{
            //    if (!_unit.Effects.CheckBuff(8000011)) //TODO Wrong place
            //    {
            //        _unit.Effects.AddEffect(new Effect(_unit, _unit, SkillCaster.GetByType(EffectOriginType.Skill), SkillManager.Instance.GetBuffTemplate(8000011), null, System.DateTime.UtcNow));
            //    }

            //    if (!_unit.Effects.CheckBuff(8000012)) //TODO Wrong place
            //    {
            //        _unit.Effects.AddEffect(new Effect(_unit, _unit, SkillCaster.GetByType(EffectOriginType.Skill), SkillManager.Instance.GetBuffTemplate(8000012), null, System.DateTime.UtcNow));
            //    }
            //}

            var goodBuffs = new List<Effect>();
            var badBuffs = new List<Effect>();
            var hiddenBuffs = new List<Effect>();

            _unit.Effects.GetAllBuffs(goodBuffs, badBuffs, hiddenBuffs);

            stream.Write((byte)goodBuffs.Count); // TODO max 32
            foreach (var buff in goodBuffs)
            {
                WriteBuff(stream, buff);
            }

            stream.Write((byte)badBuffs.Count); // TODO max 24 for 1.2, 20 for 3.0.3.0
            foreach (var buff in badBuffs)
            {
                WriteBuff(stream, buff);
            }

            stream.Write((byte)hiddenBuffs.Count); // TODO max 24 for 1.2, 28 for 3.0.3.0
            foreach (var buff in hiddenBuffs)
            {
                WriteBuff(stream, buff);
            }
            #endregion NetBuff

            return stream;
        }

        private void WriteBuff(PacketStream stream, Effect buff)
        {
            stream.Write(buff.Index);        // Id
            stream.Write(buff.SkillCaster);  // skillCaster
            stream.Write(0);                 // type(id)
            stream.Write(buff.Caster.Level); // sourceLevel
            stream.Write(buff.AbLevel);      // sourceAbLevel
            stream.WritePisc(0, buff.GetTimeElapsed(), 0, 0u); // add in 3.0.3.0
            stream.WritePisc(buff.Template.BuffId, 1, 0, 0u);  // add in 3.0.3.0
        }

        private void Inventory_Equip0(PacketStream stream, Unit unit)
        {
            #region Inventory_Equip
            var index = 0;
            var validFlags = 0;
            if (unit is Character character1)
            {
                // calculate validFlags
                var items = character1.Inventory.Equipment.GetSlottedItemsList();
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        validFlags |= 1 << index;
                    }

                    index++;
                }
                stream.Write((uint)validFlags); // validFlags for 3.0.3.0
                var itemSlot = EquipmentItemSlot.Head;
                foreach (var item in items)
                {
                    if (item == null)
                    {
                        itemSlot++;
                        continue;
                    }
                    switch (itemSlot)
                    {
                        case EquipmentItemSlot.Head:
                        case EquipmentItemSlot.Neck:
                        case EquipmentItemSlot.Chest:
                        case EquipmentItemSlot.Waist:
                        case EquipmentItemSlot.Legs:
                        case EquipmentItemSlot.Hands:
                        case EquipmentItemSlot.Feet:
                        case EquipmentItemSlot.Arms:
                        case EquipmentItemSlot.Back:
                        case EquipmentItemSlot.Undershirt:
                        case EquipmentItemSlot.Underpants:
                        case EquipmentItemSlot.Mainhand:
                        case EquipmentItemSlot.Offhand:
                        case EquipmentItemSlot.Ranged:
                        case EquipmentItemSlot.Musical:
                        case EquipmentItemSlot.Cosplay:
                            stream.Write(item);
                            break;
                        case EquipmentItemSlot.Face:
                        case EquipmentItemSlot.Hair:
                        case EquipmentItemSlot.Glasses:
                        case EquipmentItemSlot.Horns:
                        case EquipmentItemSlot.Tail:
                        case EquipmentItemSlot.Body:
                        case EquipmentItemSlot.Beard:
                            stream.Write(item.TemplateId);
                            break;
                        case EquipmentItemSlot.Ear1:
                        case EquipmentItemSlot.Ear2:
                        case EquipmentItemSlot.Finger1:
                        case EquipmentItemSlot.Finger2:
                        case EquipmentItemSlot.Backpack:
                        case EquipmentItemSlot.Stabilizer:
                            break;
                    }
                    itemSlot++;
                }
            }
            else if (unit is Npc npc)
            {
                // calculate validFlags for 3.0.3.0
                for (var i = 0; i < npc.Equipment.GetSlottedItemsList().Count; i++)
                {
                    var item = npc.Equipment.GetItemBySlot(i);
                    if (item != null)
                    {
                        validFlags |= 1 << index;
                    }

                    index++;
                }
                stream.Write((uint)validFlags); // validFlags for 3.0.3.0
                var itemSlot = EquipmentItemSlot.Head;
                var items = npc.Equipment.GetSlottedItemsList();
                foreach (var item in items)
                {
                    if (item == null)
                    {
                        itemSlot++;
                        continue;
                    }
                    switch (itemSlot)
                    {
                        case EquipmentItemSlot.Head:
                        case EquipmentItemSlot.Neck:
                        case EquipmentItemSlot.Chest:
                        case EquipmentItemSlot.Waist:
                        case EquipmentItemSlot.Legs:
                        case EquipmentItemSlot.Hands:
                        case EquipmentItemSlot.Feet:
                        case EquipmentItemSlot.Arms:
                        case EquipmentItemSlot.Back:
                        case EquipmentItemSlot.Undershirt:
                        case EquipmentItemSlot.Underpants:
                        case EquipmentItemSlot.Mainhand:
                        case EquipmentItemSlot.Offhand:
                        case EquipmentItemSlot.Ranged:
                        case EquipmentItemSlot.Musical:
                            stream.Write(item.TemplateId);
                            stream.Write(0L);
                            stream.Write((byte)0);
                            break;
                        case EquipmentItemSlot.Cosplay:
                            stream.Write(item);
                            break;
                        case EquipmentItemSlot.Face:
                        case EquipmentItemSlot.Hair:
                        case EquipmentItemSlot.Glasses:
                        case EquipmentItemSlot.Horns:
                        case EquipmentItemSlot.Tail:
                        case EquipmentItemSlot.Body:
                        case EquipmentItemSlot.Beard:
                            stream.Write(item.TemplateId);
                            break;
                        case EquipmentItemSlot.Ear1:
                        case EquipmentItemSlot.Ear2:
                        case EquipmentItemSlot.Finger1:
                        case EquipmentItemSlot.Finger2:
                        case EquipmentItemSlot.Backpack:
                        case EquipmentItemSlot.Stabilizer:
                            break;
                    }
                    itemSlot++;
                }
            }
            else // for transfer and other
            {
                stream.Write(0u); // validFlags for 3.0.3.0
            }

            if (_unit is Character chrUnit)
            {
                index = 0;
                var ItemFlags = 0;
                var items = chrUnit.Inventory.Equipment.GetSlottedItemsList();
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        var v15 = (int)item.ItemFlags << index;
                        ++index;
                        ItemFlags |= v15;
                    }
                }
                stream.Write(ItemFlags); //  ItemFlags flags for 3.0.3.0
            }
            #endregion Inventory_Equip
        }

        private void Inventory_Equip(PacketStream stream, Unit unit)
        {
            #region Inventory_Equip

            var index = 0;
            var validFlags = 0;
            switch (unit)
            {
                case Character character:
                    {
                        // calculate validFlags
                        var items = character.Inventory.Equipment.GetSlottedItemsList();
                        foreach (var item in items)
                        {
                            if (item != null)
                            {
                                validFlags |= 1 << index;
                            }

                            index++;
                        }

                        stream.Write((uint)validFlags); // validFlags for 3.0.3.0
                        foreach (var item in items)
                        {
                            if (item != null)
                            {
                                stream.Write(item);
                            }
                        }

                        break;
                    }
                case Npc npc:
                    {
                        // calculate validFlags for 3.0.3.0
                        var items = npc.Equipment.GetSlottedItemsList();
                        foreach (var item in items)
                        {
                            if (item != null)
                            {
                                validFlags |= 1 << index;
                            }

                            index++;
                        }

                        stream.Write((uint)validFlags); // validFlags for 3.0.3.0

                        for (var i = 0; i < npc.Equipment.GetSlottedItemsList().Count; i++)
                        {
                            var item = npc.Equipment.GetItemBySlot(i);

                            if (item is BodyPart)
                            {
                                stream.Write(item.TemplateId);
                            }
                            else if (item != null)
                            {
                                if (i == 27) // Cosplay
                                {
                                    stream.Write(item);
                                }
                                else
                                {
                                    stream.Write(item.TemplateId);
                                    stream.Write(0L);
                                    stream.Write((byte)0);
                                }
                            }
                        }

                        break;
                    }
                // for transfer and other
                default:
                    stream.Write(0u); // validFlags for 3.0.3.0
                    break;
            }

            index = 0;
            validFlags = 0;
            if (_unit is Character chrUnit)
            {
                foreach (var item in chrUnit.Inventory.Equipment.GetSlottedItemsList())
                {
                    if (item == null) { continue; }

                    var _tmp = (int)item.ItemFlags << index;
                    ++index;
                    validFlags |= _tmp;
                }
            }
            stream.Write(validFlags); //  ItemFlags flags for 3.0.3.0

            #endregion Inventory_Equip
        }
    }
}
