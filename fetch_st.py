import urllib.request
import re
import ssl

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

url = "https://raw.githubusercontent.com/SillyTavern/SillyTavern/main/src/endpoints/novelai.js"
with urllib.request.urlopen(url, context=ctx) as r:
    content = r.read().decode('utf-8')
    
    # Just print the whole model mapping array/dict
    lines = content.split('\n')
    for i, line in enumerate(lines):
        if 'models = ' in line or 'MODELS' in line or 'modelList' in line or 'modelNames' in line:
            print("FOUND A MODEL LIST at line", i)
            for j in range(max(0, i-5), min(len(lines), i+30)):
                print(lines[j])
