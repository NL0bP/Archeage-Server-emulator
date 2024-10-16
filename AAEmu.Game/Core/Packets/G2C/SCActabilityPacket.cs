﻿using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCActabilityPacket : GamePacket
{
    private readonly bool _last;
    private readonly Actability[] _actabilities;

    public SCActabilityPacket(bool last, Actability[] actabilities) : base(SCOffsets.SCActabilityPacket, 5)
    {
        _last = last;
        _actabilities = actabilities;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(_last);
        stream.Write((byte)_actabilities.Length); // TODO max count 100
        foreach (var actability in _actabilities)
        {
            stream.WritePisc(actability.Id, actability.Point); // pish (2)
            stream.Write(actability.Step);
        }

        return stream;
    }
}
