using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>데이터 정의의 목록·사전 기본값은 비어 있어야 한다.
    /// Newtonsoft 는 이미 채워진 목록을 새로 만들지 않고 JSON 값을 뒤에 덧붙이므로, 기본값이 있으면 같은 값이 두 번 들어간다
    /// 배열은 새로 만들어 넣으니 괜찮다. (2026-11-01 보갑 가중치 [51,34,15] 가 여섯 개로 읽힌 일).</summary>
    public class DataDefaultsTests
    {
        [Fact] public void 데이터_정의의_목록_기본값은_비어_있다()
        {
            var types = typeof(GameData).Assembly.GetTypes()
                .Where(t => t.Namespace == "BloodRain.Core.Data" && t.IsClass && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null && t != typeof(GameData));
            var bad = new System.Collections.Generic.List<string>();
            foreach (var t in types)
            {
                object o;
                try { o = Activator.CreateInstance(t)!; } catch { continue; }
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (f.FieldType == typeof(string) || f.FieldType.IsArray || !typeof(ICollection).IsAssignableFrom(f.FieldType)) continue;
                    if (f.GetValue(o) is ICollection c && c.Count > 0) bad.Add($"{t.Name}.{f.Name}({c.Count})");
                }
            }
            Assert.True(bad.Count == 0, "기본값이 든 목록: " + string.Join(", ", bad));
        }

        [Fact] public void 절기_깨어나는_순간은_데이터_그대로_두_개()
        {
            var d = TestData.Load();
            var json = JObject.Parse(TestData.ReadText(System.IO.Path.Combine(TestData.DataDir, "balance.json")));
            var want = json["evolution"]!["triggers"]!.Select(x => (string)x!).ToList();
            Assert.Equal(want, d.Balance.Evolution.Triggers);
            Assert.Equal(want.Distinct().Count(), d.Balance.Evolution.Triggers.Count);
        }
    }
}
