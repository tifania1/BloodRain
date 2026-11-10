#nullable enable
// 땅의 주인을 쓰러뜨린 순간 띄우는 짧은 붓글씨 알림(제목 한 줄 + 보상 줄). 웹 lordDown 의 toast 와 같은 글.
//   첫 처치:   "원각 — 쓰러뜨렸다" / "봉인석 천추석 · 소림로 환생할 수 있다 · 윤회의 기 +300 · 혈정 +N"
//   혈영(다시): 몸줄 앞에 "혈영으로 다시 일어날 것이다"
// 종장 혈마는 엔딩으로 넘어가니 알림이 없다(Of 가 null). 대표 인물 합류는 객잔에 돌아와서(21장)라 여기 없다.
// 이름은 Strings 로 옮기고, 키가 없으면 데이터의 한국어 원문을 쓴다.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public sealed class BossKillNotice
    {
        public string Title = "";
        /// <summary>보상 줄. 화면에는 " · " 로 이어 한 줄로 써도 되고 줄마다 써도 된다.</summary>
        public List<string> Lines = new List<string>();
        public string Body => string.Join(" · ", Lines);

        public static BossKillNotice? Of(GameData data, Strings s, BossKill kill)
        {
            if (kill.IsFinal) return null;
            var boss = data.Bosses.FirstOrDefault(b => b.Id == kill.BossId);
            string name = s.Get($"enemies.bosses.{kill.BossId}.name", boss?.NameKo ?? kill.BossId);
            var n = new BossKillNotice { Title = s.Format("ui.bosskill.title", name) };
            if (!kill.First) n.Lines.Add(s.Get("ui.bosskill.shadow"));
            if (kill.SealStone != null)
                n.Lines.Add(s.Format("ui.bosskill.seal_stone", s.Get($"cheonha.regions.{kill.RegionId}.sealStone", kill.SealStone)));
            if (kill.UnlockSect != null)
            {
                var sect = data.Cheonha.Sects.FirstOrDefault(x => x.Id == kill.UnlockSect);
                n.Lines.Add(s.Format("ui.bosskill.unlock_sect", s.Get($"cheonha.sects.{kill.UnlockSect}.name", sect?.NameKo ?? kill.UnlockSect)));
            }
            n.Lines.Add(s.Format("ui.bosskill.rewards", kill.RebirthQi, kill.BloodCrystal));
            return n;
        }
    }
}
