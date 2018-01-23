//Requires: SSNNotifier

using System.Collections.Generic;
using System;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SSNTeleport", "Umlaut", "0.1.0")]
    class SSNTeleport : RustPlugin
    {

        class ConfigData
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

        //

        void LoadConfig()
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

        void SaveConfig()
        {
            Config.WriteObject<ConfigData>(m_configData, true);
        }

        void InsertDefaultMessages()
        {
            m_configData.insertDefaultMessage("invalid_arguments", "Invalid arguments.");
            m_configData.insertDefaultMessage("player_not_found", "Player not found.");
        }

        // Hooks

        void Loaded()
        {
            LoadConfig();

            if (!permission.PermissionExists("SSNTeleport.teleport"))
            {
                permission.RegisterPermission("SSNTeleport.teleport", this);
            }
        }

        protected override void LoadDefaultConfig()
        {
            m_configData = new ConfigData();
            InsertDefaultMessages();
            Config.WriteObject(m_configData, true);
        }

        [ChatCommand("tp")]
        void cmdTp(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNTeleport.teleport"))
            {
                return;
            }

            BasePlayer targetPlayer = null;
            Vector3 position;
            bool offset = false;

            if (args.Length == 1 || (args.Length == 2 && args[1] == "offset"))
            {
                targetPlayer = player;

                ulong userId = SSNNotifier.Call<ulong>("UserIdByAlias", player.userID, args[0]);
                if (userId == 0)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }

                BasePlayer currentPlayer = BasePlayer.FindByID(userId);
                if (currentPlayer == null)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }
                else
                {
                    position = currentPlayer.transform.position;

                    if (args.Length == 2)
                    {
                        offset = true;
                    }
                }
            }
            else if (args.Length == 2 || (args.Length == 3 && args[2] == "offset"))
            {
                ulong userId1 = SSNNotifier.Call<ulong>("UserIdByAlias", player.userID, args[0]);
                if (userId1 == 0)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }

                targetPlayer = BasePlayer.FindByID(userId1);
                if (targetPlayer == null)
                {
                    player.ChatMessage(m_configData.Messages["player_not_found"]);
                    return;
                }
                else
                {
                    ulong userId2 = SSNNotifier.Call<ulong>("UserIdByAlias", player.userID, args[1]);
                    if (userId2 == 0)
                    {
                        player.ChatMessage(m_configData.Messages["player_not_found"]);
                        return;
                    }
                    else
                    {
                        BasePlayer currentPlayer = BasePlayer.FindByID(userId2);
                        if (currentPlayer == null)
                        {
                            player.ChatMessage(m_configData.Messages["player_not_found"]);
                            return;
                        }
                        else
                        {
                            position = currentPlayer.transform.position;
                        }
                    }
                }

                if (args.Length == 3)
                {
                    offset = true;
                }
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            if (offset)
            {
                position.y -= 25;
            }

            rust.ForcePlayerPosition(targetPlayer, position.x, position.y, position.z);
        }
    }
}
