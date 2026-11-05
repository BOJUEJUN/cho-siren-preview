import json, base64, io, zipfile, time, sys
from tencentcloud.common import credential
from tencentcloud.scf.v20180416 import scf_client, models
c=json.load(open('/Users/bojuejun/.cho-siren-cos.json'))
env={}
for line in open('/Users/bojuejun/XiaomiMiMoProjects/krea-studio-models/.env.local'):
    line=line.strip()
    if '=' in line and not line.startswith('#'): k,v=line.split('=',1); env[k.strip()]=v.strip().strip('"').strip("'")
cred=credential.Credential(c['secretId'],c['secretKey']); cl=scf_client.ScfClient(cred,'ap-guangzhou')
buf=io.BytesIO()
with zipfile.ZipFile(buf,'w',zipfile.ZIP_DEFLATED) as z: z.write('/Users/bojuejun/cho-siren-unity/Tools/ai-dress-fn/index.py','index.py')
code=base64.b64encode(buf.getvalue()).decode()
import os
variables=[{"Key":"RUNNINGHUB_AI_KEY","Value":open(os.path.expanduser("~/.cho-siren-rh-ai.key")).read().strip()},{"Key":"RUNNINGHUB_API_KEY","Value":env['RUNNINGHUB_API_KEY']},{"Key":"COS_BUCKET","Value":c['bucket']},{"Key":"COS_REGION","Value":c['region']},
   {"Key":"COS_SECRET_ID","Value":c['secretId']},{"Key":"COS_SECRET_KEY","Value":c['secretKey']}]
def status():
    g=models.GetFunctionRequest(); g.FunctionName='cho-siren-ai-dress'
    try: return cl.GetFunction(g).Status
    except Exception: return None
if status() is None:
    r=models.CreateFunctionRequest()
    r.from_json_string(json.dumps({"FunctionName":"cho-siren-ai-dress","Runtime":"Python3.9","Handler":"index.main_handler","Timeout":60,"MemorySize":128,
      "Code":{"ZipFile":code},"Description":"CHO-SIREN 创意工坊 AI 换装","Environment":{"Variables":variables}}))
    print('create', cl.CreateFunction(r).RequestId)
else:
    u=models.UpdateFunctionCodeRequest(); u.from_json_string(json.dumps({"FunctionName":"cho-siren-ai-dress","ZipFile":code,"Handler":"index.main_handler"}))
    print('update code', cl.UpdateFunctionCode(u).RequestId)
    for i in range(40):
        if status()=='Active': break
        time.sleep(3)
    cfg=models.UpdateFunctionConfigurationRequest(); cfg.from_json_string(json.dumps({"FunctionName":"cho-siren-ai-dress","Timeout":60,"Environment":{"Variables":variables}}))
    print('update config', cl.UpdateFunctionConfiguration(cfg).RequestId)
    time.sleep(3)
for i in range(40):
    st=status(); 
    if st in ('Active','CreateFailed','UpdateFailed'): print('status',st); break
    time.sleep(3)
