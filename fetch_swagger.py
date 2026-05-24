import urllib.request
import json
import ssl

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

urls = [
    "https://api.novelai.net/docs/json",
    "https://api.novelai.net/docs/swagger.json",
    "https://api.novelai.net/openapi.json",
    "https://api.novelai.net/swagger.json",
    "https://text.novelai.net/docs/swagger.json"
]

for url in urls:
    print(f"Trying {url}")
    try:
        with urllib.request.urlopen(url, context=ctx) as r:
            data = json.loads(r.read().decode())
            print("SUCCESS!")
            # Find the model enum for ai/generate
            paths = data.get("paths", {})
            gen = paths.get("/ai/generate", {}).get("post", {})
            schema = gen.get("requestBody", {}).get("content", {}).get("application/json", {}).get("schema", {})
            props = schema.get("properties", {})
            model = props.get("model", {})
            print("Model Enum:", model.get("enum", []))
            break
    except Exception as e:
        print(f"Failed: {e}")
