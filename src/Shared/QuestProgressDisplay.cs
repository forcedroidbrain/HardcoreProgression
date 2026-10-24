using System;
using System.Collections.Generic;
using System.Text;

namespace HardcoreProgression.Display
{
    public sealed class QuestProgressResponse
    {
        public bool Available { get; set; } = true;
        public Dictionary<string, Dictionary<string, string>> Locales { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        public Dictionary<string, QuestProgressInfo> Quests { get; set; } = new Dictionary<string, QuestProgressInfo>();
    }

    public sealed class QuestProgressInfo
    {
        public int Raids { get; set; }
        public int Required { get; set; }
        public bool Unlocked { get; set; }
        public List<RepeatExchangeProgress> Repeats { get; set; } = new List<RepeatExchangeProgress>();
    }

    public sealed class RepeatExchangeProgress
    {
        public string ItemName { get; set; } = "";
        public string ItemTemplate { get; set; } = "";
        public int Raids { get; set; }
        public int Required { get; set; }
    }

    public static class DescriptionBanner
    {
        private const string Begin = "<link=\"local-hardcore-progress\">";
        private const string End = "</link>\n\n";

        public static string Remove(string description)
        {
            if (!description.StartsWith(Begin, StringComparison.Ordinal)) return description;
            int end = description.IndexOf(End, Begin.Length, StringComparison.Ordinal);
            return end < 0 ? description : description.Substring(end + End.Length);
        }

        public static string Apply(string description, QuestProgressInfo progress, Dictionary<string, string> strings)
        {
            string original = Remove(description ?? "");
            if (progress.Required < 1) return original;
            var text = new StringBuilder(Begin);
            text.Append("<color=#D6B46A><b>").Append(strings["Raids"])
                .Append(Math.Max(0, Math.Min(progress.Raids, progress.Required))).Append('/').Append(progress.Required).Append("</b>");
            if (progress.Unlocked) text.Append(strings["Unlocked"]);
            text.Append("</color>");
            foreach (var repeat in progress.Repeats)
            {
                if (repeat.Required < 1) continue;
                string name = (repeat.ItemName ?? "").Replace('<', '‹').Replace('>', '›');
                text.Append("\n").Append(strings["Repeat"]).Append(name).Append(": ")
                    .Append(Math.Max(0, Math.Min(repeat.Raids, repeat.Required))).Append('/').Append(repeat.Required);
            }
            return text.Append(End).Append(original).ToString();
        }
    }
}
