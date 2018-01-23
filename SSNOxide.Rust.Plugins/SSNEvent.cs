//Requires: SSNNotifier

using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SSNEvent", "Umlaut", "0.0.1")]
    class SSNEvent : RustPlugin
    {
        // Описание типов

        public class Position
        {
            public float x = 0;
            public float y = 0;
            public float z = 0;

            public Position() { }
            public Position(Vector3 vector3) { x = vector3.x; y = vector3.y; z = vector3.z; }

            public Vector3 vector3() { return new Vector3(x, y, z); }

            public static Position operator -(Position p1, Position p2)
            {
                Position p0 = new Position();
                p0.x = p1.x - p2.x;
                p0.y = p1.y - p2.y;
                p0.z = p1.z - p2.z;
                return p0;
            }
        }

        private enum EventState
        {
            Disabled = 0,
            Deathmatch = 1,
            TeamDeathmatch = 2
        };

        enum Team
        {
            Common = 0,
            Red = 1,
            Blue = 2
        };

        private class ConfigData
        {
            public Dictionary<string, string> Messages = new Dictionary<string, string>();
            public Dictionary<Team, List<Position>> SpawnPoints = new Dictionary<Team, List<Position>>();
            public Dictionary<uint, Team> LootGetters = new Dictionary<uint, Team>();
            public Dictionary<Team, string> KitByTeam = new Dictionary<Team, string>();
            public Dictionary<Team, string> ColorByTeam = new Dictionary<Team, string>();
            public List<Position> ArenaPoints = new List<Position>();

            public ConfigData() { }
        }

        // Члены класса

        private string PluginCommand = "SSNEvent.event";

        [PluginReference]
        private Plugin SSNKits;

        [PluginReference]
        private Plugin SSNNotifier;

        private ConfigData m_configData;

        private EventState m_state = EventState.Disabled;

        private Dictionary<ulong, Team> m_players = new Dictionary<ulong, Team>();
        private HashSet<ulong> m_lootedPlayers = new HashSet<ulong>();

        private System.Random m_random = new System.Random();
        private Team m_lootGetterWaiting = Team.Common;

        private Dictionary<ulong, int> m_playersMurders = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> m_playersDeaths = new Dictionary<ulong, int>();

        private int m_fragsMax = 0;

        // Загрузка данных

        void LoadData()
        {
            try
            {
                m_configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        // Сохранение данных

        void SaveData()
        {
            Config.WriteObject<ConfigData>(m_configData, true);
        }

        // Стандартные хуки

        void Loaded()
        {
            LoadData();
            if (!permission.PermissionExists(PluginCommand))
            {
                permission.RegisterPermission(PluginCommand, this);
            }
        }

        void Unload()
        {
            SaveData();
        }

        protected override void LoadDefaultConfig()
        {
            m_configData = new ConfigData();
            m_configData.Messages["invalid_arguments"] = "Invalid arguments.";
            m_configData.Messages["event_on"] = "You got to the \"%team\" team. Now you needed only to die.";
            m_configData.Messages["event_off"] = "You refused participation in an event.";
            m_configData.Messages["player_declared_already"] = "You are already declared on event in \"%team\" team.";
            m_configData.Messages["player_not_declared"] = "You aren't declared on event.";
            m_configData.Messages["spawn_point_was_added"] = "Spawn point was added for team \"%team\".";
            m_configData.Messages["spawn_points_was_cleared"] = "Spawn points was cleared.";
            m_configData.Messages["loot_getter_was_added"] = "Loot getter %loot_getter was added for team \"%team\".";
            m_configData.Messages["loot_getter_was_removed"] = "Loot getter %loot_getter was removed for team \"%team\".";
            m_configData.Messages["loot_getter_wating_on"] = "Waiting loot getter for team \"%team\".";
            m_configData.Messages["loot_getter_wating_off"] = "End of waiting loot getter.";
            m_configData.Messages["player_death"] = "Player %victim_name was died.";
            m_configData.Messages["player_murder"] = "Player %killer_name killed %victim_name.";
            m_configData.Messages["event_is_disabled"] = "Event is disabled.";
            m_configData.Messages["event_status_was_changed"] = "Event status was chenged to: %status.";
            m_configData.Messages["event_was_disabled"] = "Event was disabled.";
            m_configData.Messages["event_was_started"] = "Event was disabled. Type: %event_type. Frags for win: %frags.";
            m_configData.Messages["endless"] = "endless";
            m_configData.Messages["player_win"] = "<color=red>Player %player win!</color>";
            m_configData.Messages["server_state"] = "Server state: %state";
			m_configData.Messages["player_on"] = "Player %player was declared on event. Count of members: %count";

            m_configData.ColorByTeam.Add(Team.Common, "<color=yellow>common</color>");
            m_configData.ColorByTeam.Add(Team.Blue, "<color=blue>blue</color>");
            m_configData.ColorByTeam.Add(Team.Red, "<color=red>red</color>");

            m_configData.KitByTeam.Add(Team.Common, "event_common");
            m_configData.KitByTeam.Add(Team.Blue, "event_blue");
            m_configData.KitByTeam.Add(Team.Red, "event_red");

            m_configData.SpawnPoints[Team.Blue] = new List<Position>();
            m_configData.SpawnPoints[Team.Red] = new List<Position>();

            SaveData();
        }

        [ChatCommand("event")]
        void cmdEvent(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            if (args[0] == "yes")
            {
                if (m_players.ContainsKey(player.userID))
                {
                    player.ChatMessage(m_configData.Messages["player_declared_already"].Replace("%team", m_configData.ColorByTeam[m_players[player.userID]]));
                }
                else
                {
                    Team team = Team.Common;
                    if (m_state == EventState.Disabled)
                    {
                        player.ChatMessage(m_configData.Messages["event_is_disabled"]);
                        return;
                    }
                    else if (m_state == EventState.TeamDeathmatch)
                    {
                        int redCount = 0;
                        int blueCount = 0;
                        foreach (ulong currentPlayer in m_players.Keys)
                        {
                            if (m_players[currentPlayer] == Team.Red) redCount++;
                            else if (m_players[currentPlayer] == Team.Blue) blueCount++;
                        }

                        team = redCount < blueCount ? Team.Red : Team.Blue;
                    }
                    m_players[player.userID] = team;
                    if (!m_playersDeaths.ContainsKey(player.userID))
                    {
                        m_playersDeaths[player.userID] = 0;
                    }
                    if (!m_playersMurders.ContainsKey(player.userID))
                    {
                        m_playersMurders[player.userID] = 0;
                    }
                    player.ChatMessage(m_configData.Messages["event_on"].Replace("%team", m_configData.ColorByTeam[team]));
                    PrintToChat(m_configData.Messages["player_on"].Replace("%player", playerScore(player.userID)).Replace("%count", m_players.Count.ToString()));
                }
            }
            else if (args[0] == "no")
            {
                if (m_players.ContainsKey(player.userID))
                {
                    m_players.Remove(player.userID);
                    player.ChatMessage(m_configData.Messages["event_off"]);
                }
                else
                {
                    player.ChatMessage(m_configData.Messages["player_not_declared"]);
                }
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
            }
        }

        [ChatCommand("event_arena_points")]
        void cmdEventArenaPoints(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), PluginCommand)) return;

            if (args.Length == 0)
            {
                player.ChatMessage(IsPointInside(new Position(player.transform.position)).ToString());
                player.ChatMessage(m_configData.ArenaPoints.Count.ToString());
            }
            else if (args.Length == 1)
            {
                if (args[0] == "clear")
                {
                    m_configData.ArenaPoints.Clear();
                    SaveData();
                }
                else if (args[0] == "add")
                {
                    m_configData.ArenaPoints.Add(new Position(player.transform.position));
                    SaveData();
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

        [ChatCommand("event_state")]
        void cmdEventType(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), PluginCommand)) return;

            if (args.Length == 0)
            {
                player.ChatMessage(m_configData.Messages["server_state"].Replace("%state", m_state.ToString()));
                List<ulong> players = new List<ulong>();
                Dictionary<ulong, int> playerScores = new Dictionary<ulong, int>();
                foreach (ulong steamid in m_players.Keys)
                {
                    players.Add(steamid);
                    playerScores[steamid] = m_playersMurders[steamid];
                }
                for (int i = 0; i < players.Count - 1; i++)
                {
                    for (int j = i; j < players.Count; j++)
                    {
                        if (playerScores[players[i]] < playerScores[players[j]])
                        {
                            ulong buf = players[i];
                            players[i] = players[j];
                            players[j] = buf;
                        }
                    }
                }
                string scores = "";
                for (int i = 0; i < players.Count; i++)
                {
                    scores += (i + 1).ToString() + ") " + playerScore(players[i]);
                    if (i < players.Count - 1)
                    {
                        scores += ", ";
                    }
                }
                player.ChatMessage(scores);
            }
            else if (args.Length > 1)
            {
                if (args[0] == "set")
                {
                    EventState rState;
                    if (args[1] == EventState.Disabled.ToString())
                    {
                        rState = EventState.Disabled;
                    }
                    else if (args[1] == EventState.Deathmatch.ToString())
                    {
                        rState = EventState.Deathmatch;
                    }
                    else if (args[1] == EventState.TeamDeathmatch.ToString())
                    {
                        rState = EventState.TeamDeathmatch;
                    }
                    else
                    {
                        player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                        return;
                    }

                    if ((rState == EventState.Deathmatch && m_state == EventState.TeamDeathmatch) ||
                        (rState == EventState.TeamDeathmatch && m_state == EventState.Deathmatch))
                    {
                        player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                        return;
                    }

                    m_lootedPlayers.Clear();
                    m_players.Clear();
                    m_playersDeaths.Clear();
                    m_playersMurders.Clear();
                    m_state = rState;

                    if (m_state == EventState.Disabled)
                    {
                        player.ChatMessage(m_configData.Messages["event_was_disabled"]);
                    }
                    else
                    {
                        int frags = 0;
                        if (args.Length == 3)
                        {
                            if (!int.TryParse(args[2], out frags))
                            {
                                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                                return;
                            }
                        }
                        m_fragsMax = frags;
                        PrintToChat(m_configData.Messages["event_was_started"].Replace("%event_type", m_state.ToString()).Replace("%frags", m_fragsMax == 0 ? m_configData.Messages["endless"] : m_fragsMax.ToString()));
                    }
                }
                else
                {
                    player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                    return;
                }
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }
        }

        [ChatCommand("event_spawn_points")]
        void cmdEventSpawnPoints(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), PluginCommand)) return;

            if (args.Length == 0)
            {
                foreach (Team team in m_configData.SpawnPoints.Keys)
                {
                    player.ChatMessage(team.ToString() + ": " + m_configData.SpawnPoints[team].Count.ToString());
                }
            }
            else if (args.Length == 1)
            {
                if (args[0] == "clear")
                {
                    m_configData.SpawnPoints[Team.Blue] = new List<Position>();
                    m_configData.SpawnPoints[Team.Red] = new List<Position>();
                    SaveData();
                    player.ChatMessage(m_configData.Messages["spawn_points_was_cleared"]);
                }
                else
                {
                    player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                    return;
                }
            }
            else if (args.Length == 2)
            {
                if (args[0] == "set")
                {
                    Team team;
                    if (args[1] == "blue")
                    {
                        team = Team.Blue;
                    }
                    else if (args[1] == "red")
                    {
                        team = Team.Red;
                    }
                    else
                    {
                        player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                        return;
                    }

                    if (!m_configData.SpawnPoints.ContainsKey(team))
                    {
                        m_configData.SpawnPoints[team] = new List<Position>();
                    }
                    m_configData.SpawnPoints[team].Add(new Position(player.transform.position));
                    SaveData();
                    player.ChatMessage(m_configData.Messages["spawn_point_was_added"].Replace("%team", m_configData.ColorByTeam[team]));
                }
                else
                {
                    player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                    return;
                }
            }
            else
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }
        }

        [ChatCommand("event_loot_getters")]
        void cmdEventLootGetters(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), PluginCommand)) return;

            if (args.Length > 0)
            {
                if (args[0] == "clear")
                {
                    m_configData.LootGetters.Clear();
                }
                else if (args[0] == "set")
                {
                    if (args.Length != 2)
                    {
                        player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                        return;
                    }

                    if (args[1] == "blue")
                    {
                        m_lootGetterWaiting = Team.Blue;
                    }
                    else if (args[1] == "red")
                    {
                        m_lootGetterWaiting = Team.Red;
                    }
                    else if (args[1] == "off")
                    {
                        m_lootGetterWaiting = Team.Common;
                    }
                    else
                    {
                        player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                        return;
                    }

                    if (m_lootGetterWaiting == Team.Common)
                    {
                        player.ChatMessage(m_configData.Messages["loot_getter_wating_off"]);
                    }
                    else
                    {
                        player.ChatMessage(m_configData.Messages["loot_getter_wating_on"].Replace("%team", m_configData.ColorByTeam[m_lootGetterWaiting]));
                    }
                    SaveConfig();
                    return;
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

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null) return null;

            if (IsPointInside(new Position(entity.transform.position)))
            {
                if (entity as BasePlayer != null) return null;

                if (hitInfo == null) return "handled";
                BasePlayer player = hitInfo.Initiator as BasePlayer;
                BasePlayer targetPlayer = entity as BasePlayer;

                if (player != null && targetPlayer != null)
                {
                    if (m_players.ContainsKey(player.userID) && m_players.ContainsKey(targetPlayer.userID) &&
                        m_players[player.userID] == m_players[targetPlayer.userID] &&
                        m_state == EventState.TeamDeathmatch)
                    {
                        player.health = player.health / 2;
                        return false;
                    }
                }

                Signage sign = entity as Signage;
            
                if (player == null || sign == null) return "handled";

                uint lootGetterId = sign.net.ID;

                if (m_lootGetterWaiting != Team.Common && (player.net.connection.authLevel > 0 || permission.UserHasPermission(player.userID.ToString(), PluginCommand)))
                {
                    if (m_configData.LootGetters.ContainsKey(lootGetterId))
                    {
                        player.ChatMessage(m_configData.Messages["loot_getter_was_removed"].Replace("%loot_getter", lootGetterId.ToString()).Replace("%team", m_configData.ColorByTeam[m_configData.LootGetters[lootGetterId]]));
                        m_configData.LootGetters.Remove(lootGetterId);
                    }
                    else
                    {
                        m_configData.LootGetters[lootGetterId] = m_lootGetterWaiting;
                        player.ChatMessage(m_configData.Messages["loot_getter_was_added"].Replace("%loot_getter", lootGetterId.ToString()).Replace("%team", m_configData.ColorByTeam[m_configData.LootGetters[lootGetterId]]));
                    }
                    SaveData();
                    return "handled";
                }

                if (!m_configData.LootGetters.ContainsKey(lootGetterId)) return "handled";
                if (!m_players.ContainsKey(player.userID)) return "handled";

                Team gettetTeam = m_configData.LootGetters[lootGetterId];
                Team playerTeam = m_players[player.userID];

                if (gettetTeam == playerTeam || playerTeam == Team.Common)
                {
                    if (!m_lootedPlayers.Contains(player.userID))
                    {
                        SSNKits.Call<bool>("LoadKitToPlayer", player, m_configData.KitByTeam[playerTeam]);
                        m_lootedPlayers.Add(player.userID);
                    }
                }
                return "handled";
            }
            else
            {
                return null;
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (m_players.ContainsKey(player.userID))
            {
                List<Position> positions;
                Team team = m_players[player.userID];

                if (team == Team.Common)
                {
                    positions = new List<Position>();
                    foreach (Team currentTeam in m_configData.SpawnPoints.Keys)
                    {
                        foreach (Position currentPosition in m_configData.SpawnPoints[currentTeam])
                        {
                            positions.Add(currentPosition);
                        }
                    }
                }
                else
                {
                    positions = m_configData.SpawnPoints[team];
                }

                if (positions.Count == 0)
                {
                    Puts("error - positions for player spawn not found");
                }

                int index = m_random.Next(positions.Count - 1);

                m_lootedPlayers.Remove(player.userID);
                player.ChangeHealth(100);

                Position position = positions[m_random.Next(positions.Count - 1)];

                //

                Oxide.Game.Rust.Libraries.Rust rust = Interface.Oxide.GetLibrary<Oxide.Game.Rust.Libraries.Rust>("Rust");

                player.StartSleeping();
                player.MovePosition(position.vector3());
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position.vector3());
                
                rust.ForcePlayerPosition(player, position.x, position.y, position.z);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                player.UpdateNetworkGroup();
                player.SendFullSnapshot();
                player.ChangeHealth(100);
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            m_players.Remove(player.userID);
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            BasePlayer playerVictim = entity as BasePlayer;
            if (playerVictim == null) return;
            if (!m_players.ContainsKey(playerVictim.userID)) return;

            playerVictim.inventory.Strip();

            BasePlayer playerKiller = null;
            if (hitInfo != null)
            {
                if (hitInfo.Initiator != null)
                {
                    playerKiller = hitInfo.Initiator as BasePlayer;
                    if (playerKiller != null)
                    {
                        if (!m_players.ContainsKey(playerKiller.userID)) return;
                    }
                }
            }
            if (playerKiller == playerVictim)
            {
				m_playersDeaths[playerVictim.userID]++;
                m_playersMurders[playerVictim.userID]--;
            }
            else
            {
                if (m_state == EventState.TeamDeathmatch)
                {
                    if (m_players[playerVictim.userID] == m_players[playerKiller.userID])
                    {
                        playerKiller.health = 1;
                        m_playersMurders[playerKiller.userID]--;
                    }
                    else
                    {
						m_playersDeaths[playerVictim.userID]++;
                        m_playersMurders[playerKiller.userID]++;
                    }
                }
                else
                {
					m_playersDeaths[playerVictim.userID]++;
                    m_playersMurders[playerKiller.userID]++;
                }
            }

            string message;
            if (playerKiller != null && playerKiller != playerVictim)
            {
                message = m_configData.Messages["player_murder"].Replace("%killer_name", playerScore(playerKiller.userID));
            }
            else
            {
                message = m_configData.Messages["player_death"];
            }
            message = message.Replace("%victim_name", playerScore(playerVictim.userID));
            PrintToChat(message);
            if (m_fragsMax != 0 && m_playersMurders[playerKiller.userID] >= m_fragsMax)
            {
                PrintToChat(m_configData.Messages["player_win"].Replace("%player", playerKiller.displayName));
                m_lootedPlayers.Clear();
                m_players.Clear();
                m_playersDeaths.Clear();
                m_playersMurders.Clear();
                m_state = EventState.Disabled;
            }
        }

        string playerScore(ulong steamid)
        {
            string color;
            if (m_players[steamid] == Team.Blue)
            {
                color = "blue";
            }
            else if (m_players[steamid] == Team.Red)
            {
                color = "red";
            }
            else if (m_players[steamid] == Team.Common)
            {
                color = "yellow";
            }
            else
            {
                color = "white";
            }

            int murders = m_playersMurders.ContainsKey(steamid) ? m_playersMurders[steamid] : 0;
            int deaths = m_playersDeaths.ContainsKey(steamid) ? m_playersDeaths[steamid] : 0;
            string score = "[" + murders.ToString() + "/" + deaths.ToString() + "]";
            return "<color=" + color + ">" + SSNNotifier.Call<string>("CustomOrRealPlayerName", steamid) + " " + score + "</color>";
        }

        bool IsPointInside(Position point)
        {
            if (m_configData.ArenaPoints.Count < 3)
                return false;

            int intersections_num = 0;
            int prev = m_configData.ArenaPoints.Count - 1;
            bool prev_under = m_configData.ArenaPoints[prev].z < point.z;

            for (int i = 0; i < m_configData.ArenaPoints.Count; ++i)
            {
                bool cur_under = m_configData.ArenaPoints[i].z < point.z;

                Position a = m_configData.ArenaPoints[prev] - point;
                Position b = m_configData.ArenaPoints[i] - point;

                float t = (a.x * (b.z - a.z) - a.z * (b.x - a.x));
                if (cur_under && !prev_under)
                {
                    if (t > 0)
                        intersections_num += 1;
                }
                if (!cur_under && prev_under)
                {
                    if (t< 0)
                        intersections_num += 1;
                }

                prev = i;        
                prev_under = cur_under;        
            }

            return (intersections_num&1) != 0;
        }
    }
}
