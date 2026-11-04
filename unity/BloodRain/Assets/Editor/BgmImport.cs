using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // 배경음 가져오기 설정(BGM-03a). Assets/Audio/BGM 아래 .ogg 는 길어서 메모리에 다 올리지 않고 흘려 읽는다.
    //   Load Type Streaming, Vorbis 품질 70%, Preload Audio Data 끔, Force To Mono 끔(스테레오 그대로).
    // 곡은 tools/postprocess_audio.py 가 넣고 목록은 같은 폴더 bgm.json. 곡을 트는 쪽은 기획이 정해진 뒤.
    public class BgmImport : AssetPostprocessor
    {
        const string BgmDir = "Assets/Audio/BGM/";

        public static bool IsBgm(string path) =>
            path.Replace('\\', '/').StartsWith(BgmDir) &&
            path.EndsWith(".ogg", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessAudio()
        {
            if (!IsBgm(assetPath)) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = false;
            importer.defaultSampleSettings = s;
        }
    }
}
