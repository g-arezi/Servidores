using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("Clan Team", "deivismac", "1.0.1")]
    [Description("Adds clan members to the same team")]

    // Requires: Clans

    class ClanTeam : RustPlugin
    {

        #region Definitions

        [PluginReference]
        private Plugin Clans;

        private Dictionary<string, List<string>> clan = new Dictionary<string, List<string>>();

        #endregion

        #region Functions

        private bool compareTeams(List<ulong> currentIds, List<string> clanIds)
        {
            foreach (string id in clanIds)
            {
                if (!currentIds.Contains(ulong.Parse(id))) return false;
            }

            return true;
        }

        private void generateClanTeam(List<string> ids)
        {

            RelationshipManager.PlayerTeam team = RelationshipManager.Instance.CreateTeam();

            if (clan.ContainsKey(clanTag(ids[0]))) clan.Remove(clanTag(ids[0]));
            clan[clanTag(ids[0])] = new List<string>();

            foreach (string id in ids)
            {
                BasePlayer player = BasePlayer.FindByID(ulong.Parse(id));
                if (player != null)
                {
                    if (player.currentTeam != 0UL)
                    {
                        RelationshipManager.PlayerTeam current = RelationshipManager.Instance.FindTeam(player.currentTeam);
                        current.RemovePlayer(player.userID);
                    }
                    team.AddPlayer(player);

                    clan[clanTag(id)].Add(player.UserIDString);

                    if (isAnOwner(player)) team.SetTeamLeader(player.userID);
                }
            }
        }


        private bool isAnOwner(BasePlayer p)
        {
            JObject clanInfo = Clans?.Call("GetClan", Clans?.Call("GetClanOf", p.userID) as string) as JObject;
            string owner = (string)clanInfo["owner"];
            if (owner == p.UserIDString)
            {
                return true;
            }
            else return false;
        }

        private string clanTag(string id)
        {
            return Clans?.Call("GetClanOf", id) as string;
        }

        private List<string> clanPlayersTag(string tag)
        {
            var playersinList = new List<string>();

           

                JObject clanInfo = Clans?.Call("GetClan", tag) as JObject;
                JArray players = clanInfo["members"] as JArray;

               

                foreach (string id in players)
                {

                    playersinList.Add(id);
                }

      

            return playersinList;
        }

        private List<string>  clanPlayers(BasePlayer p)
        {
            var playersinList = new List<string>();

            JObject clanInfo = Clans?.Call("GetClan", Clans?.Call("GetClanOf", p.userID) as string) as JObject;
            JArray players = clanInfo["members"] as JArray;

            foreach(string id in players)
            {
                playersinList.Add(id);
            }

            return playersinList;
        }
        #endregion

        #region Hooks

        private void OnClanCreate(string tag)
        {
                timer.Once(1f, () =>
                {

                    var playersinList = new List<string>();

                    JObject clanInfo = Clans?.Call("GetClan", tag) as JObject;
                    JArray players = clanInfo["members"] as JArray;

                    foreach (string id in players)
                    {
                        playersinList.Add(id);
                    }

                    generateClanTeam(playersinList);

                });
        }

        private void OnClanUpdate(string tag)
        {
            generateClanTeam(clanPlayersTag(tag));
        }

        private void OnClanDestroy(string tag)
        {
            BasePlayer player = BasePlayer.FindByID(ulong.Parse(clan[tag][0]));
 
            if (player == null) return;
            RelationshipManager.PlayerTeam team = RelationshipManager.Instance.FindTeam(player.currentTeam);
            foreach(string id in clan[tag])
            {
                 team.RemovePlayer(ulong.Parse(id));
            }
            RelationshipManager.Instance.DisbandTeam(team);
            clan.Remove(tag);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (clanTag(player.UserIDString) == null) return;
            List<string> clanplayers = clanPlayers(player);
            if (player.currentTeam != 0UL)
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.Instance.FindTeam(player.currentTeam);
                List<ulong> ids = team.members;
                if (compareTeams(ids, clanplayers))
                {
                    return;
                }

             }
                generateClanTeam(clanplayers);
        }

    }

    #endregion

    }
