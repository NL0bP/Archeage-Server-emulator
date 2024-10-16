﻿using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSDismissExpeditionPacket : GamePacket
{
    public CSDismissExpeditionPacket() : base(CSOffsets.CSDismissExpeditionPacket, 5)
    {
    }

    public override void Read(PacketStream stream)
    {
        Logger.Debug("DismissExpedition");
        // Empty struct
        ExpeditionManager.Disband(Connection.ActiveChar);
    }
}
