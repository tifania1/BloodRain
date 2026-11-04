using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Presentation.Meta
{
    // 비급 id(연환 id 또는 "문파:단계", SynergyDefs.ManualIdOf)를 화면에 쓸 이름·설명으로 바꾼다.
    // 장경각 화면과 정산 화면이 같이 쓴다.
    public static class ManualNames
    {
        /// <summary>(이름, 한자, 설명). 모르는 id 면 id 를 그대로 이름으로.</summary>
        public static (string name, string hanja, string desc) Of(GameData data, string manualId)
        {
            var syn = data.Synergies;
            var link = syn.Links.FirstOrDefault(l => l.ManualId == manualId);
            if (link != null)
                return (Loc.T($"synergies.links.{link.Id}.name", link.NameKo), Loc.Hanja(link.Hanja), Loc.T($"synergies.links.{link.Id}.desc", link.Desc));
            int colon = manualId.IndexOf(':');
            if (colon > 0 && int.TryParse(manualId.Substring(colon + 1), out int count))
            {
                var school = syn.Schools.FirstOrDefault(s => s.Id == manualId.Substring(0, colon));
                int tier = school == null ? -1 : school.Tiers.FindIndex(t => t.Count == count);
                if (school != null && tier >= 0)
                    return (Loc.F("ui.manual.lineage", Loc.T($"synergies.schools.{school.Id}.name", school.NameKo), count), Loc.Hanja(school.Hanja),
                        Loc.T($"synergies.schools.{school.Id}.tiers.{tier}.desc", school.Tiers[tier].Desc));
            }
            return (manualId, "", "");
        }

        public static string Name(GameData data, string manualId) => Of(data, manualId).name;
    }
}
