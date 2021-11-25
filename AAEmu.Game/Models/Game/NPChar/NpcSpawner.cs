using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Transfers.Paths;
using AAEmu.Game.Models.Game.Units.Route;
using AAEmu.Game.Models.Game.World;

using NLog;

namespace AAEmu.Game.Models.Game.NPChar
{
    public class NpcSpawner : Spawner<Npc>
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly List<Npc> _spawned;
        private Npc _lastSpawn;
        private int _scheduledCount;
        private int _spawnCount;
        private static Dictionary<uint, bool> s_inUse = new Dictionary<uint, bool>();

        public uint Count { get; set; }

        public NpcSpawner()
        {
            _spawned = new List<Npc>();
            Count = 1;
        }

        public List<Npc> SpawnAll()
        {
            var list = new List<Npc>();
            for (var num = _scheduledCount; num < Count; num++)
            {
                var npc = Spawn(0);
                if (npc != null)
                {
                    list.Add(npc);
                }
            }

            return list;
        }


        public override Npc Spawn(uint objId)
        {
            var npc = NpcManager.Instance.Create(objId, UnitId);
            if (npc == null)
            {
                _log.Warn("Npc {0}, from spawn not exist at db", UnitId);
                return null;
            }

            npc.Spawner = this;
            npc.Position = Position.Clone();
            npc.Pos = new WorldPos(Helpers.ConvertLongX(Position.X), Helpers.ConvertLongY(Position.Y), Position.Z);
            npc.Rot = new Quaternion(Helpers.ConvertDirectionToRadian(Position.RotationX), Helpers.ConvertDirectionToRadian(Position.RotationY), Helpers.ConvertDirectionToRadian(Position.RotationZ), 1f);
            npc.Vel = new Vector3();
            npc.AngVel = new Vector3();

            if (npc.Position == null)
            {
                _log.Error("Can't spawn npc {1} from spawn {0}", Id, UnitId);
                return null;
            }

            // кому разрешено бродить по пути-дороге
            var npcMove = new List<NpcMoveList>();
            npcMove.Add(new NpcMoveList() { objId = 8271, templateId = 11999 }); // Forest Keeper Arthur
            npcMove.Add(new NpcMoveList() { objId = 8281, templateId = 12143 }); // Woodcutter Solace
            npcMove.Add(new NpcMoveList() { objId = 6165, templateId = 7035 }); // Weeding Soldier
            npcMove.Add(new NpcMoveList() { objId = 8124, templateId = 8172 });

            var npcMove2 = new List<NpcMovePathList>();
            npcMove2.Add(new NpcMovePathList() { objId = 8156, templateId = 8176, path = "guard3" }); // на площади, ходит вокруг фонтана
            npcMove2.Add(new NpcMovePathList() { objId = 8162, templateId = 8176, path = "guard1" }); // ходит по дороге
            npcMove2.Add(new NpcMovePathList() { objId = 8155, templateId = 8176, path = "guard2" }); // ходит по дороге

            // TODO добавить пути для Npc
            //8172,
            //3576,
            //3626,
            //7660,
            //4499,
            //3591,
            //3626,

            // использование путей из логов с помощью файла npc_paths.json
            if (!npc.IsInPatrol)
            {
                var path = new SimulationNpc(npc);
                var go = true;
                foreach (var nm in npcMove)
                {
                    // организуем последовательность "Дорог" для следования "Гвардов" и других Npc
                    if (npc.TemplateId != nm.templateId || npc.ObjId != nm.objId) { continue; }

                    var lnpp = new List<NpcsPathPoint>();
                    foreach (var np in NpcsPath.NpcsPaths.Where(np => np.ObjId == nm.objId && !s_inUse.ContainsKey(np.ObjId)))
                    {
                        lnpp.AddRange(np.Pos);
                        path.NpcsRoutes.TryAdd(npc.TemplateId, lnpp);
                        s_inUse.Add(np.ObjId, true);
                        break;
                    }
                    break;
                }

                if (path.NpcsRoutes.Count == 0)
                {
                    go = false;
                }
                else
                {
                    if (path.NpcsRoutes.Any(route => route.Value.Count < 2)) // TODO == 0
                    {
                        go = false;
                    }
                }
                //if (path.Routes2.Count != 0)
                if (go)
                {
                    path.LoadNpcPathFromNpcsRoutes(npc.TemplateId); // начнем с самого начала
                                                                    //_log.Warn("TransfersPath #" + transfer.TemplateId);
                                                                    //_log.Warn("First spawn myX=" + transfer.Position.X + " myY=" + transfer.Position.Y + " myZ=" + transfer.Position.Z + " rotZ=" + transfer.Rot.Z + " rotationZ=" + transfer.Position.RotationZ);
                    npc.IsInPatrol = true; // so as not to run the route a second time

                    //path.GoToPath(npc, true);
                    npc.SimulationNpc = path;
                    npc.SimulationNpc.FollowPath = true;
                }
                else
                {
                    //_log.Warn("No path found for Npc: " + npc.TemplateId + " ...");
                }
            }

            // использование путей из папки Path
            if (!npc.IsInPatrol)
            {
                var path = new SimulationNpc(npc);
                //var go = true;
                foreach (var nm in npcMove2)
                {
                    // организуем последовательность "Дорог" для следования "Гвардов" и других Npc
                    if (npc.TemplateId != nm.templateId || npc.ObjId != nm.objId) { continue; }

                    // считываем путь из файла
                    path.MoveFileName = nm.path;
                    path.ReadPath();
                    npc.IsInPatrol = true; // so as not to run the route a second time
                    //path.GoToPath(npc, true);
                    npc.SimulationNpc = path;
                    npc.SimulationNpc.FollowPath = true;
                    break;
                }

                //if (go)
                //{
                //    path.LoadPath(""); // считываем путь из файла
                //    npc.IsInPatrol = true; // so as not to run the route a second time
                //    path.GoToPath(npc, true);
                //    //npc.SimulationNpc = path;
                //    //npc.SimulationNpc.FollowPath = true;
                //}
                //else
                //{
                //    _log.Warn("No path found for Npc: " + npc.TemplateId + " ...");
                //}
            }
            
            npc.Spawn();
            _lastSpawn = npc;
            _spawned.Add(npc);
            _scheduledCount--;
            _spawnCount++;

            return npc;
        }

        public override void Despawn(Npc npc)
        {
            npc.Delete();
            if (npc.Respawn == DateTime.MinValue)
            {
                _spawned.Remove(npc);
                ObjectIdManager.Instance.ReleaseId(npc.ObjId);
                _spawnCount--;
            }

            if (_lastSpawn == null || _lastSpawn.ObjId == npc.ObjId)
            {
                _lastSpawn = _spawned.Count != 0 ? _spawned[_spawned.Count - 1] : null;
            }
        }

        public void DecreaseCount(Npc npc)
        {
            _spawnCount--;
            _spawned.Remove(npc);
            if (RespawnTime > 0 && (_spawnCount + _scheduledCount) < Count)
            {
                npc.Respawn = DateTime.UtcNow.AddSeconds(RespawnTime);
                SpawnManager.Instance.AddRespawn(npc);
                _scheduledCount++;
            }

            npc.Despawn = DateTime.UtcNow.AddSeconds(DespawnTime);
            SpawnManager.Instance.AddDespawn(npc);
        }
    }
}
