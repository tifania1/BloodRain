#nullable enable
// 몸 그림 고르기(기획서 13장 '움직임은 이미지 한 장 + 연출로', 21장 몸 성별, 2026-11-08 클라우드 BD-01).
//   한 생의 플레이어는 그 몸의 원화 한 장(Assets/Art/Characters/char_<문파>_<m|f>.png)으로 그린다.
//   서장 몸은 진소운 제 그림 char_jinsowoon. 문파 몸은 성별 그림 → 같은 문파 다른 성별 그림 차례로 찾고,
//   그것도 없으면(아직 안 뽑은 M3·M4 문파) Unity 가 자리 표시로 그린다. 파일이 있는지는 Unity 가 본다.
using System.Collections.Generic;

namespace BloodRain.Core.Meta
{
    public static class BodyArt
    {
        public const string Prologue = "char_jinsowoon";

        /// <summary>찾아볼 그림 이름(확장자 없음), 앞에서부터. 빈 목록은 없다.</summary>
        public static IReadOnlyList<string> SpriteIds(RebirthBody body)
        {
            if (body.IsPrologue || body.Sect == null) return new[] { Prologue };
            string sect = body.Sect.Id;
            string own = body.Female ? "f" : "m", other = body.Female ? "m" : "f";
            return new[] { $"char_{sect}_{own}", $"char_{sect}_{other}" };
        }
    }
}
