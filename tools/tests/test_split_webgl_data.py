"""tools/split_webgl_data.py 테스트. brotli 가 없으면 건너뛴다."""
from __future__ import annotations

import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
try:
    import brotli
except ImportError:
    brotli = None
import split_webgl_data as sw

# Unity 6 기본 WebGL 템플릿에서 이 도구가 건드리는 줄만 남긴 것
INDEX = """<script>
      var buildUrl = "Build";
      var loaderUrl = buildUrl + "/BloodRain.loader.js";
      var config = {
        arguments: [],
        dataUrl: buildUrl + "/BloodRain.data.br",
        frameworkUrl: buildUrl + "/BloodRain.framework.js.br",
      };
      var script = document.createElement("script");
      script.src = loaderUrl;
      document.body.appendChild(script);
</script>"""


class 색인고치기(unittest.TestCase):
    def test_세_군데를_고치고_두_번_해도_같다(self):
        out = sw.patch_index(INDEX, ["a.data.br", "b.data.br"])
        self.assertIn('const dataParts = ["a.data.br", "b.data.br"];', out)
        self.assertIn('dataUrl: "",', out)
        self.assertNotIn("BloodRain.data.br", out)
        self.assertIn("config.dataUrl = u; document.body.appendChild(script);", out)
        self.assertEqual(out, sw.patch_index(out, ["a.data.br"]))

    def test_템플릿이_다르면_멈춘다(self):
        with self.assertRaises(ValueError):
            sw.patch_index(INDEX.replace('dataUrl: buildUrl + "/BloodRain.data.br",', 'dataUrl: "x",'), ["a"])


class 자동동기화(unittest.TestCase):
    INDEX = "      var config = {\n        // config.autoSyncPersistentDataPath = true;\n      };"

    def test_주석을_풀고_두_번_해도_같다(self):
        out = sw.patch_autosync(self.INDEX)
        self.assertIn(sw.AUTOSYNC_ON, out)
        self.assertNotIn(sw.AUTOSYNC_OFF, out)
        self.assertEqual(out, sw.patch_autosync(out))

    def test_줄이_없으면_멈춘다(self):
        with self.assertRaises(ValueError):
            sw.patch_autosync(INDEX)

    def test_PC_빌드와_같은_글이다(self):
        cs = (Path(__file__).resolve().parents[2] / "unity/BloodRain/Assets/Editor/WebGLBuild.cs").read_text(encoding="utf-8")
        self.assertIn('"%s"' % sw.AUTOSYNC_OFF, cs)
        self.assertIn('"%s"' % sw.AUTOSYNC_ON, cs)


@unittest.skipIf(brotli is None, "brotli 없음")
class 나누기(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp()
        os.makedirs(os.path.join(self.root, "Build"))
        Path(self.root, "index.html").write_text(INDEX, encoding="utf-8")
        self.raw = os.urandom(300_000)  # 무작위라 압축해도 안 줄어 상한을 넘긴다
        Path(self.root, "Build", "BloodRain.data.br").write_bytes(brotli.compress(self.raw))

    def tearDown(self):
        shutil.rmtree(self.root)

    def test_조각을_이으면_원래_data_이고_조각마다_상한_안이다(self):
        names = sw.split(self.root, limit=100_000, chunk=400_000)
        self.assertGreaterEqual(len(names), 3)
        joined = b""
        for n in names:
            p = Path(self.root, "Build", n).read_bytes()
            self.assertLessEqual(len(p), 100_000)
            self.assertTrue(n.endswith(".data.br"))  # web/_headers 규칙이 걸리는 이름
            joined += brotli.decompress(p)
        self.assertEqual(joined, self.raw)
        self.assertFalse(Path(self.root, "Build", "BloodRain.data.br").exists())
        self.assertIn("dataParts", Path(self.root, "index.html").read_text(encoding="utf-8"))

    def test_상한_안이면_그대로_둔다(self):
        self.assertEqual(sw.split(self.root, limit=10_000_000), [])
        self.assertTrue(Path(self.root, "Build", "BloodRain.data.br").exists())

    def test_색인을_못_고치면_아무것도_안_바꾼다(self):
        Path(self.root, "index.html").write_text("<html></html>", encoding="utf-8")
        with self.assertRaises(ValueError):
            sw.split(self.root, limit=100_000)
        self.assertEqual(os.listdir(os.path.join(self.root, "Build")), ["BloodRain.data.br"])


if __name__ == "__main__":
    unittest.main()
