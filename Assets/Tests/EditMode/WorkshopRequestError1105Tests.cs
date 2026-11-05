using NUnit.Framework;

namespace ChoSiren.Tests
{
    public sealed class WorkshopRequestError1105Tests
    {
        [Test]
        public void LostPostResponseWarnsThatItMayAlreadyBeAccepted()
        {
            string message = WorkshopRequestError1105.Message(0, "", "Request timeout", true, out bool retryable);
            Assert.That(message, Does.Contain("超时").And.Contain("可能已提交").And.Contain("请勿重复生成"));
            Assert.That(retryable, Is.False, "An automatic POST retry can create a second paid generation.");
        }

        [Test]
        public void PollTransportFailureCanResumeWithoutCreatingAnotherTask()
        {
            string message = WorkshopRequestError1105.Message(0, null, "Cannot resolve host", false, out bool retryable);
            Assert.That(message, Does.Contain("正在重试"));
            Assert.That(retryable, Is.True);
        }

        [TestCase(400, "请求不完整")]
        [TestCase(403, "未授权")]
        [TestCase(404, "任务不存在")]
        public void DefinitiveClientErrorsDoNotLoopForever(long status, string expected)
        {
            string message = WorkshopRequestError1105.Message(status, "<html>gateway error</html>", "", false, out bool retryable);
            Assert.That(message, Does.Contain(expected));
            Assert.That(retryable, Is.False);
        }

        [Test]
        public void OversizeHtmlReplyIsAnUploadSizeError()
        {
            string message = WorkshopRequestError1105.Message(413, "<html>Request Entity Too Large</html>", "", true, out bool retryable);
            Assert.That(message, Does.Contain("立绘上传过大"));
            Assert.That(retryable, Is.False);
        }

        [Test]
        public void DailyQuotaJsonRemainsDistinctFromNetworkFailure()
        {
            string message = WorkshopRequestError1105.Message(429, "{\"error\":\"今天的 AI 换装次数用完了（每天 5 次）\"}", "", false, out bool retryable);
            Assert.That(message, Does.Contain("次数用完"));
            Assert.That(message, Does.Not.Contain("网络"));
            Assert.That(retryable, Is.False);
        }

        [Test]
        public void BusyGatewayOnlyRetriesQueries()
        {
            string post = WorkshopRequestError1105.Message(502, "<html>Bad Gateway</html>", "", true, out bool postRetry);
            string get = WorkshopRequestError1105.Message(502, "<html>Bad Gateway</html>", "", false, out bool getRetry);
            Assert.That(post, Does.Contain("可能已提交"));
            Assert.That(postRetry, Is.False);
            Assert.That(get, Does.Contain("正在重试"));
            Assert.That(getRetry, Is.True);
        }

        [Test]
        public void InvalidJsonReplyDoesNotEscapeAsAnException()
        {
            string message = WorkshopRequestError1105.Message(200, "{not json}", "", true, out bool retryable);
            Assert.That(message, Does.Contain("返回异常"));
            Assert.That(retryable, Is.False);
        }

        [Test]
        public void ServerAndTransportSecretsNeverReachThePlayer()
        {
            const string secret = "sk-private-fixture-only-123456";
            const string url = "https://private.example/task?token=hidden";
            string message = WorkshopRequestError1105.Message(502, "{\"error\":\"" + secret + " " + url + "\"}", url, true, out _);
            Assert.That(message, Does.Not.Contain(secret).And.Not.Contain(url).And.Not.Contain("hidden"));
        }
    }
}
