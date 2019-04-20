﻿using System;
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Tasks.UnitMove;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSChangeTargetPacket : GamePacket
    {
        public CSChangeTargetPacket() : base(0x02c, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var targetId = stream.ReadBc();
            Connection
                    .ActiveChar
                    .CurrentTarget = targetId > 0 ? WorldManager.Instance.GetUnit(targetId) : null;

            Connection
                .ActiveChar
                .BroadcastPacket(
                    new SCTargetChangedPacket(Connection.ActiveChar.ObjId,
                        Connection.ActiveChar.CurrentTarget?.ObjId ?? 0), true);

            if (Connection.ActiveChar.CurrentTarget == null)
                return;
            if (Connection.ActiveChar.CurrentTarget is Npc npc) { 
                Connection.ActiveChar.SendMessage("ObjId: {0}, TemplateId: {1}", targetId, npc.TemplateId);

                TaskManager.Instance.Schedule(
                    new UnitMove(new Patrol(),Connection.ActiveChar, npc), TimeSpan.FromMilliseconds(365)
                 );
            }
            else if (Connection.ActiveChar.CurrentTarget is Character character)
                Connection.ActiveChar.SendMessage("ObjId: {0}, CharacterId: {1}", targetId, character.Id);

        }
    }
}
