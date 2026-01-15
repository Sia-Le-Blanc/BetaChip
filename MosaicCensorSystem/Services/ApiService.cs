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