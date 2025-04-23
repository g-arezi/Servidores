using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Linq;

namespace Oxide.Plugins{
    [Info("RandomCommands", "Goku", "0.0.1")]
    [Description("Plugin have a multiples commands by Goku 0.0.1")]
        /*---------------------------------------------------------------------
                                 PLUGIN MADE BY GOKU
        ----------------------------------------------------------------------- */
    class RandomCommands : RustPlugin
    {
        #region Defaults:
        //Defaults variables
        //Plugin create by Goku
        //Plugin version 0.0.1
        //System of RandomCommands
        bool messageAgain = true;
        bool Changed;

        bool giveo = false;

        string tag { get; set; }
        string tagColor {get; set; }
        string nameSay {get; set; }
        string nameSayColor {get; set;}
        string messageColor {get; set;}

        string DEFAULTtag          = "RandomCommands";
        string DEFAULTtagColor     = "orange";
        string DEFAULTnameSay      = "<color=#F90133>{DEV}<color=#3E63F3>Goku</color></color>";
        string DEFAULTnameSayColor = "red";
        string DEFAULTmessageColor = "orange";

        void LoadDefaultConfig() => PrintWarning("New config are created! By Goku 0.0.1");

        void LoadConfigValue()
        {
            tag = GetConfig("Settings", "Chat tag", DEFAULTtag);
            nameSay = GetConfig("Settings", "Message name", DEFAULTnameSay);
			tagColor = GetConfig("Messages", "Chat tag color", DEFAULTtagColor);
            nameSayColor = GetConfig("Messages", "Message name color", DEFAULTnameSayColor);
            messageColor = GetConfig("Messages", "Message color", DEFAULTmessageColor);
			//---
            if (!Changed) return;
			//---
            PrintWarning("Alterações na config feitas!");
            SaveConfig();
        }

        void Loaded() {
            LoadConfigValue();
            permission.RegisterPermission("notice.admin", this);
        }
        [ConsoleCommand("reload")]
        void cmdReload (ConsoleSystem.Arg arg)
        {
            string args = arg.GetString(0, "text");
            rust.RunServerCommand($"oxide.reload {args}");
        }
        [ConsoleCommand("load")]
        void cmdLoad (ConsoleSystem.Arg arg)
        {
            string args = arg.GetString(0, "text");
            rust.RunServerCommand($"oxide.load {args}");
        }
        [ConsoleCommand("unload")]
        void cmdUnload (ConsoleSystem.Arg arg)
        {
            string args = arg.GetString(0, "text");
            rust.RunServerCommand($"oxide.unload {args}");
        }

        [ConsoleCommand("chat")]
        void cmdChat (ConsoleSystem.Arg arg)
        {
            string args = arg.GetString(1, "text");
            BasePlayer player = BasePlayer.Find(arg.GetString(0, "text"));
            if(player != null)
            {
                SendReply(player, "<color=orange>[DE GOKU PARA " + player.displayName + "]</color>: " + args);
            }
            else {
                Puts("Jogador não encontrado!");
                return;
            }
        }

        T GetConfig<T>(string category, string setting, T defaultvalue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if(data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                Changed = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultvalue;
            data[setting] = value;
            Changed = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }
        #endregion
        void Say(string msg) => Server.Broadcast($"<size=15><color={nameSayColor}>{nameSay}</color>: <color=yellow></></color><color=cyan> {msg}</color></size>", null, 76561198878194965);


        private object OnPlayerChat(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Connection.player as BasePlayer;
            string mensagem = arg.GetString(0, "text");
            if(mensagem.Contains("hack") || mensagem.Contains("rack") || mensagem.Contains("jacked"))
            {
                rust.SendChatMessage(player, "<size=15><color=red>RustDinasty</color>: </size>", "<size=15>Hack no servidor? Avise-me no discord: <color=orange>Goku#1010</color></size>");
                return false;
            }
            return null;
        }
        object OnServerMessage(string message, string name, string color, ulong id)
        {
            if(message.Contains("gave"))
            {
                if(!giveo)
                {
                    return true;
                }
                PrintToChat($"<size=15><color=red>[POLICIA]</color> : " + message + "</size>");
                return true;
            }
            if(name == "SERVER")
            {
                if(message.Contains("Kicking") || message.Contains("Ban"))
                {
                    PrintToChat($"<size=15><color=red>[POLICIA]</color> : " + message + "</size>");
                    return true;
                }
                Say(message);
                return true;
            }
            return null;
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            var pos = player.transform.position.ToString();
            if(messageAgain == false)
            {
                return;
            }
            messageAgain = false;
            Puts("O jogador " + player.displayName.ToString() + " está raidando!");
            PrintToChat($"<size=15><color=red>[POLICIA]</color> : O jogador <color=orange>" + player.displayName.ToString() + "</color> está raidando com <color=orange>BAZUCA</color> em <color=orange>" + pos + "</color></size>");
            timer.Once(5, () =>{
                messageAgain = true;
            });
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if(messageAgain == false)
            {
                return;
            }
            var pos = player.transform.position.ToString();
            messageAgain = false;
            string name = entity.name.ToString();
            Puts("O jogador " + player.displayName.ToString() + " está raidando!");
            timer.Once(5, () =>{
                messageAgain = true;
            });
            if(name.Contains("explosive.satchel")) { name = "Explosivo de sacola";  }
            else if (name.Contains("explosive.timed")) { name = "C4"; }
            else if (name.Contains("grenade.f1")) { name = "Granada"; }
            else if (name.Contains("grenade.beancan")) { name = "Granada de feijão"; }
            else if (name.Contains("flare")) { return; }
            else if (name.Contains("surveycharge")) {
                name = "Explosivo de pesquisa";
                PrintToChat($"<size=15><color=red>[POLICIA]</color> : O jogador <color=orange>" + player.displayName.ToString() + "</color> está procurando uma mineradora com <color=orange>" + name + "</color> na localização <color=orange>" + pos + "</color></size>");
                return;
            }
            else if (name.Contains("grenade.smoke")) {
                name = "Supply";
                PrintToChat($"<size=15><color=red>[POLICIA]</color> : O jogador <color=orange>" + player.displayName.ToString() + "</color> jogou uma <color=orange>" + name + "</color> chamando um <color=orange>airdrop</color></size>!");
                return;
            }
            PrintToChat($"<size=15><color=red>[POLICIA]</color> : O jogador <color=orange>" + player.displayName.ToString() + "</color> está raidando com <color=orange>" + name + "</color> em <color=orange>" + pos + "</color></size>");
        }

        [ChatCommand("loc")]
        void cmdLocation(BasePlayer player, string command, string[] args)
        {
            var pos = player.transform.position.ToString();
            SendReply(player, "<size=15><color=red>RUSTDINASTY</color> : Sua localização atual é: <color=orange>" + pos + "</color></size>");
        }
        [ChatCommand("ip")]
        void cmdIP(BasePlayer player, string command, string[] args)
        {
            var playerip = BasePlayer.Find("statedamerian").net.connection.ipaddress.ToString();
            Puts(playerip.ToString());
        }
        [ChatCommand("location")]
        void cmdLoc(BasePlayer player, string command, string[] args)
        {
            cmdLocation(player, command, args);
        }
        [ChatCommand("n")]
        void cmdNotice(BasePlayer player, string command, string[] args)
        {
            if((!permission.UserHasPermission(player.UserIDString, "notice.admin")) && (player.net.connection.authLevel < 2))
            {
                SendReply(player, $"<color={tagColor}>{tag}</color> : Você não tem permissão para <color=orange>utilizar</color> este comando");
                return;
            }
            if (args.Length < 1)
            {
                SendReply(player, $"<color={tagColor}>{tag}</color> : Utilize: <color=orange>/n \"message\"</color>");
                return;
            }
            string message = " ";
            foreach(string arg in args)
            {
                message = message + " " + arg;
            }
            rust.RunServerCommand("say " + message);
        }
    }
}