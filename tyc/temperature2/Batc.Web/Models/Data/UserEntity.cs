// Batc.Web/Models/Data/UserEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Batc.Web.Models.Data
{
    [Table("Users")]
    public class UserEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]   // ★ 手動主鍵（非 Identity）
        public int LoginID { get; set; }

        [Required, MaxLength(100)]
        public string Username { get; set; } = "";

        [Required]
        public int RoleID { get; set; }                     // 1=Doctor, 2=Nurse, 3=Admin

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
