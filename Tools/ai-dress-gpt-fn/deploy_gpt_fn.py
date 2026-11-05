"""Deploy the GPT 换装 test function to Tencent Cloud ap-singapore (keys read from local files only, never printed).

Run with a Python that has tencentcloud-sdk-python-scf:  python -I deploy_gpt_fn.py
Reads ~/.cho-siren-cos.json (Tencent credentials), ~/.openai.key and ~/.cho-siren-gpt-test.code.
"""
import base64, io, json, os, sys, time, zipfile
from tencentcloud.common import credential
from tencentcloud.scf.v20180416 import scf_client, models

NAME, REGION = "cho-siren-ai-dress-gpt", "ap-singapore"
HERE = os.path.dirname(os.path.abspath(__file__))
home = os.path.expanduser
c = json.load(open(home("~/.cho-siren-cos.json")))
cl = scf_client.ScfClient(credential.Credential(c["secretId"], c["secretKey"]), REGION)
buf = io.BytesIO()
with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(os.path.join(HERE, "index.py"), "index.py")
code = base64.b64encode(buf.getvalue()).decode()
variables = [{"Key": "OPENAI_API_KEY", "Value": open(home("~/.openai.key")).read().strip()},
             {"Key": "TEST_CODE", "Value": open(home("~/.cho-siren-gpt-test.code")).read().strip()},
             {"Key": "PUBLIC_URL", "Value": "https://1318245308-m2ju450cfv.ap-singapore.tencentscf.com"}]


def call(name, payload):
    request = getattr(models, name + "Request")()
    request.from_json_string(json.dumps(payload))
    return json.loads(getattr(cl, name)(request).to_json_string())


def status():
    try:
        return call("GetFunction", {"FunctionName": NAME}).get("Status")
    except Exception:
        return None


def wait():
    for _ in range(60):
        st = status()
        if st in ("Active", "CreateFailed", "UpdateFailed"):
            return st
        time.sleep(3)


if status() is None:
    call("CreateFunction", {"FunctionName": NAME, "Runtime": "Python3.9", "Handler": "index.main_handler",
                            "Timeout": 30, "MemorySize": 256, "Code": {"ZipFile": code},
                            "Description": "CHO-SIREN GPT 换装 personal test", "Environment": {"Variables": variables}})
    print("created", wait())
else:
    call("UpdateFunctionCode", {"FunctionName": NAME, "ZipFile": code, "Handler": "index.main_handler"})
    wait()
    call("UpdateFunctionConfiguration", {"FunctionName": NAME, "Timeout": 30, "MemorySize": 256,
                                         "Environment": {"Variables": variables}})
    print("updated", wait())

triggers = call("ListTriggers", {"FunctionName": NAME}).get("Triggers") or []
http = [t for t in triggers if t["Type"] == "http"]
if not http:
    call("CreateTrigger", {"FunctionName": NAME, "TriggerName": "url", "Type": "http",
                           "TriggerDesc": json.dumps({"AuthType": "NONE", "NetConfig": {
                               "EnableIntranet": False, "EnableExtranet": True}})})
    http = [t for t in call("ListTriggers", {"FunctionName": NAME})["Triggers"] if t["Type"] == "http"]
print("url", json.loads(http[0]["TriggerDesc"])["NetConfig"]["ExtranetUrl"])
