using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("AutomaticAuthorization", "k1lly0u/Arainrr", "1.0.0", ResourceId = 2063)]
    public class AutomaticAuthorization : RustPlugin
    {
        #region Fields

        [PluginReference] private readonly Plugin Clans, Friends;
        private bool init;
        private const string PERMISSION_USE = "automaticauthorization.use";
        private const string DATAPATH = "automaticauthorization_data";
        private Dictionary<ulong, HashSet<BaseEntity>> playerEntites = new Dictionary<ulong, HashSet<BaseEntity>>();

        #endregion Fields

        #region OxideHooks

        private void Init()
        {
            init = false;
            LoadData();
            LoadConfig();
            permission.RegisterPermission(PERMISSION_USE, this);
            cmd.AddChatCommand(configData.command, this, nameof(CmdAuth));
        }

        private void OnServerInitialized()
        {
            init = true;
            if (!configData.defaultAuto) Unsubscribe(nameof(OnPlayerInit));
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                if (entity == null || entity?.OwnerID == 0) continue;
                if (entity is BuildingPrivlidge || entity is AutoTurret)
                {
                    HashSet<BaseEntity> entities = new HashSet<BaseEntity>();
                    if (playerEntites.TryGetValue(entity.OwnerID, out entities))
                    {
                        entities.Add(entity);
                        playerEntites[entity.OwnerID] = entities;
                    }
                    else playerEntites.Add(entity.OwnerID, new HashSet<BaseEntity> { entity });
                }
            }
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (!shareData.headAddedPlayers.Contains(player.userID))
            {
                shareData.automatedClans.Add(player.userID);
                shareData.automatedFriends.Add(player.userID);
                shareData.headAddedPlayers.Add(player.userID);
            }
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), () => SaveData());

        private void Unload() => SaveData();

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (!init) return;
            if (entity == null || entity?.OwnerID == 0) return;
            if (entity is BuildingPrivlidge || entity is AutoTurret)
            {
                HashSet<BaseEntity> entities = new HashSet<BaseEntity>();
                if (playerEntites.TryGetValue(entity.OwnerID, out entities))
                {
                    entities.Add(entity);
                    playerEntites[entity.OwnerID] = entities;
                }
                else playerEntites.Add(entity.OwnerID, new HashSet<BaseEntity> { entity });

                if (!permission.UserHasPermission(entity.OwnerID.ToString(), PERMISSION_USE)) return;
                BasePlayer player = RelationshipManager.FindByID(entity.OwnerID);
                if (entity is AutoTurret)
                {
                    (entity as AutoTurret).authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = entity.OwnerID,
                        username = player == null ? string.Empty : player.displayName,
                        ShouldPool = true
                    });
                }
                UpdateAuthList(new HashSet<BaseEntity> { entity }, entity.OwnerID, true);
            }
        }

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock?.GetParentEntity() == null || baseLock?.GetParentEntity()?.OwnerID == 0 || !baseLock.IsLocked()) return null;
            ulong ownerID = baseLock.GetParentEntity().OwnerID;
            if (!permission.UserHasPermission(ownerID.ToString(), PERMISSION_USE)) return null;
            if ((shareData.automatedClans.Contains(ownerID) && SameClan(ownerID, player.userID)) ||
                (shareData.automatedFriends.Contains(ownerID) && HasFriend(ownerID, player.userID)))
            {
                return true;
            }
            return null;
        }

        #endregion OxideHooks

        #region Functions

        private void UpdateAuthList(HashSet<BaseEntity> entities, ulong playerId, bool isSpawn = false)
        {
            if (!permission.UserHasPermission(playerId.ToString(), PERMISSION_USE)) return;
            BasePlayer player = RelationshipManager.FindByID(playerId);
            if (player == null) return;
            bool isAuto = false;
            HashSet<ulong> sharePlayers = new HashSet<ulong> { player.userID };
            if (shareData.automatedClans.Contains(player.userID))
            {
                isAuto = true;
                foreach (var id in GetClanMembers(player.userID))
                    sharePlayers.Add(id);
            }
            if (shareData.automatedFriends.Contains(player.userID))
            {
                isAuto = true;
                foreach (var id in GetFriends(player.userID))
                    sharePlayers.Add(id);
            }
            if (isAuto == false) return;
            SortAuthList(entities, sharePlayers.ToList(), player, isSpawn);
        }

        private void SortAuthList(HashSet<BaseEntity> entities, List<ulong> authList, BasePlayer player = null, bool isSpawn = false)
        {
            Dictionary<ulong, string> friendData = new Dictionary<ulong, string>();
            for (int i = 0; i < authList.Count; i++)
            {
                var foundPlayer = RelationshipManager.FindByID(authList[i]);
                if (foundPlayer != null)
                    friendData.Add(authList[i], foundPlayer.displayName);
                else
                    friendData.Add(authList[i], string.Empty);
            }
            HashSet<BuildingPrivlidge> buildingPrivlidges = new HashSet<BuildingPrivlidge>();
            HashSet<AutoTurret> autoTurrets = new HashSet<AutoTurret>();
            foreach (var entity in entities)
            {
                if (entity == null || entity.IsDestroyed) continue;
                if (entity is BuildingPrivlidge)
                    buildingPrivlidges.Add(entity as BuildingPrivlidge);
                else autoTurrets.Add(entity as AutoTurret);
            }
            AuthToCupboard(buildingPrivlidges, friendData);
            AuthToTurret(autoTurrets, friendData);
            NextTick(() =>
            {
                if (player == null) return;
                player.SendNetworkUpdateImmediate();
                if (!isSpawn) return;
                if (buildingPrivlidges.Count > 0)
                    Print(player, Lang("cupboardSuccess", player.UserIDString, authList.Count - 1, buildingPrivlidges.Count()));
                if (autoTurrets.Count > 0)
                    Print(player, Lang("turretSuccess", player.UserIDString, authList.Count - 1, autoTurrets.Count()));
            });
        }

        private void AuthToCupboard(HashSet<BuildingPrivlidge> buildingPrivlidges, Dictionary<ulong, string> authList)
        {
            foreach (var buildingPrivlidge in buildingPrivlidges)
            {
                buildingPrivlidge.authorizedPlayers.Clear();
                foreach (var friend in authList)
                {
                    buildingPrivlidge.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = friend.Key,
                        username = friend.Value,
                        ShouldPool = true,
                    });
                }
                buildingPrivlidge.SendNetworkUpdateImmediate();
            }
        }

        private void AuthToTurret(HashSet<AutoTurret> autoTurrets, Dictionary<ulong, string> authList)
        {
            foreach (var autoTurret in autoTurrets)
            {
                bool isOnline = false;
                if (autoTurret.IsOnline())
                {
                    autoTurret.SetIsOnline(false);
                    isOnline = true;
                }
                autoTurret.authorizedPlayers.Clear();
                foreach (var friend in authList)
                {
                    autoTurret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                    {
                        userid = friend.Key,
                        username = friend.Value,
                        ShouldPool = true,
                    });
                }
                autoTurret.SendNetworkUpdateImmediate();
                if (isOnline)
                    autoTurret.SetIsOnline(true);
            }
        }

        #region Clan

        private void OnClanUpdate(string clanName)
        {
            foreach (var member in GetClanMembers(clanName))
                if (playerEntites.ContainsKey(member))
                    UpdateAuthList(playerEntites[member], member);
        }

        private List<ulong> GetClanMembers(ulong ownerId)
        {
            var clanName = Clans?.Call("GetClanOf", ownerId);
            if (clanName != null)
                return GetClanMembers((string)clanName);
            return new List<ulong>();
        }

        private List<ulong> GetClanMembers(string clanName)
        {
            List<ulong> authList = new List<ulong>();
            var clan = Clans?.Call("GetClan", clanName);
            if (clan != null && clan is JObject)
            {
                var members = (clan as JObject).GetValue("members");
                if (members != null && members is JArray)
                {
                    foreach (var member in (JArray)members)
                    {
                        ulong ID;
                        if (!ulong.TryParse(member.ToString(), out ID)) continue;
                        authList.Add(ID);
                    }
                }
            }
            return authList;
        }

        private bool SameClan(ulong playerID, ulong otherPlayerID)
        {
            if (Clans == null) return false;
            var playerClan = (string)Clans.Call("GetClanOf", playerID);
            var otherPlayerClan = (string)Clans.Call("GetClanOf", otherPlayerID);
            if (playerClan == null || otherPlayerClan == null) return false;
            return playerClan == otherPlayerClan;
        }

        #endregion Clan

        #region Friend

        private void OnFriendAdded(string playerID, string friendID) => UpdateFriendAuthList(playerID, friendID);

        private void OnFriendRemoved(string playerID, string friendID) => UpdateFriendAuthList(playerID, friendID);

        private void UpdateFriendAuthList(string playerID, string friendID)
        {
            ulong ID = 0;
            if (!ulong.TryParse(playerID, out ID)) return;
            if (playerEntites.ContainsKey(ID))
                UpdateAuthList(playerEntites[ID], ID);
        }

        private List<ulong> GetFriends(ulong playerID)
        {
            var friends = Friends?.Call("GetFriends", playerID);
            if (friends is ulong[])
                return (friends as ulong[]).ToList();
            return new List<ulong>();
        }

        private bool HasFriend(ulong playerID, ulong otherPlayerID)
        {
            if (Friends == null) return false;
            return (bool)Friends.Call("HasFriend", playerID, otherPlayerID);
        }

        #endregion Friend

        #endregion Functions

        #region ChatCommands

        private void CmdAuth(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                if (args == null || args.Length == 0)
                {
                    StringBuilder stringBuilder = new StringBuilder();
                    if (Clans)
                    {
                        stringBuilder.AppendLine(Lang("clanSyn", player.UserIDString, configData.command));
                        stringBuilder.AppendLine(Lang("autoShareClans", player.UserIDString, (shareData.automatedClans.Contains(player.userID) ? Lang("enabled", player.UserIDString) : Lang("disabled", player.UserIDString))));
                    }
                    if (Friends)
                    {
                        stringBuilder.AppendLine(Lang("friendSyn", player.UserIDString, configData.command));
                        stringBuilder.AppendLine(Lang("autoShareFriends", player.UserIDString, (shareData.automatedFriends.Contains(player.userID) ? Lang("enabled", player.UserIDString) : Lang("disabled", player.UserIDString))));
                    }
                    if (!Clans && !Friends)
                    {
                        Print(player, Lang("noSharePlugin", player.UserIDString));
                    }
                    else Print(player, $"\n{stringBuilder.ToString()}");
                    return;
                }

                switch (args[0].ToLower())
                {
                    case "autoclan":
                        if (shareData.automatedClans.Contains(player.userID))
                        {
                            shareData.automatedClans.Remove(player.userID);
                            Print(player, Lang("autoClansDisabled", player.UserIDString));
                        }
                        else
                        {
                            shareData.automatedClans.Add(player.userID);
                            Print(player, Lang("autoClansEnabled", player.UserIDString));
                        }
                        if (playerEntites.ContainsKey(player.userID))
                            UpdateAuthList(playerEntites[player.userID], player.userID);
                        return;

                    case "autofriend":
                        if (shareData.automatedFriends.Contains(player.userID))
                        {
                            shareData.automatedFriends.Remove(player.userID);
                            Print(player, Lang("autoFriendsDisabled", player.UserIDString));
                        }
                        else
                        {
                            shareData.automatedFriends.Add(player.userID);
                            Print(player, Lang("autoFriendsEnabled", player.UserIDString));
                        }
                        if (playerEntites.ContainsKey(player.userID))
                            UpdateAuthList(playerEntites[player.userID], player.userID);
                        return;

                    default:
                        return;
                }
            }
        }

        #endregion ChatCommands

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Default automatic authorization")]
            public bool defaultAuto = false;

            [JsonProperty(PropertyName = "Chat command")]
            public string command = "autoauth";

            [JsonProperty(PropertyName = "Chat prefix")]
            public string prefix = "[AutoAuth]:";

            [JsonProperty(PropertyName = "Chat prefix color")]
            public string prefixColor = "#00FFFF";

            [JsonProperty(PropertyName = "Chat steamID icon")]
            public ulong steamIDIcon = 0;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
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
            configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion ConfigurationFile

        #region DataFile

        private ShareData shareData;

        private class ShareData
        {
            public HashSet<ulong> automatedClans = new HashSet<ulong>();
            public HashSet<ulong> automatedFriends = new HashSet<ulong>();
            public HashSet<ulong> headAddedPlayers = new HashSet<ulong>();
        }

        private void LoadData()
        {
            try
            {
                shareData = Interface.Oxide.DataFileSystem.ReadObject<ShareData>(DATAPATH);
            }
            catch
            {
                ClearData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(DATAPATH, shareData);

        private void OnNewSave(string filename) => ClearData();

        private void ClearData()
        {
            shareData = new ShareData();
            SaveData();
        }

        #endregion DataFile

        #region LanguageFile

        private void Print(BasePlayer player, string message) => Player.Message(player, message, $"<color={configData.prefixColor}>{configData.prefix}</color>", configData.steamIDIcon);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"turretSuccess", "Successfully added <color=#ce422b>{0}</color> friends/clan members to <color=#ce422b>{1}</color> turrets auth list" },
                {"cupboardSuccess", "Successfully added <color=#ce422b>{0}</color> friends/clan members to <color=#ce422b>{1}</color> cupboards auth list" },
                {"clanSyn", "<color=#ce422b>/{0} autoclan</color> - Automatically authorizes your clan mates to objects when you place them" },
                {"friendSyn", "<color=#ce422b>/{0} autofriend</color> - Automatically authorizes your friends to objects when you place them" },
                {"noSharePlugin", "Clans and Friends is not installed on this server. Unable to automatically authorize other players" },
                {"autoClansDisabled", "You have disabled automatic authorization for clan members" },
                {"autoClansEnabled", "You have enabled automatic authorization for clan members" },
                {"autoFriendsDisabled", "You have disabled automatic authorization for friends" },
                {"autoFriendsEnabled", "You have enabled automatic authorization for friends" },
                {"enabled", "<color=#8ee700>Enabled</color>" },
                {"disabled", "<color=#ce422b>Disabled</color>" },
                {"autoShareClans", "Auto share for clans is: {0}" },
                {"autoShareFriends", "Auto share for friends is: {0}" },
            }, this);
        }

        #endregion LanguageFile
    }
}