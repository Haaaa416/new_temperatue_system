// Models/UserDraft.cs
using System.ComponentModel.DataAnnotations;

namespace Batc.Web.Models
{
    public class UserDraft
    {
        [Required, MaxLength(100)]
        public string Username { get; set; } = "";

        public int? LoginID { get; set; }

        [Range(1, 3)] // 1=Doctor, 2=Nurse, 3=Admin
        public int RoleID { get; set; } = 1;
    }
}

