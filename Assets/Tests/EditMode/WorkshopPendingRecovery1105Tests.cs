using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class WorkshopPendingRecovery1105Tests
    {
        private const string Endpoint = "https://example.invalid";
        private static readonly Type PendingType = typeof(WorkshopAi1104).GetNestedType("Pending", BindingFlags.NonPublic);
        private string directory, path;

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "cho-pending-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "feiyin.json");
        }

        [TearDown] public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static void Set(object job, string field, object value) => PendingType.GetField(field).SetValue(job, value);
        private static object Get(object job, string field) => PendingType.GetField(field).GetValue(job);
        private static object Job(bool submitting = true)
        {
            object job = Activator.CreateInstance(PendingType, true);
            Set(job, "member", "feiyin"); Set(job, "endpoint", Endpoint); Set(job, "submitting", submitting);
            Set(job, "width", 10); Set(job, "height", 20); Set(job, "imageWidth", 20); Set(job, "imageHeight", 40);
            return job;
        }

        private static bool Save(object job, string target) => (bool)typeof(WorkshopAi1104)
            .GetMethod("SavePendingAtPath", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new[] { job, target });

        private object Load(out string error, string endpoint = Endpoint)
        {
            object[] args = { "feiyin", endpoint, path, null };
            object result = typeof(WorkshopAi1104).GetMethod("LoadPendingAtPath", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, args);
            error = (string)args[3]; return result;
        }

        [Test] public void MissingRecordIsTheOnlyNormalFreshStart()
        {
            Assert.That(Load(out string error), Is.Null);
            Assert.That(error, Is.Null);
        }

        [Test] public void SubmissionMarkerSurvivesBeforeAnyTaskIdArrives()
        {
            Assert.That(Save(Job(), path), Is.True);
            object loaded = Load(out string error);
            Assert.That(error, Is.Null);
            Assert.That(Get(loaded, "submitting"), Is.True);
            Assert.That(Get(loaded, "task"), Is.Null.Or.Empty);
        }

        [Test] public void ReceivedTaskAtomicallyReplacesSubmissionMarker()
        {
            object job = Job(); Assert.That(Save(job, path), Is.True);
            Set(job, "task", "o:existing-task"); Set(job, "submitting", false);
            Assert.That(Save(job, path), Is.True);
            object loaded = Load(out string error);
            Assert.That(error, Is.Null);
            Assert.That(Get(loaded, "task"), Is.EqualTo("o:existing-task"));
            Assert.That(Get(loaded, "submitting"), Is.False);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test] public void CorruptRecordBlocksASecondPaidSubmissionAndIsKept()
        {
            const string broken = "{broken record";
            File.WriteAllText(path, broken);
            Assert.That(Load(out string error), Is.Null);
            Assert.That(error, Does.Contain("未重复生成"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(broken));
        }

        [Test] public void ChangedEndpointDoesNotTurnAnExistingJobIntoANewRequest()
        {
            Assert.That(Save(Job(), path), Is.True);
            string before = File.ReadAllText(path);
            Assert.That(Load(out string error, "https://other.invalid"), Is.Null);
            Assert.That(error, Does.Contain("另一生成服务"));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test] public void InterruptedTemporaryRecordIsNotSilentlyIgnored()
        {
            File.WriteAllText(path + ".tmp", "partial");
            Assert.That(Load(out string error), Is.Null);
            Assert.That(error, Does.Contain("未保存完整"));
            Assert.That(File.Exists(path + ".tmp"), Is.True);
        }

        [Test] public void StorageFailureIsReturnedAndExistingDataIsUntouched()
        {
            string blocked = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blocked, "keep this file");
            Assert.That(Save(Job(), Path.Combine(blocked, "feiyin.json")), Is.False);
            Assert.That(File.ReadAllText(blocked), Is.EqualTo("keep this file"));
        }

        [Test] public void MissingRequiredFieldsCannotStartAnotherGeneration()
        {
            File.WriteAllText(path, "{}");
            Assert.That(Load(out string error), Is.Null);
            Assert.That(error, Does.Contain("记录异常"));
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
