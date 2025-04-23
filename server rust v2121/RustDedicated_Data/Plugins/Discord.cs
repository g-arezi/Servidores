using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;
using System.Net;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Oxide.Plugins;

namespace Oxide.Plugins
{
    [Info("Discord", "Goku", "1.0.0")]
    class Discord : RustPlugin
    {
        //Plugin made by Goku
        //Dont alter the line codes with not (")

        bool configChanged;

        string tag            = "RustDinasty";
        int sizeTag           = 15;
        string colorTag       = "red";
        string discordInvite  = "discord.gg/Y2wmSSc";
        string mensagemAuto   = "Utilize <color=orange>/discord</color> para obter nosso servidor de <color=lightblue>discord</color>";
        int messageTime       = 120;

        void Init()
        {
            LoadConfigValues();
            timer.Every(messageTime, () =>{
                Broadcast(mensagemAuto);
            });
        }

        protected override void LoadDefaultConfig() => Puts("Foi criado uma configuração!");
        void LoadConfigValues()
        {
            tag           = GetConfig("Configurações", "Tag no chat", tag);
            sizeTag       = GetConfig("Opções", "Tamanho da tag e das mensagens", sizeTag);
            colorTag      = GetConfig("Opções", "Cor da tag do chat", colorTag);
            discordInvite = GetConfig("Configurações", "Link do convite do discord", discordInvite);
            mensagemAuto  = GetConfig("Configurações", "Mensagem automatica do plugin", mensagemAuto);
            messageTime   = GetConfig("Opções", "Tempo de envio da mensagem automatica do servidor", messageTime);
            
            if(!configChanged) return;
            Puts("Configurações atualizadas! By Goku");
            SaveConfig();
        }

        [ChatCommand("discord")]
        void cmdDiscord (BasePlayer player, string command, string[] args)
        {
            Say(player, "Este é o link de convite do nosso servidor do <color=lightblue>discord</color>: <color=lightblue>" + discordInvite + "</color>");
        }

        void Say(BasePlayer player, string message)
        {
            var color = Convert.ToString(colorTag);
            var size = Convert.ToString(sizeTag);
            Player.Message(player, $"<size={size}><color={color}>{tag}</color>: {message}</size>", null, 76561198878194965);
        }

        void Broadcast(string message)
        {
            var color = Convert.ToString(colorTag);
            var size = Convert.ToString(sizeTag);
            Server.Broadcast($"<size={size}><color={color}>{tag}</color>: {message}</size>", null, 76561198878194965);
        }
        
        T GetConfig<T>(string category, string setting, T defaultValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[category] = data;
                configChanged = true;
            }
            if (data.TryGetValue(setting, out value)) return (T)Convert.ChangeType(value, typeof(T));
            value = defaultValue;
            data[setting] = value;
            configChanged = true;
            return (T)Convert.ChangeType(value, typeof(T));
        }

        void SetConfigValue<T>(string category, string setting, T newValue)
        {
            var data = Config[category] as Dictionary<string, object>;
            object value;
            if (data != null && data.TryGetValue(setting, out value))
            {
                value = newValue;
                data[setting] = value;
                configChanged = true;
            }
            SaveConfig();
        }
    }
}