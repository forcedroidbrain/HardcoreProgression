using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BepInEx;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using HardcoreProgression.Display;
using Newtonsoft.Json;
using SPT.Common.Http;
using SPT.Reflection.Patching;
using TMPro;

namespace HardcoreProgression.Client
{
    [BepInPlugin("com.forcedroidbrain.hardcoreprogression", "forcedroidbrain-HardcoreProgression", "2.0.0")]
    [BepInDependency("com.SPT.core", "4.1.0")]
    public sealed class QuestProgressPlugin : BaseUnityPlugin
    {
        private const string Route = "/hardcore-progression/quest-progress";
        private static QuestProgressPlugin? instance;
        private readonly ConditionalWeakTable<TMP_Text, ViewRequest> requests = new ConditionalWeakTable<TMP_Text, ViewRequest>();
        private bool warned;

        private sealed class ViewRequest { }

        private void Awake()
        {
            instance = this;
            new FullDescriptionPatch().Enable();
            new ShortDescriptionPatch().Enable();
            Logger.LogInfo("Hardcore quest raid counters enabled for trader and task descriptions.");
        }

        public static void Refresh(TMP_Text text, Quest? quest)
        {
            if (instance == null || text == null) return;
            // A new binding invalidates any response belonging to a previous quest/profile shown in this reused panel.
            instance.requests.Remove(text);
            var request = new ViewRequest();
            instance.requests.Add(text, request);
            if (quest == null || quest.QuestStatus != EQuestStatus.Started) return;
            instance.StartCoroutine(instance.RefreshDescription(text, quest, request));
        }

        private IEnumerator RefreshDescription(TMP_Text text, Quest quest, ViewRequest request)
        {
            string expectedDescription = DescriptionBanner.Remove(text.text);
            Task<string>? task = null;
            try { task = RequestHandler.GetJsonAsync(Route); }
            catch (Exception ex) { Warn(ex.Message); }
            if (task == null) yield break;
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) { Warn(task.Exception?.GetBaseException().Message ?? "Request failed"); yield break; }
            if (task.IsCanceled || text == null || !text.isActiveAndEnabled || quest.QuestStatus != EQuestStatus.Started) yield break;
            if (!requests.TryGetValue(text, out var current) || !ReferenceEquals(current, request)) yield break;
            if (!string.Equals(DescriptionBanner.Remove(text.text), expectedDescription, StringComparison.Ordinal)) yield break;
            QuestProgressResponse? response = null;
            try { response = JsonConvert.DeserializeObject<QuestProgressResponse>(task.Result); }
            catch (Exception ex) { Warn(ex.Message); }
            if (response == null || !response.Available || !response.Quests.TryGetValue(quest.Id, out var progress)) yield break;
            string language = (LocalizationManager.Instance?.Culture ?? "en").Split('-')[0];
            if (!response.Locales.TryGetValue(language, out var strings) && !response.Locales.TryGetValue("en", out strings)) yield break;
            foreach (var repeat in progress.Repeats)
                if (!string.IsNullOrEmpty(repeat.ItemTemplate))
                    repeat.ItemName = (repeat.ItemTemplate + " Name").Localized(null);
            // The task-list panel disables rich text; preserve the panel's own rendering mode.
            text.text = DescriptionBanner.Apply(text.text, progress, strings, text.richText);
            warned = false;
        }

        private void Warn(string message)
        {
            if (warned) return;
            warned = true;
            Logger.LogWarning("Could not update hardcore raid counter; original quest description retained: " + message);
        }
    }

    public sealed class FullDescriptionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(NotesTaskDescription).GetMethod(nameof(NotesTaskDescription.Show), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(NotesTaskDescription).FullName, nameof(NotesTaskDescription.Show));
        [PatchPostfix]
        private static void Postfix(NotesTaskDescription __instance, Quest quest) => QuestProgressPlugin.Refresh(__instance._description, quest);
    }

    public sealed class ShortDescriptionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => (typeof(NotesTaskDescriptionShort).GetMethod(nameof(NotesTaskDescriptionShort.ShowDescription), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(typeof(NotesTaskDescriptionShort).FullName, nameof(NotesTaskDescriptionShort.ShowDescription))).MakeGenericMethod(typeof(Quest));
        [PatchPostfix]
        private static void Postfix(NotesTaskDescriptionShort __instance, object conditional)
        {
            QuestProgressPlugin.Refresh(__instance._description, conditional as Quest);
        }
    }
}
