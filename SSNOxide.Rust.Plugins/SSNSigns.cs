//Requires: SSNNotifier

using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SSNSigns", "Umlaut", "0.0.1")]
    internal class SSNSigns : RustPlugin
    {
        // Types

        private class AccessItem
        {
            public string dateTime;

            public AccessItem()
            {
                dateTime = "";
            }
        }

        private class SignItem
        {
            public Dictionary<ulong, AccessItem> AccessItems;

            public SignItem()
            {
                AccessItems = new Dictionary<ulong, AccessItem>();
            }
        }

        private class SignsLogs
        {
            public uint WorldSize = 0;
            public uint WorldSeed = 0;
            public Dictionary<ulong, SignItem> SignItems = new Dictionary<ulong, SignItem>();
        }

        private class SignBlock
        {
            public string datetime;
            public string reason;

            public SignBlock()
            {
                datetime = "";
                reason = "";
            }
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

            public Dictionary<ulong, SignBlock> SignBlocks = new Dictionary<ulong, SignBlock>();
        }

        // Members

        [PluginReference]
        private Plugin SSNNotifier;
        
        private ConfigData m_configData;
        private SignsLogs m_signsLogs;
        private HashSet<ulong> m_signsShowInfo = new HashSet<ulong>();

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
                m_signsLogs = Interface.GetMod().DataFileSystem.ReadObject<SignsLogs>("SignsLogs");
            }
            catch
            {
                m_signsLogs = new SignsLogs();
            }
        }

        private void SaveDynamic()
        {
            Interface.GetMod().DataFileSystem.WriteObject("SignsLogs", m_signsLogs);
        }

        // Hooks

        private void Loaded()
        {
            LoadConfig();
            LoadDynamic();

            if (m_signsLogs.WorldSeed != World.Seed || m_signsLogs.WorldSize != World.Size)
            {
                m_signsLogs.SignItems.Clear();
                
                m_signsLogs.WorldSeed = World.Seed;
                m_signsLogs.WorldSize = World.Size;
                SaveDynamic();
            }

            timer.Repeat(60, 0, () => SaveDynamic());

            if (!permission.PermissionExists("SSNSigns.signs"))
            {
                permission.RegisterPermission("SSNSigns.signs", this);
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
            m_configData.insertDefaultMessage("sign_not_found", "Sign <color=cyan>%sign_id</color> not found.");
            m_configData.insertDefaultMessage("forbidden", "Sign drawning is forbidden for <color=cyan>%player_name</color> by reason <color=cyan>%reason</color>.");
            m_configData.insertDefaultMessage("sign_unblocked", "Sign drawning was unblocked for player <color=cyan>%player_name</color>.");
            m_configData.insertDefaultMessage("invalid_arguments", "Invalid arguments.");
            m_configData.insertDefaultMessage("player_not_found", "Player not found.");
            m_configData.insertDefaultMessage("player_access", "<color=cyan>%timestamp</color> - <color=cyan>%player_name</color>(<color=cyan>%player_steamid</color>)");
        }

        private void Unload()
        {
            SaveDynamic();
        }

        void OnSignUpdated(Signage sign, BasePlayer player, string text)
        {
            uint signId = sign.net.ID;

            SignItem signItem;
            if (m_signsLogs.SignItems.ContainsKey(signId))
            {
                signItem = m_signsLogs.SignItems[signId];
            }
            else
            {
                signItem = new SignItem();
                m_signsLogs.SignItems[signId] = signItem;
            }

            AccessItem accessItem;
            if (signItem.AccessItems.ContainsKey(player.userID))
            {
                accessItem = signItem.AccessItems[player.userID];
            }
            else
            {
                accessItem = new AccessItem();
                signItem.AccessItems[player.userID] = accessItem;
            }

            accessItem.dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo == null || entity == null) return null;

            BasePlayer player = hitInfo.Initiator as BasePlayer;
            Signage sign = entity as Signage;

            if (player && sign && m_signsShowInfo.Contains(player.userID))
            {
                if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNSigns.signs"))
                {
                    return null;
                }

                uint signId = sign.net.ID;

                player.ChatMessage("Sign: <color=cyan>" + signId + "</color>");

                if (m_signsLogs.SignItems.ContainsKey(signId))
                {
                    List<ulong> contextPlayers = new List<ulong>();
                    SignItem signItem = m_signsLogs.SignItems[signId];
                    int i = 0;
                    foreach (ulong userID in sortedByDatetime(signItem.AccessItems))
                    {
                        contextPlayers.Add(userID);

                        AccessItem accessItem = m_signsLogs.SignItems[signId].AccessItems[userID];

                        string message = m_configData.Messages["player_access"];
                        message = message.Replace("%player_steamid", userID.ToString());
                        message = message.Replace("%player_name", SSNNotifier.Call<string>("PlayerName", userID));
                        message = message.Replace("%timestamp", accessItem.dateTime);
                        player.ChatMessage((++i).ToString() + ") " + message);
                    }
                    SSNNotifier.Call("SetContextPlayers", player.userID, contextPlayers);
                }
                return "handled";
            }
            return null;
        }

        List<ulong> sortedByDatetime(Dictionary<ulong, AccessItem> accessItems)
        {
            List<ulong> sorted = new List<ulong>();
            foreach (ulong userID in accessItems.Keys)
            {
                sorted.Add(userID);
            }

            for (int i = 0; i < sorted.Count - 1; ++i)
            {
                for (int k = i; k < sorted.Count; ++k)
                {
                    if (accessItems[sorted[i]].dateTime.CompareTo(accessItems[sorted[k]].dateTime) > 0)
                    {
                        ulong buffer = sorted[i];
                        sorted[i] = sorted[k];
                        sorted[k] = buffer;
                    }
                }
            }
            return sorted;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null) return;

            Signage sign = entity as Signage;

            if (sign)
            {
                uint signId = sign.net.ID;

                if (m_signsLogs.SignItems.ContainsKey(signId))
                {
                    m_signsLogs.SignItems.Remove(signId);
                }
            }
        }

        bool CanUpdateSign(Signage sign, BasePlayer player)
        {
            if (m_configData.SignBlocks.ContainsKey(player.userID))
            {
                SignBlock signBlock = m_configData.SignBlocks[player.userID];
                player.ChatMessage(m_configData.Messages["forbidden"].Replace("%reason", signBlock.reason).Replace("%player_name", player.displayName));
                sign.Kill();
                return false;
            }
            return true;
        }

        [ChatCommand("signs_show_info")]
        void cmdChatSignsShowInfo(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNSigns.signs"))
            {
                return;
            }

            if (args.Length == 1)
            {
                if (args[0] == "on")
                {
                    m_signsShowInfo.Add(player.userID);
                }
                else if (args[0] == "off")
                {
                    m_signsShowInfo.Remove(player.userID);
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

        [ChatCommand("signs_clear")]
        void cmdChatSignsClear(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNSigns.signs"))
            {
                return;
            }

            var signs = UnityEngine.Object.FindObjectsOfType<Signage>();
            player.ChatMessage(signs.Length.ToString() + " signs will be removed.");
            foreach (var sign in signs) sign.Kill();

            m_signsLogs.SignItems.Clear();
        }

        [ChatCommand("signs_block")]
        void cmdChatSignsBlock(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNSigns.signs"))
            {
                return;
            }

            if (args.Length >= 2)
            {
                ulong userID = SSNNotifier.Call<ulong>("UserIdByAlias", player.userID, args[0]);
                if (userID == 0)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }

                string reason = "";
                for (int i = 1; i < args.Length; ++i)
                {
                    reason += args[i];
                    if (i < args.Length - 1)
                    {
                        reason += " ";
                    }
                }

                SignBlock signBlock = new SignBlock();
                signBlock.datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                signBlock.reason = reason;
                m_configData.SignBlocks[userID] = signBlock;

                string message = m_configData.Messages["forbidden"];
                message = message.Replace("%player_name", SSNNotifier.Call<string>("PlayerName", userID));
                message = message.Replace("%reason", reason);
                PrintToChat(message);

                SaveConfig();
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
            }
        }

        [ChatCommand("signs_unblock")]
        void cmdChatSignsUnblock(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNSigns.signs"))
            {
                return;
            }

            if (args.Length == 1)
            {
                ulong userID = SSNNotifier.Call<ulong>("UserIdByAlias", player.userID, args[0]);
                if (userID == 0)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }

                m_configData.SignBlocks.Remove(userID);
                SaveConfig();

                string message = m_configData.Messages["sign_unblocked"];
                message = message.Replace("%player_name", SSNNotifier.Call<string>("PlayerName", userID));
                PrintToChat(message);
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
            }
        }

        [ChatCommand("signs_blocks")]
        void cmdChatSignsBlocks(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNSigns.signs"))
            {
                return;
            }

            if (args.Length == 0 || args.Length == 1)
            {
                int i = 0;
                foreach (ulong userID in m_configData.SignBlocks.Keys)
                {
                    string playerName = SSNNotifier.Call<string>("PlayerName", userID);

                    if (args.Length == 1)
                    {
                        if (!playerName.Contains(args[0], System.Globalization.CompareOptions.IgnoreCase))
                        {
                            continue;
                        }
                    }

                    SignBlock signBlock = m_configData.SignBlocks[userID];
                    player.ChatMessage((++i).ToString() + ") " + signBlock.datetime + " - " + userID.ToString() + " - " + playerName + " - " + signBlock.reason);
                }
            }
        }
    }
}