import json,sys,time,urllib.request
p=json.load(open(sys.argv[1]))
r=urllib.request.Request('http://127.0.0.1:8188/prompt',json.dumps({'prompt':p}).encode(),{'Content-Type':'application/json'})
try: res=json.load(urllib.request.urlopen(r))
except urllib.error.HTTPError as e: print(e.read().decode()[:3000]); sys.exit(1)
print(res)
if res.get('node_errors'): sys.exit(1)
pid=res['prompt_id']
while True:
    h=json.load(urllib.request.urlopen('http://127.0.0.1:8188/history/'+pid))
    if pid in h:
        s=h[pid]['status']; print(s['status_str']); 
        for m in s['messages']:
            if m[0] in('execution_error',): print(json.dumps(m[1])[:2500])
        print(json.dumps(h[pid]['outputs'])[:1500]); break
    time.sleep(10)
