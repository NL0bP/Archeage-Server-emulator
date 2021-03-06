﻿using System.Numerics;

using AAEmu.Commons.Network;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Models.Game.Units.Movements
{
    public enum UnitMovementType
    {
        None = 0,
        Actor = 1,
        Vehicle = 2,
        Ship = 3,
        ShipInput = 4,
        Transfer = 5
    }

    public abstract class UnitMovement : PacketMarshaler
    {
        public UnitMovementType Type { get; set; }
        public uint Time { get; set; }
        public WorldPos WorldPos { get; set; }
        // +++
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        // +++
        public Quaternion Rot { get; set; } // значение поворота по оси Z должно быть в радианах
        // ---
        public sbyte RotationX { get; set; }
        public sbyte RotationY { get; set; }
        public sbyte RotationZ { get; set; }
        // ---
        public Vector3 Velocity { get; set; }
        // ---
        public short VelX { get; set; }
        public short VelY { get; set; }
        public short VelZ { get; set; }
        // ---
        public byte Flags { get; set; }  // всегда 0 ?
        public ActorMoveType actorFlags { get; set; } // 5-walk, 4-run, 3-stand still и ещё предположительно мы : 32-на корабле, 36-на дилижансе
        public uint ScType { get; set; }
        public byte Phase { get; set; }



        public override void Read(PacketStream stream)
        {
            //Type = (UnitMovementType)stream.ReadByte(); // read in CSMoveuUitPacket
            Time = stream.ReadUInt32();
            Flags = stream.ReadByte();
            if ((Flags & 0x10) == 0x10)
            {
                ScType = stream.ReadUInt32();
                Phase = stream.ReadByte();
            }
        }

        public override PacketStream Write(PacketStream stream)
        {
            //stream.Write((byte)Type); // write in SCUnitMovementsPacket & SCOneUnitMovementPacket
            stream.Write(Time);
            stream.Write(Flags);
            if ((Flags & 0x10) == 0x10)
            {
                stream.Write(ScType);
                stream.Write(Phase);
            }

            return stream;
        }

        public static UnitMovement GetType(UnitMovementType type)
        {
            UnitMovement mType;
            switch (type)
            {
                case UnitMovementType.Actor:
                    mType = new ActorData();
                    break;
                case UnitMovementType.Vehicle:
                    mType = new Vehicle();
                    break;
                case UnitMovementType.Ship:
                    mType = new Ship();
                    break;
                case UnitMovementType.ShipInput:
                    mType = new ShipInput();
                    break;
                case UnitMovementType.Transfer:
                    mType = new TransferData();
                    break;
                default:
                    mType = new DefaultUnitMovement();
                    break;
            }

            mType.Type = type;
            return mType;
        }
    }
}
