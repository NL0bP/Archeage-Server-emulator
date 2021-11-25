using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;

using NLog;

namespace AAEmu.Game.Models.Game.Transfers.Paths
{
    [Serializable]
    public sealed class NpcsPath : IComparable<NpcsPath>, IComparable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        // это нужно записывать в json
        public uint ObjId { get; set; }     // здесь будет objId
        public string Name { get; set; }
        public uint Type { get; set; }   // TemplateId -> owner_id != 0, указывает на участок начала пути
        public List<NpcsPathPoint> Pos { get; set; }
        // =================================
        //                       objId, TemplateId
        private static readonly Dictionary<uint, uint> types = new Dictionary<uint, uint>();
        //                         idx, 
        private static readonly Dictionary<uint, NpcsPath> npcsPaths = new Dictionary<uint, NpcsPath>();
        public static List<NpcsPath> NpcsPaths { get { return npcsPaths.Values.ToList(); } }

        public NpcsPath()
        {
            Name = String.Empty;
            Pos = new List<NpcsPathPoint>();
        }

        public static void GetNpcPaths()
        {
            Clear();
            var contents = FileManager.GetFileContents($"{FileManager.AppPath}Data/Worlds/main_world/npc_paths.json");
            if (string.IsNullOrWhiteSpace(contents))
            {
                _log.Warn($"File {FileManager.AppPath}Data/Worlds/main_world/npc_paths.json doesn't exists or is empty.");
            }
            else
            {
                if (JsonHelper.TryDeserializeObject(contents, out List<NpcsPath> spawners, out _))
                {
                    foreach (var spawner in spawners)
                    {
                        AddNpc(spawner);
                    }
                }
                else
                {
                    throw new Exception($"SpawnManager: Parse {FileManager.AppPath}Data/Worlds/main_world/npc_paths.json file");
                }
            }
        }
        public static void AddNpc(NpcsPath spawner)
        {
            if (!npcsPaths.ContainsKey(spawner.ObjId))
            {
                npcsPaths.Add(spawner.ObjId, spawner);
            }
            // сортируем по Steering и PathPointIndex
            //spawner.Pos = spawner.Pos.OrderBy(x => x.Steering).ThenBy(x => x.PathPointIndex).ToList();
        }

        public static void AddPos(uint objid, NpcsPathPoint position)
        {
            var npcPath = new NpcsPath();

            try
            {
                npcPath.Type = NpcsPath.types[objid]; // get objid
            }
            catch (Exception e)
            {
                return;
            }

            npcPath.ObjId = objid;
            npcPath.Name = "";

            if (npcsPaths.TryGetValue(objid, out var val))
            {
                // value exists!
                //val.Pos.Add(position);
                npcPath.AddOrUpdatePos(val.Pos, position);
                //npcPath.AddAllPos(val.Pos, position);
                //npcPath.AddOrUpdateFirstPos(val.Pos, position);
                npcsPaths[objid] = val;
            }
            else
            {
                // add the value
                npcPath.Pos.Add(position);
                npcsPaths.Add(objid, npcPath);
            }
        }

        public void AddOrUpdatePos(List<NpcsPathPoint> pos, NpcsPathPoint position)
        {
            var valueExists = false;
            foreach (var p in pos)
            {
                if (p.X.Equals(position.X) && p.Y.Equals(position.Y) && p.Z.Equals(position.Z))
                {
                    valueExists = true;

                    // yay, value exists!
                    p.Flags = position.Flags;
                    //p.X = position.X; // координаты не обновляем, так как они есть
                    //p.Y = position.Y;
                    //p.Z = position.Z;
                    p.VelX = position.VelX;
                    p.VelY = position.VelY;
                    p.VelZ = position.VelZ;
                    p.RotationX = position.RotationY;
                    p.RotationY = position.RotationY;
                    p.RotationZ = position.RotationZ;
                    p.ActorDeltaMovementX = position.ActorDeltaMovementX;
                    p.ActorDeltaMovementY = position.ActorDeltaMovementY;
                    p.ActorDeltaMovementZ = position.ActorDeltaMovementZ;
                    p.ActorStance = position.ActorStance;
                    p.ActorAlertness = position.ActorAlertness;
                    p.ActorFlags = position.ActorFlags;
                    break;
                }
            }

            if (!valueExists)
            {
                // darn, lets add the value			
                pos.Add(position);
            }
        }

        //public void AddAllPos(List<NpcPathPoint> pos, NpcPathPoint position)
        //{
        //	var valueExists = false;
        //	foreach (var p in pos)
        //	{
        //		if (
        //			p.Steering.Equals(position.Steering)
        //			&& p.PathPointIndex.Equals(position.PathPointIndex)

        //			&& p.X.Equals(position.X)
        //			&& p.Y.Equals(position.Y)
        //			&& p.Z.Equals(position.Z)
        //			&& p.VelX.Equals(position.VelX)
        //			&& p.VelY.Equals(position.VelY)
        //			&& p.VelZ.Equals(position.VelZ)

        //			&& p.RotationY.Equals(position.RotationY)
        //			&& p.RotationY.Equals(position.RotationY)
        //			&& p.RotationZ.Equals(position.RotationZ)

        //			&& p.AngVelX.Equals(position.AngVelX)
        //			&& p.AngVelY.Equals(position.AngVelY)
        //			&& p.AngVelZ.Equals(position.AngVelZ)

        //			&& p.Speed.Equals(position.Speed)
        //			&& p.AngVelZ.Equals(position.AngVelZ)
        //			)
        //		{
        //			valueExists = true;
        //			break;
        //		}
        //	}

        //	if (!valueExists)
        //	{
        //		// darn, lets add the value			
        //		pos.Add(position);
        //	}
        //}

        public void AddAllPos(List<NpcsPathPoint> pos, NpcsPathPoint position)
        {
            // darn, lets add the value
            pos.Add(position);
        }
        public void AddOrUpdateFirstPos(List<NpcsPathPoint> pos, NpcsPathPoint position)
        {
            foreach (var p in pos)
            {
                if (p.X.Equals(position.X) && p.Y.Equals(position.Y) && p.Z.Equals(position.Z)
                    //&& p.Speed.Equals(position.Speed)
                    && p.VelX.Equals(position.VelX) && p.VelY.Equals(position.VelY) && p.VelZ.Equals(position.VelZ)
                //&& p.RotationZ.Equals(position.RotationZ)
                )
                {
                    // закомменитовать если нужна первая точка
                    // раскомментировать если нужна последняя точка
                    // yay, value exists!
                    //p.X = position.X;
                    //p.Y = position.Y;
                    //p.Z = position.Y;
                    //p.VelX = position.VelX;
                    //p.VelY = position.VelY;
                    //p.VelZ = position.VelZ;
                    //p.RotationX = position.RotationY;
                    //p.RotationY = position.RotationY;
                    //p.RotationZ = position.RotationZ;
                    //p.AngVelX = position.AngVelX;
                    //p.AngVelY = position.AngVelY;
                    //p.AngVelZ = position.AngVelZ;
                    //p.Steering = position.Steering;
                    //p.PathPointIndex = position.PathPointIndex;
                    //p.Speed = position.Speed;
                    //p.Reverse = position.Reverse;
                    break;
                }
                // darn, lets add the value
                pos.Add(position);
                break;
            }
        }

        public static void AddOrUpdate(uint key, uint newValue)
        {
            if (types.TryGetValue(key, out var val))
            {
                // yay, value exists!
                types[key] = val + newValue;
            }
            else
            {
                // darn, lets add the value
                types.Add(key, newValue);
            }
        }

        public static void AddPos(NpcsPath spawner)
        {
            if (!npcsPaths.ContainsKey(spawner.ObjId))
            {
                npcsPaths.Add(spawner.ObjId, spawner);
            }
            // сортируем по Steering и PathPointIndex
            //spawner.Pos = spawner.Pos.OrderBy(x => x.Steering).ThenBy(x => x.PathPointIndex).ToList();
        }

        public static void AddOrUpdate(Dictionary<uint, uint> dic, uint key, uint newValue)
        {
            if (dic.TryGetValue(key, out var val))
            {
                // yay, value exists!
                dic[key] = val + newValue;
            }
            else
            {
                // darn, lets add the value
                dic.Add(key, newValue);
            }
        }

        public static void AddTransferEquals(uint objid, uint tid, NpcsPathPoint position)
        {
            foreach (var npc in npcsPaths)
            {
                //if (npc.Value.Position.Equals(position))
                //if (npc.Value.Position != null && npc.Value != null && position != null && npc.Value.Position.Equals(new Vector2 { X = position.X, Y = position.Y }))
                //{
                //	//npc.Value.Title = title;
                //	npc.Value.Position = position;
                //	break;
                //}
            }
        }

        public static void Clear()
        {
            npcsPaths.Clear();
        }

        public int CompareTo(NpcsPath other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            return Type.CompareTo(other.Type);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return 1;
            }

            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            return obj is NpcsPath other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(NpcsPath)}");
        }

        public static bool operator <(NpcsPath left, NpcsPath right)
        {
            return Comparer<NpcsPath>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(NpcsPath left, NpcsPath right)
        {
            return Comparer<NpcsPath>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(NpcsPath left, NpcsPath right)
        {
            return Comparer<NpcsPath>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(NpcsPath left, NpcsPath right)
        {
            return Comparer<NpcsPath>.Default.Compare(left, right) >= 0;
        }
    }
    public class NpcMoveList
    {
        public uint objId;
        public uint templateId;
    }
    public class NpcMovePathList
    {
        public uint objId;
        public uint templateId;
        public string path;
    }
}
