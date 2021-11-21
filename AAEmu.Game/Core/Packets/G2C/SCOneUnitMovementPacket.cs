using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Core.Packets.G2C
{
    public class SCOneUnitMovementPacket : GamePacket // TODO ... SCUnitMovementsPacket
    {
        private readonly uint _id;
        private readonly UnitMovement _type;
        private readonly byte _unitMovementType;

        public SCOneUnitMovementPacket(uint id, UnitMovement type) : base(SCOffsets.SCOneUnitMovementPacket, 1)
        {
            _id = id;
            _type = type;
            _unitMovementType = (byte)type.Type;

            // ---- test Ai ----
            var unit = WorldManager.Instance.GetUnit(id);
            //if (unit is not Npc && unit is not Transfer && unit is not Gimmick) { return; }

            var movementAction = new MovementAction(
                new Point(type.X, type.Y, type.Z, Helpers.ConvertRadianToSbyteDirection(type.Rot.X), Helpers.ConvertRadianToSbyteDirection(type.Rot.Y), Helpers.ConvertRadianToSbyteDirection(type.Rot.Z)),
                new Point(0, 0, 0),
                (sbyte)type.Rot.Z,
                3,
                UnitMovementType.Actor
            );
            unit.VisibleAi.OwnerMoved(movementAction);
            // ---- test Ai ----
        }

        public override PacketStream Write(PacketStream stream)
        {
            stream.WriteBc(_id);
            stream.Write(_unitMovementType);
            stream.Write(_type);

            return stream;
        }
    }
}
