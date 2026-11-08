using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChoSiren.Tests
{
    public sealed class TeamSwapPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private ChoSirenApp app;
        private GameModel model;
        private Canvas canvas;
        private Camera camera;
        private RenderTexture target;
        private int standby;
        private bool backedUp, hadSave, hadLegacy;
        private string saved, legacy;
        private readonly List<GameObject> createdSystems = new List<GameObject>();
        private readonly List<GameObject> suspendedApps = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadSave = PlayerPrefs.HasKey(GameModel.SaveKey);
            saved = PlayerPrefs.GetString(GameModel.SaveKey);
            hadLegacy = PlayerPrefs.HasKey(GameModel.LegacySaveKey);
            legacy = PlayerPrefs.GetString(GameModel.LegacySaveKey);
            backedUp = true;
            foreach (ChoSirenApp old in Object.FindObjectsByType<ChoSirenApp>(FindObjectsSortMode.None))
                if (old.gameObject.activeSelf)
                { suspendedApps.Add(old.gameObject); old.gameObject.SetActive(false); }

            // Sign only in this reversible in-memory fixture so the authoritative stable-ID
            // roster and its index projection agree when the real app reloads the save.
            var fresh = new GameModel(() => DateTime.Now, new InMemorySaveStore());
            standby = FindValidStandby(fresh, 1);
            Assert.That(fresh.SignCandidate(standby, 1, out string signingMessage), Is.True, signingMessage);
            fresh.Save.StoryProgress = 100;
            fresh.Save.MusicEnabled = false;
            fresh.Save.SfxEnabled = false;
            PlayerPrefs.SetString(GameModel.SaveKey, JsonUtility.ToJson(fresh.Save));
            PlayerPrefs.Save();
            var systemsBefore = new HashSet<EventSystem>(
                Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None));
            app = new GameObject("Team swap UI fixture").AddComponent<ChoSirenApp>();
            foreach (EventSystem system in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (!systemsBefore.Contains(system)) createdSystems.Add(system.gameObject);
            yield return null;
            model = (GameModel)typeof(ChoSirenApp).GetField("model", Private).GetValue(app);
            Assert.That(model.IsUnlocked(standby), Is.True, "The stable-ID save must retain the signed fixture member.");
            Assert.That(model.IsInTeam(standby), Is.False, "The fixture member must start on standby.");
            canvas = app.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            camera = new GameObject("Team swap raycast camera").AddComponent<Camera>();
            camera.enabled = false;
            target = new RenderTexture(720, 1536, 24);
            target.Create(); camera.targetTexture = target;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 10f; canvas.scaleFactor = 1f;
            Canvas.ForceUpdateCanvases();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Find("StartupLoading") != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Find("StartupLoading"), Is.Null, "The actual loading gate must finish before testing raycasts.");
            Click("Nav-team");
            yield return null;
            Assert.That(Find("TeamReferenceBoard038"), Is.Not.Null);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                if (app != null) Object.Destroy(app.gameObject);
                foreach (GameObject system in createdSystems) if (system != null) Object.Destroy(system);
                yield return null;
            }
            finally
            {
                if (backedUp)
                {
                    if (hadSave) PlayerPrefs.SetString(GameModel.SaveKey, saved); else PlayerPrefs.DeleteKey(GameModel.SaveKey);
                    if (hadLegacy) PlayerPrefs.SetString(GameModel.LegacySaveKey, legacy); else PlayerPrefs.DeleteKey(GameModel.LegacySaveKey);
                    PlayerPrefs.Save();
                }
                foreach (GameObject old in suspendedApps) if (old != null) old.SetActive(true);
                suspendedApps.Clear(); createdSystems.Clear();
                if (camera != null) { camera.targetTexture = null; Object.DestroyImmediate(camera.gameObject); }
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        [UnityTest]
        public IEnumerator WholeInformationBandOpensPickerWhilePortraitKeepsDetailsAndCurrentMemberIsDisabled()
        {
            int[] before = model.Save.Team.ToArray();
            RectTransform band = Require("TeamLabel-0").GetComponent<RectTransform>();
            // 2026-11-07 (团队.psd): the band is the card's bottom strip — still a large, phone-sized target.
            Assert.That(band.rect.width, Is.GreaterThanOrEqualTo(280f));
            Assert.That(band.rect.height, Is.GreaterThanOrEqualTo(80f));
            // Hit the left-hand information area, deliberately far from the right tag.
            Click("TeamLabel-0", new Vector2(.10f, .50f));
            yield return null;
            Assert.That(Find("TeamMemberPicker"), Is.Not.Null);
            GameObject current = Require("PickMember-" + before[0]);
            Assert.That(current.GetComponent<Button>().interactable, Is.False);
            Assert.That(Require("PickerState-" + before[0]).GetComponent<Text>().text, Is.EqualTo("当前成员"));
            Assert.That(current.GetComponent<RectTransform>().rect.height, Is.EqualTo(ChoSirenApp.PickerCardHeight1101).Within(.1f));
            Click(current.name);
            yield return null;
            Assert.That(model.Save.Team, Is.EqualTo(before));
            Assert.That(Find("TeamMemberPicker"), Is.Not.Null);
            Click("CloseProgression");
            yield return null;
            Click("TeamOrbit-0", new Vector2(.50f, .80f));
            yield return null;
            Assert.That(Find("MemberModal"), Is.Not.Null, "Portrait still opens the original member details.");
        }

        [UnityTest]
        public IEnumerator DeployedCandidateSwapsBothSlotsAndPersistsTheNewCaptain()
        {
            int[] before = model.Save.Team.ToArray();
            Click("TeamLabel-0"); yield return null;
            Assert.That(Require("PickerHelp").GetComponent<Text>().text, Does.Contain("新成员将成为队长"));
            Assert.That(Require("PickerState-" + before[1]).GetComponent<Text>().text, Is.EqualTo("交换位置"));
            Click("PickMember-" + before[1]); yield return null;
            Assert.That(model.Save.Team[0], Is.EqualTo(before[1]));
            Assert.That(model.Save.Team[1], Is.EqualTo(before[0]));
            Assert.That(model.Save.Team.Distinct().Count(), Is.EqualTo(before.Length));
            Assert.That(new GameModel().Save.Team, Is.EqualTo(model.Save.Team), "The model persists the swap.");
            Assert.That(Require("TeamPowerValue").GetComponent<Text>().text, Is.EqualTo(model.TeamPower.ToString("N0")));
        }

        [UnityTest]
        public IEnumerator StandbyCandidateReplacesTheSlotAndKeepsBothMembersAndTheirEquipment()
        {
            int previous = model.Save.Team[1];
            int previousEquipment = model.EquippedAccessoryFor(previous);
            int standbyEquipment = model.EquippedAccessoryFor(standby);
            int count = model.Save.Team.Count;
            Click("TeamLabel-1"); yield return null;
            Assert.That(Require("PickerState-" + standby).GetComponent<Text>().text, Is.EqualTo("替换上阵"));
            Click("PickMember-" + standby); yield return null;
            Assert.That(model.Save.Team[1], Is.EqualTo(standby));
            Assert.That(model.Save.Team, Has.No.Member(previous));
            Assert.That(model.IsUnlocked(previous) && model.IsUnlocked(standby), Is.True);
            Assert.That(model.Save.Team.Count, Is.EqualTo(count));
            Assert.That(model.Save.Team.Distinct().Count(), Is.EqualTo(count));
            Assert.That(model.EquippedAccessoryFor(previous), Is.EqualTo(previousEquipment));
            Assert.That(model.EquippedAccessoryFor(standby), Is.EqualTo(standbyEquipment));
            Assert.That(new GameModel().Save.Team, Is.EqualTo(model.Save.Team));
            Assert.That(Require("TeamPowerValue").GetComponent<Text>().text, Is.EqualTo(model.TeamPower.ToString("N0")));
        }

        [UnityTest]
        public IEnumerator AllFilterAllowsAnonymousLockedProfileWhileSearchNeverRevealsSignsOrReplacesTheMember()
        {
            int[] before = model.Save.Team.ToArray();
            int[] ownedBefore = model.Save.UnlockedMembers.ToArray();
            int locked = Enumerable.Range(0, GameModel.Members.Length).First(i => !model.IsUnlocked(i));
            Click("TeamLabel-1"); yield return null;
            Click("PickerOwnership"); yield return null;
            InputField search = Require("PickerSearch").GetComponent<InputField>();
            search.text = GameModel.Members[locked].Name;
            search.onEndEdit.Invoke(search.text);
            yield return null;
            Assert.That(Find("PickMember-" + locked), Is.Null,
                "Searching a real name must never reveal an unsigned member's identity.");
            Assert.That(Find("PickerEmpty"), Is.Not.Null);
            Assert.That(model.Save.Team, Is.EqualTo(before));
            Assert.That(model.Save.UnlockedMembers, Is.EqualTo(ownedBefore));

            search = Require("PickerSearch").GetComponent<InputField>();
            search.text = GameModel.Members[standby].Name;
            search.onEndEdit.Invoke(search.text);
            yield return null;
            Assert.That(Require("PickerName-" + standby).GetComponent<Text>().text,
                Is.EqualTo(GameModel.Members[standby].Name), "Owned members remain searchable.");
            Assert.That(Require("PickerState-" + standby).GetComponent<Text>().text, Is.EqualTo("替换上阵"));

            search = Require("PickerSearch").GetComponent<InputField>();
            search.text = string.Empty;
            search.onEndEdit.Invoke(search.text);
            yield return null;
            // All includes anonymous locked rows after the five owned members, so reach
            // the actual page through its public pager rather than assuming page one.
            for (int page = 0; Find("PickMember-" + locked) == null && page < GameModel.Members.Length; page++)
            {
                Assert.That(Require("PickerNext").GetComponent<Button>().interactable, Is.True,
                    "The all-members picker must expose the locked member on a reachable page.");
                Click("PickerNext");
                yield return null;
            }
            Assert.That(Require("PickerName-" + locked).GetComponent<Text>().text, Is.EqualTo("未签约成员"));
            Assert.That(Require("PickerState-" + locked).GetComponent<Text>().text, Is.EqualTo("仅查看"));
            Assert.That(Require("PickerActionHint-" + locked).GetComponent<Text>().text, Is.EqualTo("未签约，仅可查看档案"));
            Click("PickMember-" + locked); yield return null;
            Assert.That(Find("MemberModal"), Is.Not.Null);
            Assert.That(model.Save.Team, Is.EqualTo(before));
            Assert.That(model.Save.UnlockedMembers, Is.EqualTo(ownedBefore));
            Assert.That(model.IsUnlocked(locked), Is.False);
            Assert.That(new GameModel().Save.Team, Is.EqualTo(before));
        }

        private static int FindValidStandby(GameModel fresh, int slot)
        {
            for (int candidate = 0; candidate < GameModel.Members.Length; candidate++)
            {
                if (fresh.Save.Team.Contains(candidate) || fresh.IsUnlocked(candidate)) continue;
                var probe = new GameModel(() => DateTime.Now, new InMemorySaveStore());
                if (!probe.SignCandidate(candidate, 1, out _)) continue;
                if (probe.ReplaceTeamSlot(slot, candidate, out _)) return candidate;
            }
            Assert.Fail("The real roster must contain a valid standby fixture candidate.");
            return -1;
        }

        private GameObject Find(string name) => app == null ? null : app.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == name && t.gameObject.activeInHierarchy).Select(t => t.gameObject).FirstOrDefault();

        private GameObject Require(string name)
        {
            GameObject value = Find(name); Assert.That(value, Is.Not.Null, name); return value;
        }

        private void Click(string name, Vector2? normalizedPoint = null)
        {
            GameObject button = Require(name);
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 uv = normalizedPoint ?? new Vector2(.5f, .5f);
            Vector3 local = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, uv.x),
                Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, uv.y), 0f);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(local));
            var pointer = new PointerEventData(EventSystem.current)
                { position = point, button = PointerEventData.InputButton.Left, clickCount = 1 };
            var hits = new List<RaycastResult>();
            canvas.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), name + " must be reached by a real UI raycast.");
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button), name + " must win the raycast over overlays or its parent.");
            Assert.That(ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.Not.Null);
        }
    }
}
