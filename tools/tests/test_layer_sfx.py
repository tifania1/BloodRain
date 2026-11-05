"""tools/layer_sfx.py 테스트. numpy·ffmpeg 가 없으면 건너뛴다."""
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
    import layer_sfx as ls
    import synth_sfx as ss
except ImportError:
    ls = None

ROOT = Path(__file__).resolve().parent.parent.parent


@unittest.skipIf(ls is None, "numpy 없음")
class 겹치기(unittest.TestCase):
    def test_큐의_층_줄_코드_합성_재료는_굽는_법이_있다(self):
        rows = [json.loads(l) for l in (ROOT / "sfx_queue" / "queue.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        need = {(r["id"], l["what"]) for r in rows if r["route"] == "layer" for l in r["layers"] if l["from"] == "synth"}
        self.assertTrue(need)
        self.assertEqual(need, set(ss.LAYER_RECIPES))

    def test_방울은_두세_번_울리고_같은_시드면_같다(self):
        row = {"id": "sfx_enemy_gangsi_land", "seed": 5}
        a, b, c = ss.bake_layer(row, "방울", 0), ss.bake_layer(row, "방울", 0), ss.bake_layer(row, "방울", 1)
        self.assertTrue(np.array_equal(a, b))
        self.assertFalse(np.array_equal(a, c))
        self.assertAlmostEqual(float(np.max(np.abs(a))), 0.89, 3)
        self.assertTrue(np.all(np.isfinite(a)))

    def test_겹치기는_늦게_시작한_만큼_길어진다(self):
        x = np.ones(100) * 0.5
        m = ls.mix([(x, 0.0, 0.0), (x, 1.0, -6.0)])
        self.assertEqual(len(m), ls.RATE + 100)
        self.assertAlmostEqual(float(np.max(np.abs(m))), 0.89, 6)

    @unittest.skipIf(shutil.which("ffmpeg") is None, "ffmpeg 없음")
    def test_재료가_다_있으면_후보를_쓰고_generated(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            row = {"id": "sfx_enemy_gangsi_land", "route": "layer", "category": "enemy", "seconds": 0.8, "candidates": 3,
                   "seed": 1900, "save_dir": "art_inbox/sfx/sfx_enemy_gangsi_land/", "status": "todo",
                   "layers": [{"from": "ai", "what": "쿵"}, {"from": "synth", "what": "방울", "offset_s": 0.03, "gain_db": -6}]}
            q = root / "queue.jsonl"
            q.write_text(json.dumps(row, ensure_ascii=False) + "\n", encoding="utf-8")
            self.assertEqual(ls.run(q, root), 0)  # AI 후보가 없으면 건너뜀
            ai = root / row["save_dir"] / "ai"
            t = np.arange(int(0.3 * ls.RATE)) / ls.RATE
            ss.write_wav(np.sin(2 * np.pi * 80 * t) * np.exp(-t / 0.05) * 0.8, ai / "sfx_enemy_gangsi_land_00001.wav")
            self.assertEqual(ls.run(q, root), 1)
            out = sorted((root / row["save_dir"]).glob("*_mix_*.wav"))
            self.assertEqual([p.name for p in out], [f"sfx_enemy_gangsi_land_mix_{k:02d}.wav" for k in (1, 2, 3)])
            self.assertEqual(json.loads(q.read_text(encoding="utf-8"))["status"], "generated")


if __name__ == "__main__":
    unittest.main()
