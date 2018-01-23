//Requires: SSNNotifier

using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SSNStorages", "Umlaut", "0.0.1")]
    internal class SSNStorages : RustPlugin
    {
        // Types

        public class Point
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }

            public Point()
            {
                x = y = z = 0;
            }

            public Point(Vector3 vector)
            {
                x = vector.x;
                y = vector.y;
                z = vector.z;
            }

            public Vector3 vector()
            {
                Vector3 vector = new Vector3();
                vector.x = x;
                vector.y = y;
                vector.z = z;
                return vector;
            }
        }

        private class AccessItem
        {
            public string dateTime;

            public AccessItem()
            {
                dateTime = "";
            }
        }

        private class StorageItem
        {
            public Point Position;
            public Dictionary<ulong, AccessItem> AccessItems;
            public ulong CrashPlayer;
            public string CrashDateTime;

            public StorageItem()
            {
                Position = new Point();
                AccessItems = new Dictionary<ulong, AccessItem>();
                CrashPlayer = 0;
                CrashDateTime = "";
            }
        }

        private class StorageItems : Dictionary<ulong, StorageItem>
        {
            public StorageItems()
            {
            }
        }

        private class StoragesLogs
        {
            public uint WorldSize = 0;
            public uint WorldSeed = 0;
            public StorageItems AliveStorages = new StorageItems();
            public StorageItems CrashedStorages = new StorageItems();
        }

        private class ConfigData
        {
            public Dictionary<string, string> Messages = new Dictionary<string, string>();
            public void insertDefaultMessage(string key, string message)
            {
                if (!Messages.ContainsKey(key))
                {
                    Messages.Add(key, message);
                }
            }
        }

        // Members

        [PluginReference]
        private Plugin SSNNotifier;

        private ConfigData m_configData;
        private StoragesLogs m_storagesLogs;
        private Dictionary<ulong, bool> m_storagesShowInfo = new Dictionary<ulong, bool>();

        //

        private void LoadConfig()
        {
            try
            {
                m_configData = Config.ReadObject<ConfigData>();
                InsertDefaultMessages();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        private void SaveConfig()
        {
            Config.WriteObject<ConfigData>(m_configData, true);
        }

        private void LoadDynamic()
        {
            try
            {
                m_storagesLogs = Interface.GetMod().DataFileSystem.ReadObject<StoragesLogs>("StoragesLogs");
            }
            catch
            {
                m_storagesLogs = new StoragesLogs();
            }
        }

        private void SaveDynamic()
        {
            Interface.GetMod().DataFileSystem.WriteObject("StoragesLogs", m_storagesLogs);
        }

        // Hooks

        private void Loaded()
        {
            LoadConfig();
            LoadDynamic();

            if (m_storagesLogs.WorldSeed != World.Seed || m_storagesLogs.WorldSize != World.Size)
            {
                m_storagesLogs.AliveStorages.Clear();
                m_storagesLogs.CrashedStorages.Clear();

                m_storagesLogs.WorldSeed = World.Seed;
                m_storagesLogs.WorldSize = World.Size;
                SaveDynamic();
            }

            timer.Repeat(60, 0, () => SaveDynamic());

            if (!permission.PermissionExists("SSNStorages.storages"))
            {
                permission.RegisterPermission("SSNStorages.storages", this);
            }
        }

        protected override void LoadDefaultConfig()
        {
            m_configData = new ConfigData();
            InsertDefaultMessages();
            Config.WriteObject(m_configData, true);
        }

        void InsertDefaultMessages()
        {
            m_configData.insertDefaultMessage("storage_not_found", "Storage <color=cyan>%storage_id</color> not found.");
            m_configData.insertDefaultMessage("invalid_arguments", "Invalid arguments.");
            m_configData.insertDefaultMessage("player_access", "<color=cyan>%timestamp</color> - <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>)");
        }

        private void Unload()
        {
            SaveDynamic();
        }

        void OnLootEntity(BasePlayer player, BaseEntity targetEntity)
        {
            if (player == null || targetEntity == null) return;

            StorageContainer storage = targetEntity as StorageContainer;

            if (storage == null) return;

            uint storageId = storage.net.ID;
            StorageItem storageItem;
            if (m_storagesLogs.AliveStorages.ContainsKey(storageId))
            {
                storageItem = m_storagesLogs.AliveStorages[storageId];
            }
            else
            {
                storageItem = new StorageItem();
                storageItem.Position = new Point(storage.transform.position);
                m_storagesLogs.AliveStorages[storageId] = storageItem;
            }

            AccessItem accessItem = new AccessItem();
            accessItem.dateTime = dateTimeToString(DateTime.Now);
            storageItem.AccessItems[player.userID] = accessItem;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || entity == null) return;

            BasePlayer player = hitInfo.Initiator as BasePlayer;
            StorageContainer storage = entity as StorageContainer;

            if (storage == null || player == null) return;

            uint storageId = storage.net.ID;

            StorageItem storageItem;
            if (m_storagesLogs.AliveStorages.ContainsKey(storageId))
            {
                storageItem = m_storagesLogs.AliveStorages[storageId];
                m_storagesLogs.AliveStorages.Remove(storageId);
            }
            else
            {
                storageItem = new StorageItem();
                storageItem.Position = new Point(storage.transform.position);
            }

            storageItem.CrashDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            storageItem.CrashPlayer = player.userID;
            m_storagesLogs.CrashedStorages[storageId] = storageItem;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || entity == null) return null;

            BasePlayer player = hitInfo.Initiator as BasePlayer;
            StorageContainer storage = entity as StorageContainer;

            if (player == null || storage == null) return null;

            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNStorages.storages"))
            {
                return null;
            }

            uint storageId = storage.net.ID;
            if (m_storagesShowInfo.ContainsKey(player.userID) && m_storagesShowInfo[player.userID])
            {
                if (m_storagesLogs.AliveStorages.ContainsKey(storageId))
                {
                    printStorageInfo(player, storageId, m_storagesLogs.AliveStorages[storageId], 0);
                }
                else
                {
                    player.ChatMessage(m_configData.Messages["storage_not_found"].Replace("%storage_id", storageId.ToString()));
                }
                return "handled";
            }
            else
            {
                return null;
            }
        }

        [ChatCommand("storages_show_info")]
        void cmdChatStoragesShowInfo(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNStorages.storages"))
            {
                return;
            }

            if (args.Length == 1)
            {
                if (args[0] == "on")
                {
                    m_storagesShowInfo[player.userID] = true;
                }
                else if (args[0] == "off")
                {
                    m_storagesShowInfo[player.userID] = false;
                }
                else
                {
                    player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                }
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
            }
        }

        [ChatCommand("storages_crashed")]
        void cmdChatStoragesCrashed(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNStorages.storages"))
            {
                return;
            }

            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            double maxDistance = 0;
            if (!double.TryParse(args[0], out maxDistance) || maxDistance <= 0)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            List<uint> storageIds = new List<uint>();
            List<double> storageDistances = new List<double>();
            List<StorageItem> storages = new List<StorageItem>();
            foreach (uint storageId in m_storagesLogs.CrashedStorages.Keys)
            {
                StorageItem storageItem = m_storagesLogs.CrashedStorages[storageId];

                double distance = Math.Sqrt(
                    Math.Pow(player.transform.position.x - storageItem.Position.x, 2) +
                    Math.Pow(player.transform.position.y - storageItem.Position.y, 2) +
                    Math.Pow(player.transform.position.z - storageItem.Position.z, 2));

                if (distance < maxDistance)
                {
                    storageIds.Add(storageId);
                    storageDistances.Add(distance);
                    storages.Add(storageItem);
                }
            }

            for (int i = 0; i < storageDistances.Count - 1; ++i)
            {
                for (int j = i + 1; j < storageDistances.Count; ++j)
                {
                    if (storageDistances[i] < storageDistances[j])
                    {
                        double storageDistance = storageDistances[i];
                        storageDistances[i] = storageDistances[j];
                        storageDistances[j] = storageDistance;

                        StorageItem storage = storages[i];
                        storages[i] = storages[j];
                        storages[j] = storage;

                        uint storageId = storageIds[i];
                        storageIds[i] = storageIds[j];
                        storageIds[j] = storageId;
                    }
                }
            }

            for (int i = 0; i < storageDistances.Count; ++i)
            {
                printStorageInfo(player, storageIds[i], storages[i], storageDistances[i]);
            }
        }

        [ChatCommand("storages_players")]
        void cmdChatStoragesPlayers(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNStorages.storages"))
            {
                return;
            }

            if (args.Length > 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            ulong userID = 0;
            if (args.Length == 1)
            {
                userID = SSNNotifier.Call<ulong>("UserIdByAlias", args[0]);
                if (userID == 0)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }
            }

            Dictionary<ulong, List<Vector3>> storages = new Dictionary<ulong, List<Vector3>>();
            foreach (ulong storageId in m_storagesLogs.AliveStorages.Keys)
            {
                StorageItem sotrageItem = m_storagesLogs.AliveStorages[storageId];
                foreach (ulong playerId in sotrageItem.AccessItems.Keys)
                {
                    if (userID != 0 && userID != playerId) continue;

                    List<Vector3> playerStorages;
                    if (!storages.ContainsKey(playerId))
                    {
                        storages[playerId] = new List<Vector3>();
                    }
                    playerStorages = storages[playerId];
                    playerStorages.Add(sotrageItem.Position.vector());
                }
            }

            List<ulong> playersList = new List<ulong>();
            foreach (ulong playerId in storages.Keys)
            {
                playersList.Add(playerId);
            }

            for (int i = 0; i < playersList.Count - 1; ++i)
            {
                for (int j = i; j < playersList.Count; ++j)
                {
                    if (storages[playersList[i]].Count > storages[playersList[j]].Count)
                    {
                        ulong buf = playersList[i];
                        playersList[i] = playersList[j];
                        playersList[j] = buf;
                    }
                }
            }

            int ii = 0;
            foreach (ulong playerId in playersList)
            {
                player.ChatMessage((playersList.Count - (ii++)).ToString() + ") " + SSNNotifier.Call<string>("PlayerName", playerId) + ": " + storages[playerId].Count.ToString());
            }
        }

        //

        void printStorageInfo(BasePlayer player, uint storageId, StorageItem storageItem, double distance)
        {
            string line;

            if (distance == 0)
            {
                line = "Storage: " + storageId.ToString();
            }
            else
            {
                string crashUserId = storageItem.CrashPlayer.ToString();
                string crashTimestamp = storageItem.CrashDateTime;
                string crashUserName = "unknown";
                BasePlayer targetPlayer = BasePlayer.FindByID(ulong.Parse(crashUserId));
                if (targetPlayer != null)
                {
                    crashUserName = targetPlayer.displayName;
                }
                line = Math.Round(distance, 1).ToString() + ") " + crashTimestamp + " - " + crashUserId + " (" + crashUserName + ") - " + storageId;
            }

            player.ChatMessage(line);

            Dictionary<ulong, AccessItem> accessPlayers = storageItem.AccessItems;

            // Сортировка по времени

            List<ulong> userIDs = new List<ulong>();
            List<string> times = new List<string>();
            foreach (ulong userID in accessPlayers.Keys)
            {
                userIDs.Add(userID);
                times.Add(accessPlayers[userID].dateTime);
            }
            for (int i = 0; i < userIDs.Count - 1; ++i)
            {
                for (int j = i + 1; j < userIDs.Count; ++j)
                {
                    if (times[i].CompareTo(times[j]) > 0)
                    {
                        ulong userId = userIDs[i];
                        userIDs[i] = userIDs[j];
                        userIDs[j] = userId;

                        string time = times[i];
                        times[i] = times[j];
                        times[j] = time;
                    }
                }
            }

            //

            List<ulong> contextPlayers = new List<ulong>();
            for (int i = 0; i < userIDs.Count; ++i)
            {
                string message = m_configData.Messages["player_access"];
                message = message.Replace("%player_steamid", userIDs[i].ToString());
                message = message.Replace("%player_name", SSNNotifier.Call<string>("PlayerName", userIDs[i]));
                message = message.Replace("%timestamp", times[i]);
                player.ChatMessage((i + 1).ToString() + ") " + message);
                contextPlayers.Add(userIDs[i]);
            }
            SSNNotifier.Call("SetContextPlayers", player.userID, contextPlayers);
        }

        static public string dateTimeToString(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}