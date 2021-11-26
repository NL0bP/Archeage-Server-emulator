using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Gimmicks;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Transfers.Paths;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Tasks.UnitMove;
using AAEmu.Game.Utils;

using Point = AAEmu.Game.Models.Game.World.Point;

namespace AAEmu.Game.Models.Game.Units.Route
{
    /// <summary>
    /// Control NPC to move along this route
    /// </summary>
    public class SimulationNpc : Patrol
    {
        public Character character;
        public Npc npc;
        public Transfer transfer;
        public bool AbandonTo { get; set; } = false; // для прерывания repeat()
        // +++
        private float _maxVelocityForward = 4.5f;
        private readonly float _maxVelocityBackward = -3.0f;
        private readonly float _velAccel = 0.5f;
        private readonly float _angVel = 1.0f;
        private readonly int Steering;
        private readonly float diffX;
        private readonly float diffY;
        private readonly float diffZ;

        //
        public NpcsPathPoint pp;

        public uint UseSkill;
        //
        public double _angleTmp;
        public Vector3 _vPosition;
        public Vector3 _vTarget;
        public Vector3 _vDistance;
        public Vector3 _vVelocity;
        public double rad;
        // movement data
        public List<string> MovePath;     //  the data we're going to be moving on at the moment
        public List<Point> transferPath;  // path from client file
        public List<NpcsPathPoint> transferPath2;  // path from client file
        public List<NpcsPathPoint> NpcPath;  // path from client file
        public List<string> RecordPath;   // data to write the path
        public Dictionary<uint, List<NpcsPathPoint>> NpcsRoutes; //
        public Dictionary<int, List<Point>> Routes; // Steering, TransferPath
        public Dictionary<uint, Dictionary<int, List<Point>>> _allRoutes; // templateId, Steering, TransferPath
        public Dictionary<uint, Dictionary<int, string>> _allRouteNames; // templateId, Steering, TransferPath
        // +++
        public int PointsCount { get; set; }              // number of points in the process of recording the path
        public bool SavePathEnabled { get; set; }         // flag, path recording
        public bool MoveToPathEnabled { get; set; }       // flag, road traffic
        public bool MoveToForward { get; set; }           // movement direction true -> forward, true -> back
        public bool RunningMode { get; set; } = false;    // movement mode true -> run, true -> walk
        public bool Move { get; set; } = false;           // movement mode true -> moving to the point #, false -> get next point #
        public int MoveStepIndex { get; set; }            // current checkpoint (where are we running now)

        private float _oldX, _oldY, _oldZ;
        //*******************************************************
        public string RecordFilesPath = @"./Data/Path/"; // path where our files are stored
        public string RecordFileExt = @".path";          // default extension
        public string MoveFilesPath = @"./Data/Path/";   // path where our files are stored
        public string MoveFileExt = @".path";            // default extension
        public string MoveFileName = "";                 // default name
        private float _tempMovingDistance;
        private readonly float _rangeToCheckPoint = 0.5f; // distance to checkpoint at which it is considered that we have reached it
        private readonly int _moveTrigerDelay = 1000;     // triggering the timer for movement 1 sec

        //*******************************************************
        /*
           by alexsl
           a little mite in scripting, someone might need it.
           
           what they're doing:
           - automatically writes the route to the file;
           - you can load the path data from the file;
           - moves along the route.
           
           To start with, you need to create a route(s), the recording takes place as follows:
           1. Start recording - "rec";
           2. Walk along the route;
           3. stop recording - "save".
           === here is an approximate file structure (x,y,z)=========.
           |15629,0|14989,02|141,2055|
           |15628,0|14987,24|141,3826|
           |15626,0|14983,88|141,3446|
           ==================================================
           */
        //***************************************************************
        public SimulationNpc(Unit unit, float velocityForward = 4.5f, float velocityBackward = -3.0f, float velAcceleration = 0.5f, float angVelocity = 1.0f)
        {
            if (unit is Npc)
            {
                _velAccel = velAcceleration; //per s
                _maxVelocityForward = velocityForward; // 9.6f;
                _maxVelocityBackward = velocityBackward;
                _angVel = angVelocity;
                Steering = 0;
                UseSkill = 0;

                //var linInertia = 0.3f;    //per s   // TODO Move to the upper motion control module
                //var linDeaccelInertia = 0.1f;  //per s   // TODO Move to the upper motion control module
                //var maxVelBackward = -2.0f; //per s
                //var diffX = 0f;
                //var diffY = 0f;
                //var diffZ = 0f;
            }
            Init(unit);
        }

        public int Delta(float vPositionX1, float vPositionY1, float vPositionX2, float vPositionY2)
        {
            //return Math.Round(Math.Sqrt((vPositionX1-vPositionX2)*(vPositionX1-vPositionX2))+(vPositionY1-vPositionY2)*(vPositionY1-vPositionY2));
            var dx = vPositionX1 - vPositionX2;
            var dy = vPositionY1 - vPositionY2;
            var summa = dx * dx + dy * dy;
            if (Math.Abs(summa) < Tolerance)
            {
                return 0;
            }

            return (int)Math.Round(Math.Sqrt(summa));
        }

        //***************************************************************
        // Orientation on the terrain: Check if the given point is within reach
        //public bool PosInRange(Npc npc, float targetX, float targetY, float targetZ, int distance)
        //***************************************************************
        public bool PosInRange(Unit unit, float targetX, float targetY, int distance)
        {
            if (unit is Npc npc)
            {
                return Delta(targetX, targetY, npc.Position.X, npc.Position.Y) <= distance;
            }

            return false;
        }
        //***************************************************************
        public string GetValue(string valName)
        {
            return RecordPath.Find(x => x == valName);
        }
        //***************************************************************
        public void SetValue(string valName, string value)
        {
            var index = RecordPath.IndexOf(RecordPath.Where(x => x == valName).FirstOrDefault());
            RecordPath[index] = value;
        }
        //***************************************************************
        public float ExtractValue(string sData, int nIndex)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            int i;
            var j = 0;
            var s = sData;
            while (j < nIndex)
            {
                i = s.IndexOf('|');
                if (i >= 0)
                {
                    s = s.Substring(i + 1, s.Length - (i + 1));
                    j++;
                }
                else
                {
                    break;
                }
            }
            i = s.IndexOf('|');
            if (i >= 0)
            {
                s = s.Substring(0, i - 1);
            }
            var result = Convert.ToSingle(s);
            return result;
        }
        //***************************************************************
        public int GetMinCheckPoint(Unit unit, List<string> pointsList)
        {
            string s;
            var index = -1;

            // check for a route
            if (pointsList.Count == 0)
            {
                _log.Warn("no data on the route.");
                return -1;
            }

            if (unit is Npc npc)
            {
                int m, minDist;
                minDist = -1;
                for (var i = 0; i < pointsList.Count; i++)
                {
                    s = pointsList[i];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);

                    _log.Warn(s + " #" + i + " x:=" + _vPosition.X + " y:=" + _vPosition.Y + " z:=" + _vPosition.Z);

                    m = Delta(_vPosition.X, _vPosition.Y, npc.Position.X, npc.Position.Y);

                    if (index == -1)
                    {
                        minDist = m;
                        index = i;
                    }
                    else if (m < minDist)
                    {
                        minDist = m;
                        index = i;
                    }
                }
            }

            return index;
        }
        //***************************************************************
        private int GetMinCheckPointFromNpcPath(Unit unit, List<NpcsPathPoint> pointsList)
        {
            var index = -1;
            // check for a route
            if (pointsList.Count == 0)
            {
                _log.Warn("no route data.");
                return -1;
            }
            if (unit is Npc npc)
            {
                int m, minDist;
                minDist = -1;
                for (var i = 0; i < pointsList.Count; i++)
                {
                    _vPosition.X = pointsList[i].X;
                    _vPosition.Y = pointsList[i].Y;
                    _vPosition.Z = pointsList[i].Z;

                    _log.Warn("#" + i + " x:=" + _vPosition.X + " y:=" + _vPosition.Y + " z:=" + _vPosition.Z);

                    m = Delta(_vPosition.X, _vPosition.Y, npc.Position.X, npc.Position.Y);

                    //if (m <= 0) { continue; }
                    if (m < 0) { continue; }

                    if (index == -1)
                    {
                        minDist = m;
                        index = i;
                    }
                    else if (m < minDist)
                    {
                        minDist = m;
                        index = i;
                    }
                }
            }

            return index;
        }
        //***************************************************************
        //***************************************************************
        //***************************************************************
        public (int, int) GetMinCheckPointFromRoutes2(Unit unit)
        {
            var pointIndex = 0;
            var routeIndex = 0;
            for (var i = 0; i < Routes.Count; i++)
            {
                pointIndex = GetMinCheckPointFromRoutes(unit, Routes[i]);
                if (pointIndex == -1) { continue; }

                routeIndex = i;
                break; // нашли нужную точку res в "пути" с индексом index
            }
            return (pointIndex, routeIndex);
        }
        //***************************************************************
        //***************************************************************
        public int GetMinCheckPointFromRoutes(Unit unit, List<Point> pointsList, float distance = 200f)
        {
            var pointIndex = -1;
            // check for a route
            if (pointsList.Count == 0)
            {
                _log.Warn("no route data.");
                return -1;
            }

            return pointIndex;
        }
        //***************************************************************
        private int GetMinCheckPoint(Unit unit, List<Point> pointsList)
        {
            var index = -1;
            // check for a route
            if (pointsList.Count == 0)
            {
                _log.Warn("no route data.");
                return -1;
            }
            if (unit is Npc npc)
            {
                int m, minDist;
                minDist = -1;
                for (var i = 0; i < pointsList.Count; i++)
                {
                    _vPosition.X = pointsList[i].X;
                    _vPosition.Y = pointsList[i].Y;
                    _vPosition.Z = pointsList[i].Z;

                    _log.Warn("#" + i + " x:=" + _vPosition.X + " y:=" + _vPosition.Y + " z:=" + _vPosition.Z);

                    m = Delta(_vPosition.X, _vPosition.Y, npc.Position.X, npc.Position.Y);

                    if (m <= 0) { continue; }

                    if (index == -1)
                    {
                        minDist = m;
                        index = i;
                    }
                    else if (m < minDist)
                    {
                        minDist = m;
                        index = i;
                    }
                }
            }

            return index;
        }

        //***************************************************************
        /// <summary>
        /// Пробую сделать движение транспорта из спарсенных с лога точек пути
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="pointsList"></param>
        //***************************************************************
        private int GetMinCheckPoint2(Unit unit, List<NpcsPathPoint> pointsList)
        {
            var index = -1;
            // check for a route
            if (pointsList.Count == 0)
            {
                _log.Warn("no route data.");
                return -1;
            }

            return index;
        }
        //***************************************************************
        public void StartRecord(SimulationNpc sim, Character ch)
        {
            if (SavePathEnabled) { return; }
            if (MoveToPathEnabled)
            {
                _log.Warn("while following the route, recording is not possible.");
                return;
            }
            RecordPath.Clear();
            PointsCount = 0;
            _log.Warn("route recording started ...");
            SavePathEnabled = true;
            RepeatTo(ch, _moveTrigerDelay, sim);
        }
        //***************************************************************
        public void Record(SimulationNpc sim, Character ch)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            //if (!SavePathEnabled) { return; }
            var s = "|" + ch.Position.X + "|" + ch.Position.Y + "|" + ch.Position.Z + "|";
            RecordPath.Add(s);
            PointsCount++;
            _log.Warn("added checkpoint # {0}", PointsCount);
            RepeatTo(ch, _moveTrigerDelay, sim);
        }
        //***************************************************************
        public void StopRecord(SimulationNpc sim)
        {
            // write to file
            using (var sw = new StreamWriter(GetRecordFileName()))
            {
                foreach (var b in RecordPath)
                {
                    sw.WriteLine(b.ToString());
                }
            }
            _log.Warn("Route recording completed.");
            SavePathEnabled = false;
        }
        //***************************************************************
        public string GetRecordFileName()
        {
            var result = RecordFilesPath + MoveFileName + RecordFileExt;
            return result;
        }
        //***************************************************************
        public string GetMoveFileName()
        {
            var result = MoveFilesPath + MoveFileName + MoveFileExt;
            return result;
        }

        //***************************************************************
        public void LoadAllPath(Point position)
        {
            Routes = TransferManager.Instance.GetAllTransferPath(position);

        }

        //***************************************************************
        public void ParseMoveClient(Unit unit)
        {
            if (!SavePathEnabled) { return; }
            if (unit is Npc npc)
            {
                _vPosition.X = npc.Position.X;
                _vPosition.Y = npc.Position.Y;
                _vPosition.Z = npc.Position.Z;
            }
            var s = "|" + _vPosition.X + "|" + _vPosition.Y + "|" + _vPosition.Z + "|";
            RecordPath.Add(s);
            PointsCount++;
            _log.Warn("added checkpoint # {0}", PointsCount);
        }
        //***************************************************************
        public void GoToPath(Unit unit, bool toForward)
        {
            if (unit is Npc npc)
            {
                if (MovePath.Count > 0)
                {
                    MoveToPathEnabled = !MoveToPathEnabled;
                    MoveToForward = toForward;
                    if (!MoveToPathEnabled)
                    {
                        _log.Warn("the route is stopped.");
                        StopMove(npc);
                        return;
                    }

                    // presumably the path is already registered in MovePath
                    _log.Warn("trying to get on the path ...");
                    // first go to the closest checkpoint
                    var i = GetMinCheckPoint(npc, MovePath);
                    if (i < 0)
                    {
                        _log.Warn("checkpoint not found.");
                        StopMove(npc);
                        return;
                    }

                    _log.Warn("found nearest checkpoint # " + i + " run there ...");
                    MoveToPathEnabled = true;
                    MoveStepIndex = i;
                    _log.Warn("checkpoint #" + i);
                    var s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);

                    if (Math.Abs(_oldX - _vPosition.X) > Tolerance && Math.Abs(_oldY - _vPosition.Y) > Tolerance && Math.Abs(_oldZ - _vPosition.Z) > Tolerance)
                    {
                        _oldX = _vPosition.X;
                        _oldY = _vPosition.Y;
                        _oldZ = _vPosition.Z;
                    }
                }
                else if (NpcPath.Count > 0)
                {
                    MoveToPathEnabled = !MoveToPathEnabled;
                    MoveToForward = toForward;
                    if (!MoveToPathEnabled)
                    {
                        _log.Warn("the route is stopped.");
                        StopMove(npc);
                        return;
                    }

                    // presumably the path is already registered in MovePath
                    _log.Warn("trying to get on the path ...");
                    // first go to the closest checkpoint
                    var i = GetMinCheckPointFromNpcPath(npc, NpcPath);
                    if (i < 0)
                    {
                        _log.Warn("checkpoint not found.");
                        StopMove(npc);
                        return;
                    }

                    _log.Warn("found nearest checkpoint # " + i + " run there ...");
                    MoveToPathEnabled = true;
                    MoveStepIndex = i;
                    _log.Warn("checkpoint #" + i);
                    var s = NpcPath[MoveStepIndex];
                    _vPosition.X = s.X;
                    _vPosition.Y = s.Y;
                    _vPosition.Z = s.Z;
                    pp = s; // передаем инфу по точке для движения транспорта

                    if (Math.Abs(_oldX - _vPosition.X) > Tolerance && Math.Abs(_oldY - _vPosition.Y) > Tolerance && Math.Abs(_oldZ - _vPosition.Z) > Tolerance)
                    {
                        _oldX = _vPosition.X;
                        _oldY = _vPosition.Y;
                        _oldZ = _vPosition.Z;
                    }
                }
                RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
            }
        }

        //***************************************************************
        /// <summary>
        /// Пробую сделать движение Гвардов из спарсенных с лога точек пути
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="unit"></param>
        /// <param name="targetX"></param>
        /// <param name="targetY"></param>
        /// <param name="targetZ"></param>
        /// <param name="angle"></param>
        //***************************************************************
        public void MoveToPathNpc(SimulationNpc sim, Unit unit, float targetX, float targetY, float targetZ, NpcsPathPoint pp = null)
        {
            if (unit is Npc npc)
            {
                if (!npc.IsInPatrol)
                {
                    PauseMove(npc);
                    return;
                }
                if (MovePath.Count > 0)
                {
                    MoveTo(sim, unit, targetX, targetY, targetZ);
                }
                else if (NpcPath.Count > 0)
                {

                    var x = npc.Position.X - targetX;
                    var y = npc.Position.Y - targetY;
                    var z = npc.Position.Z - targetZ;
                    var maxXyz = Math.Max(Math.Max(Math.Abs(x), Math.Abs(y)), Math.Abs(z));

                    npc.Position.X = pp.X;
                    npc.Position.Y = pp.Y;
                    npc.Position.Z = pp.Z;

                    npc.Vel = new Vector3(pp.VelX, pp.VelY, pp.VelZ);
                    npc.Rot = new Quaternion(pp.RotationX, pp.RotationY, pp.RotationZ, 1);

                    moveType = (ActorData)UnitMovement.GetType(UnitMovementType.Actor);
                    //var tmpZ = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                    //moveType.WorldPos = new WorldPos(npc.Pos.X, npc.Pos.Y, tmpZ);

                    moveType.X = npc.Position.X;
                    moveType.Y = npc.Position.Y;
                    moveType.Z = npc.Position.Z;
                    moveType.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;

                    moveType.Rot = new Quaternion(Helpers.ConvertDirectionToRadian(pp.RotationX), Helpers.ConvertDirectionToRadian(pp.RotationY), Helpers.ConvertDirectionToRadian(pp.RotationZ), 1f);
                    moveType.DeltaMovement = new Vector3(pp.ActorDeltaMovementX * 0.00787401574803149606299212598425f, pp.ActorDeltaMovementY * 0.00787401574803149606299212598425f, pp.ActorDeltaMovementZ * 0.00787401574803149606299212598425f);

                    moveType.Stance = pp.ActorStance;       // COMBAT = 0x0, IDLE = 0x1
                    moveType.Alertness = pp.ActorAlertness; // IDLE = 0x0, ALERT = 0x1, COMBAT = 0x2
                    moveType.actorFlags = pp.ActorFlags;    // 5-walk, 4-run, 3-stand still

                    moveType.Time += 50;                    // has to change all the time for normal motion.

                    // moving to the point #
                    npc.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);
                    //RepeatMove(sim, npc, targetX, targetY, targetZ, pp);
                    OnMove(npc);
                }
            }
        }
        public void MoveTo(SimulationNpc sim, Unit unit, float targetX, float targetY, float targetZ)
        {
            if (unit is Npc npc)
            {
                if (!MoveToPathEnabled || npc.Position == null || !npc.IsInPatrol)
                {
                    StopMove(npc);
                    return;
                }

                vTarget = new Vector3(targetX, targetY, targetZ);
                vPosition = new Vector3(npc.Position.X, npc.Position.Y, npc.Position.Z);

                // distance to the point where we are moving
                Distance = MathUtil.GetDistance(vTarget, vPosition);

                if (!(Distance > 0))
                {
                    InPatrol = false;
                    // get next path or point # in current path
                    OnMove2(npc);
                    return;
                }

                DeltaTime = (float)(DateTime.UtcNow - UpdateTime).TotalSeconds;
                UpdateTime = DateTime.UtcNow;
                if (DeltaTime > 1)
                {
                    DeltaTime = 1.0f / 20.0f;
                }

                //var velocity = MaxVelocityForward * DeltaTime;
                var velocity = _maxVelocityForward * DeltaTime;
                // вектор направление на таргет (последовательность аргументов важно, чтобы смотреть на таргет)
                vDistance = vTarget - vPosition; // dx, dy, dz
                // вектор направления необходимо нормализовать
                var direction = vDistance != Vector3.Zero ? Vector3.Normalize(vDistance) : Vector3.Zero;

                // вектор скорости (т.е. координаты, куда попадём двигаясь со скоростью velociti по направдению direction)
                var diff = direction * velocity;
                npc.Position.X += diff.X;
                npc.Position.Y += diff.Y;
                npc.Position.Z += diff.Z;
                var move = false;
                if (Math.Abs(diff.X) > 0 || Math.Abs(diff.Y) > 0) { move = true; }
                if (Math.Abs(vDistance.X) < RangeToCheckPoint) { npc.Position.X = vTarget.X; }
                if (Math.Abs(vDistance.Y) < RangeToCheckPoint) { npc.Position.Y = vTarget.Y; }
                if (Math.Abs(vDistance.Z) < RangeToCheckPoint) { npc.Position.Z = vTarget.Z; }

                // Simulated unit
                moveType = (ActorData)UnitMovement.GetType(UnitMovementType.Actor);
                // Change NPC coordinates
                moveType.X = npc.Position.X;
                moveType.Y = npc.Position.Y;
                moveType.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                if (npc.Position.Z - moveType.Z > 2.0)
                {
                    moveType.Z = npc.Position.Z;
                }
                // looks in the direction of movement
                Angle = MathUtil.CalculateAngleFrom(vPosition.X, vPosition.Y, vTarget.X, vTarget.Y);
                var _rotZ = MathUtil.ConvertDegreeToDirection(Angle);
                moveType.Rot = new Quaternion(0f, 0f, Helpers.ConvertDirectionToRadian(_rotZ), 1f);

                npc.Rot = moveType.Rot;
                if (RunningMode)
                {
                    moveType.actorFlags = ActorMoveType.Run; // 5-walk, 4-run, 3-stand still
                    moveType.DeltaMovement = new Vector3(0, 1f, 0);
                }
                else
                {
                    moveType.actorFlags = ActorMoveType.Walk; // 5-walk, 4-run, 3-stand still
                    moveType.DeltaMovement = new Vector3(0, 0.5f, 0);
                }

                moveType.Stance = EStance.Idle;           // COMBAT = 0x0, IDLE = 0x1
                moveType.Alertness = AiAlertness.Idle;    // IDLE = 0x0, ALERT = 0x1, COMBAT = 0x2
                moveType.Time += 100;                     // has to change all the time for normal motion
                npc.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);

                // If not far from the spawn point, continue adding tasks or stop moving
                if (move)
                {
                    RepeatMove(sim, npc, targetX, targetY, targetZ);
                }
                else
                {
                    InPatrol = false;
                    // get next path or point # in current path
                    OnMove2(npc);
                }
            }
        }

        public void MoveTo2(SimulationNpc sim, Unit unit, float targetX, float targetY, float targetZ)
        {
            if (unit is Npc npc)
            {
                if (!npc.IsInPatrol)
                {
                    StopMove(npc);
                    return;
                }
                var move = false;
                var x = npc.Position.X - targetX;
                var y = npc.Position.Y - targetY;
                var z = npc.Position.Z - targetZ;
                var maxXyz = Math.Max(Math.Max(Math.Abs(x), Math.Abs(y)), Math.Abs(z));

                if (RunningMode)
                {
                    MovingDistance = 0.5f;
                }
                else
                {
                    MovingDistance = 0.25f;
                }


                if (Math.Abs(x) > _rangeToCheckPoint)
                {
                    if (Math.Abs(maxXyz - Math.Abs(x)) > Tolerance)
                    {
                        _tempMovingDistance = Math.Abs(x) / (maxXyz / MovingDistance);
                        _tempMovingDistance = Math.Min(_tempMovingDistance, MovingDistance);
                    }
                    else
                    {
                        _tempMovingDistance = MovingDistance;
                    }

                    if (x < 0)
                    {
                        npc.Position.X += _tempMovingDistance;
                    }
                    else
                    {
                        npc.Position.X -= _tempMovingDistance;
                    }
                    if (Math.Abs(x) < _tempMovingDistance)
                    {
                        npc.Position.X = _vPosition.X;
                    }
                    move = true;
                }
                if (Math.Abs(y) > _rangeToCheckPoint)
                {
                    if (Math.Abs(maxXyz - Math.Abs(y)) > Tolerance)
                    {
                        _tempMovingDistance = Math.Abs(y) / (maxXyz / MovingDistance);
                        _tempMovingDistance = Math.Min(_tempMovingDistance, MovingDistance);
                    }
                    else
                    {
                        _tempMovingDistance = MovingDistance;
                    }
                    if (y < 0)
                    {
                        npc.Position.Y += _tempMovingDistance;
                    }
                    else
                    {
                        npc.Position.Y -= _tempMovingDistance;
                    }
                    if (Math.Abs(y) < _tempMovingDistance)
                    {
                        npc.Position.Y = _vPosition.Y;
                    }
                    move = true;
                }
                if (Math.Abs(z) > _rangeToCheckPoint)
                {
                    if (Math.Abs(maxXyz - Math.Abs(z)) > Tolerance)
                    {
                        _tempMovingDistance = Math.Abs(z) / (maxXyz / MovingDistance);
                        _tempMovingDistance = Math.Min(_tempMovingDistance, MovingDistance);
                    }
                    else
                    {
                        _tempMovingDistance = MovingDistance;
                    }
                    if (z < 0)
                    {
                        npc.Position.Z += _tempMovingDistance;
                    }
                    else
                    {
                        npc.Position.Z -= _tempMovingDistance;
                    }
                    if (Math.Abs(z) < _tempMovingDistance)
                    {
                        npc.Position.Z = _vPosition.Z;
                    }
                    move = true;
                }
                // simulation unit. return the unitMovement object
                moveType = (ActorData)UnitMovement.GetType(UnitMovementType.Actor);
                // Change the NPC coordinates
                //moveType.X = npc.Position.X;
                //moveType.Y = npc.Position.Y;
                //moveType.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                //// looks in the direction of movement
                ////-----------------------взгляд_NPC_будет(движение_откуда->движение_куда)
                //angle = MathUtil.CalculateAngleFrom(npc.Position.X, npc.Position.Y, vPosition.X, vPosition.Y);
                //var rotZ = MathUtil.ConvertDegreeToDirection(angle);
                //moveType.RotationX = 0;
                //moveType.RotationY = 0;
                //moveType.RotationZ = rotZ;
                //if (RunningMode)
                //{
                //    moveType.Flags = 4;      // 5-walk, 4-run, 3-stand still
                //}
                //else
                //{
                //    moveType.Flags = 5;      // 5-walk, 4-run, 3-stand still
                //}
                //moveType.DeltaMovement = new sbyte[3];
                //moveType.DeltaMovement[0] = 0;
                //moveType.DeltaMovement[1] = 127;
                //moveType.DeltaMovement[2] = 0;

                var tmpZ = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                moveType.WorldPos = new WorldPos(npc.Pos.X, npc.Pos.Y, tmpZ);

                var direction = new Vector3();
                if (_vDistance != Vector3.Zero)
                {
                    direction = Vector3.Normalize(_vDistance);
                }

                var rotation = (float)Math.Atan2(direction.Y, direction.X);
                moveType.Rot = Quaternion.CreateFromAxisAngle(direction, rotation);

                if (RunningMode)
                {
                    moveType.actorFlags = ActorMoveType.Run;                // 5-walk, 4-run, 3-stand still
                }
                else
                {
                    moveType.actorFlags = ActorMoveType.Walk;                // 5-walk, 4-run, 3-stand still
                }

                moveType.DeltaMovement = direction;

                moveType.Stance = EStance.Idle;         // COMBAT = 0x0, IDLE = 0x1
                moveType.Alertness = AiAlertness.Idle;  // IDLE = 0x0, ALERT = 0x1, COMBAT = 0x2
                moveType.Time = Seq;                    // has to change all the time for normal motion.
                if (move)
                {
                    // moving to the point #
                    npc.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);
                    RepeatMove(sim, npc, targetX, targetY, targetZ, pp);
                }
                else
                {
                    OnMove(npc);
                }
            }
        }

        //***************************************************************
        public Doodad SpawnFlag(float posX, float posY)
        {
            // spawn flag
            var combatFlag = new DoodadSpawner
            {
                Id = 0,
                UnitId = 5014, // Combat Flag Id=5014;
                Position = new Point
                {
                    ZoneId = WorldManager.Instance.GetZoneId(1, posX, posY),
                    WorldId = 1,
                    X = posX,
                    Y = posY
                }
            };
            combatFlag.Position.Z = WorldManager.Instance.GetHeight(combatFlag.Position.ZoneId, combatFlag.Position.X, combatFlag.Position.Y);
            return combatFlag.Spawn(0); // set CombatFlag
        }

        //***************************************************************
        public Doodad SpawnFlag(Vector3 pos)
        {
            // spawn flag
            var combatFlag = new DoodadSpawner
            {
                Id = 0,
                UnitId = 5014, // Combat Flag Id=5014;
                Position = new Point
                {
                    ZoneId = WorldManager.Instance.GetZoneId(1, pos.X, pos.Y),
                    WorldId = 1,
                    X = pos.X,
                    Y = pos.Y
                }
            };
            combatFlag.Position.Z = WorldManager.Instance.GetHeight(combatFlag.Position.ZoneId, combatFlag.Position.X, combatFlag.Position.Y);
            return combatFlag.Spawn(0); // set CombatFlag
        }

        //***************************************************************
        public void DespawnFlag(Doodad doodad)
        {
            // spawn flag
            var combatFlag = new DoodadSpawner();
            combatFlag.Despawn(doodad);
        }
        //***************************************************************
        public void RepeatMove(SimulationNpc sim, Unit unit, float targetX, float targetY, float targetZ, NpcsPathPoint pp = null, double time = 130, uint skillId = 0)
        {
            if (unit is Npc npc)
            {
                if (skillId > 0)
                {
                    var useSkill = new Skill(SkillManager.Instance.GetSkillTemplate(skillId));
                    var casterType = SkillCaster.GetByType(EffectOriginType.Skill); // who uses
                    casterType.ObjId = npc.ObjId;
                    var targetType = sim.GetSkillCastTarget(npc, useSkill);
                    var flag = 0;
                    var flagType = flag & 15;
                    var skillObject = SkillObject.GetByType((SkillObjectType)flagType);
                    useSkill.Use(npc, casterType, targetType, skillObject);
                    if (useSkill.Template.CastingTime != 0 && useSkill.Template.CooldownTime != 0)
                        time = useSkill.Template.CastingTime + useSkill.Template.CooldownTime;
                }

                TaskManager.Instance.Schedule(new MoveNpc(sim ?? this, npc, targetX, targetY, targetZ, pp), TimeSpan.FromMilliseconds(time));
            }
        }

        //***************************************************************
        public void RepeatTo(Character ch, double time = 1000, SimulationNpc sim = null)
        {
            if ((sim ?? this).SavePathEnabled)
            {
                TaskManager.Instance.Schedule(new Record(sim ?? this, ch), TimeSpan.FromMilliseconds(time));
            }
        }

        //***************************************************************
        public void StopMove(Unit unit, NpcsPathPoint pp = null)
        {
            if (unit is Npc npc)
            {
                _log.Warn("stop moving ...");
                npc.IsInPatrol = false;
                PauseMove(npc, pp);
            }
        }

        public void PauseMove(Unit unit, NpcsPathPoint pp = null)
        {
            if (unit is Npc npc)
            {
                _log.Warn("let's stand a little...");
                moveType = (ActorData)UnitMovement.GetType(UnitMovementType.Actor);
                moveType.X = npc.Position.X;
                moveType.Y = npc.Position.Y;
                moveType.Z = npc.Position.Z;
                moveType.Z = AppConfiguration.Instance.HeightMapsEnable ? WorldManager.Instance.GetHeight(npc.Position.ZoneId, npc.Position.X, npc.Position.Y) : npc.Position.Z;
                moveType.WorldPos = new WorldPos(Helpers.ConvertLongX(npc.Position.X), Helpers.ConvertLongY(npc.Position.Y), npc.Position.Z);

                if (pp == null)
                {
                    moveType.Rot = npc.Rot;
                }
                else
                {
                    moveType.Rot = new Quaternion(Helpers.ConvertDirectionToRadian(pp.RotationX), Helpers.ConvertDirectionToRadian(pp.RotationY), Helpers.ConvertDirectionToRadian(pp.RotationZ), 1f);
                }

                moveType.DeltaMovement = Vector3.Zero;
                moveType.actorFlags = ActorMoveType.Walk; // 5-walk, 4-run, 3-stand still
                moveType.Stance = EStance.Idle;           // COMBAT = 0x0, IDLE = 0x1
                moveType.Alertness = AiAlertness.Idle;    // IDLE = 0x0, ALERT = 0x1, COMBAT = 0x2
                moveType.Time += 100;                     // has to change all the time for normal motion.
                npc.BroadcastPacket(new SCOneUnitMovementPacket(npc.ObjId, moveType), true);
            }
        }

        public void OnMove(BaseUnit unit)
        {
            if (unit is Npc npc)
            {
                if (!MoveToPathEnabled)
                {
                    _log.Warn("OnMove disabled");
                    StopMove(npc);
                    return;
                }
                if (MovePath.Count > 0)
                {
                    var s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);
                    if (!PosInRange(npc, _vPosition.X, _vPosition.Y, 3))
                    {
                        RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                        return;
                    }

                    if (MoveToForward)
                    {
                        if (MoveStepIndex == MovePath.Count - 1)
                        {
                            _log.Warn("we are ideally at the end point.");
                            PauseMove(npc, pp);
                            MoveToForward = false; //turn back
                            MoveStepIndex--;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = MovePath[MoveStepIndex];
                            _vPosition.X = ExtractValue(s, 1);
                            _vPosition.Y = ExtractValue(s, 2);
                            _vPosition.Z = ExtractValue(s, 3);
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }

                        MoveStepIndex++;
                        _log.Warn("we have reached checkpoint go on...");
                    }
                    else
                    {
                        if (MoveStepIndex > 0)
                        {
                            MoveStepIndex--;
                            _log.Warn("we reached checkpoint go further ...");
                        }
                        else
                        {
                            _log.Warn("we are ideally at the starting point.");
                            PauseMove(npc);
                            MoveToForward = true; //turn back
                            MoveStepIndex++;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = MovePath[MoveStepIndex];
                            _vPosition.X = ExtractValue(s, 1);
                            _vPosition.Y = ExtractValue(s, 2);
                            _vPosition.Z = ExtractValue(s, 3);
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }
                    }

                    _log.Warn("walk to #" + MoveStepIndex);
                    s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);
                    RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                }
                if (NpcPath.Count > 0)
                {
                    var s = NpcPath[MoveStepIndex];
                    _vPosition.X = s.X;
                    _vPosition.Y = s.Y;
                    _vPosition.Z = s.Z;

                    _log.Warn("x=" + s.X + " y=" + s.Y + " z=" + s.Z + " rotZ=" + s.RotationZ);

                    pp = s; // передаем инфу по точке для движения транспорта

                    if (MoveToForward)
                    {
                        if (MoveStepIndex == NpcPath.Count - 1)
                        {
                            _log.Warn("we are ideally at the end point.");
                            PauseMove(npc);
                            //MoveToForward = false; //turn back
                            var i = GetMinCheckPointFromNpcPath(npc, NpcPath);
                            if (i < 0)
                            {
                                _log.Warn("checkpoint not found.");
                                StopMove(npc);
                                return;
                            }
                            MoveStepIndex = i; // начнем с начала
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = NpcPath[MoveStepIndex];
                            _vPosition.X = s.X;
                            _vPosition.Y = s.Y;
                            _vPosition.Z = s.Z;
                            pp = s; // передаем инфу по точке для движения транспорта
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 5000);
                            return;
                        }

                        MoveStepIndex++;
                        _log.Warn("we have reached checkpoint go on...");
                    }
                    else
                    {
                        if (MoveStepIndex > 0)
                        {
                            MoveStepIndex--;
                            _log.Warn("we reached checkpoint go further ...");
                        }
                        else
                        {
                            _log.Warn("we are ideally at the starting point.");
                            PauseMove(npc);
                            MoveToForward = true; //turn back
                            MoveStepIndex++;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = NpcPath[MoveStepIndex];
                            _vPosition.X = s.X;
                            _vPosition.Y = s.Y;
                            _vPosition.Z = s.Z;
                            pp = s; // передаем инфу по точке для движения транспорта
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 5000);
                            return;
                        }
                    }

                    _log.Warn("walk to #" + MoveStepIndex);
                    s = NpcPath[MoveStepIndex];
                    var time = 50;
                    UseSkill = s.SkillId;
                    if (UseSkill == 0)
                    {
                        _vPosition.X = s.X;
                        _vPosition.Y = s.Y;
                        _vPosition.Z = s.Z;
                    }
                    else
                    {
                        // заменим нулевые координаты у скилла на текущие координаты, пропуская если скиллы идут подряд,
                        // а у них координаты равны = 0
                        for (var i = 1; i < 5; i++)
                        {
                            s = NpcPath[MoveStepIndex - i];
                            if (s.X > 0)
                            {
                                time = 60000;
                                _vPosition.X = s.X;
                                _vPosition.Y = s.Y;
                                _vPosition.Z = s.Z;
                                break;
                            }
                        }
                    }
                    pp = s; // передаем инфу по точке для движения транспорта

                    RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, time, UseSkill);
                }
            }
        }

        private void OnMove2(BaseUnit unit)
        {
            if (unit is Npc npc)
            {
                if (!MoveToPathEnabled)
                {
                    _log.Warn("OnMove disabled");
                    StopMove(npc);
                    return;
                }
                if (MovePath.Count > 0)
                {
                    var s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);
                    if (!PosInRange(npc, _vPosition.X, _vPosition.Y, 3))
                    {
                        RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                        return;
                    }

                    if (MoveToForward)
                    {
                        if (MoveStepIndex == MovePath.Count - 1)
                        {
                            _log.Warn("we are ideally at the end point.");
                            PauseMove(npc);
                            MoveToForward = false; //turn back
                            MoveStepIndex--;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = MovePath[MoveStepIndex];
                            _vPosition.X = ExtractValue(s, 1);
                            _vPosition.Y = ExtractValue(s, 2);
                            _vPosition.Z = ExtractValue(s, 3);
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }

                        MoveStepIndex++;
                        _log.Warn("we have reached checkpoint go on...");
                    }
                    else
                    {
                        if (MoveStepIndex > 0)
                        {
                            MoveStepIndex--;
                            _log.Warn("we reached checkpoint go further ...");
                        }
                        else
                        {
                            _log.Warn("we are ideally at the starting point.");
                            PauseMove(npc);
                            MoveToForward = true; //turn back
                            MoveStepIndex++;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = MovePath[MoveStepIndex];
                            _vPosition.X = ExtractValue(s, 1);
                            _vPosition.Y = ExtractValue(s, 2);
                            _vPosition.Z = ExtractValue(s, 3);
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }
                    }

                    _log.Warn("walk to #" + MoveStepIndex);
                    s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);
                    RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                }
                if (transferPath.Count > 0)
                {
                    var s = transferPath[MoveStepIndex];
                    _vPosition.X = s.X;
                    _vPosition.Y = s.Y;
                    _vPosition.Z = s.Z;
                    if (!PosInRange(npc, _vPosition.X, _vPosition.Y, 3))
                    {
                        RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                        return;
                    }

                    if (MoveToForward)
                    {
                        if (MoveStepIndex == transferPath.Count - 1)
                        {
                            _log.Warn("we are ideally at the end point.");
                            PauseMove(npc);
                            MoveToForward = false; //turn back
                            MoveStepIndex--;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = transferPath[MoveStepIndex];
                            _vPosition.X = s.X;
                            _vPosition.Y = s.Y;
                            _vPosition.Z = s.Z;
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }

                        MoveStepIndex++;
                        _log.Warn("we have reached checkpoint go on...");
                    }
                    else
                    {
                        if (MoveStepIndex > 0)
                        {
                            MoveStepIndex--;
                            _log.Warn("we reached checkpoint go further ...");
                        }
                        else
                        {
                            _log.Warn("we are ideally at the starting point.");
                            PauseMove(npc);
                            MoveToForward = true; //turn back
                            MoveStepIndex++;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = transferPath[MoveStepIndex];
                            _vPosition.X = s.X;
                            _vPosition.Y = s.Y;
                            _vPosition.Z = s.Z;
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }
                    }

                    _log.Warn("walk to #" + MoveStepIndex);
                    s = transferPath[MoveStepIndex];
                    _vPosition.X = s.X;
                    _vPosition.Y = s.Y;
                    _vPosition.Z = s.Z;
                    RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                }
            }
        }
        public void NextPathOrPointInPath(Unit unit)
        {
            if (unit is Npc npc)
            {
                if (!MoveToPathEnabled)
                {
                    _log.Warn("Move disabled");
                    StopMove(npc);
                    return;
                }
                if (MovePath.Count > 0)
                {
                    var s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);
                    if (!PosInRange(npc, _vPosition.X, _vPosition.Y, 3))
                    {
                        RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                        return;
                    }

                    if (MoveToForward)
                    {
                        if (MoveStepIndex == MovePath.Count - 1)
                        {
                            _log.Warn("we are at the end point.");
                            PauseMove(npc);
                            MoveToForward = false; //turn back
                            MoveStepIndex--;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = MovePath[MoveStepIndex];
                            _vPosition.X = ExtractValue(s, 1);
                            _vPosition.Y = ExtractValue(s, 2);
                            _vPosition.Z = ExtractValue(s, 3);
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }

                        MoveStepIndex++;
                        _log.Warn("we have reached checkpoint go on...");
                    }
                    else
                    {
                        if (MoveStepIndex > 0)
                        {
                            MoveStepIndex--;
                            _log.Warn("we reached checkpoint go further ...");
                        }
                        else
                        {
                            _log.Warn("we are at the starting point.");
                            PauseMove(npc);
                            MoveToForward = true; //turn back
                            MoveStepIndex++;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = MovePath[MoveStepIndex];
                            _vPosition.X = ExtractValue(s, 1);
                            _vPosition.Y = ExtractValue(s, 2);
                            _vPosition.Z = ExtractValue(s, 3);
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }
                    }

                    _log.Warn("walk to #" + MoveStepIndex);
                    s = MovePath[MoveStepIndex];
                    _vPosition.X = ExtractValue(s, 1);
                    _vPosition.Y = ExtractValue(s, 2);
                    _vPosition.Z = ExtractValue(s, 3);
                    RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                }
                if (transferPath.Count > 0)
                {
                    var s = transferPath[MoveStepIndex];
                    _vPosition.X = s.X;
                    _vPosition.Y = s.Y;
                    _vPosition.Z = s.Z;
                    if (!PosInRange(npc, _vPosition.X, _vPosition.Y, 3))
                    {
                        RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                        return;
                    }

                    if (MoveToForward)
                    {
                        if (MoveStepIndex == transferPath.Count - 1)
                        {
                            _log.Warn("we are at the end point.");
                            PauseMove(npc);
                            MoveToForward = false; //turn back
                            MoveStepIndex--;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = transferPath[MoveStepIndex];
                            _vPosition.X = s.X;
                            _vPosition.Y = s.Y;
                            _vPosition.Z = s.Z;
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }

                        MoveStepIndex++;
                        _log.Warn("we have reached checkpoint go on...");
                    }
                    else
                    {
                        if (MoveStepIndex > 0)
                        {
                            MoveStepIndex--;
                            _log.Warn("we reached checkpoint go further ...");
                        }
                        else
                        {
                            _log.Warn("we are at the starting point.");
                            PauseMove(npc);
                            MoveToForward = true; //turn back
                            MoveStepIndex++;
                            _log.Warn("walk to #" + MoveStepIndex);
                            s = transferPath[MoveStepIndex];
                            _vPosition.X = s.X;
                            _vPosition.Y = s.Y;
                            _vPosition.Z = s.Z;
                            RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp, 20000);
                            return;
                        }
                    }

                    _log.Warn("walk to #" + MoveStepIndex);
                    s = transferPath[MoveStepIndex];
                    _vPosition.X = s.X;
                    _vPosition.Y = s.Y;
                    _vPosition.Z = s.Z;
                    RepeatMove(this, npc, _vPosition.X, _vPosition.Y, _vPosition.Z, pp);
                }
            }
        }

        public void Init(Unit unit) // Called when the script is start
        {
            Routes = new Dictionary<int, List<Point>>();
            NpcsRoutes = new Dictionary<uint, List<NpcsPathPoint>>();
            unit.WorldPos = new WorldPos();
            RecordPath = new List<string>();
            MovePath = new List<string>();
            transferPath = new List<Point>();
        }

        public void ReadPath() // Called when the script is start
        {
            try
            {
                MovePath = new List<string>();
                MovePath = File.ReadLines(GetMoveFileName()).ToList();
                _log.Info("Loading {0} transfer_path...", GetMoveFileName());
            }
            catch (Exception e)
            {
                _log.Warn("Error in read MovePath: {0}", e);
                //StopMove(npc);
            }
            //try
            //{
            //    RecordPath = new List<string>();
            //    //RecordPath = File.ReadLines(GetMoveFileName()).ToList();
            //}
            //catch (Exception e)
            //{
            //    _log.Warn("Error in read RecordPath: {0}", e);
            //    //StopMove(npc);
            //}
        }

        public List<Point> LoadPath(string namePath) //Вызывается при включении скрипта
        {
            _log.Info("TransfersPath: Loading {0} transfer_path...", namePath);
            transferPath = TransferManager.Instance.GetTransferPath(namePath);
            return transferPath;
        }
        public void LoadNpcPathFromRoutes(int steering) // загрузить путь под номером steering
        {
            _log.Info("TransfersPath: Loading {0} transfer_path...", steering);
            transferPath = Routes[steering];
        }
        public void LoadNpcPathFromRoutes2(uint templateId) // загрузить путь под номером templateId
        {
            //_log.Info("npcsPath: Loading {0} transfer_path...", steering);
            transferPath2 = NpcsRoutes[templateId];
        }
        public void LoadNpcPathFromNpcsRoutes(uint templateId) // загрузить путь под номером steering
        {
            //_log.Info("TransfersPath: Loading {0} transfer_path...", steering);
            NpcPath = NpcsRoutes[templateId];
        }

        public void ReadPath(string namePath) //Вызывается при включении скрипта
        {
            _log.Info("TransfersPath: Reading {0} transfer_path...", namePath);
            transferPath = TransferManager.Instance.GetTransferPath(namePath);
        }

        public void AddPath(string namePath) //Добавить продолжение маршрута
        {
            _log.Info("TransfersPath: Adding {0} transfer_path...", namePath);
            transferPath.AddRange(TransferManager.Instance.GetTransferPath(namePath));
        }

        public override void Execute(BaseUnit unit)
        {
            throw new NotImplementedException();
        }

        public override void Execute(Npc npc)
        {
            OnMove(npc);
        }

        public override void Execute(Transfer transfer)
        {
            throw new NotImplementedException();
        }
        public override void Execute(Gimmick gimmick)
        {
            throw new NotImplementedException();
        }
    }
}
