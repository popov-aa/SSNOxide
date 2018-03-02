//Requires: SSNNotifier

using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("SSNVotes", "Umlaut", "0.0.1")]
    class SSNVotes : RustPlugin
    {
        // Описание типов

        class Answer
        {
            public string text = "";
            public HashSet<ulong> voters = new HashSet<ulong>();

            public Answer(string text)
            {
                this.text = text;
            }
        }

        class Vote
        {
            public string text = "";
            public List<Answer> answers = new List<Answer>();

            public Vote(string text)
            {
                this.text = text;
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
            public List<Vote> votes = new List<Vote>();
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

            if (!permission.PermissionExists("SSNVotes.votes"))
            {
                permission.RegisterPermission("SSNVotes.votes", this);
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
            m_configData.insertDefaultMessage("votes_using", "For listing votes try: <color=cyan>/votes</color>");
            m_configData.insertDefaultMessage("vote_using", "For voting try: <color=cyan>/vote number_of_vote number_of_answer</color>");
            m_configData.insertDefaultMessage("you_voted", "You voted for <color=cyan>%voteIndex) %question - [%answerIndex] %answer</color> <color=red>%rating</color>.");
            m_configData.insertDefaultMessage("votes_not_found", "Votes not found.");
            m_configData.insertDefaultMessage("vote_was_added", "Vote was added as <color=cyan>%number) %question</color>.");
            m_configData.insertDefaultMessage("vote_was_removed", "Vote <color=cyan>%number) %question</color> was removed.");
            m_configData.insertDefaultMessage("no_answers", "no answers");
            m_configData.insertDefaultMessage("answer_was_added", "Answer <color=cyan>[%number] %answer</color> was added.");
            m_configData.insertDefaultMessage("answer_was_removed", "Answer <color=cyan>[%number] %answer</color> was removed.");

            SaveConfig();
        }

        [ChatCommand("votes")]
        void cmdChatVotes(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNVotes.votes")) return;

            if (m_configData.votes.Count == 0)
            {
                player.ChatMessage(m_configData.Messages["votes_not_found"]);
            }
            else
            {
                printVotes(player);
            }
        }

        [ChatCommand("vote_add")]
        void cmdChatVoteAdd(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNVotes.votes")) return;

            if (args.Length == 0)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            string text = "";
            for (int i = 0; i < args.Length; ++i)
            {
                text += args[i];
                if (i < args.Length - 1)
                {
                    text += " ";
                }
            }
            Vote vote = new Vote(text);
            m_configData.votes.Add(vote);

            SaveConfig();

            player.ChatMessage(m_configData.Messages["vote_was_added"].Replace("%number", m_configData.votes.Count.ToString()).Replace("%question", text));
        }

        [ChatCommand("vote_remove")]
        void cmdChatVoteRemove(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNVotes.votes")) return;

            if (args.Length != 1)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            int index = int.Parse(args[0]);
            if (index < 1 || index > m_configData.votes.Count)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            Vote vote = m_configData.votes[index - 1];
            m_configData.votes.RemoveAt(index - 1);
            SaveConfig();

            player.ChatMessage(m_configData.Messages["vote_was_removed"].Replace("%number", index.ToString()).Replace("%question", vote.text));
        }

        [ChatCommand("answer_add")]
        void cmdChatAnswerAdd(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNVotes.votes")) return;

            if (args.Length < 2)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            int index = int.Parse(args[0]);
            if (index < 1 || index > m_configData.votes.Count)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            Vote vote = m_configData.votes[index - 1];

            string answer = "";
            for (int i = 1; i < args.Length; ++i)
            {
                answer += args[i];
                if (i < args.Length - 1)
                {
                    answer += " ";
                }
            }
            vote.answers.Add(new Answer(answer));
            
            SaveConfig();

            player.ChatMessage(m_configData.Messages["answer_was_added"].Replace("%number", vote.answers.Count.ToString()).Replace("%answer", answer));
        }

        [ChatCommand("answer_remove")]
        void cmdChatAnswerRemove(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel == 0 && !permission.UserHasPermission(player.userID.ToString(), "SSNVotes.votes")) return;

            if (args.Length != 2)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            int voteIndex = int.Parse(args[0]);
            if (voteIndex < 1 || voteIndex > m_configData.votes.Count)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            Vote vote = m_configData.votes[voteIndex - 1];

            int answerIndex = int.Parse(args[1]);
            if (answerIndex < 1 || answerIndex > vote.answers.Count)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            string answer = vote.answers[answerIndex - 1].text;
            vote.answers.RemoveAt(answerIndex - 1);

            SaveConfig();

            player.ChatMessage(m_configData.Messages["answer_was_removed"].Replace("%number", vote.answers.Count.ToString()).Replace("%answer", answer));
        }

        [ChatCommand("vote")]
        void cmdChatVote(BasePlayer player, string command, string[] args)
        {
            if (args.Length != 2)
            {
                player.ChatMessage(m_configData.Messages["vote_using"]);
                return;
            }

            int voteIndex = int.Parse(args[0]);
            if (voteIndex < 1 || voteIndex > m_configData.votes.Count)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            Vote vote = m_configData.votes[voteIndex - 1];

            int answerIndex = int.Parse(args[1]);
            if (answerIndex < 1 || answerIndex > vote.answers.Count)
            {
                player.ChatMessage(m_configData.Messages["invalid_arguments"]);
                return;
            }

            Answer answer = vote.answers[answerIndex - 1];
            foreach (Answer _answer in vote.answers)
            {
                _answer.voters.Remove(player.userID);
            }
            answer.voters.Add(player.userID);
            SaveConfig();

            player.ChatMessage(m_configData.Messages["you_voted"]
                .Replace("%voteIndex", voteIndex.ToString())
                .Replace("%question", vote.text)
                .Replace("%answerIndex", answerIndex.ToString())
                .Replace("%answer", answer.text)
                .Replace("%rating", answer.voters.Count.ToString()));
        }

        void printVotes(BasePlayer player)
        {
            int i = 1;
            foreach (Vote vote in m_configData.votes)
            {
                string message = "<color=cyan>" + i++ + ")</color> " + vote.text + ": ";
                if (vote.answers.Count > 0)
                {
                    int k = 1;
                    foreach (Answer answer in vote.answers)
                    {
                        message += "<color=cyan>[" + k++ + "]</color> " + answer.text + " <color=red>" + answer.voters.Count + "</color> ";
                    }
                }
                else
                {
                    message += m_configData.Messages["no_answers"] + ".";
                }
                player.ChatMessage(message);
            }
            player.ChatMessage(m_configData.Messages["vote_using"]);
        }

    }
}
