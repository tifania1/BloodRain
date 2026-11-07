"""postprocess_art.py 배경 제거(PR-02)와 split_props.py 칸 찾기."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from PIL import Image, ImageDraw  # noqa: E402

import postprocess_art as pa  # noqa: E402
import split_props as sp  # noqa: E402


def figure() -> Image.Image:
    """배경 위 먹 몸, 그 안에 배경색과 비슷한 얼굴(옅은 담채)과 배경색 그대로인 빈 종이 덩이."""
    im = Image.new("RGB", (400, 400), pa.BG)
    d = ImageDraw.Draw(im)
    d.ellipse((50, 50, 350, 350), fill=(20, 20, 20))
    d.ellipse((130, 130, 270, 270), fill=(236, 224, 200))  # 얼굴: 배경색과 tol 안쪽 차이지만 tol/2 보다 멂
    d.rectangle((70, 170, 110, 230), fill=pa.BG)  # 몸 안에 갇힌 빈 종이(팔 사이 같은)
    d.rectangle((300, 195, 302, 197), fill=pa.BG)  # 아주 작은 배경색 점(붓 결) — 남아야 함
    return im


class RemoveBgSpriteTests(unittest.TestCase):
    def test_바깥_배경은_지우고_안쪽_얼굴은_남긴다(self):
        a = pa.remove_bg_sprite(figure()).getchannel("A")
        self.assertEqual(a.getpixel((5, 5)), 0)
        self.assertEqual(a.getpixel((200, 200)), 255)
        self.assertEqual(a.getpixel((200, 80)), 255)

    def test_갇힌_넓은_빈_종이는_지우고_작은_점은_남긴다(self):
        a = pa.remove_bg_sprite(figure()).getchannel("A")
        self.assertLess(a.getpixel((90, 200)), 30)
        self.assertGreater(a.getpixel((301, 196)), 200)

    def test_예전_방식은_얼굴을_뚫었다(self):
        # 바꾼 까닭을 남겨 둠: remove_bg 는 tol 안쪽이면 어디든 지운다
        self.assertLess(pa.remove_bg(figure()).getchannel("A").getpixel((200, 200)), 255)


class SplitPropsTests(unittest.TestCase):
    def sheet(self) -> Image.Image:
        im = Image.new("RGBA", (600, 600), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rectangle((40, 60, 200, 200), fill=(10, 10, 10, 255))   # 위 줄 왼쪽
        d.ellipse((350, 30, 560, 230), fill=(10, 10, 10, 255))    # 위 줄 오른쪽(조금 더 높음)
        d.polygon([(200, 260), (450, 260), (450, 400)], fill=(10, 10, 10, 255))  # 가운데 줄 삼각형
        d.rectangle((210, 345, 270, 400), fill=(10, 10, 10, 255))  # 삼각형 칸 안 빈 귀퉁이에 따로 선 덩이
        d.rectangle((60, 470, 160, 560), fill=(10, 10, 10, 255))  # 아래 줄
        return im

    def test_읽는_차례로_찾는다(self):
        boxes = [b for b, _ in sp.find_parts(self.sheet())]
        self.assertEqual(len(boxes), 5)
        lefts = [b[0] for b in boxes]
        self.assertLess(lefts[0], lefts[1])  # 위 줄 왼쪽 먼저
        self.assertLess(lefts[2], lefts[3])  # 가운데 줄: 삼각형(200) → 네모(210)
        self.assertGreater(boxes[4][1], 450)

    def test_겹친_칸에서도_옆_덩이가_묻지_않는다(self):
        img = self.sheet()
        parts = sp.find_parts(img)
        box, mask = parts[2]
        part = sp.tight(img, box, mask)
        # 삼각형 칸 안에 네모가 들어 있어도 삼각형만 남아 크기가 삼각형 크기와 같고, 네모 자리는 비어 있다
        self.assertEqual((part.width - sp.PAD * 2, part.height - sp.PAD * 2), (251, 141))
        self.assertEqual(part.getpixel((sp.PAD + 30, sp.PAD + 120))[3], 0)


if __name__ == "__main__":
    unittest.main()
