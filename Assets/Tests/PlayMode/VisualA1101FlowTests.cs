using System.Collections;
using System.Linq;
using System.Reflection;
using ChoSiren.Systems.Economy;
using ChoSiren.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ChoSiren.Tests
{
    /// <summary>
    /// 2026-11-02 (visual-a, 赵金 1101c): real click chains through the rebuilt low-text pages. The older smoke tests
    /// that cover these flows stop in SetUp (they wait for the retired LobbyCards object), so they say nothing about
    /// them; these open each page on a live app and click the same buttons a player does.
    /// </summary>
    public sealed class VisualA1101FlowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string AgreementKey = "cho-agreement-v1";
        private ChoSirenApp app;
        private GameModel model;
        private string save, legacy;
        private bool had, hadLegacy, hadAgreement;
        private int agreement;

        [UnitySetUp] public IEnumerator SetUp()
        {
            had = PlayerPrefs.HasKey(GameModel.SaveKey); save = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey); legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            hadAgreement = PlayerPrefs.HasKey(AgreementKey); agreement = PlayerPrefs.GetInt(AgreementKey, 0);
            LogAssert.ignoreFailingMessages = true;
            var data = new GameSave { HomeCharacterIndex = 0, StoryProgress = 100, PlayerName = "经纪人" };
            data.CompletedStories.Add(GameModel.PrologueStoryId);
            data.ClearedStages.Add(new StageClear { Id = "stage-1-1", Stars = 3 });
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(data));
            foreach (var old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            yield return null;
            app = new GameObject("VisualA1101Flow").AddComponent<ChoSirenApp>();
            yield return null;
            model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
            Call("ShowScreenSafely", "lobby");
            yield return null;
            Call("CloseModal");
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            foreach (var old in Object.FindObjectsByType<NativeTitleScreen>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            if (app != null) Object.Destroy(app.gameObject);
            yield return null;
            if (had) PlayerPrefs.SetString(GameModel.SaveKey, save); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
            if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
            if (hadAgreement) PlayerPrefs.SetInt(AgreementKey, agreement); else PlayerPrefs.DeleteKey(AgreementKey);
            PlayerPrefs.Save();
        }

        [UnityTest] public IEnumerator ResetAsksFirstCancelKeepsTheSaveAndConfirmClearsIt()
        {
            Call("OpenSettings"); yield return null;
            Click("Reset"); yield return null;
            Require("InfoModal");
            Assert.That(model.Save.ClearedStages, Is.Not.Empty, "打开确认卡不能清档");
            Click("Cancel"); yield return null;
            Require("SettingsModal");
            Assert.That(model.Save.ClearedStages, Is.Not.Empty, "取消不能清档");
            Click("Reset"); yield return null;
            Click("Close"); yield return null;
            Assert.That(Find("InfoModal"), Is.Null);
            Assert.That(model.Save.ClearedStages, Is.Not.Empty, "关闭不能清档");
            Call("OpenSettings"); yield return null;
            Click("Reset"); yield return null;
            Click("Primary"); yield return null;
            Assert.That(model.Save.ClearedStages, Is.Empty, "确认后才清档");
        }

        [UnityTest] public IEnumerator GoldExchangeCancelIsFreeAndConfirmChargesExactlyOnce()
        {
            int gold = model.Save.Gold, diamonds = model.Save.Diamonds;
            Assert.That(diamonds, Is.GreaterThanOrEqualTo(GameModel.GoldExchangeDiamondCost));
            Call("OpenCurrency", CurrencyIds.Gold); yield return null;
            Click("CurrencyExchange"); yield return null;
            Require("CurrencyConfirmation");
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds), "打开确认卡不能扣费");
            Click("CancelExchange"); yield return null;
            Require("CurrencyModal");
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds), "取消不能扣费");
            Click("CurrencyExchange"); yield return null;
            Button submit = Require("ConfirmExchange").GetComponent<Button>();
            Click("ConfirmExchange");
            submit.onClick.Invoke(); // a queued duplicate must not charge twice
            yield return null;
            Assert.That(model.Save.Gold, Is.EqualTo(gold + GameModel.GoldExchangeAmount));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds - GameModel.GoldExchangeDiamondCost));
            Assert.That(Require("CurrencyBalance").GetComponent<Text>().text, Does.Contain(model.Save.Gold.ToString("N0")));
        }

        [UnityTest] public IEnumerator StaminaRefillPaysOnceAndAFullGaugeCannotBuy()
        {
            model.Save.Stamina = 40;
            int diamonds = model.Save.Diamonds;
            int amount = Mathf.Min(GameModel.StaminaRefillMaximum, model.StaminaCap - 40);
            Call("OpenCurrency", CurrencyIds.Stamina); yield return null;
            Assert.That(Require("CurrencyBalance").GetComponent<Text>().text, Does.Contain("40"));
            Click("CurrencyExchange"); yield return null;
            Click("ConfirmExchange"); yield return null;
            Assert.That(model.Save.Stamina, Is.EqualTo(40 + amount));
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds - amount));
            model.Save.Stamina = model.StaminaCap;
            Call("OpenCurrency", CurrencyIds.Stamina); yield return null;
            Assert.That(Require("CurrencyExchange").GetComponent<Button>().interactable, Is.False, "体力满时不能购买");
        }

        [UnityTest] public IEnumerator CurrencyHelpMovedBehindTheQuestionMarkAndCloses()
        {
            Call("OpenCurrency", CurrencyIds.Gold); yield return null;
            Assert.That(Find("HelpBubble"), Is.Null);
            Click("CurrencyHelpButton"); yield return null;
            GameObject bubble = Require("HelpBubble");
            Assert.That(bubble.GetComponentsInChildren<Text>().Any(t => t.text.Contains("训练成员")), Is.True);
            Click("HelpBubble"); yield return null;
            Assert.That(Find("HelpBubble"), Is.Null);
            Require("CurrencyModal");
        }

        [UnityTest] public IEnumerator MailOpensALetterAndClaimsOnceFromEitherPlace()
        {
            int diamonds = model.Save.Diamonds;
            Call("OpenInbox"); yield return null;
            Click("Mail-idle-stage"); yield return null;
            Assert.That(Require("MailBody-idle-stage").GetComponent<Text>().text, Does.Contain("闪耀舞台"), "无附件邮件要能读到正文");
            Click("MailBack"); yield return null;
            Require("MailClaim-welcome-2026");
            Click("Mail-welcome-2026"); yield return null;
            Require("MailBody-welcome-2026");
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds), "打开邮件不能领取");
            Click("MailClaim-welcome-2026"); yield return null;
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + 150));
            Assert.That(Find("MailClaim-welcome-2026"), Is.Null, "领完不再有领取按钮");
            Require("MailClaimed");
        }

        [UnityTest] public IEnumerator SignInClaimsTodayOnceAndStampsTheDay()
        {
            Call("OpenSignIn"); yield return null;
            Assert.That(model.SignedToday, Is.False);
            Click("SignInClaim"); yield return null;
            Assert.That(model.SignedToday, Is.True);
            Assert.That(Require("SignInClaim").GetComponent<Button>().interactable, Is.False);
            Assert.That(Require("SignInDay-1").transform.Find("Stamp"), Is.Not.Null);
        }

        [UnityTest] public IEnumerator AchievementClaimPaysAndOnlyFinishedOnesOfferIt()
        {
            int diamonds = model.Save.Diamonds;
            Call("OpenAchievements"); yield return null;
            Transform first = Require("Achievement-first-live").transform;
            Assert.That(first.Find("Claim"), Is.Not.Null);
            Assert.That(Require("Achievement-chapter-two").transform.Find("Claim"), Is.Null, "未完成的成就没有领取按钮");
            first.Find("Claim").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(model.Save.Diamonds, Is.EqualTo(diamonds + 50));
            Assert.That(Require("Achievement-first-live").transform.Find("Claim"), Is.Null);
        }

        [UnityTest] public IEnumerator ProfileRoutesStillReachRenameTeamMembersAndAchievements()
        {
            Call("OpenProfile"); yield return null;
            Click("ProfileRename"); yield return null;
            Require("PlayerContract1028");
            Call("OpenProfile"); yield return null;
            Click("ProfileAchievements"); yield return null;
            Require("AchievementModal");
            Call("OpenProfile"); yield return null;
            Click("ProfileTeam"); yield return null;
            Assert.That(Find("ProfileModal"), Is.Null);
            Call("OpenProfile"); yield return null;
            Click("ProfileMembers"); yield return null;
            Assert.That(Find("ProfileModal"), Is.Null);
        }

        [UnityTest] public IEnumerator SettingsToggleQualityAndMutedSliderSwapTheIcon()
        {
            Call("OpenSettings"); yield return null;
            int quality = model.Save.QualityLevel;
            Click("Toggle"); yield return null;
            Assert.That(model.Save.QualityLevel, Is.Not.EqualTo(quality));
            Require("SettingsModal");
            Slider slider = Require("Setting-音乐").GetComponentInChildren<Slider>();
            Image glyph = Require("Setting-音乐").transform.Find("SettingIcon/Glyph").GetComponent<Image>();
            slider.value = 0;
            Assert.That(glyph.sprite, Is.SameAs(VisualA1101.Icon("music")), "音乐静音图标沿用音符（变灰）");
            Assert.That(glyph.color, Is.EqualTo(VisualA1101.Muted));
            Assert.That(model.Save.MusicEnabled, Is.False);
            slider.value = 60;
            Assert.That(glyph.color, Is.EqualTo(VisualA1101.Teal));
            Assert.That(model.Save.MusicEnabled, Is.True);
        }

        [UnityTest] public IEnumerator CaptainConfirmShowsFacesAndAppointsOnlyOnConfirm()
        {
            // Make sure someone is on the bench, whatever the default roster is.
            int bench = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.Save.Team.Contains(i));
            if (!model.Save.UnlockedMembers.Contains(bench)) model.Save.UnlockedMembers.Add(bench);
            int captain = model.Save.Team[0];
            Call("ConfirmCaptain", bench); yield return null;
            Require("CaptainConfirmation");
            Require("ConfirmFaceTo");
            Assert.That(model.Save.Team[0], Is.EqualTo(captain), "打开确认卡不能换队长");
            Click("ConfirmCaptain"); yield return null;
            Assert.That(model.Save.Team[0], Is.EqualTo(bench));
        }

        [UnityTest] public IEnumerator AgreementKeepsLegalWordingAndOnlyAgreeUnlocks()
        {
            PlayerPrefs.DeleteKey(AgreementKey);
            var safeRoot = (Transform)typeof(ChoSirenApp).GetField("safeRoot", Private).GetValue(app);
            NativeTitleScreen title = NativeTitleScreen.Create(safeRoot.parent, (RectTransform)safeRoot, false);
            yield return null;
            string notice = Require("Notice").GetComponent<Text>().text;
            Assert.That(notice, Does.Contain("《用户协议》"));
            Assert.That(notice, Does.Contain("不收集姓名、手机号"));
            Assert.That(notice, Does.Contain("16 周岁"));
            Click("Decline"); yield return null;
            Assert.That(PlayerPrefs.GetInt(AgreementKey, 0), Is.Zero, "不同意不能写入同意记录");
            Require("AgreementDialog");
            Click("Agree"); yield return null;
            Assert.That(PlayerPrefs.GetInt(AgreementKey, 0), Is.EqualTo(1));
            Assert.That(title.AgreementOpen, Is.False);
        }

        private void Call(string method, params object[] args)
        {
            MethodInfo info = typeof(ChoSirenApp).GetMethods(Private)
                .First(m => m.Name == method && m.GetParameters().Length == args.Length);
            info.Invoke(app, args);
        }

        private static GameObject Find(string name)
        {
            foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect.gameObject;
            return null;
        }

        private static GameObject Require(string name)
        {
            GameObject found = Find(name);
            Assert.That(found, Is.Not.Null, $"找不到激活的界面节点：{name}");
            return found;
        }

        /// <summary>Dispatches a real pointer click, so a disabled button or a missing handler fails here.</summary>
        private static void Click(string name)
        {
            GameObject target = Require(name);
            Button button = target.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name + " 不是按钮");
            Assert.That(button.IsInteractable(), Is.True, name + " 不可点击");
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
    }
}
