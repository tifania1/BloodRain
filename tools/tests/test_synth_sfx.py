"""tools/synth_sfx.py 테스트. numpy 가 없으면 건너뛴다(굽기 한 바퀴는 ffmpeg 도 있을 때만)."""
from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
try:
    import numpy as np
    import synth_sfx as ss
except ImportError:  # CI 러너에 numpy 가 없을 수 있다
    ss = None

ROOT = Path(__file__).resolve().parent.parent.parent


@unittest.skipIf(ss is None, "numpy 없음")
class 굽기(unittest.TestCase):
    def rows(self):
        return [json.loads(l) for l in (ROOT / "sfx_queue" / "queue.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]

    def test_큐의_코드_합성_줄은_모두_굽는_법이_있다(self):
        synth = {r["id"] for r in self.rows() if r["route"] == "synth"}
        self.assertEqual(synth, set(ss.RECIPES))

    def test_같은_시드면_같은_소리_벌마다_다르다(self):
        row = {"id": "sfx_levelup", "seed": 7, "seconds": 0.5}
        a, b, c = ss.bake(row, 0), ss.bake(row, 0), ss.bake(row, 1)
        self.assertTrue(np.array_equal(a, b))
        self.assertFalse(np.array_equal(a, c))
        self.assertLessEqual(float(np.max(np.abs(a))), 0.9)

    def test_풍경은_5음_음계로_오른다(self):
        f = [ss.note_freq(1000.0, k) for k in range(8)]
        self.assertEqual(f, sorted(f))
        self.assertAlmostEqual(f[5], 2000.0)
        self.assertAlmostEqual(f[3] / f[0], 1.5)

    def test_풍경_소리의_가장_센_높이가_그_음이다(self):
        row = {"id": "sfx_xp_absorb", "seed": 3, "seconds": 0.4}
        for k in (0, 4, 7):
            x = ss.bake(row, 0, k)
            spec = np.abs(np.fft.rfft(x))
            peak = np.argmax(spec) * ss.RATE / len(x)
            self.assertAlmostEqual(peak, ss.note_freq(ss.RECIPES["sfx_xp_absorb"]["f0"], k), delta=15)

    def test_심장은_두_박(self):
        x = np.abs(ss.bake({"id": "sfx_low_health_heart", "seed": 1, "seconds": 0.8}, 0))
        win = int(0.02 * ss.RATE)
        env = np.array([x[i:i + win].max() for i in range(0, len(x) - win, win)])
        env = np.concatenate(([0.0], env))  # 첫 박은 맨 앞에서 시작한다
        peaks = [i for i in range(1, len(env) - 1) if env[i] > 0.4 and env[i] >= env[i - 1] and env[i] > env[i + 1]]
        self.assertEqual(len(peaks), 2)

    def test_풍경은_음마다_파일이_따로다(self):
        names = [n for _, _, n in ss.outputs({"id": "sfx_xp_absorb", "candidates": 2})]
        self.assertEqual(len(names), 16)
        self.assertIn("sfx_xp_absorb_n8_2.ogg", names)
        self.assertEqual([n for _, _, n in ss.outputs({"id": "sfx_levelup", "candidates": 5, "picked": [4]})], ["sfx_levelup.ogg"])

    @unittest.skipIf(shutil.which("ffmpeg") is None, "ffmpeg 없음")
    def test_한_바퀴(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            q = root / "q.jsonl"
            row = {"id": "sfx_gimmick_bell", "route": "synth", "category": "ambient", "seconds": 1.0, "candidates": 2,
                   "seed": 5, "loop": False, "save_dir": "art_inbox/sfx/sfx_gimmick_bell/", "status": "todo"}
            q.write_text(json.dumps(row) + "\n", encoding="utf-8")
            self.assertEqual(ss.run(q, root / "SFX", root), 1)
            self.assertTrue((root / "SFX" / "sfx_gimmick_bell_2.ogg").exists())
            meta = (root / "SFX" / "sfx_gimmick_bell_2.ogg.meta").read_text(encoding="utf-8")
            self.assertIn("guid: " + ss.ps.guid("sfx_gimmick_bell_2.ogg"), meta)
            self.assertIn("loadType: 0", meta)
            self.assertFalse((root / "SFX.meta").exists())  # Assets/Audio 밖 폴더엔 안 만든다
            got = json.loads(q.read_text(encoding="utf-8"))
            self.assertEqual((got["status"], got["picked"]), ("imported", [1, 2]))


if __name__ == "__main__":
    unittest.main()
