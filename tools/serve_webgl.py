# Unity WebGL 빌드(build/WebGL/BloodRain, brotli)를 로컬에서 띄운다. .br 파일에 Content-Encoding: br 를 붙인다.
# python tools/serve_webgl.py → http://127.0.0.1:8765/index.html
import http.server, functools, os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "build", "WebGL", "BloodRain")

class H(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        p = self.path.split("?")[0]
        if p.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif p.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def guess_type(self, path):
        p = str(path)
        if p.endswith(".wasm.br") or p.endswith(".wasm"):
            return "application/wasm"
        if p.endswith(".js.br"):
            return "application/javascript"
        if p.endswith(".data.br"):
            return "application/octet-stream"
        return super().guess_type(path)

http.server.ThreadingHTTPServer(("127.0.0.1", 8765), functools.partial(H, directory=ROOT)).serve_forever()
