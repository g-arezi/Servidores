using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Back Pump Jack", "Arainrr", "1.3.0")]
    [Description("Obtain oil crater using survey charge.")]
    internal class BackPumpJack : RustPlugin
    {
        private const string oilEntityPrefab = "assets/prefabs/tools/surveycharge/survey_crater_oil.prefab";
        private Dictionary<SurveyCharge, OilConfigInfo> waitingSurveyCharge = new Dictionary<SurveyCharge, OilConfigInfo>();
        private HashSet<SurveyCrater> readSurveyCrater = new HashSet<SurveyCrater>();
        private List<OilEntityInfo> ailveOilEntities = new List<OilEntityInfo>();
        private List<OilEntityInfo> oilEntitiesData = new List<OilEntityInfo>();
        private ItemDefinition crudeItemDefinition;

        private void Init()
        {
            LoadConfig();
            if (!_config._cantDamage) Unsubscribe(nameof(OnEntityTakeDamage));
            if (!_config._cantDeploy) Unsubscribe(nameof(CanBuild));
            foreach (var x in _config._permissionList)
                if (!permission.PermissionExists(x.Permission, this))
                    permission.RegisterPermission(x.Permission, this);
        }

        private void OnServerInitialized()
        {
            LoadData();
            crudeItemDefinition = ItemManager.itemList.Find(x => x.shortname == "crude.oil");
            foreach (var surveyCrater in UnityEngine.Object.FindObjectsOfType<SurveyCrater>())
            {
                if (surveyCrater.ShortPrefabName != "survey_crater_oil" || surveyCrater.OwnerID == 0) continue;
                var resourceDeposit = ResourceDepositManager.GetOrCreate(surveyCrater.transform.position);
                if (resourceDeposit == null) continue;
                foreach (var resourceDepositEntry in resourceDeposit._resources)
                    ailveOilEntities.Add(new OilEntityInfo { pos = surveyCrater.transform.position, amount = resourceDepositEntry.amount, workNeeded = resourceDepositEntry.workNeeded });
            }
            CheckValidData();
        }

        private void CheckValidData()
        {
            var miningQuarries = UnityEngine.Object.FindObjectsOfType<MiningQuarry>().Where(miningQuarry => miningQuarry.ShortPrefabName == "mining.pumpjack" && miningQuarry.OwnerID != 0);
            if (miningQuarries.Count() <= 0) return;
            for (int i = 0; i < oilEntitiesData.Count(); i++)
            {
                bool validData = false;
                foreach (var miningQuarry in miningQuarries)
                {
                    if (Vector3.Distance(oilEntitiesData[i].pos, miningQuarry.transform.position) < 2f)
                    {
                        validData = true;
                        AddOilResource(oilEntitiesData[i]);
                    }
                }
                if (!validData)
                    oilEntitiesData.Remove(oilEntitiesData[i]);
            }
            SaveData();
        }

        private void AddOilResource(OilEntityInfo oilEntityInfo)
        {
            var resourceDeposit = ResourceDepositManager.GetOrCreate(oilEntityInfo.pos);
            if (resourceDeposit == null || crudeItemDefinition == null) return;
            resourceDeposit._resources.Clear();
            resourceDeposit.Add(crudeItemDefinition, 1, oilEntityInfo.amount, oilEntityInfo.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.OIL, true);
        }

        private OilConfigInfo HasPermission(BasePlayer player)
        {
            OilConfigInfo oilConfigInfo = new OilConfigInfo();
            int priority = 0;
            foreach (var oil in _config._permissionList)
            {
                if (permission.UserHasPermission(player.UserIDString, oil.Permission) && oil.Priority >= priority)
                {
                    priority = oil.Priority;
                    oilConfigInfo = oil;
                }
            }
            return oilConfigInfo;
        }

        private void OnExplosiveThrown(BasePlayer player, SurveyCharge surveyCharge)
        {
            if (surveyCharge == null) return;
            OilConfigInfo oilConfigInfo = HasPermission(player);
            if (oilConfigInfo == null || oilConfigInfo.Chance <= 0) return;
            surveyCharge.OwnerID = player.userID;
            waitingSurveyCharge.Add(surveyCharge, oilConfigInfo);
        }

        private void OnEntityKill(SurveyCharge surveyCharge)
        {
            if (surveyCharge == null) return;
            OilConfigInfo oilConfigInfo = new OilConfigInfo();
            if (waitingSurveyCharge.TryGetValue(surveyCharge, out oilConfigInfo))
            {
                waitingSurveyCharge.Remove(surveyCharge);
                Vector3 surveyChargePos = surveyCharge.transform.position;
                ulong ownerID = surveyCharge.OwnerID;
                NextTick(() =>
                {
                    List<SurveyCrater> surveyCraterList = Pool.GetList<SurveyCrater>();
                    Vis.Entities(surveyChargePos, 1f, surveyCraterList);
                    foreach (var surveyCrater in surveyCraterList)
                    {
                        if (readSurveyCrater.Contains(surveyCrater)) continue;
                        if (UnityEngine.Random.Range(0f, 100f) <= oilConfigInfo.Chance)
                        {
                            var oilEntity = GameManager.server.CreateEntity(oilEntityPrefab, surveyCrater.transform.position) as SurveyCrater;
                            if (oilEntity == null || crudeItemDefinition == null) continue;
                            surveyCrater.Kill();
                            oilEntity.Spawn();
                            oilEntity.OwnerID = ownerID;
                            var resourceDeposit = ResourceDepositManager.GetOrCreate(oilEntity.transform.position);
                            if (resourceDeposit == null) continue;
                            resourceDeposit._resources.Clear();
                            float workNeeded = 45f / UnityEngine.Random.Range(oilConfigInfo.PMMin, oilConfigInfo.PMMax);
                            int amount = UnityEngine.Random.Range(50000, 100000);
                            resourceDeposit.Add(crudeItemDefinition, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.OIL, true);
                            ailveOilEntities.Add(new OilEntityInfo { pos = oilEntity.transform.position, amount = amount, workNeeded = workNeeded });
                            readSurveyCrater.Add(oilEntity);
                        }
                        readSurveyCrater.Add(surveyCrater);
                    }
                    Pool.FreeList(ref surveyCraterList);
                });
            }
        }

        private object OnEntityTakeDamage(SurveyCrater surveyCrater, HitInfo info)
        {
            if (surveyCrater?.ShortPrefabName == "survey_crater_oil" && surveyCrater?.OwnerID != 0)
            {
                if (info?.InitiatorPlayer != null)
                {
                    var player = info.InitiatorPlayer;
                    if (info.InitiatorPlayer.userID != surveyCrater?.OwnerID)
                    {
                        Print(player, Lang("NoDamage", player.UserIDString));
                        return true;
                    }
                    else
                        return null;
                }
                return true;
            }
            return null;
        }

        private object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            var targetEntity = target.entity as SurveyCrater;
            if (targetEntity == null || targetEntity?.OwnerID == 0) return null;
            var player = planner?.GetOwnerPlayer();
            if (player == null) return null;
            if (targetEntity.ShortPrefabName == "survey_crater_oil" && targetEntity.OwnerID != player.userID)
            {
                Print(player, Lang("NoDeploy", player.UserIDString));
                return true;
            }
            return null;
        }

        private void OnEntityBuilt(Planner planner, GameObject obj)
        {
            var entity = obj?.GetComponent<BaseEntity>();
            if (entity == null || entity?.ShortPrefabName != "mining.pumpjack" || entity?.OwnerID == 0) return;
            BasePlayer player = planner?.GetOwnerPlayer();
            if (player == null) return;
            foreach (var oilEntityInfo in ailveOilEntities)
            {
                if (Vector3.Distance(oilEntityInfo.pos, entity.transform.position) < 2f)
                {
                    oilEntitiesData.Add(oilEntityInfo);
                    SaveData();
                }
            }
        }

        private void OnNewSave(string filename) => ClearData();

        private class OilEntityInfo
        {
            public Vector3 pos;
            public int amount;
            public float workNeeded;
        }

        #region DataFile

        private void LoadData()
        {
            try
            {
                oilEntitiesData = Interface.Oxide.DataFileSystem.ReadObject<List<OilEntityInfo>>(Name);
            }
            catch
            {
                ClearData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, oilEntitiesData);

        private void ClearData()
        {
            oilEntitiesData = new List<OilEntityInfo>();
            SaveData();
        }

        #endregion DataFile

        #region ConfigurationFile

        private ConfigurationFile _config;

        private class ConfigurationFile
        {
            [JsonProperty(PropertyName = "Prefix")]
            public string _prefix;

            [JsonProperty(PropertyName = "Prefix color")]
            public string _prefixColor;

            [JsonProperty(PropertyName = "SteamID icon")]
            public ulong _steamIDIcon;

            [JsonProperty(PropertyName = "Block hurt other player's oil crater")]
            public bool _cantDamage;

            [JsonProperty(PropertyName = "Block deploy pumpjack on other player's oil crater")]
            public bool _cantDeploy;

            [JsonProperty(PropertyName = "Permission List")]
            public List<OilConfigInfo> _permissionList;

            public static ConfigurationFile DefaultConfig()
            {
                return new ConfigurationFile
                {
                    _prefix = "[BackPumpJack]:",
                    _prefixColor = "#00FFFF",
                    _steamIDIcon = 0,
                    _cantDamage = true,
                    _cantDeploy = true,
                    _permissionList = new List<OilConfigInfo>
                    {
                        new OilConfigInfo
                        {
                            Permission = "backpumpjack.use",
                            Priority = 0,
                            Chance = 20f,
                            PMMin = 5f,
                            PMMax = 10f
                        },
                        new OilConfigInfo
                        {
                            Permission = "backpumpjack.vip",
                            Priority = 1,
                            Chance = 40f,
                            PMMin = 10f,
                            PMMax = 20f
                        }
                    }
                };
            }
        }

        private class OilConfigInfo
        {
            [JsonProperty(PropertyName = "Permission")]
            public string Permission;

            [JsonProperty(PropertyName = "Priority of permission")]
            public int Priority;

            [JsonProperty(PropertyName = "Oil crater chance")]
            public float Chance;

            [JsonProperty(PropertyName = "Minimum PM size")]
            public float PMMin;

            [JsonProperty(PropertyName = "Maximum PM size")]
            public float PMMax;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigurationFile>();
                if (_config == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _config = ConfigurationFile.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion ConfigurationFile

        #region LanguageFile

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoDamage"] = "You can't hurt other player's oil crater.",
                ["NoDeploy"] = "You can't deploy pumpjack on other player's oil crater.",
            }, this);
        }

        private void Print(BasePlayer player, string message)
        {
            Player.Message(player, message, $"<color={_config._prefixColor}>{_config._prefix}</color>", _config._steamIDIcon);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion LanguageFile
    }
}