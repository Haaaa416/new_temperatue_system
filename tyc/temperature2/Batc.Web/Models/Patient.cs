namespace Batc.Web.Models
{
    public class Patient
    {
        public string Id { get; set; } = "";        // �f�wID (e.g. PAT-2023-1296)
        public string Name { get; set; } = "";      // �m�W
        public int? Age { get; set; }               // �~��
        public DateTime? DateOfBirth { get; set; }  // �X�ͦ~���
        public string? Gender { get; set; }         // �ʧO
        public string? BloodType { get; set; }      // �嫬
        public double? Height { get; set; }         // ����
        public double? Weight { get; set; }         // �魫
        public string? AvatarUrl { get; set; }      // �Y���Ϥ����| (�i��)
    }
}