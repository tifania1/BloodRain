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


class 큐로_뽑기(unittest.TestCase):
    def test_ai_todo_줄만_뽑아_옮기고_generated_로_적는다(self):
        import json
        import tempfile
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            out = root / "comfy"
            (out / "sfx").mkdir(parents=True)
            rows = [
                {"id": "a", "route": "ai", "status": "todo", "seconds": 0.5, "candidates": 2, "seed": 7, "prompt": "p", "save_dir": "art_inbox/sfx/a/", "milestone": "M2"},
                {"id": "b", "route": "synth", "status": "todo", "seconds": 0.5, "prompt": "p"},
                {"id": "c", "route": "ai", "status": "todo", "seconds": 20.0, "prompt": "p", "milestone": "M2"},
                {"id": "d", "route": "ai", "status": "todo", "seconds": 0.5, "prompt": "p", "milestone": "M3"},
            ]
            q = root / "queue.jsonl"
            q.write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")

            def fake(server, prompt):
                name = prompt["57"]["inputs"]["filename_prefix"].split("/")[1]
                n = prompt["11"]["inputs"]["batch_size"]
                files = []
                for k in range(1, n + 1):
                    (out / "sfx" / f"{name}_{k:05d}_.flac").write_bytes(b"x")
                    files.append(f"sfx/{name}_{k:05d}_.flac")
                return files

            calls = []

            def fake2(server, prompt):
                calls.append((prompt["6"]["inputs"]["text"].split(",")[0], prompt["11"]["inputs"]["batch_size"], prompt["3"]["inputs"]["seed"]))
                return fake(server, prompt)

            done = sa3.run_queue(q, root, out, milestone="M2", runner=fake2, variants={"a": ["thin swish", "heavy whoosh"]})
            self.assertEqual(done, ["a"])
            # 문구 갈래마다 따로, 시드도 다르게
            self.assertEqual([(c[0], c[1]) for c in calls], [("thin swish", 1), ("heavy whoosh", 1)])
            self.assertNotEqual(calls[0][2], calls[1][2])
            # 후보가 갈래 수보다 많으면 갈래마다 하나씩 여러 번, 시드는 모두 다르다(한 묶음으로 뽑지 않는다)
            calls.clear()
            (root / "art_inbox" / "sfx" / "b").mkdir(parents=True, exist_ok=True)
            n = sa3.generate("b", ["x", "y"], 1.0, 4, 5, root / "art_inbox" / "sfx" / "b", out, "http://x", fake2)
            self.assertEqual(n, 4)
            self.assertEqual([c[1] for c in calls], [1, 1, 1, 1])
            self.assertEqual(len({c[2] for c in calls}), 4)
            self.assertEqual(sorted(p.name for p in (root / "art_inbox/sfx/a").iterdir()), ["a_00001.flac", "a_00002.flac"])
            after = [json.loads(l) for l in q.read_text(encoding="utf-8").splitlines()]
            self.assertEqual([r["status"] for r in after], ["generated", "todo", "todo", "todo"])


if __name__ == "__main__":
    unittest.main()
