using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("SSNMurders", "Umlaut", "0.0.1")]
    class SSNMurders : RustPlugin
    {

        // Описание типов

        class ConfigData
        {
            public Dictionary<string, string> Messages = new Dictionary<string, string>();
            public ConfigData() {}
        }

        class DeathEvent
        {
            public ulong killerSteamId = 0;
            public string killerName;
            public ulong killedSteamId = 0;
            public string killedName;
            public double distance = 0;
            public bool isHeadshot = false;
            public bool isSleeping = false;
            public DateTime datetime;
            public string weapon;
        }

        // Описание полей

        ConfigData m_configData;

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
            SaveData();
        }

        void Unload()
        {
            //SaveData();
        }

        protected override void LoadDefaultConfig()
        {
            m_configData = new ConfigData();

            m_configData.Messages["death"] = "%datetime: <color=cyan>%killer</color> killed <color=cyan>%killed</color>%sleepingby <color=cyan>%weapon</color> for <color=cyan>%distance</color>.";
            m_configData.Messages["sleeping"] = "sleeping";
            m_configData.Messages["headshot"] = "Headshot!";
            m_configData.Messages["deaths_invalid_arguments"] = "Invalid arguments. Usage: deaths [all|killer|killed] [alias]";

            SaveData();
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null || hitInfo.Initiator == null)
            {
                return;
            }

            BasePlayer playerKilled = entity.ToPlayer();
            BasePlayer playerKiller = hitInfo.Initiator as BasePlayer;

            if (playerKilled == null || playerKiller == null || playerKilled == playerKiller)
            {
                return;
            }

            DeathEvent deathEvent = new DeathEvent();
            deathEvent.killerSteamId = playerKiller.userID;
            deathEvent.killerName = playerKiller.displayName;
            deathEvent.killedSteamId = playerKilled.userID;
            deathEvent.killedName = playerKilled.displayName;
            deathEvent.datetime = DateTime.Now;
            deathEvent.distance = Math.Sqrt(
                Math.Pow(playerKilled.transform.position.x - playerKiller.transform.position.x, 2) +
                Math.Pow(playerKilled.transform.position.y - playerKiller.transform.position.y, 2) +
                Math.Pow(playerKilled.transform.position.z - playerKiller.transform.position.z, 2));
            deathEvent.weapon = hitInfo.Weapon.GetItem().info.displayName.english;
            deathEvent.isHeadshot = hitInfo.isHeadshot;
            deathEvent.isSleeping = playerKilled.IsSleeping();

            PrintToChat(GetDeathMessage(deathEvent));
        }

        string GetDeathMessage(DeathEvent deathEvent)
        {
            string message = m_configData.Messages["death"];
            message = message.Replace("%killer", deathEvent.killerName);
            message = message.Replace("%killed", deathEvent.killedName);
            message = message.Replace("%weapon", deathEvent.weapon);
            message = message.Replace("%distance", Math.Round(deathEvent.distance, 1).ToString());
            message = message.Replace("%datetime", deathEvent.datetime.ToString("yyyy-MM-dd HH:mm:ss"));

            if (deathEvent.isSleeping)
            {
                message = message.Replace("%sleeping", " (" + m_configData.Messages["sleeping"] + ") ");
            }
            else
            {
                message = message.Replace("%sleeping", " ");
            }

            if (deathEvent.isHeadshot)
            {
                message += " " + m_configData.Messages["headshot"];
            }
            return message;
        }
    }
}