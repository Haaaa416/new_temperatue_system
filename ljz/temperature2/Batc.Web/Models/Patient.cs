namespace Batc.Web.Models
{
    public class Patient
    {
        public string Id { get; set; } = "";        // 病患ID (e.g. PAT-2023-1296)
        public string Name { get; set; } = "";      // 姓名
        public int? Age { get; set; }               // 年齡
        public DateTime? DateOfBirth { get; set; }  // 出生年月日
        public string? Gender { get; set; }         // 性別
        public string? BloodType { get; set; }      // 血型
        public double? Height { get; set; }         // 身高
        public double? Weight { get; set; }         // 體重
        public string? AvatarUrl { get; set; }      // 頭像圖片路徑 (可選)
    }
}