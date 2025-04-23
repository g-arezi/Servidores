using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Godmode", "Wulf/lukespragg/Arainrr", "4.2.2", ResourceId = 673)]
    [Description("Allows players with permission to be invulerable and god-like")]
    internal class Godmode : CovalencePlugin
    {
        #region Initialization

        private readonly Dictionary<string, DateTime> informHistory = new Dictionary<string, DateTime>();

        private const string permAdmin = "godmode.admin";
        private const string permInvulerable = "godmode.invulnerable";
        private const string permLootPlayers = "godmode.lootplayers";
        private const string permLootProtection = "godmode.lootprotection";
        private const string permNoAttacking = "godmode.noattacking";
        private const string permToggle = "godmode.toggle";
        private const string permUntiring = "godmode.untiring";

        private void Init()
        {
            LoadData();
            LoadConfig();
            AddCovalenceCommand(configData.godCommand, nameof(GodCommand));
            AddCovalenceCommand(configData.godsCommand, nameof(GodsCommand));

            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permInvulerable, this);
            permission.RegisterPermission(permLootPlayers, this);
            permission.RegisterPermission(permLootProtection, this);
            permission.RegisterPermission(permNoAttacking, this);
            permission.RegisterPermission(permToggle, this);
            permission.RegisterPermission(permUntiring, this);
        }

        private void OnServerInitialized()
        {
            foreach (var god in storeData.godPlayers)
            {
                var player = RelationshipManager.FindByID(ulong.Parse(god));
                if (player == null) continue;
                ModifyMetabolism(player, true);

                if (configData.showNamePrefix)
                    Rename(player, true);
            }
            CheckGods();
        }

        private void Unload()
        {
            foreach (var god in storeData.godPlayers)
            {
                var player = RelationshipManager.FindByID(ulong.Parse(god));
                if (player == null) continue;
                ModifyMetabolism(player, false);
                if (configData.showNamePrefix)
                    Rename(player, false);
            }
            SaveData();
        }

        private void CheckGods()
        {
            if (storeData.godPlayers.Count > 0)
            {
                Subscribe(nameof(CanBeWounded));
                Subscribe(nameof(CanLootPlayer));
                Subscribe(nameof(OnEntityTakeDamage));
                Subscribe(nameof(OnRunPlayerMetabolism));
            }
            else
            {
                Unsubscribe(nameof(CanBeWounded));
                Unsubscribe(nameof(CanLootPlayer));
                Unsubscribe(nameof(OnEntityTakeDamage));
                Unsubscribe(nameof(OnRunPlayerMetabolism));
            }
        }

        #endregion Initialization

        #region Commands

        private void GodCommand(IPlayer player, string command, string[] args)
        {
            if ((args.Length > 0 && !player.HasPermission(permAdmin)) || !player.HasPermission(permToggle))
            {
                Print(player, Lang("NotAllowed", player.Id, command));
                return;
            }
            var target = args.Length > 0 ? FindPlayer(args[0])?.IPlayer : player;
            if (target == null)
            {
                Print(player, Lang("PlayerNotFound", player.Id, args[0].Sanitize()));
                return;
            }

            if (player.Id == "server_console" && player == target)
            {
                Print(player, "The server console cannot use godmode");
                return;
            }
            ToggleGodmode(target, player);
        }

        private void GodsCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Print(player, Lang("NotAllowed", player.Id, command));
                return;
            }
            if (storeData.godPlayers.Count == 0)
                Print(player, Lang("NoGods", player.Id));
            else foreach (var god in storeData.godPlayers)
                {
                    IPlayer iPlayer = covalence.Players.FindPlayerById(god);
                    if (iPlayer == null) continue;
                    Print(player, $"[{god}] {iPlayer.Name}\n".TrimEnd());
                }
        }

        #endregion Commands

        #region Godmode Toggle

        private void Rename(BasePlayer player, bool isGod)
        {
            if (player == null) return;
            if (isGod && !player.displayName.Contains(configData.namePrefix))
                RenameFunction(player, $"{configData.namePrefix} {player.displayName}");
            else
                RenameFunction(player, player.displayName.Replace(configData.namePrefix, "").Trim());
        }

        private void RenameFunction(BasePlayer player, string name)
        {
            if (player?.IPlayer == null) return;
            name = (string.IsNullOrEmpty(name.Trim()) ? player.displayName : name);
            if (player.net?.connection != null) player.net.connection.username = name;
            player.displayName = name;
            player._name = name;
            player.IPlayer.Name = name;
            permission.UpdateNickname(player.UserIDString, name);
            player.SendNetworkUpdateImmediate();
        }

        private void EnableGodmode(IPlayer iPlayer)
        {
            var player = RelationshipManager.FindByID(ulong.Parse(iPlayer.Id));
            if (player == null) return;
            storeData.godPlayers.Add(player.UserIDString);
            if (configData.showNamePrefix)
                Rename(player, true);

            ModifyMetabolism(player, true);
            CheckGods();
        }

        private void DisableGodmode(IPlayer iPlayer)
        {
            var player = RelationshipManager.FindByID(ulong.Parse(iPlayer.Id));
            if (player == null) return;
            if (IsGod(player.UserIDString))
                storeData.godPlayers.Remove(player.UserIDString);
            if (configData.showNamePrefix)
                Rename(player, false);

            ModifyMetabolism(player, false);
            CheckGods();
        }

        private void ToggleGodmode(IPlayer target, IPlayer player)
        {
            object obj = Interface.CallHook("ChangeGodmodeState", target.Id, !IsGod(target.Id));
            if (obj != null) return;

            if (IsGod(target.Id))
            {
                DisableGodmode(target);
                if (target == player)
                    Print(player, Lang("GodmodeDisabled", player.Id));
                else
                {
                    Print(player, Lang("GodmodeDisabledFor", player.Id, target.Name));
                    Print(target, Lang("GodmodeDisabledBy", target.Id, player.Name));
                }
            }
            else
            {
                EnableGodmode(target);
                if (target == player)
                    Print(player, Lang("GodmodeEnabled", player.Id));
                else
                {
                    Print(player, Lang("GodmodeEnabledFor", player.Id, target.Name));
                    Print(target, Lang("GodmodeEnabledBy", target.Id, player.Name));
                }
                if (configData.timeLimit > 0)
                    timer.Once(configData.timeLimit, () => DisableGodmode(target));
            }
        }

        #endregion Godmode Toggle

        private object CanBeWounded(BasePlayer player) => IsGod(player.UserIDString) ? (object)false : null;

        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (target == null || looter == null) return null;
            if (permission.UserHasPermission(target.UserIDString, permLootProtection) && !permission.UserHasPermission(looter.UserIDString, permLootPlayers))
            {
                NextTick(() =>
                {
                    looter.EndLooting();
                    Print(looter, Lang("NoLooting", looter.UserIDString));
                });
                return false;
            }
            return null;
        }

        private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || (player?.userID.IsSteamId() == false)) return null;
            var attacker = info?.Initiator as BasePlayer;
            if (IsGod(player.UserIDString) && permission.UserHasPermission(player.UserIDString, permInvulerable))
            {
                if (configData.informOnAttack && attacker != null)
                    InformPlayers(player, attacker);
                NullifyDamage(ref info);
                return true;
            }
            if (attacker != null && IsGod(attacker.UserIDString) && permission.UserHasPermission(attacker.UserIDString, permNoAttacking))
            {
                if (configData.informOnAttack)
                    InformPlayers(player, attacker);
                NullifyDamage(ref info);
                return true;
            }
            return null;
        }

        private object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player, float delta)
        {
            if (player == null) return null;
            if (!IsGod(player.UserIDString)) return null;
            metabolism.hydration.value = 250;
            if (!permission.UserHasPermission(player.UserIDString, permUntiring)) return null;
            var craftLevel = player.currentCraftLevel;
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench1, craftLevel == 1f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench2, craftLevel == 2f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench3, craftLevel == 3f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.NoSprint, false);
            return true;
        }

        #region RUST Function

        private void NullifyDamage(ref HitInfo info)
        {
            info.damageTypes = new DamageTypeList();
            info.HitMaterial = 0;
            info.PointStart = Vector3.zero;
        }

        private static void ModifyMetabolism(BasePlayer player, bool isGod)
        {
            if (player == null) return;
            if (isGod)
            {
                player.health = player._maxHealth;
                player.metabolism.bleeding.max = 0;
                player.metabolism.bleeding.value = 0;
                player.metabolism.calories.min = 500;
                player.metabolism.calories.value = 500;
                player.metabolism.dirtyness.max = 0;
                player.metabolism.dirtyness.value = 0;
                player.metabolism.heartrate.min = 0.5f;
                player.metabolism.heartrate.max = 0.5f;
                player.metabolism.heartrate.value = 0.5f;
                //player.metabolism.hydration.min = 250;
                player.metabolism.hydration.value = 250;
                player.metabolism.oxygen.min = 1;
                player.metabolism.oxygen.value = 1;
                player.metabolism.poison.max = 0;
                player.metabolism.poison.value = 0;
                player.metabolism.radiation_level.max = 0;
                player.metabolism.radiation_level.value = 0;
                player.metabolism.radiation_poison.max = 0;
                player.metabolism.radiation_poison.value = 0;
                player.metabolism.temperature.min = 32;
                player.metabolism.temperature.max = 32;
                player.metabolism.temperature.value = 32;
                player.metabolism.wetness.max = 0;
                player.metabolism.wetness.value = 0;
            }
            else
            {
                player.metabolism.bleeding.min = 0;
                player.metabolism.bleeding.max = 1;
                player.metabolism.calories.min = 0;
                player.metabolism.calories.max = 500;
                player.metabolism.dirtyness.min = 0;
                player.metabolism.dirtyness.max = 100;
                player.metabolism.heartrate.min = 0;
                player.metabolism.heartrate.max = 1;
                //player.metabolism.hydration.min = 0;
                player.metabolism.hydration.max = 250;
                player.metabolism.oxygen.min = 0;
                player.metabolism.oxygen.max = 1;
                player.metabolism.poison.min = 0;
                player.metabolism.poison.max = 100;
                player.metabolism.radiation_level.min = 0;
                player.metabolism.radiation_level.max = 100;
                player.metabolism.radiation_poison.min = 0;
                player.metabolism.radiation_poison.max = 500;
                player.metabolism.temperature.min = -100;
                player.metabolism.temperature.max = 100;
                player.metabolism.wetness.min = 0;
                player.metabolism.wetness.max = 1;
            }
            player.metabolism.SendChangesToClient();
        }

        private void InformPlayers(BasePlayer victim, BasePlayer attacker)
        {
            if (victim == null || attacker == null || victim == attacker) return;
            if (!informHistory.ContainsKey(victim.UserIDString)) informHistory.Add(victim.UserIDString, DateTime.MinValue);
            if (!informHistory.ContainsKey(attacker.UserIDString)) informHistory.Add(attacker.UserIDString, DateTime.MinValue);
            if (IsGod(victim.UserIDString))
            {
                if (DateTime.Now.Subtract(informHistory[victim.UserIDString]).TotalSeconds > 15)
                {
                    Print(attacker, Lang("InformAttacker", attacker.UserIDString, victim.displayName));
                    informHistory[victim.UserIDString] = DateTime.Now;
                }
                if (DateTime.Now.Subtract(informHistory[attacker.UserIDString]).TotalSeconds > 15)
                {
                    Print(victim, Lang("InformVictim", victim.UserIDString, attacker.displayName));
                    informHistory[attacker.UserIDString] = DateTime.Now;
                }
            }
            else if (IsGod(attacker.UserIDString))
            {
                if (DateTime.Now.Subtract(informHistory[victim.UserIDString]).TotalSeconds > 15)
                {
                    Print(attacker, Lang("CantAttack", attacker.UserIDString, victim.displayName));
                    informHistory[victim.UserIDString] = DateTime.Now;
                }
                if (DateTime.Now.Subtract(informHistory[attacker.UserIDString]).TotalSeconds > 15)
                {
                    Print(victim, Lang("InformVictim", victim.UserIDString, attacker.displayName));
                    informHistory[attacker.UserIDString] = DateTime.Now;
                }
            }
        }

        #endregion RUST Function

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Inform On Attack (true/false)")]
            public bool informOnAttack;

            [JsonProperty(PropertyName = "Show Name Prefix (true/false)")]
            public bool showNamePrefix;

            [JsonProperty(PropertyName = "Name Prefix (Default [God])")]
            public string namePrefix;

            [JsonProperty(PropertyName = "Time Limit(Seconds, 0 to Disable)")]
            public float timeLimit;

            [JsonProperty(PropertyName = "Chat Prefix (Default [Godmode]:)")]
            public string prefix;

            [JsonProperty(PropertyName = "Chat Prefix color (Default #00FFFF)")]
            public string prefixColor;

            [JsonProperty(PropertyName = "God commands")]
            public string[] godCommand;

            [JsonProperty(PropertyName = "Gods commands")]
            public string[] godsCommand;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData()
                {
                    informOnAttack = true,
                    showNamePrefix = true,
                    timeLimit = 0f,
                    namePrefix = "[God]",
                    prefix = "[Godmode]:",
                    prefixColor = "#00FFFF",
                    godCommand = new string[] { "god", "godmode" },
                    godsCommand = new string[] { "gods", "godlist" },
                };
            }
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
            configData = ConfigData.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(configData);

        #endregion ConfigurationFile

        #region DataFile

        private StoreData storeData;

        private class StoreData
        {
            public HashSet<string> godPlayers = new HashSet<string>();
        }

        private void LoadData()
        {
            try
            {
                storeData = Interface.Oxide.DataFileSystem.ReadObject<StoreData>(Name);
            }
            catch
            {
                ClearData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storeData);

        private void ClearData()
        {
            storeData = new StoreData();
            SaveData();
        }

        #endregion DataFile

        #region LanguageFile

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GodmodeDisabled"] = "You have <color=#FF4500>Disabled</color> godmode",
                ["GodmodeDisabledBy"] = "Your godmode has been <color=#FF4500>Disabled</color> by {0}",
                ["GodmodeDisabledFor"] = "You have <color=#FF4500>Disabled</color> godmode for {0}",
                ["GodmodeEnabled"] = "You have <color=#00FF00>Enabled</color> godmode",
                ["GodmodeEnabledBy"] = "Your godmode has been <color=#00FF00>Enabled</color> by {0}",
                ["GodmodeEnabledFor"] = "You have <color=#00FF00>Enabled</color> godmode for {0}",
                ["InformAttacker"] = "{0} is in godmode and can't take any damage",
                ["InformVictim"] = "{0} just tried to deal damage to you",
                ["CantAttack"] = "you is in godmode and can't attack {0}",
                ["NoGods"] = "No players currently have godmode enabled",
                ["NoLooting"] = "You are not allowed to loot a player with godmode",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["PlayerNotFound"] = "Player '{0}' was not found",
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion LanguageFile

        #region Helpers

        private void Print(IPlayer iPlayer, string message)
        {
            if (iPlayer == null || iPlayer?.IsConnected == false) return;
            if (iPlayer.Id == "server_console")
                iPlayer.Reply(message, $"{configData.prefix}");
            else
                iPlayer.Reply(message, $"<color={configData.prefixColor}>{configData.prefix}</color>");
        }

        private void Print(BasePlayer player, string message) => player.ChatMessage($"<color={configData.prefixColor}>{configData.prefix}</color> {message}");

        private static BasePlayer FindPlayer(string name) => BasePlayer.Find(name) ?? BasePlayer.FindSleeping(name) ?? null;

        private bool IsGod(string id) => storeData.godPlayers.Contains(id);

        #endregion Helpers
    }
}