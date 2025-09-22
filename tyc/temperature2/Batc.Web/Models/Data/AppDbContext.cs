using Microsoft.EntityFrameworkCore;


namespace Batc.Web.Models.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        public DbSet<UserEntity> Users => Set<UserEntity>();
        public DbSet<PatientEntity> Patients => Set<PatientEntity>();

        protected override void OnModelCreating(ModelBuilder mb)
        {

            // === Users ===
            mb.Entity<UserEntity>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.LoginID);
                e.Property(x => x.LoginID).ValueGeneratedNever();        // ★ 手動 PK
                e.Property(x => x.Username).IsRequired().HasMaxLength(100);
                e.HasIndex(x => x.Username).IsUnique();
                e.Property(x => x.RoleID).IsRequired();
                e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            mb.Entity<PatientEntity>(e =>
            {
                e.ToTable("Patients");
                e.HasKey(x => x.Id);
                e.Property(x => x.PatientCode).IsRequired().HasMaxLength(24);
                e.HasIndex(x => x.PatientCode).IsUnique();
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.Property(x => x.Gender).HasMaxLength(10);
                e.Property(x => x.BloodType).HasMaxLength(5);

                // ★ 指定精度（擇一即可）
                e.Property(x => x.HeightCm).HasPrecision(5, 2);
                e.Property(x => x.WeightKg).HasPrecision(5, 2);
            });

            mb.Entity<UserEntity>()
            .HasIndex(u => new { u.LoginID, u.RoleID })
            .HasDatabaseName("IX_Users_LoginID_RoleID");
        }
    }
    
    public class PatientEntity
    {
        public int Id { get; set; }                        // DB PK (自動編號)
        public string PatientCode { get; set; } = "";      // 你的前端會產生的 Code (e.g., PAT-0000-0000)
        public string Name { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? BloodType { get; set; }
        public decimal? HeightCm { get; set; }
        public decimal? WeightKg { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? AvatarUrl { get; set; }
    }
}
