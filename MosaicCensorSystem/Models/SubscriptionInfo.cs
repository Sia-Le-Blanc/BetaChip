using System;

namespace MosaicCensorSystem.Models
{
    public class SubscriptionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Tier { get; set; } = "free";
        public string? Hwid { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}