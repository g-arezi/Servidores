using System.Collections.Generic;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins {
    [Info("Auto Plant", "Egor Blagov", "1.0.0")]
    [Description("Grants ability to plant whole planter box at once")]
    class AutoPlant : RustPlugin {
        private const string permUse = "autoplant.use";
        private void Init() {
            permission.RegisterPermission(permUse, this);
        }

        private void OnEntityBuilt(Planner plan, GameObject seed) {
            BasePlayer player = plan.GetOwnerPlayer();

            if (player == null)
                return;

            if (!permission.UserHasPermission(player.UserIDString, permUse)) {
                return;
            }

            var plant = seed.GetComponent<PlantEntity>();

            if (plant == null) {
                return;
            }

            var held = player.GetActiveItem();
            NextTick(() => {
                if (plant.GetParentEntity() is PlanterBox && player.serverInput.IsDown(BUTTON.SPRINT)) {
                    var planterBox = plant.GetParentEntity() as PlanterBox;
                    if (held.amount == 0) {
                        return;
                    }
                    var construction = PrefabAttribute.server.Find<Construction>(plan.GetDeployable().prefabID);
                    List<Construction.Target> targets = Pool.GetList<Construction.Target>();
                    foreach (var sock in PrefabAttribute.server.FindAll<Socket_Base>(planterBox.prefabID)) {
                        if (!sock.female) {
                            continue;
                        }
                        var socketPoint = planterBox.transform.TransformPoint(sock.worldPosition);
                        Construction.Target target = new Construction.Target();

                        target.entity = planterBox;
                        target.ray = new Ray(socketPoint + Vector3.up * 1.0f, Vector3.down);
                        target.onTerrain = false;
                        target.position = socketPoint;
                        target.normal = Vector3.up;
                        target.rotation = new Vector3();
                        target.player = player;
                        target.valid = true;
                        target.socket = sock;
                        target.inBuildingPrivilege = true;

                        if (!IsFree(construction, target)) {
                            continue;
                        }

                        targets.Add(target);
                    }

                    Unsubscribe(nameof(OnEntityBuilt));
                    foreach (var target in targets) {
                        plan.DoBuild(target, construction);
                        if (held.amount == 0) {
                            break;
                        }
                    }
                    Subscribe(nameof(OnEntityBuilt));

                    Pool.FreeList(ref targets);
                }
            });
        }

        private bool IsFree(Construction common, Construction.Target target) {
            List<Socket_Base> list = Facepunch.Pool.GetList<Socket_Base>();
            common.FindMaleSockets(target, list);
            Socket_Base socketBase = list[0];
            Facepunch.Pool.FreeList(ref list);
            return !target.entity.IsOccupied(target.socket) && socketBase.CheckSocketMods(socketBase.DoPlacement(target));
        }
    }
}
