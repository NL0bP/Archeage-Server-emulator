using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCAboxTeleportPacket : GamePacket
    {
        private readonly int _x;
        private readonly int _y;
        
        public SCAboxTeleportPacket(int x, int y) : base(0x198, 1)
        {
            _x = x;
            _y = y;
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.Write(_x);
            stream.Write(_y);
            return stream;
        }
    }
}
