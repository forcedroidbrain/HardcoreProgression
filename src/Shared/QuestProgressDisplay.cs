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
        private const string PlainBegin = "[Hardcore Progression] ";
        private const string PlainEnd = "\n\n";

        public static string Remove(string description)
        {
            string begin;
            string ending;
            if (description.StartsWith(Begin, StringComparison.Ordinal)) { begin = Begin; ending = End; }
            else if (description.StartsWith(PlainBegin, StringComparison.Ordinal)) { begin = PlainBegin; ending = PlainEnd; }
            else return description;
            int end = description.IndexOf(ending, begin.Length, StringComparison.Ordinal);
            return end < 0 ? description : description.Substring(end + ending.Length);
        }

        public static string Apply(string description, QuestProgressInfo progress, Dictionary<string, string> strings, bool richText = true)
        {
            string original = Remove(description ?? "");
            if (progress.Required < 1) return original;
            var text = new StringBuilder(richText ? Begin : PlainBegin);
            if (richText) text.Append("<color=#D6B46A><b>");
            text.Append(strings["Raids"])
                .Append(Math.Max(0, Math.Min(progress.Raids, progress.Required))).Append('/').Append(progress.Required);
            if (richText) text.Append("</b>");
            if (progress.Unlocked) text.Append(strings["Unlocked"]);
            if (richText) text.Append("</color>");
            foreach (var repeat in progress.Repeats)
            {
                if (repeat.Required < 1) continue;
                string name = (repeat.ItemName ?? "").Replace('<', '‹').Replace('>', '›').Replace('\r', ' ').Replace('\n', ' ');
                text.Append("\n").Append(strings["Repeat"]).Append(name).Append(": ")
                    .Append(Math.Max(0, Math.Min(repeat.Raids, repeat.Required))).Append('/').Append(repeat.Required);
            }
            return text.Append(richText ? End : PlainEnd).Append(original).ToString();
        }
    }
}
