using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace ChoSiren.Tests
{
    public sealed class WorkshopTransport1105Tests
    {
        [Serializable] private sealed class Recovered { public Item[] tasks; }
        [Serializable] private sealed class Item { public string task, visuallyIdentifiedMember; }
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;

        // Opt-in, non-billable: an invalid POST is rejected before model submission; all other calls GET existing jobs.
        [UnityTest, Timeout(600000)]
        public IEnumerator ExistingJobsDownloadAndInvalidUploadReturnsARealHttpError()
        {
            string manifest = Environment.GetEnvironmentVariable("WORKSHOP_RECOVERY_MANIFEST");
            if (string.IsNullOrEmpty(manifest) || !File.Exists(manifest)) Assert.Ignore("No existing-task fixture");
            string keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cho-siren-gpt-test.code");
            string code = File.ReadAllText(keyPath).Trim();
            Type type = typeof(WorkshopAi1104);
            var codeField = type.GetField("testCode", Hidden);
            var loadedField = type.GetField("testCodeLoaded", Hidden);
            object oldCode = codeField.GetValue(null), oldLoaded = loadedField.GetValue(null);
            string output = Path.GetFullPath("Builds/qa-logs/workshop-review-1105");
            Directory.CreateDirectory(output);
            try
            {
                codeField.SetValue(null, code); loadedField.SetValue(null, true);
                using (var upload = new UnityWebRequest(WorkshopAi1104.GptTestEndpoint + "/dress", "POST"))
                {
                    // Empty prompt guarantees the service returns 400 before contacting the provider.
                    string body = "{\"device\":\"nonbillable-transport-probe\",\"prompt\":\"\",\"image\":\"" + new string('A', 1400000) + "\"}";
                    upload.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
                    upload.downloadHandler = new DownloadHandlerBuffer(); upload.timeout = 90;
                    upload.useHttpContinue = false;
                    upload.SetRequestHeader("Content-Type", "application/json");
                    upload.SetRequestHeader("X-Test-Code", code);
                    yield return upload.SendWebRequest();
                    Assert.That(upload.responseCode, Is.EqualTo(400), "invalid upload is rejected without a generation");
                    Assert.That(upload.downloadHandler.text, Does.Contain("请求不完整"));
                    File.WriteAllText(Path.Combine(output, "nonbillable-upload.json"), "{\"http\":400,\"newGeneration\":false}");
                }
                Type pendingType = type.GetNestedType("Pending", BindingFlags.NonPublic);
                MethodInfo save = type.GetMethod("SavePending", Hidden);
                MethodInfo pathMethod = type.GetMethod("PendingPath", Hidden);
                foreach (Item item in JsonUtility.FromJson<Recovered>(File.ReadAllText(manifest)).tasks)
                {
                    string member = item.visuallyIdentifiedMember;
                    int index = GameModel.MemberIndexFor1101(member);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0));
                    Sprite still = Resources.Load<Sprite>(GameModel.Members[index].ResourcePath);
                    WorkshopAi1104.EncodeStill(still, 1024, out RectInt figure, out Vector2Int size);
                    object pending = Activator.CreateInstance(pendingType, true);
                    void Set(string name, object value) => pendingType.GetField(name).SetValue(pending, value);
                    Set("member", member); Set("task", item.task); Set("endpoint", WorkshopAi1104.GptTestEndpoint);
                    Set("x", figure.x); Set("y", figure.y); Set("width", figure.width); Set("height", figure.height);
                    Set("imageWidth", size.x); Set("imageHeight", size.y);
                    string path = (string)pathMethod.Invoke(null, new object[] { member });
                    byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : null;
                    try
                    {
                        Assert.That((bool)save.Invoke(null, new[] { pending }), Is.True);
                        File.WriteAllText(Path.Combine(output, "phone-recovery-" + member + ".json"), JsonUtility.ToJson(pending));
                        Texture2D result = null; string error = null;
                        yield return WorkshopAi1104.Generate(member, still, "", _ => { }, (image, message) => { result = image; error = message; });
                        Assert.That(error, Is.Null, "existing-task retrieval succeeds");
                        Assert.That(result, Is.Not.Null, "the existing image decodes, keys and fits the original still");
                        File.WriteAllBytes(Path.Combine(output, "recovered-" + member + ".png"), result.EncodeToPNG());
                        UnityEngine.Object.Destroy(result);
                    }
                    finally
                    {
                        if (before == null) { if (File.Exists(path)) File.Delete(path); }
                        else File.WriteAllBytes(path, before);
                    }
                }
                File.WriteAllText(Path.Combine(output, "existing-task-downloads.json"), "{\"passed\":true,\"newGeneration\":false,\"diamondsCharged\":false}");
            }
            finally { codeField.SetValue(null, oldCode); loadedField.SetValue(null, oldLoaded); }
        }
    }
}
