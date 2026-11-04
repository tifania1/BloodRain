"""tools/postprocess_audio.py 테스트. 저장소 루트에서: python -m unittest discover -s tools/tests

샘플 계산(무음 자르기·자르기·루프 이음매·이득)은 ffmpeg 없이 돌고,
실제 MP3 → ogg 한 바퀴는 ffmpeg 가 있을 때만 돈다.
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
import postprocess_audio as pa  # noqa: E402

RATE = 1000  # 계산 테스트는 작은 표본률로


def tone(seconds: float, rate: int = RATE, amp: int = 10000, freq: float = 7.0) -> array:
    """스테레오 사인파. 오른쪽은 왼쪽의 반."""
    out = array("h")
    for i in range(int(seconds * rate)):
        v = int(amp * math.sin(2 * math.pi * freq * i / rate))
        out.extend((v, v // 2))
    return out


def silence(seconds: float, rate: int = RATE) -> array:
    return array("h", [0] * (int(seconds * rate) * 2))


class 계산(unittest.TestCase):
    def test_앞뒤_무음을_자르고_조금_남긴다(self):
        s = silence(1) + tone(2) + silence(1.5)
        t = pa.trim_silence(s, 2, RATE, pad_s=0.05)
        # 사인파 첫 샘플(0)은 문턱 아래라 소리 시작은 한 프레임 뒤. 앞뒤로 50프레임씩 남는다
        self.assertAlmostEqual(pa.frames(t), 2000 + 100, delta=3)

    def test_전부_무음이면_빈다(self):
        self.assertEqual(len(pa.trim_silence(silence(1), 2, RATE)), 0)

    def test_자르면_길이가_맞고_끝은_0이다(self):
        c = pa.cut(tone(5), 2.0, 2, RATE, fade_s=0.5)
        self.assertEqual(pa.frames(c), 2000)
        self.assertEqual((c[-2], c[-1]), (0, 0))

    def test_루프_이음매는_원곡의_이어진_두_프레임이다(self):
        s = tone(8)
        n = pa.frames(s)
        k = pa.loop_fade_frames(n, RATE, 2.0)
        out = pa.crossfade_loop(s, 2, RATE, 2.0)
        self.assertEqual(pa.frames(out), n - k)
        # 결과 첫 프레임 = 원래 꼬리 첫 프레임, 결과 끝 프레임 = 꼬리 바로 앞 프레임
        self.assertEqual(tuple(out[0:2]), tuple(s[(n - k) * 2:(n - k) * 2 + 2]))
        self.assertEqual(tuple(out[-2:]), tuple(s[(n - k - 1) * 2:(n - k) * 2]))
        # 겹친 구간 뒤로는 원곡 그대로
        self.assertEqual(out[k * 2:], s[k * 2:(n - k) * 2])

    def test_짧은_곡은_겹치는_길이가_4분의_1을_넘지_않는다(self):
        self.assertEqual(pa.loop_fade_frames(4000, RATE, 2.0), 1000)
        self.assertEqual(pa.loop_fade_frames(40000, RATE, 2.0), 2000)

    def test_이득은_최고점_천장을_넘지_않는다(self):
        self.assertEqual(pa.gain_db(-24.0, -10.0), 6.0)       # 여유가 있으면 목표까지
        self.assertEqual(pa.gain_db(-24.0, -4.0), 2.5)        # 최고점 -4 → -1.5 까지만
        self.assertEqual(pa.gain_db(-12.0, -0.5), -6.0)       # 크면 줄인다

    def test_loop_아닌_곡은_끝을_살짝_줄인다(self):
        s = silence(0.2) + tone(3) + silence(0.2)
        out = pa.shape(s, {"id": "x", "loop": False}, RATE)
        self.assertEqual((out[-2], out[-1]), (0, 0))

    def test_inbox_를_주면_art_inbox_를_바꿔_읽는다(self):
        row = {"save_as": "art_inbox/audio/bgm_inn.mp3"}
        self.assertEqual(pa.source_path(row, Path("/r")), Path("/r/art_inbox/audio/bgm_inn.mp3"))
        self.assertEqual(pa.source_path(row, Path("/r"), Path("/pc/inbox")), Path("/pc/inbox/audio/bgm_inn.mp3"))


@unittest.skipIf(shutil.which("ffmpeg") is None or shutil.which("ffprobe") is None, "ffmpeg 없음")
class 한바퀴(unittest.TestCase):
    def test_MP3_를_ogg_로_바꾸고_큐와_목록을_고친다(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            inbox = root / "art_inbox" / "audio"
            inbox.mkdir(parents=True)
            # 앞 1초 무음 + 6초 사인파(작게) + 뒤 1초 무음
            subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i",
                            "aevalsrc='if(between(t,1,7),0.05*sin(2*PI*220*t),0)':s=48000:d=8",
                            "-ac", "2", str(inbox / "bgm_test.mp3")], check=True)
            queue = root / "queue.jsonl"
            rows = [{"id": "bgm_test", "use": "시험", "loop": True, "save_as": "art_inbox/audio/bgm_test.mp3", "status": "picked"},
                    {"id": "bgm_skip", "use": "시험", "loop": True, "save_as": "art_inbox/audio/none.mp3", "status": "todo"}]
            queue.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
            out = root / "BGM"
            self.assertEqual(pa.run(queue, out, root), 1)

            ogg = out / "bgm_test.ogg"
            self.assertTrue(ogg.exists())
            dur = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(ogg)],
                                       check=True, capture_output=True, text=True).stdout)
            # 무음 2초를 자르면(앞뒤 50ms 남김) 6.1초, 루프로 겹치는 길이는 2초 대신 1/4 상한(1.525초)이라 약 4.575초
            self.assertAlmostEqual(dur, 6.1 * 0.75, delta=0.1)
            lufs, tp = pa.measure(pa.decode(ogg))
            self.assertAlmostEqual(lufs, pa.TARGET_LUFS, delta=1.0)
            self.assertLessEqual(tp, pa.TRUE_PEAK_DB + 0.5)

            got = [json.loads(l) for l in queue.read_text(encoding="utf-8").splitlines()]
            self.assertEqual([r["status"] for r in got], ["imported", "todo"])
            m = json.loads((out / pa.MANIFEST).read_text(encoding="utf-8"))
            self.assertEqual([t["id"] for t in m["tracks"]], ["bgm_test"])
            self.assertTrue(m["tracks"][0]["loop"])


if __name__ == "__main__":
    unittest.main()
