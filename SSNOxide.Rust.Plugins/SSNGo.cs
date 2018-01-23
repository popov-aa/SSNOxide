//Requires: SSNNotifier
//Requires: SSNKits

using System.Collections.Generic;
using System;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SSNGo", "Umlaut", "0.0.1")]
    class SSNGo : RustPlugin
    {
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

        //

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

            public List<Point> SpawnPoints = new List<Point>();
            public uint WorldSize = 0;
            public uint WorldSeed = 0;
            public uint WorldSalt = 0;
            public string kit = "";
        }

        //

        [PluginReference]
        private Plugin SSNNotifier;
        [PluginReference]
        private Plugin SSNKits;
        private ConfigData m_configData;

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

        void SaveConfig()
        {
            Config.WriteObject<ConfigData>(m_configData, true);
        }


        // Hooks

        private void Loaded()
        {
            LoadConfig();

            if (m_configData.WorldSeed != World.Seed || m_configData.WorldSize != World.Size || m_configData.WorldSalt != World.Salt)
            {
                m_configData.SpawnPoints.Clear();

                m_configData.WorldSeed = World.Seed;
                m_configData.WorldSize = World.Size;
                m_configData.WorldSalt = World.Salt;
                SaveConfig();
            }

            timer.Repeat(300, 0, () => SendWellcomeMessage());
        }

        void SendWellcomeMessage()
        {
            if (m_configData.SpawnPoints.Count != 0)
            {
                PrintToChat(m_configData.Messages["wellcome"]);
            }
        }

        protected override void LoadDefaultConfig()
        {
            m_configData = new ConfigData();
            InsertDefaultMessages();
            SaveConfig();
        }

        void InsertDefaultMessages()
        {
            m_configData.insertDefaultMessage("swapn_point_was_added", "Spawn point was added.");
            m_configData.insertDefaultMessage("swapn_points_was_cleared", "Spawn point was cleared.");
            m_configData.insertDefaultMessage("invalid_arguments", "Invalid arguments.");
            m_configData.insertDefaultMessage("event_is_disabled", "This event is disabled.");
            m_configData.insertDefaultMessage("wellcome", "Wellcome to event! Just type <color=cyan>/go</color>");
            SaveConfig();
        }

        [ChatCommand("go_spawn_points")]
        void cmdChatGoSpawnPoints(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNMutes.go"))
            {
                return;
            }

            if (args.Length == 1)
            {
                if (args[0] == "add")
                {
                    m_configData.SpawnPoints.Add(new Point(player.transform.position));
                    player.ChatMessage(m_configData.Messages["swapn_point_was_added"]);
                    SaveConfig();
                }
                else if (args[0] == "clear")
                {
                    m_configData.SpawnPoints.Clear();
                    player.ChatMessage(m_configData.Messages["swapn_points_was_cleared"]);
                    SaveConfig();
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

        [ChatCommand("go")]
        void cmdChatGo(BasePlayer player, string command, string[] args)
        {
            if (m_configData.SpawnPoints.Count == 0)
            {
                player.ChatMessage(m_configData.Messages["event_is_disabled"]);
            }
            else
            {
                int index = Oxide.Core.Random.Range(0, m_configData.SpawnPoints.Count - 1);
                Point point = m_configData.SpawnPoints[index];
                rust.ForcePlayerPosition(player, point.x, point.y, point.z);
                player.ChangeHealth(100);
                player.SendNetworkUpdate();
                player.inventory.Strip();
                SSNKits.Call("LoadKitToPlayer", player, m_configData.kit);
            }
        }
    }
}
