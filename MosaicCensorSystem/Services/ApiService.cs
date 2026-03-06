using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MosaicCensorSystem.Models;

namespace MosaicCensorSystem.Services
{
    public class ApiService
    {
        // 서버 주소 (BetaChip.Api가 실행 중인 주소)
        private static readonly HttpClient _httpClient = new HttpClient 
        { 
            BaseAddress = new Uri("http://localhost:5020/") 
        };

        public async Task<SubscriptionInfo?> GetSubscriptionAsync(string userId)
        {
#if DEBUG
            // 개발자 모드: 실제 서버 요청 없이 Mock 구독 정보를 즉시 반환합니다.
            // 릴리즈 빌드에서는 이 블록이 컴파일되지 않습니다.
            if (Config.IsDevelopmentMode)
            {
                Console.WriteLine("[DEV MODE] Mock 구독 정보 반환 (Tier=plus, 모든 프리미엄 기능 활성화)");
                return await Task.FromResult(new SubscriptionInfo
                {
                    Id = "dev-user-local",
                    Email = "dev@betachip.local",
                    Tier = "plus",          // "plus" = Patreon Plus 수준 (최고 등급)
                    ExpiresAt = DateTime.Now.AddYears(99),
                });
            }
#endif
            try
            {
                // 서버의 GET api/subscription/{id} 호출
                return await _httpClient.GetFromJsonAsync<SubscriptionInfo>($"api/subscription/{userId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] 서버 연결 실패: {ex.Message}");
                return null;
            }
        }
    }
}