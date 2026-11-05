"""tools/postprocess_sfx.py 테스트. 저장소 루트에서: python -m unittest discover -s tools/tests

샘플 계산은 ffmpeg 없이, flac → ogg 한 바퀴는 ffmpeg 가 있을 때만 돈다.
"""
from __future__ import annotations

import json
import math
import shutil
import subprocess
import sys
import tempfile
import unittest
from array import array
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
import postprocess_sfx as ps  # noqa: E402

RATE = 1000


def tone(seconds: float, amp: int = 10000, freq: float = 7.0, rate: int = RATE) -> array:
    return array("h", [int(amp * math.sin(2 * math.pi * freq * i / rate)) for i in range(int(seconds * rate))])


def silence(seconds: float, rate: int = RATE) -> array:
    return array("h", [0] * int(seconds * rate))


class 계산(unittest.TestCase):
    def test_앞_무음을_잘라_첫_타격이_5ms_안에_온다(self):
        rate = 48000
        s = silence(0.3, rate) + tone(0.5, rate=rate, freq=200)
        out = ps.trim_lead(s, rate)
        self.assertLess(ps.lead_silence_s(out, rate), 0.005)
        self.assertEqual(out[0], 0)  # 남긴 몫은 0 에서 올라온다

    def test_뒤_무음은_조금만_남긴다(self):
        s = tone(1) + silence(2)
        out = ps.trim_tail(s, RATE, pad_s=0.02)
        self.assertAlmostEqual(len(out), 1000 + 20, delta=3)

    def test_최대_길이를_넘으면_자르고_끝은_0(self):
        out = ps.shape(silence(0.2) + tone(5), {"id": "x", "category": "weapon"}, RATE)
        self.assertLessEqual(len(out), int(ps.CATEGORY_MAX_S["weapon"] * RATE))
        self.assertEqual(out[-1], 0)

    def test_줄의_max_s_가_갈래_기본값보다_앞선다(self):
        self.assertEqual(ps.max_seconds({"category": "weapon", "max_s": 0.4}), 0.4)
        self.assertEqual(ps.max_seconds({"category": "ambient"}), ps.CATEGORY_MAX_S["ambient"])

    def test_루프_소리는_끝을_앞에_겹친다(self):
        out = ps.shape(tone(8), {"id": "rain", "category": "ambient", "loop": True}, RATE)
        self.assertEqual(len(out), 8000 - int(ps.LOOP_FADE_S * RATE))

    def test_무음만이면_멈춘다(self):
        with self.assertRaises(ValueError):
            ps.shape(silence(1), {"id": "x"}, RATE)

    def test_이득은_최고점을_천장에_맞춘다(self):
        self.assertEqual(ps.gain_db(-7.0), 6.0)
        self.assertEqual(ps.gain_db(0.3), -1.3)
        self.assertEqual(ps.gain_db(-7.0, extra_db=-3.0), 3.0)   # 줄 이득은 낮추기만
        self.assertEqual(ps.gain_db(-7.0, extra_db=4.0), 6.0)    # 천장 위로는 안 올린다

    def test_후보_번호는_이름_끝_숫자(self):
        with tempfile.TemporaryDirectory() as d:
            box = Path(d) / "art_inbox" / "sfx" / "sfx_x"
            box.mkdir(parents=True)
            for n in ("sfx_x_00010.flac", "sfx_x_00002.flac", "note.txt"):
                (box / n).write_bytes(b"")
            got = [p.name for p in ps.candidates({"id": "sfx_x"}, Path(d))]
            self.assertEqual(got, ["sfx_x_00002.flac", "sfx_x_00010.flac"])
            self.assertEqual(ps.candidate_dir({"id": "sfx_x"}, Path(d), Path("/pc/inbox")), Path("/pc/inbox/sfx/sfx_x"))


@unittest.skipIf(shutil.which("ffmpeg") is None, "ffmpeg 없음")
class 한바퀴(unittest.TestCase):
    def _make(self, path: Path, expr: str, seconds: float) -> None:
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", f"aevalsrc='{expr}':s=48000:d={seconds}",
                        "-ac", "2", str(path)], check=True)

    def test_후보를_다듬고_고른_것을_넣는다(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            box = root / "art_inbox" / "sfx" / "sfx_wpn_test"
            box.mkdir(parents=True)
            # 앞 0.3초 무음 + 0.6초 소리(작게) + 1초 무음, 둘째는 크고 2.5초
            self._make(box / "sfx_wpn_test_00001.flac", "if(between(t,0.3,0.9),0.1*sin(2*PI*440*t),0)", 1.9)
            self._make(box / "sfx_wpn_test_00002.flac", "if(gt(t,0.1),0.99*sin(2*PI*330*t),0)", 2.6)
            queue = root / "queue.jsonl"
            row = {"id": "sfx_wpn_test", "category": "weapon", "route": "ai", "status": "generated"}
            ps.write_rows(queue, [row])

            self.assertEqual(ps.run_candidates(queue, root), 2)
            t1 = box / "trimmed" / "sfx_wpn_test_01.ogg"
            s1 = ps.decode(t1)
            self.assertLess(ps.lead_silence_s(s1), 0.008)          # 인코딩 앞머리 몫까지 봐도 짧다
            self.assertAlmostEqual(len(s1) / ps.RATE, 0.62, delta=0.05)
            self.assertAlmostEqual(ps.true_peak(s1), ps.PEAK_DBTP, delta=0.6)
            s2 = ps.decode(box / "trimmed" / "sfx_wpn_test_02.ogg")
            self.assertLessEqual(len(s2) / ps.RATE, ps.CATEGORY_MAX_S["weapon"] + 0.02)

            ps.write_rows(queue, [dict(row, status="picked", picked=[2, 1])])
            out = root / "SFX"
            self.assertEqual(ps.run(queue, out, root), 1)
            self.assertTrue((out / "sfx_wpn_test_1.ogg").exists() and (out / "sfx_wpn_test_2.ogg").exists())
            self.assertEqual(ps.read_rows(queue)[0]["status"], "imported")
            m = json.loads((out / ps.MANIFEST).read_text(encoding="utf-8"))
            self.assertEqual([v["file"] for v in m["sounds"][0]["variants"]], ["sfx_wpn_test_1.ogg", "sfx_wpn_test_2.ogg"])

    def test_30Hz_아래를_줄인다(self):
        with tempfile.TemporaryDirectory() as d:
            src = Path(d) / "rumble.wav"
            self._make(src, "0.5*sin(2*PI*12*t)+0.5*sin(2*PI*400*t)", 1.0)
            s = ps.decode(src)
            # 12Hz 를 빼면 400Hz 0.5 만 남아 최고점이 거의 반으로(-6dB 근처)
            self.assertLess(max(abs(v) for v in s[4800:]) / 32768, 0.6)


if __name__ == "__main__":
    unittest.main()
