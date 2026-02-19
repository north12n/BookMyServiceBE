namespace BookMyServiceBE.Models
{
    public class SystemSetting
    {
       

        public int SystemSettingId { get; set; }
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string SettingKey { get; internal set; } = string.Empty;
        public string SettingValue { get; internal set; } = string.Empty;
        public string Description { get; internal set; } = string.Empty;
    }
}
