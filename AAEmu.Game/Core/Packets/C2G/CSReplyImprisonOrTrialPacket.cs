using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSReplyImprisonOrTrialPacket : GamePacket
    {
        public CSReplyImprisonOrTrialPacket() : base(0x06d, 1)
        {
        }

        public override void Read(PacketStream stream)
        {
            var trial = stream.ReadBoolean();

            _log.Warn("ReplyImprisonOrTrial, Trial: {0}", trial);
        }
    }
}
