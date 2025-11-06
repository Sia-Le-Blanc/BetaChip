using System;
using System.IO;

namespace MosaicCensorSystem.Utils
{
    /// <summary>
    /// 단순한 사용자 설정 저장/로드 유틸리티. 현재는 DPI 호환성 모드
    /// 사용 여부만 관리합니다. 설정 파일은 응용 프로그램 실행 파일과
    /// 동일한 디렉터리에 `user.config` 이름으로 저장됩니다.
    /// </summary>
    internal static class UserSettings
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user.config");

        /// <summary>
        /// DPI 호환성 모드 사용 여부를 반환합니다. 파일이 없거나 파싱에
        /// 실패하면 기본값(true)을 반환합니다.
        /// </summary>
        public static bool IsCompatibilityModeEnabled()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var lines = File.ReadAllLines(ConfigFilePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("CompatMode=", StringComparison.OrdinalIgnoreCase))
                        {
                            var value = trimmed.Substring("CompatMode=".Length);
                            if (bool.TryParse(value, out bool parsed))
                                return parsed;
                        }
                    }
                }
            }
            catch
            {
                // 읽기 오류 시 기본값 사용
            }
            return true;
        }

        /// <summary>
        /// DPI 호환성 모드 사용 여부를 저장합니다. 다른 설정이 있을 수 있으므로
        /// 기존 파일을 읽어 수정 후 다시 씁니다.
        /// </summary>
        public static void SetCompatibilityModeEnabled(bool enabled)
        {
            try
            {
                string[] existing = Array.Empty<string>();
                if (File.Exists(ConfigFilePath))
                {
                    existing = File.ReadAllLines(ConfigFilePath);
                }
                bool found = false;
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i].Trim().StartsWith("CompatMode=", StringComparison.OrdinalIgnoreCase))
                    {
                        existing[i] = $"CompatMode={enabled}";
                        found = true;
                    }
                }
                if (!found)
                {
                    Array.Resize(ref existing, existing.Length + 1);
                    existing[^1] = $"CompatMode={enabled}";
                }
                File.WriteAllLines(ConfigFilePath, existing);
            }
            catch
            {
                // 저장 실패는 무시
            }
        }
    }
}