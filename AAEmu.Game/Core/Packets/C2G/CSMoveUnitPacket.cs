﻿using System;
using System.Numerics;

using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Core.Packets.C2G
{
    public class CSMoveUnitPacket : GamePacket
    {
        public CSMoveUnitPacket() : base(CSOffsets.CSMoveUnitPacket, 5)
        {
        }

        public override void Read(PacketStream stream)
        {
            var objId = stream.ReadBc();
            var myObjId = Connection.ActiveChar.ObjId;

            var type = (UnitMovementType)stream.ReadByte();
            var moveType = UnitMovement.GetType(type);

            stream.Read(moveType); // Read UnitMovement
            var extraFlag = stream.ReadByte(); // add in 3.0.3.0

            // ---- test Ai ----
            //var movementAction = new MovementAction(
            //    new Point(
            //        moveType.X, moveType.Y, moveType.Z,
            //        (sbyte)moveType.Rot.X, (sbyte)moveType.Rot.Y, (sbyte)moveType.Rot.Z
            //        ),
            //    new Point(0, 0, 0),
            //    (sbyte)moveType.Rot.Z,
            //    3,
            //    UnitMovementType.Actor
            //    );
            //Connection.ActiveChar.VisibleAi.OwnerMoved(movementAction);
            // ---- test Ai ----

            if (objId != myObjId) // Can be mate
            {
                switch (moveType)
                {
                    case ShipInput shipRequestMoveType:
                        {
                            var slave = SlaveManager.Instance.GetActiveSlaveByOwnerObjId(myObjId);
                            if (slave == null) { return; }

                            slave.ThrottleRequest = shipRequestMoveType.Throttle;
                            slave.SteeringRequest = shipRequestMoveType.Steering;
                            // Also update driver's position
                            Connection.ActiveChar.SetPosition(slave.Position.X, slave.Position.Y, slave.Position.Z, (sbyte)slave.Rot.X, (sbyte)slave.Rot.Y, (sbyte)slave.Rot.Z);
                            break;
                        }
                    case Vehicle VehicleMoveType:
                        {
                            var (yaw, pitch, roll) = MathUtil.GetSlaveRotationInDegrees(VehicleMoveType.Rot.X, VehicleMoveType.Rot.Y, VehicleMoveType.Rot.Z);
                            var reverseQuat = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
                            var reverseZ = reverseQuat.Y / 0.00003052f;
                            Connection.ActiveChar.SendMessage("Client: " + VehicleMoveType.RotationZ + ". Yaw (deg): " + (yaw * 180 / Math.PI) + ". Reverse: " + reverseZ);
                            var slave = SlaveManager.Instance.GetActiveSlaveByOwnerObjId(Connection.ActiveChar.ObjId);
                            if (slave == null) { return; }

                            slave.SetPosition(VehicleMoveType.X, VehicleMoveType.Y, VehicleMoveType.Z, VehicleMoveType.RotationX, VehicleMoveType.RotationY, VehicleMoveType.RotationZ);
                            slave.BroadcastPacket(new SCOneUnitMovementPacket(objId, VehicleMoveType), true);
                            break;
                        }
                    // TODO : check target has Telekinesis buff
                    case ActorData dmt:
                        {
                            var unit = WorldManager.Instance.GetUnit(objId);
                            if (unit == null) { break; }

                            unit.SetPosition(dmt.X, dmt.Y, dmt.Z, (sbyte)dmt.Rot.X, (sbyte)dmt.Rot.Y, (sbyte)dmt.Rot.Z);
                            unit.BroadcastPacket(new SCOneUnitMovementPacket(objId, dmt), true);
                            break;
                        }
                }

                var mateInfo = MateManager.Instance.GetActiveMateByMateObjId(objId);
                if (mateInfo == null) { return; }

                RemoveEffects(mateInfo, moveType);
                mateInfo.SetPosition(moveType.X, moveType.Y, moveType.Z, (sbyte)moveType.Rot.X, (sbyte)moveType.Rot.Y, (sbyte)moveType.Rot.Z);
                mateInfo.BroadcastPacket(new SCOneUnitMovementPacket(objId, moveType), true);

                if (mateInfo.Attached1 > 0)
                {

                    var owner = WorldManager.Instance.GetCharacterByObjId(mateInfo.Attached1);
                    if (owner != null)
                    {
                        RemoveEffects(owner, moveType);
                        owner.SetPosition(moveType.X, moveType.Y, moveType.Z, (sbyte)moveType.Rot.X, (sbyte)moveType.Rot.Y, (sbyte)moveType.Rot.Z);
                        owner.BroadcastPacket(new SCOneUnitMovementPacket(owner.ObjId, moveType), true);
                    }
                }

                if (mateInfo.Attached2 > 0)
                {
                    var passenger = WorldManager.Instance.GetCharacterByObjId(mateInfo.Attached2);
                    if (passenger != null)
                    {
                        RemoveEffects(passenger, moveType);
                        passenger.SetPosition(moveType.X, moveType.Y, moveType.Z, (sbyte)moveType.Rot.X, (sbyte)moveType.Rot.Y, (sbyte)moveType.Rot.Z);
                        passenger.BroadcastPacket(new SCOneUnitMovementPacket(passenger.ObjId, moveType), true);
                    }
                }
            }
            else
            {
                RemoveEffects(Connection.ActiveChar, moveType);
                // This will allow you to walk on a boat, but crashes other clients. Not sure why yet.
                if (moveType is ActorData mType && ((ushort)mType.actorFlags & 0x20) == 0x20)
                {
                    Connection
                        .ActiveChar
                        .SetPosition(mType.X2 + mType.X, mType.Y2 + mType.Y, mType.Z2 + mType.Z, (sbyte)mType.Rot.X, (sbyte)mType.Rot.Y, (sbyte)mType.Rot.Z);

                }
                else
                {
                    Connection
                        .ActiveChar
                        .SetPosition(moveType.X, moveType.Y, moveType.Z, (sbyte)moveType.Rot.X, (sbyte)moveType.Rot.Y, (sbyte)moveType.Rot.Z);

                }
                Connection.ActiveChar.BroadcastPacket(new SCOneUnitMovementPacket(objId, moveType), true);
            }
        }

        private static void RemoveEffects(BaseUnit unit, UnitMovement unitMovement)
        {
            // снять эффекты при начале движения персонажа
            if (Math.Abs(unitMovement.Velocity.X) > 0 || Math.Abs(unitMovement.Velocity.Y) > 0 || Math.Abs(unitMovement.Velocity.Z) > 0)
            {
                var effects = unit.Effects.GetEffectsByType(typeof(BuffTemplate));
                foreach (var effect in effects)
                {
                    if (((BuffTemplate)effect.Template).RemoveOnMove)
                    {
                        effect.Exit();
                    }
                }

                effects = unit.Effects.GetEffectsByType(typeof(BuffEffect));
                foreach (var effect in effects)
                {
                    if (((BuffEffect)effect.Template).Buff.RemoveOnMove)
                    {
                        effect.Exit();
                    }
                }
            }
        }
    }
}
