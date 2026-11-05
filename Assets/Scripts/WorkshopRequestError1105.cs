using System;
using UnityEngine;

namespace ChoSiren
{
    /// <summary>Friendly transport/protocol errors. A lost POST response never proves that generation was not accepted.</summary>
    public static class WorkshopRequestError1105
    {
        [Serializable] private sealed class ServiceError { public string error; }

        /// <summary>Call for a failed request or malformed reply. No raw response, URL, key or transport detail is returned.</summary>
        public static string Message(long status, string body, string transportError, bool submitting, out bool retryable)
        {
            retryable = false;
            string serviceError = ReadError(body);

            if (status == 403 || Contains(serviceError, "未授权"))
                return "测试服务未授权，请更新测试版";
            if (status == 404)
                return submitting ? "生成服务地址不可用，请稍后再试" : "生成任务不存在，请重新选择造型";
            if (status == 400 || Contains(serviceError, "请求不完整"))
                return "生成请求不完整，请重新选择造型";
            if (status == 413 || Contains(serviceError, "立绘太大"))
                return "立绘上传过大，请更新客户端后再试";
            if (Contains(serviceError, "次数用完") || Contains(serviceError, "daily limit"))
                return "今天的 AI 换装次数用完了";
            if (status == 429)
            {
                retryable = !submitting;
                return submitting ? "请求太频繁，请稍后再试" : "查询过于频繁，正在稍后重试";
            }
            if (Contains(serviceError, "审核") || Contains(serviceError, "不合适") || Contains(serviceError, "moderation"))
                return "这套造型未通过审核，请换个风格";
            if (Contains(serviceError, "任务不存在"))
                return "生成任务不存在，请重新选择造型";

            bool timeout = Contains(transportError, "timeout") || Contains(transportError, "timed out")
                || Contains(transportError, "超时") || status == 408 || status == 504;
            if (timeout)
            {
                retryable = !submitting;
                return submitting ? "提交响应超时，可能已提交，请勿重复生成" : "查询超时，正在重试";
            }
            if (status <= 0 || status >= 500)
            {
                retryable = !submitting;
                return submitting ? "服务连接中断，可能已提交，请勿重复生成" : "暂时连不上生成服务，正在重试";
            }
            if (status >= 400)
                return "生成请求未被接受，请稍后再试";
            if (Contains(serviceError, "生成失败"))
                return "这套造型未能生成，请换个风格";

            retryable = !submitting;
            return submitting ? "服务返回异常，可能已提交，请勿重复生成" : "查询返回异常，正在重试";
        }

        private static bool Contains(string value, string part) =>
            !string.IsNullOrEmpty(value) && value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string ReadError(string body)
        {
            if (string.IsNullOrWhiteSpace(body) || body.Length > 16384 || !body.TrimStart().StartsWith("{"))
                return null;
            try { return JsonUtility.FromJson<ServiceError>(body)?.error; }
            catch { return null; }
        }
    }
}
