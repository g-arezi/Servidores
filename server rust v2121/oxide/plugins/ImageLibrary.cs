//Reference: Facepunch.Sqlite
//Reference: UnityEngine.UnityWebRequestModule
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Image Library", "Absolut & K1lly0u", "2.0.49")]
    [Description("Plugin API for downloading and managing images")]
    class ImageLibrary : RustPlugin
    {
        #region Fields

        private ImageIdentifiers imageIdentifiers;
        private ImageURLs imageUrls;
        private SkinInformation skinInformation;
        private DynamicConfigFile identifiers;
        private DynamicConfigFile urls;
        private DynamicConfigFile skininfo;

        private static ImageLibrary il;
        private ImageAssets assets;

        private Queue<LoadOrder> loadOrders = new Queue<LoadOrder>();
        private bool orderPending;
        private bool isInitialized;

        private readonly Regex avatarFilter = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

        private const string STEAM_API_URL = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

        private string[] itemShortNames;

        #endregion Fields

        #region Oxide Hooks

        private void Loaded()
        {
            identifiers = Interface.Oxide.DataFileSystem.GetFile("ImageLibrary/image_data");
            urls = Interface.Oxide.DataFileSystem.GetFile("ImageLibrary/image_urls");
            skininfo = Interface.Oxide.DataFileSystem.GetFile("ImageLibrary/skin_data");

            il = this;
            LoadData();
        }

        private void OnServerInitialized()
        {           
            itemShortNames = ItemManager.itemList.Select(x => x.shortname).ToArray();

            foreach (ItemDefinition item in ItemManager.itemList)
            {
                string workshopName = item.displayName.english.ToLower().Replace("skin", "").Replace(" ", "").Replace("-", "");
                if (!workshopNameToShortname.ContainsKey(workshopName))
                    workshopNameToShortname.Add(workshopName, item.shortname);
            }

            AddDefaultUrls();

            CheckForRefresh();

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player) => GetPlayerAvatar(player?.UserIDString);

        private void Unload()
        {
            SaveData();
            UnityEngine.Object.Destroy(assets);
            il = null;
        }

        #endregion Oxide Hooks

        #region Functions

        private void GetItemSkins()
        {
            PrintWarning("Retrieving item skin lists...");
            webrequest.Enqueue("http://s3.amazonaws.com/s3.playrust.com/icons/inventory/rust/schema.json", null, (code, response) =>
            {
                if (!(response == null && code == 200))
                {
                    Rust.Workshop.ItemSchema.Item[] items = JsonConvert.DeserializeObject<Rust.Workshop.ItemSchema>(response).items;

                    PrintWarning($"Found {items.Length} item skins. Gathering image URLs");
                    foreach (var item in items)
                    {
                        if (!string.IsNullOrEmpty(item.itemshortname) && !string.IsNullOrEmpty(item.icon_url))
                        {
                            string identifier;
                            ItemDefinition def = ItemManager.FindItemDefinition(item.itemshortname);
                            if (def == null)
                                continue;

                            int skinCount = def.skins.Count(k => k.id == item.itemdefid);
                            if (skinCount == 0)
                                identifier = $"{item.itemshortname}_{item.workshopid}";
                            else identifier = $"{item.itemshortname}_{item.itemdefid}";
                            if (!imageUrls.URLs.ContainsKey(identifier))
                                imageUrls.URLs.Add(identifier, item.icon_url);

                            skinInformation.skinData[identifier] = new Dictionary<string, object>
                                {
                                    {"title", item.name },
                                    {"votesup", 0 },
                                    {"votesdown", 0 },
                                    {"description", item.description },
                                    {"score", 0 },
                                    {"views", 0 },
                                    {"created", new DateTime() },
                                };
                        }
                    }
                    SaveUrls();
                    SaveSkinInfo();

                    if (!orderPending)
                        ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
                }
            }, this);
        }

        private IEnumerator ProcessLoadOrders()
        {
            yield return new WaitWhile(() => !isInitialized);

            if (loadOrders.Count > 0)
            {
                if (orderPending)
                    yield break;

                LoadOrder nextLoad = loadOrders.Dequeue();
                if (!nextLoad.loadSilent)
                    Puts("Starting order " + nextLoad.loadName);

                if (nextLoad.imageList != null && nextLoad.imageList.Count > 0)
                {
                    foreach (KeyValuePair<string, string> item in nextLoad.imageList)
                        assets.Add(item.Key, item.Value);
                }
                if (nextLoad.imageData != null && nextLoad.imageData.Count > 0)
                {
                    foreach (KeyValuePair<string, byte[]> item in nextLoad.imageData)
                        assets.Add(item.Key, null, item.Value);
                }

                orderPending = true;

                assets.RegisterCallback(nextLoad.callback);

                assets.BeginLoad(nextLoad.loadSilent ? string.Empty : nextLoad.loadName);
            }
        }

        private void GetPlayerAvatar(string userId)
        {
            if (!configData.StoreAvatars || string.IsNullOrEmpty(userId) || HasImage(userId, 0))
                return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (response != null && code == 200)
                {
                    string avatar = avatarFilter.Match(response).Groups[1].ToString();
                    if (!string.IsNullOrEmpty(avatar))
                        AddImage(avatar, userId, 0);
                }
            }, this);
        }

        private void RefreshImagery()
        {
            imageIdentifiers.imageIds.Clear();
            imageIdentifiers.lastCEID = CommunityEntity.ServerInstance.net.ID;

            AddImage("http://i.imgur.com/sZepiWv.png", "NONE", 0);
            AddImage("http://i.imgur.com/lydxb0u.png", "LOADING", 0);
            foreach (var image in configData.UserImages)
            {
                if (!string.IsNullOrEmpty(image.Value))
                    AddImage(image.Value, image.Key, 0);
            }

            GetItemSkins();
        }

        private void CheckForRefresh()
        {
            if (assets == null)
                assets = new GameObject("WebObject").AddComponent<ImageAssets>();

            isInitialized = true;

            if (imageIdentifiers.lastCEID != CommunityEntity.ServerInstance.net.ID)
            {
                if (imageIdentifiers.imageIds.Count < 2)
                {
                    RefreshImagery();
                }
                else
                {
                    PrintWarning("The CommunityEntity instance ID has changed! Due to the way CUI works in Rust all previously stored images must be removed and re-stored using the new ID as reference so clients can find the images. These images will be added to a new load order. Interupting this process will result in being required to re-download these images from the web");
                    RestoreLoadedImages();
                }
            }
        }

        private void RestoreLoadedImages()
        {
            orderPending = true;
            int failed = 0;

            Dictionary<string, byte[]> oldFiles = new Dictionary<string, byte[]>();

            for (int i = imageIdentifiers.imageIds.Count - 1; i >= 0; i--)
            {
                var image = imageIdentifiers.imageIds.ElementAt(i);

                uint imageId;
                if (!uint.TryParse(image.Value, out imageId))
                    continue;

                byte[] bytes = FileStorage.server.Get(imageId, FileStorage.Type.png, imageIdentifiers.lastCEID);
                if (bytes != null)
                    oldFiles.Add(image.Key, bytes);
                else
                {
                    failed++;
                    imageIdentifiers.imageIds.Remove(image.Key);
                }
            }

            Facepunch.Sqlite.Database db = new Facepunch.Sqlite.Database();
            try
            {
                db.Open($"{ConVar.Server.rootFolder}/sv.files.0.db");
                db.Execute("DELETE FROM data WHERE entid = ?", imageIdentifiers.lastCEID);
                db.Close();
            }
            catch { }

            loadOrders.Enqueue(new LoadOrder("Image restoration from previous database", oldFiles));
            PrintWarning($"{imageIdentifiers.imageIds.Count - failed} images queued for restoration, {failed} images failed");
            imageIdentifiers.lastCEID = CommunityEntity.ServerInstance.net.ID;
            SaveData();

            orderPending = false;
            ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
        }
       
        #endregion Functions

        #region Workshop Names and Image URLs

        private void AddDefaultUrls()
        {
            foreach(ItemDefinition itemDefinition in ItemManager.itemList)
            {
                string identifier = $"{itemDefinition.shortname}_0";
                if (!imageUrls.URLs.ContainsKey(identifier))
                    imageUrls.URLs.Add(identifier, $"https://www.rustedit.io/images/imagelibrary/{itemDefinition.shortname}.png");
            }
            SaveUrls();
        }

        private readonly Dictionary<string, string> workshopNameToShortname = new Dictionary<string, string>
        {
            {"ak47", "rifle.ak" },
            {"balaclava", "mask.balaclava" },
            {"bandana", "mask.bandana" },
            {"bearrug", "rug.bear" },
            {"beenie", "hat.beenie" },
            {"boltrifle", "rifle.bolt" },
            {"boonie", "hat.boonie" },
            {"buckethat", "bucket.helmet" },
            {"burlapgloves", "burlap.gloves" },
            {"burlappants", "burlap.trousers" },
            {"cap", "hat.cap" },
            {"collaredshirt", "shirt.collared" },
            {"deerskullmask", "deer.skull.mask" },
            {"hideshirt", "attire.hide.vest" },
            {"hideshoes", "attire.hide.boots" },
            {"longtshirt", "tshirt.long" },
            {"lr300", "rifle.lr300" },
            {"minerhat", "hat.miner" },
            {"mp5", "smg.mp5" },
            {"pipeshotgun", "shotgun.waterpipe" },
            {"roadsignpants", "roadsign.kilt" },
            {"roadsignvest", "roadsign.jacket" },
            {"semiautopistol", "pistol.semiauto" },
            {"snowjacket", "jacket.snow" },
            {"sword", "salvaged.sword" },
            {"vagabondjacket", "jacket" },
            {"woodstorage", "box.wooden" },
            {"workboots", "shoes.boots" }
        };
       
        #endregion Workshop Names and Image URLs

        #region API

        [HookMethod("AddImage")]
        public bool AddImage(string url, string imageName, ulong imageId, Action callback = null)
        {
            loadOrders.Enqueue(new LoadOrder(imageName, new Dictionary<string, string> { { $"{imageName}_{imageId}", url } }, true, callback));
            if (!orderPending)
                ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
            return true;
        }

        [HookMethod("AddImageData")]
        public bool AddImageData(string imageName, byte[] array, ulong imageId, Action callback = null)
        {
            loadOrders.Enqueue(new LoadOrder(imageName, new Dictionary<string, byte[]> { { $"{imageName}_{imageId}", array } }, true, callback));
            if (!orderPending)
                ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
            return true;
        }

        [HookMethod("GetImageURL")]
        public string GetImageURL(string imageName, ulong imageId = 0)
        {
            string identifier = $"{imageName}_{imageId}";
            string value;
            if (imageUrls.URLs.TryGetValue(identifier, out value))
                return value;
            return imageIdentifiers.imageIds["NONE_0"];
        }

        [HookMethod("GetImage")]
        public string GetImage(string imageName, ulong imageId = 0, bool returnUrl = false)
        {
            string identifier = $"{imageName}_{imageId}";
            string value;
            if (imageIdentifiers.imageIds.TryGetValue(identifier, out value))
                return value;
            else
            {
                if (imageUrls.URLs.TryGetValue(identifier, out value))
                {
                    AddImage(value, imageName, imageId);
                    return imageIdentifiers.imageIds["LOADING_0"];
                }               
            }

            if (returnUrl && !string.IsNullOrEmpty(value))
                return value;

            return imageIdentifiers.imageIds["NONE_0"];
        }

        [HookMethod("GetImageList")]
        public List<ulong> GetImageList(string name)
        {
            List<ulong> skinIds = new List<ulong>();
            var matches = imageUrls.URLs.Keys.Where(x => x.StartsWith(name)).ToArray();
            for (int i = 0; i < matches.Length; i++)
            {
                var index = matches[i].IndexOf("_");
                if (matches[i].Substring(0, index) == name)
                {
                    ulong skinID;
                    if (ulong.TryParse(matches[i].Substring(index + 1), out skinID))
                        skinIds.Add(ulong.Parse(matches[i].Substring(index + 1)));
                }
            }
            return skinIds;
        }

        [HookMethod("GetSkinInfo")]
        public Dictionary<string, object> GetSkinInfo(string name, ulong id)
        {
            Dictionary<string, object> skinInfo;
            if (skinInformation.skinData.TryGetValue($"{name}_{id}", out skinInfo))
                return skinInfo;
            return null;
        }

        [HookMethod("HasImage")]
        public bool HasImage(string imageName, ulong imageId)
        {
            if (imageIdentifiers.imageIds.ContainsKey($"{imageName}_{imageId}") && IsInStorage(uint.Parse(imageIdentifiers.imageIds[$"{imageName}_{imageId}"])))
                return true;

            return false;
        }

        public bool IsInStorage(uint crc) => FileStorage.server.Get(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID) != null;

        [HookMethod("IsReady")]
        public bool IsReady() => loadOrders.Count == 0 && !orderPending;

        [HookMethod("ImportImageList")]
        public void ImportImageList(string title, Dictionary<string, string> imageList, ulong imageId = 0, bool replace = false, Action callback = null)
        {
            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();
            foreach (var image in imageList)
            {
                if (!replace && HasImage(image.Key, imageId))
                    continue;
                newLoadOrder[$"{image.Key}_{imageId}"] = image.Value;
            }
            if (newLoadOrder.Count > 0)
            {
                loadOrders.Enqueue(new LoadOrder(title, newLoadOrder, false, callback));
                if (!orderPending)
                    ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
            }
            else
            {
                if (callback != null)
                    callback.Invoke();
            }
        }

        [HookMethod("ImportItemList")]
        public void ImportItemList(string title, Dictionary<string, Dictionary<ulong, string>> itemList, bool replace = false, Action callback = null)
        {
            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();
            foreach (var image in itemList)
            {
                foreach (var skin in image.Value)
                {
                    if (!replace && HasImage(image.Key, skin.Key))
                        continue;
                    newLoadOrder[$"{image.Key}_{skin.Key}"] = skin.Value;
                }
            }
            if (newLoadOrder.Count > 0)
            {
                loadOrders.Enqueue(new LoadOrder(title, newLoadOrder, false, callback));
                if (!orderPending)
                    ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
            }
            else
            {
                if (callback != null)
                    callback.Invoke();
            }
        }

        [HookMethod("ImportImageData")]
        public void ImportImageData(string title, Dictionary<string, byte[]> imageList, ulong imageId = 0, bool replace = false, Action callback = null)
        {
            Dictionary<string, byte[]> newLoadOrder = new Dictionary<string, byte[]>();
            foreach (var image in imageList)
            {
                if (!replace && HasImage(image.Key, imageId))
                    continue;
                newLoadOrder[$"{image.Key}_{imageId}"] = image.Value;
            }
            if (newLoadOrder.Count > 0)
            {
                loadOrders.Enqueue(new LoadOrder(title, newLoadOrder, false, callback));
                if (!orderPending)
                    ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
            }
            else
            {
                if (callback != null)
                    callback.Invoke();
            }
        }
        
        [HookMethod("LoadImageList")]
        public void LoadImageList(string title, List<KeyValuePair<string, ulong>> imageList, Action callback = null)
        {
            Dictionary<string, string> newLoadOrderURL = new Dictionary<string, string>();
            List<KeyValuePair<string, ulong>> workshopDownloads = new List<KeyValuePair<string, ulong>>();

            foreach (KeyValuePair<string, ulong> image in imageList)
            {
                if (HasImage(image.Key, image.Value))
                    continue;

                string identifier = $"{image.Key}_{image.Value}";

                if (imageUrls.URLs.ContainsKey(identifier) && !newLoadOrderURL.ContainsKey(identifier))
                {
                    newLoadOrderURL.Add(identifier, imageUrls.URLs[identifier]);
                }
                else
                {
                    workshopDownloads.Add(new KeyValuePair<string, ulong>(image.Key, image.Value));
                }
            }

            if (workshopDownloads.Count > 0)
            {
                QueueWorkshopDownload(title, newLoadOrderURL, workshopDownloads, callback);
                return;
            }

            if (newLoadOrderURL.Count > 0)
            {
                loadOrders.Enqueue(new LoadOrder(title, newLoadOrderURL, null, false, callback));
                if (!orderPending)
                    ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
            }
            else
            {
                if (callback != null)
                    callback.Invoke();
            }
        }

        [HookMethod("RemoveImage")]
        public void RemoveImage(string imageName, ulong imageId)
        {
            if (HasImage(imageName, imageId))
                return;

            uint crc = uint.Parse(GetImage(imageName, imageId));
            FileStorage.server.Remove(crc, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID);
        }

        #endregion API

        #region Steam API
        private void QueueWorkshopDownload(string title, Dictionary<string, string> newLoadOrderURL, List<KeyValuePair<string, ulong>> workshopDownloads, Action callback = null)
        {
            List<ulong> requestedSkins = workshopDownloads.Select(x => x.Value).ToList();

            string details = string.Format("?key={0}&itemcount={1}", configData.SteamAPIKey, requestedSkins.Count);

            for (int i = 0; i < requestedSkins.Count; i++)
                details += string.Format("&publishedfileids[{0}]={1}", i, requestedSkins[i]);

            try
            {
                webrequest.Enqueue(STEAM_API_URL, details, (code, response) =>
                {
                    PublishedFileQueryResponse query = JsonConvert.DeserializeObject<PublishedFileQueryResponse>(response);
                    if (query == null || query.response == null || query.response.publishedfiledetails.Length == 0)
                    {
                        if (code != 200)
                            PrintError($"There was a error querying Steam for workshop item data : Code ({code})");

                        if (newLoadOrderURL.Count > 0)
                        {
                            loadOrders.Enqueue(new LoadOrder(title, newLoadOrderURL, null, false, callback));
                            if (!orderPending)
                                ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
                        }
                        else
                        {
                            if (callback != null)
                                callback.Invoke();
                        }
                        return;
                    }
                    else
                    {
                        if (query.response.publishedfiledetails.Length > 0)
                        {
                            Dictionary<string, Dictionary<ulong, string>> loadOrder = new Dictionary<string, Dictionary<ulong, string>>();

                            foreach (PublishedFileQueryDetail item in query.response.publishedfiledetails)
                            {
                                if (!string.IsNullOrEmpty(item.preview_url))
                                {
                                    ulong skinId = Convert.ToUInt64(item.publishedfileid);

                                    KeyValuePair<string, ulong>? kvp = workshopDownloads.Find(x => x.Value == skinId);

                                    if (kvp.HasValue)
                                    {
                                        string identifier = $"{kvp.Value.Key}_{kvp.Value.Value}";

                                        if (!newLoadOrderURL.ContainsKey(identifier))
                                            newLoadOrderURL.Add(identifier, item.preview_url);

                                        if (!imageUrls.URLs.ContainsKey(identifier))
                                            imageUrls.URLs.Add(identifier, item.preview_url);

                                        skinInformation.skinData[identifier] = new Dictionary<string, object>
                                        {
                                            {"title", item.title },
                                            {"votesup",  0 },
                                            {"votesdown", 0 },
                                            {"description", item.description },
                                            {"score", 0 },
                                            {"views", item.views },
                                            {"created", new DateTime(item.time_created) },
                                        };

                                        requestedSkins.Remove(skinId);
                                    }
                                }
                            }

                            SaveUrls();
                            SaveSkinInfo();

                            if (requestedSkins.Count != 0)
                            {
                                Puts($"{requestedSkins.Count} workshop skin ID's for image batch ({title}) are invalid! They may have been removed from the workshop\nIDs: {requestedSkins.ToSentence()}");
                            }
                        }

                        if (newLoadOrderURL.Count > 0)
                        {
                            loadOrders.Enqueue(new LoadOrder(title, newLoadOrderURL, null, false, callback));
                            if (!orderPending)
                                ServerMgr.Instance.StartCoroutine(ProcessLoadOrders());
                        }
                        else
                        {
                            if (callback != null)
                                callback.Invoke();
                        }
                    }
                }, 
                this, 
                Core.Libraries.RequestMethod.POST);
            }
            catch { }
        }

        #region JSON Response Classes
        public class PublishedFileQueryResponse
        {
            public FileResponse response { get; set; }
        }

        public class FileResponse
        {
            public int result { get; set; }
            public int resultcount { get; set; }
            public PublishedFileQueryDetail[] publishedfiledetails { get; set; }
        }

        public class PublishedFileQueryDetail
        {
            public string publishedfileid { get; set; }
            public int result { get; set; }
            public string creator { get; set; }
            public int creator_app_id { get; set; }
            public int consumer_app_id { get; set; }
            public string filename { get; set; }
            public int file_size { get; set; }
            public string preview_url { get; set; }
            public string hcontent_preview { get; set; }
            public string title { get; set; }
            public string description { get; set; }
            public int time_created { get; set; }
            public int time_updated { get; set; }
            public int visibility { get; set; }
            public int banned { get; set; }
            public string ban_reason { get; set; }
            public int subscriptions { get; set; }
            public int favorited { get; set; }
            public int lifetime_subscriptions { get; set; }
            public int lifetime_favorited { get; set; }
            public int views { get; set; }
            public Tag[] tags { get; set; }

            public class Tag
            {
                public string tag { get; set; }
            }
        }
        #endregion
        #endregion

        #region Commands

        [ConsoleCommand("cancelstorage")]
        private void cmdCancelStorage(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.authLevel > 0)
            {
                if (!orderPending)
                    PrintWarning("No images are currently being downloaded");
                else
                {
                    assets.ClearList();
                    loadOrders.Clear();
                    PrintWarning("Pending image downloads have been cancelled!");
                }
            }
        }

        private List<ulong> pendingAnswers = new List<ulong>();

        [ConsoleCommand("refreshallimages")]
        private void cmdRefreshAllImages(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.authLevel > 0)
            {
                SendReply(arg, "Running this command will wipe all of your ImageLibrary data, meaning every registered image will need to be re-downloaded. Are you sure you wish to continue? (type yes or no)");

                ulong userId = arg.Connection == null || arg.IsRcon ? 0U : arg.Connection.userid;
                if (!pendingAnswers.Contains(userId))
                {
                    pendingAnswers.Add(userId);
                    timer.In(5, () =>
                    {
                        if (pendingAnswers.Contains(userId))
                            pendingAnswers.Remove(userId);
                    });
                }
            }
        }

        [ConsoleCommand("yes")]
        private void cmdRefreshAllImagesYes(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.authLevel > 0)
            {
                ulong userId = arg.Connection == null || arg.IsRcon ? 0U : arg.Connection.userid;
                if (pendingAnswers.Contains(userId))
                {
                    PrintWarning("Wiping ImageLibrary data and redownloading ImageLibrary specific images. All plugins that have registered images via ImageLibrary will need to be re-loaded!");
                    RefreshImagery();

                    pendingAnswers.Remove(userId);
                }
            }
        }

        [ConsoleCommand("no")]
        private void cmdRefreshAllImagesNo(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.authLevel > 0)
            {
                ulong userId = arg.Connection == null || arg.IsRcon ? 0U : arg.Connection.userid;

                if (pendingAnswers.Contains(userId))
                {
                    SendReply(arg, "ImageLibrary data wipe aborted!"); 
                    pendingAnswers.Remove(userId);
                }
            }
        }

        #endregion Commands

        #region Image Storage

        private struct LoadOrder
        {
            public string loadName;
            public bool loadSilent;

            public Dictionary<string, string> imageList;
            public Dictionary<string, byte[]> imageData;

            public Action callback;
           
            public LoadOrder(string loadName, Dictionary<string, string> imageList, bool loadSilent = false, Action callback = null)
            {
                this.loadName = loadName;
                this.imageList = imageList;
                this.imageData = null;
                this.loadSilent = loadSilent;
                this.callback = callback;
            }
            public LoadOrder(string loadName, Dictionary<string, byte[]> imageData, bool loadSilent = false, Action callback = null)
            {
                this.loadName = loadName;
                this.imageList = null;
                this.imageData = imageData;
                this.loadSilent = loadSilent;
                this.callback = callback;
            }
            public LoadOrder(string loadName, Dictionary<string, string> imageList, Dictionary<string, byte[]> imageData, bool loadSilent = false, Action callback = null)
            {
                this.loadName = loadName;
                this.imageList = imageList;
                this.imageData = imageData;
                this.loadSilent = loadSilent;
                this.callback = callback;
            }
        }

        private class ImageAssets : MonoBehaviour
        {
            private Queue<QueueItem> queueList = new Queue<QueueItem>();
            private bool isLoading;
            private double nextUpdate;
            private int listCount;
            private string request;

            private Action callback;

            private void OnDestroy()
            {
                queueList.Clear();
            }

            public void Add(string name, string url = null, byte[] bytes = null)
            {
                queueList.Enqueue(new QueueItem(name, url, bytes));
            }

            public void RegisterCallback(Action callback) => this.callback = callback;

            public void BeginLoad(string request)
            {
                this.request = request;
                nextUpdate = UnityEngine.Time.time + il.configData.UpdateInterval;
                listCount = queueList.Count;
                Next();
            }

            public void ClearList()
            {
                queueList.Clear();
                il.orderPending = false;
            }

            private void Next()
            {
                if (queueList.Count == 0)
                {
                    il.orderPending = false;
                    il.SaveData();
                    if (!string.IsNullOrEmpty(request))
                        print($"Image batch ({request}) has been stored successfully");

                    request = string.Empty;
                    listCount = 0;

                    if (callback != null)
                        callback.Invoke();

                    StartCoroutine(il.ProcessLoadOrders());
                    return;
                }
                if (il.configData.ShowProgress && listCount > 1)
                {
                    var time = UnityEngine.Time.time;
                    if (time > nextUpdate)
                    {
                        var amountDone = listCount - queueList.Count;
                        print($"{request} storage process at {Math.Round((amountDone / (float)listCount) * 100, 0)}% ({amountDone}/{listCount})");
                        nextUpdate = time + il.configData.UpdateInterval;
                    }
                }
                isLoading = true;

                QueueItem queueItem = queueList.Dequeue();
                if (!string.IsNullOrEmpty(queueItem.url))
                    StartCoroutine(DownloadImage(queueItem));
                else StoreByteArray(queueItem.bytes, queueItem.name);
            }

            private IEnumerator DownloadImage(QueueItem info)
            {
                UnityWebRequest www = UnityWebRequest.Get(info.url);

                yield return www.SendWebRequest();
                if (il == null) yield break;
                if (www.isNetworkError || www.isHttpError)
                {
                    print(string.Format("Image failed to download! Error: {0} - Image Name: {1} - Image URL: {2}", www.error, info.name, info.url));
                    www.Dispose();
                    isLoading = false;
                    Next();
                    yield break;
                }

                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(www.downloadHandler.data);
                if (texture != null)
                {
                    byte[] bytes = texture.EncodeToPNG();
                    DestroyImmediate(texture);
                    StoreByteArray(bytes, info.name);
                }
                www.Dispose();
            }

            private void StoreByteArray(byte[] bytes, string name)
            {
                if (bytes != null)
                    il.imageIdentifiers.imageIds[name] = FileStorage.server.Store(bytes, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID).ToString();
                isLoading = false;
                Next();
            }

            private class QueueItem
            {
                public byte[] bytes;
                public string url;
                public string name;
                public QueueItem(string name, string url = null, byte[] bytes = null)
                {
                    this.bytes = bytes;
                    this.url = url;
                    this.name = name;
                }
            }
        }

        #endregion Image Storage

        #region Config

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Avatars - Store player avatars")]
            public bool StoreAvatars { get; set; }

            [JsonProperty(PropertyName = "Steam API key (get one here https://steamcommunity.com/dev/apikey)")]
            public string SteamAPIKey { get; set; }

            //[JsonProperty(PropertyName = "Workshop - Download workshop image information")]
            //public bool WorkshopImages { get; set; }

            [JsonProperty(PropertyName = "Progress - Show download progress in console")]
            public bool ShowProgress { get; set; }

            [JsonProperty(PropertyName = "Progress - Time between update notifications")]
            public int UpdateInterval { get; set; }

            [JsonProperty(PropertyName = "User Images - Manually define images to be loaded")]
            public Dictionary<string, string> UserImages { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                ShowProgress = true,
                SteamAPIKey = string.Empty,
                StoreAvatars = true,
                UpdateInterval = 20,
                UserImages = new Dictionary<string, string>(),
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 47))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        
        #endregion Config

        #region Data Management

        private void SaveData() => identifiers.WriteObject(imageIdentifiers);
        private void SaveSkinInfo() => skininfo.WriteObject(skinInformation);
        private void SaveUrls() => urls.WriteObject(imageUrls);

        private void LoadData()
        {
            try
            {
                imageIdentifiers = identifiers.ReadObject<ImageIdentifiers>();
            }
            catch
            {
                imageIdentifiers = new ImageIdentifiers();
            }
            try
            {
                skinInformation = skininfo.ReadObject<SkinInformation>();
            }
            catch
            {
                skinInformation = new SkinInformation();
            }
            try
            {
                imageUrls = urls.ReadObject<ImageURLs>();
            }
            catch
            {
                imageUrls = new ImageURLs();
            }
            if (skinInformation == null)
                skinInformation = new SkinInformation();
            if (imageIdentifiers == null)
                imageIdentifiers = new ImageIdentifiers();
            if (imageUrls == null)
                imageUrls = new ImageURLs();            
        }

        private class ImageIdentifiers
        {
            public uint lastCEID;
            public Hash<string, string> imageIds = new Hash<string, string>();
        }

        private class SkinInformation
        {
            public Hash<string, Dictionary<string, object>> skinData = new Hash<string, Dictionary<string, object>>();
        }

        private class ImageURLs
        {
            public Hash<string, string> URLs = new Hash<string, string>();
        }

        #endregion Data Management
    }
}
