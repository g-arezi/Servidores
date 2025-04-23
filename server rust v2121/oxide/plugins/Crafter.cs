using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using System;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Crafter", "America", "0.1.2")]
	[Description("Permite criar armas: lr300, m249, m92, spas12")]
    class Crafter : RustPlugin
    {
        #region Fields
        static Crafter ins;
        private bool initialized;

        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            permission.RegisterPermission("crafter.admin", this);
            permission.RegisterPermission("crafter.use", this);
        }

        private void OnServerInitialized()
        {
            ins = this;
            // LoadData();

            initialized = true;
        }
        #endregion

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm) || permission.UserHasPermission(player.UserIDString, "crafter.admin");


        #region Commands
        [ChatCommand("craft")]
        void cmdCraftUser(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "crafter.use") && !player.IsAdmin)
            {
                SendReply(player, "Você não tem permissão para usar este comando!");
                return;
            }
			
			if (args.Length == 0)
			player.ChatMessage($"<b><color=#ff0000ff>Crie a metralhadora M249, o rifle LR-300, a pistola M92 Beretta ou a espingarda Spas 12!</color></b>\n<b><color=#ce422b> <size=15>Use os comandos para criar armas.:</size></color></b>\n<b>/craft lr300</b>\n/craft m249\n/craft m92\n/craft spas");

            else if (args.Length == 1)
            {
                switch (args[0].ToLower())
				{
					case "lr300":
					{
						var mvk = player.inventory.GetAmount(317398316);//317398316 metal.refined
						var metal = player.inventory.GetAmount(69511070);//69511070 metal.fragments
						var rifle = player.inventory.GetAmount(176787552);//176787552 riflebody
						var spring = player.inventory.GetAmount(-1021495308);//-1021495308 metalspring
						
						if (mvk >= configData.CC.lr300.comp1 & metal >= configData.CC.lr300.comp2 & rifle >= configData.CC.lr300.comp3 & spring >= configData.CC.lr300.comp4)
						{
							player.inventory.Take(null, 317398316, configData.CC.lr300.comp1);
							player.inventory.Take(null, 69511070, configData.CC.lr300.comp2);
							player.inventory.Take(null, 176787552, configData.CC.lr300.comp3);
							player.inventory.Take(null, -1021495308, configData.CC.lr300.comp4);
						}
						else
						{
						player.ChatMessage($"<b><color=#ff0000ff>Recursos insuficientes!</color></b>\n<b><color=#ce422b> <size=15>Para fabricar armas, você precisa de recursos:</size></color></b>\n<b>Metal de Alta Qualidade <color=#ce422b>{configData.CC.lr300.comp1}</color> rcs. (Quantidade de Recursos <color=#ce422b>{mvk}</color> rcs.)</b>\nFragmentos de Metal <color=#ce422b>{configData.CC.lr300.comp2}</color> rcs. (Quantidade de Recursos <color=#ce422b>{metal}</color> rcs.)\n<b>Corpo de Rifle <color=#ce422b>{configData.CC.lr300.comp3}</color> rcs. (Quantidade de Recursos <color=#ce422b>{rifle}</color> rcs.)</b>\n<b>Molas <color=#ce422b>{configData.CC.lr300.comp4}</color> rcs. (Quantidade de Recursos <color=#ce422b>{spring}</color> rcs.)</b>\n\nNao tem recursoss suficientes para craftar esse item <color=#ce422b>/craft lr300</color>");
							return;
						}

						player.inventory.GiveItem(ItemManager.CreateByItemID(-1812555177, 1));
						player.ChatMessage($"O Rifle LR-300 foi craftado com sucesso!");
						return;
					}
					case "m249":
					{
						var mvk = player.inventory.GetAmount(317398316);
						var metal = player.inventory.GetAmount(69511070);
						var rifle = player.inventory.GetAmount(176787552);
						var spring = player.inventory.GetAmount(-1021495308);
						if (mvk >= configData.CC.m249.comp1 & metal >= configData.CC.m249.comp2 & rifle >= configData.CC.m249.comp3 & spring >= configData.CC.m249.comp4)
						{
							player.inventory.Take(null, 317398316, configData.CC.m249.comp1);
							player.inventory.Take(null, 69511070, configData.CC.m249.comp2);
							player.inventory.Take(null, 176787552, configData.CC.m249.comp3);
							player.inventory.Take(null, -1021495308, configData.CC.m249.comp4);
						}
						else
						{
						player.ChatMessage($"<b><color=#ff0000ff>Recursos insuficientes!</color></b>\n<b><color=#ce422b> <size=15>Materiais necessarios para craftar arma::</size></color></b>\n<b>Metal de Alta Qualidade <color=#ce422b>{configData.CC.m249.comp1}</color> rcs. (Quantidade de Recursos <color=#ce422b>{mvk}</color> rcs.)</b>\nFragmentos de Metal <color=#ce422b>{configData.CC.m249.comp2}</color> rcs. (Quantidade de Recursos <color=#ce422b>{metal}</color> rcs.)\n<b>Corpo de Rifle <color=#ce422b>{configData.CC.m249.comp3}</color> rcs. (Quantidade de Recursos <color=#ce422b>{rifle}</color> rcs.)</b>\n<b>Molas <color=#ce422b>{configData.CC.m249.comp4}</color> rcs. (Quantidade de Recursos <color=#ce422b>{spring}</color> rcs.)</b>\n\nNao tem recursoss suficientes para craftar esse item <color=#ce422b>/craft m249</color>");
							return;
						}

						player.inventory.GiveItem(ItemManager.CreateByItemID(-2069578888, 1));
						player.ChatMessage($"A M249 foi craftada com sucesso!");
						return;
					}
					case "m92":
					{
						var mvk = player.inventory.GetAmount(317398316);
						var pipe = player.inventory.GetAmount(95950017);
						var semi = player.inventory.GetAmount(573926264);
						var spring = player.inventory.GetAmount(-1021495308);
						if (mvk >= configData.CC.m92.comp1 & pipe >= configData.CC.m92.comp2 & semi >= configData.CC.m92.comp3 & spring >= configData.CC.m92.comp4)
						{
							player.inventory.Take(null, 317398316, configData.CC.m92.comp1);
							player.inventory.Take(null, 95950017, configData.CC.m92.comp2);
							player.inventory.Take(null, 573926264, configData.CC.m92.comp3);
							player.inventory.Take(null, -1021495308, configData.CC.m92.comp4);
						}
						else
						{
						player.ChatMessage($"<b><color=#ff0000ff>Recursos insuficientes!</color></b>\n<b><color=#ce422b> <size=15>Para fabrica armas, você precisa de recursos:</size></color></b>\n<b>Metal de Alta Qualidade <color=#ce422b>{configData.CC.m92.comp1}</color> rcs. (Quantidade de Recursos <color=#ce422b>{mvk}</color> rcs.)</b>\nTuberia <color=#ce422b>{configData.CC.m92.comp2}</color> rcs. (Quantidade de Recursos <color=#ce422b>{pipe}</color> rcs.)\n<b>Cuerpo Semiautomatico <color=#ce422b>{configData.CC.m92.comp3}</color> rcs. (Quantidade de Recursos <color=#ce422b>{semi}</color> rcs.)</b>\n<b>Molas <color=#ce422b>{configData.CC.m92.comp4}</color> rcs. (Quantidade de Recursos <color=#ce422b>{spring}</color> rcs.)</b>\n\nNao tem recursoss suficientes para craftar esse item <color=#ce422b>/craft m92</color>");
							return;
						}

						player.inventory.GiveItem(ItemManager.CreateByItemID(-852563019, 1));
						player.ChatMessage($"Foi craftada com sucesso!");
						return;
					}
					case "spas":
					{
						var mvk = player.inventory.GetAmount(317398316);
						var pipe = player.inventory.GetAmount(95950017);
						var metal = player.inventory.GetAmount(69511070);
						var spring = player.inventory.GetAmount(-1021495308);
						if (mvk >= configData.CC.spas.comp1 & pipe >= configData.CC.spas.comp2 & metal >= configData.CC.spas.comp3 & spring >= configData.CC.spas.comp4)
						{
							player.inventory.Take(null, 317398316, configData.CC.spas.comp1);
							player.inventory.Take(null, 95950017, configData.CC.spas.comp2);
							player.inventory.Take(null, 69511070, configData.CC.spas.comp3);
							player.inventory.Take(null, -1021495308, configData.CC.spas.comp4);
						}
						else
						{
						player.ChatMessage($"<b><color=#ff0000ff>Recursos insuficientes!</color></b>\n<b><color=#ce422b> <size=15>Para fabricar armas, você precisa de recursos:</size></color></b>\n<b>Metal de Alta Qualidade <color=#ce422b>{configData.CC.spas.comp1}</color> rcs. (Quantidade de Recursos <color=#ce422b>{mvk}</color> rcs.)</b>\nTuberia <color=#ce422b>{configData.CC.spas.comp2}</color> rcs. (Quantidade de Recursos <color=#ce422b>{pipe}</color> rcs.)\n<b>Мetal-Fragmentado <color=#ce422b>{configData.CC.spas.comp3}</color> rcs. (Quantidade de Recursos <color=#ce422b>{metal}</color> rcs.)</b>\n<b>Molas <color=#ce422b>{configData.CC.spas.comp4}</color> rcs. (Quantidade de Recursos <color=#ce422b>{spring}</color> шт.)</b>\n\nNao tem recursoss suficientes para craftar esse item <color=#ce422b>/craft spas</color>");
							return;
						}

						player.inventory.GiveItem(ItemManager.CreateByItemID(-41440462, 1));
						player.ChatMessage($"A Shotgun Spaz-12 Foi craftada com sucesso!!");
						return;
					}
				}
            
            } else {
				return;
			}

        }


        [ChatCommand("admcraft")]
        void cmdCraftAdmin(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "crafter.admin") && !player.IsAdmin) return;
			if (args.Length == 0)
			player.ChatMessage($"<b><color=#ff0000ff>Крафт пулемета M249 или винтовки LR-300!</color></b>\n<b><color=#ce422b> <size=15>Для крафта оружий используйте команды:</size></color></b>\n<b>/admcraft lr300</b>\n/admcraft m249");

            else if (args.Length == 1 || args.Length == 2)
            {
                switch (args[0].ToLower())
				{
					case "lr300":
					{
						player.inventory.GiveItem(ItemManager.CreateByItemID(-1812555177, 1));
						return;
					}
					case "m249":
					{
						player.inventory.GiveItem(ItemManager.CreateByItemID(-2069578888, 1));
						return;
					}
					case "m92":
					{
						player.inventory.GiveItem(ItemManager.CreateByItemID(-852563019, 1));
						return;
					}
					case "spas":
					{
						player.inventory.GiveItem(ItemManager.CreateByItemID(-41440462, 1));
						return;
					}
				}
            
            } else {
				return;
			}
        }

        #endregion


        #region Config        
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Компоненты необходимые для крафта")]
            public CrafterComponents CC { get; set; }

            public class CrafterComponents
            {
                [JsonProperty(PropertyName = "Для винтовки LR-300")]
                public CClr300 lr300 { get; set; }
				[JsonProperty(PropertyName = "Для пулемета M249")]
                public CCm249 m249 { get; set; }
                [JsonProperty(PropertyName = "Для пистолета M92")]
                public CCm92 m92 { get; set; }
				[JsonProperty(PropertyName = "Для дробовика Spas12")]
                public CCspas spas { get; set; }

                public class CClr300
                {
                    [JsonProperty(PropertyName = "metal de alta calidad")]
                    public int comp1 { get; set; }
                    [JsonProperty(PropertyName = "metal fragmentado")]
                    public int comp2 { get; set; }
                    [JsonProperty(PropertyName = "Cuerpo de rifle")]
                    public int comp3 { get; set; }
                    [JsonProperty(PropertyName = "Molas")]
                    public int comp4 { get; set; }
                }
				public class CCm249
                {
                    [JsonProperty(PropertyName = "metal de alta calidad")]
                    public int comp1 { get; set; }
                    [JsonProperty(PropertyName = "metal fragmentado")]
                    public int comp2 { get; set; }
                    [JsonProperty(PropertyName = "Cuerpo de rifle")]
                    public int comp3 { get; set; }
                    [JsonProperty(PropertyName = "Molas")]
                    public int comp4 { get; set; }
                }
				public class CCm92
                {
                    [JsonProperty(PropertyName = "metal de alta calidad")]
                    public int comp1 { get; set; }
                    [JsonProperty(PropertyName = "Тuberia")]
                    public int comp2 { get; set; }
                    [JsonProperty(PropertyName = "Cuerpo Semiautomatico")]
                    public int comp3 { get; set; }
                    [JsonProperty(PropertyName = "Molas")]
                    public int comp4 { get; set; }
                }
				public class CCspas
                {
                    [JsonProperty(PropertyName = "metal de alta calidad")]
                    public int comp1 { get; set; }
                    [JsonProperty(PropertyName = "Тuberia")]
                    public int comp2 { get; set; }
                    [JsonProperty(PropertyName = "metal fragmentado")]
                    public int comp3 { get; set; }
                    [JsonProperty(PropertyName = "Molas")]
                    public int comp4 { get; set; }
                }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                CC = new ConfigData.CrafterComponents
                {
                    lr300 = new ConfigData.CrafterComponents.CClr300
                    {
                        comp1 = 60,
                        comp2 = 200,
                        comp3 = 2,
                        comp4 = 10
                    },
                    m249 = new ConfigData.CrafterComponents.CCm249
                    {
                        comp1 = 120,
                        comp2 = 400,
                        comp3 = 4,
                        comp4 = 20
                    },
					m92 = new ConfigData.CrafterComponents.CCm92
                    {
                        comp1 = 15,
                        comp2 = 3,
                        comp3 = 1,
                        comp4 = 2
                    },
					spas = new ConfigData.CrafterComponents.CCspas
                    {
                        comp1 = 15,
                        comp2 = 3,
                        comp3 = 200,
                        comp4 = 1
                    }
                }
            };
        }
        #endregion
    }
}
