"""tools/sfx_sa3.py 테스트. 서버 없이 그래프만 본다. 저장소 루트에서: python -m unittest discover -s tools/tests"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import sfx_sa3 as sa3  # noqa: E402


class SfxSa3Tests(unittest.TestCase):
    def test_원하는_길이의_절반을_빈_잠재에_넣는다(self):
        # 2026-11-05 시험: 1.5초를 넣으면 2.97초가 나왔다(SA3 4096 ÷ 노드 2048)
        self.assertEqual(sa3.seconds_to_latent(3.0), 1.5)
        self.assertEqual(sa3.seconds_to_latent(1.0), 1.0)  # 노드 최솟값 1

    def test_그래프는_공식_템플릿과_같은_노드로_이어진다(self):
        p = sa3.build_prompt("sword swing", 2.0, 8, "sfx_wpn_sword", 7)
        kinds = {v["class_type"] for v in p.values()}
        self.assertEqual(kinds, {"CheckpointLoaderSimple", "CLIPLoader", "CLIPTextEncode", "EmptyLatentAudio",
                                 "KSampler", "VAEDecodeAudio", "SaveAudioAdvanced"})
        ks = p["3"]["inputs"]
        self.assertEqual((ks["steps"], ks["cfg"], ks["sampler_name"], ks["scheduler"], ks["seed"]), (8, 1.0, "lcm", "simple", 7))
        self.assertEqual(p["11"]["inputs"], {"seconds": 1.0, "batch_size": 8})
        self.assertEqual(p["26"]["inputs"]["type"], "stable_audio")
        self.assertEqual(p["57"]["inputs"]["filename_prefix"], "sfx/sfx_wpn_sword")
        # 모든 연결이 있는 노드를 가리킨다
        for v in p.values():
            for x in v["inputs"].values():
                if isinstance(x, list):
                    self.assertIn(x[0], p)

    def test_문구_끝에_건조한_소리와_길이를_붙인다(self):
        t = sa3.build_prompt("A heavy punch", 1.2, 1, "x", 1)["6"]["inputs"]["text"]
        self.assertIn("no reverb tail", t)
        self.assertTrue(t.endswith("Length: 1 seconds"))
        same = sa3.build_prompt("A punch, dry close-mic, no music, no reverb tail. Length: 1 seconds", 1.2, 1, "x", 1)["6"]["inputs"]["text"]
        self.assertEqual(same.count("no reverb tail"), 1)


if __name__ == "__main__":
    unittest.main()
