//Requires: SSNNotifier

using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SSNKits", "Umlaut", "0.0.1")]
    class SSNKits : RustPlugin
    {
        // Описание типов

        class KitItem
        {
            public string Name;
            public int Id;
            public int Amount;

            public KitItem()
            {
            }
        }

        class Kit
        {
            public HashSet<KitItem> BeltItems = new HashSet<KitItem>();
            public HashSet<KitItem> MainItems = new HashSet<KitItem>();
            public HashSet<KitItem> WearItems = new HashSet<KitItem>();

            public Kit()
            {
            }
        }

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
            public Dictionary<string, Kit> Kits = new Dictionary<string, Kit>();
        }

        // Члены класса

        [PluginReference]
        private Plugin SSNNotifier;

        ConfigData m_configData;

        // Загрузка данных

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

        // Сохранение данных

        void SaveConfig()
        {
            Config.WriteObject<ConfigData>(m_configData, true);
        }

        // Стандартные хуки

        void Loaded()
        {
            LoadConfig();

            if (!permission.PermissionExists("SSNKits.kits"))
            {
                permission.RegisterPermission("SSNKits.kits", this);
            }
        }

        protected override void LoadDefaultConfig()
        {
            m_configData = new ConfigData();
            InsertDefaultMessages();
        }

        void InsertDefaultMessages()
        {
            m_configData.insertDefaultMessage("invalid_arguments", "Invalid arguments.");
            m_configData.insertDefaultMessage("kit_not_found", "Kit <color=cyan>%kit</color> not found.");
            m_configData.insertDefaultMessage("kit_was_issued", "Kit <color=cyan>%kit</color> was issued.");
            m_configData.insertDefaultMessage("kit_was_saved", "Kit <color=cyan>%kit</color> was saved.");
            SaveConfig();
        }

        [ChatCommand("kits")]
        void cmdChatKits(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNKits.kits")) return;

            int index = 0;
            string message = "Kits: ";
            foreach (string kitKey in m_configData.Kits.Keys)
            {
                message += kitKey;
                if (index-- < m_configData.Kits.Count - 1)
                {
                    message += ", ";
                }
            }
            player.ChatMessage(message);
        }

        [ChatCommand("kit_save")]
        void cmdChatKitSave(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNKits.kits")) return;

            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            Kit kit = new Kit();
            kit.BeltItems = GetItemsByItemContainer(player.inventory.containerBelt);
            kit.MainItems = GetItemsByItemContainer(player.inventory.containerMain);
            kit.WearItems = GetItemsByItemContainer(player.inventory.containerWear);
            m_configData.Kits[args[0]] = kit;
            SaveConfig();

            player.ChatMessage(m_configData.Messages["kit_was_saved"].Replace("%kit", args[0]));
        }

        [ChatCommand("kit_load")]
        void cmdChatKitLoad(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNKits.kits")) return;

            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            if (LoadKitToPlayer(player, args[0]))
            {
                player.ChatMessage(m_configData.Messages["kit_was_issued"].Replace("%kit", args[0]));
            }
            else
            {
                player.ChatMessage(m_configData.Messages["kit_not_found"].Replace("%kit", args[0]));
            }
        }

        HashSet<KitItem> GetItemsByItemContainer(ItemContainer itemContainer)
        {
            HashSet<KitItem> items = new HashSet<KitItem>();

            foreach (Item item in itemContainer.itemList)
            {
                KitItem kitItem = new KitItem();
                kitItem.Name = item.info.displayName.english;
                kitItem.Id = item.info.itemid;
                kitItem.Amount = item.amount;
                items.Add(kitItem);
            }

            return items;
        }

        bool LoadKitToPlayer(BasePlayer player, string kitKey)
        {
            if (m_configData.Kits.ContainsKey(kitKey))
            {
                Kit kit = m_configData.Kits[kitKey];
                LoadKitItemsToContainer(player, kit.BeltItems, player.inventory.containerBelt);
                LoadKitItemsToContainer(player, kit.MainItems, player.inventory.containerMain);
                LoadKitItemsToContainer(player, kit.WearItems, player.inventory.containerWear);
                return true;
            }
            return false;
        }

        void LoadKitItemsToContainer(BasePlayer player, HashSet<KitItem> kitItems, ItemContainer itemContainer)
        {
            foreach (KitItem kitItem in kitItems)
            {
                player.inventory.GiveItem(ItemManager.CreateByItemID(kitItem.Id, kitItem.Amount), itemContainer);
            }
        }

    }
}
