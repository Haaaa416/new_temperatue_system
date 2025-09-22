using Batc.Web.Models.Data;
using Batc.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Batc.Web.Services
{
    public interface IPatientsService
    {
        Task EnsureCacheAsync();                    // 若快取空→打 DB 灌回 AppState
        Task<List<Patient>> GetAllAsync();          // 直接回 AppState（若空會確保載入）
        Task<Patient?> GetByCodeAsync(string code); // 先看 AppState，沒有才打 DB
        Task<Patient> CreateAsync(Patient p);       // 寫 DB → 更新 AppState
        Task UpdateAsync(Patient p);                // 寫 DB → 更新 AppState
        Task DeleteByCodeAsync(string code);        // 刪 DB → 更新 AppState
    }

    public class PatientsService : IPatientsService
    {
        private readonly AppDbContext _db;
        private readonly AppState _state;
        public PatientsService(AppDbContext db, AppState state)
        {
            _db = db;
            _state = state;
        }

        public async Task EnsureCacheAsync()
        {
            if (_state.Patients.Count > 0) return;
            var rows = await _db.Patients
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            _state.Patients.Clear();
            _state.Patients.AddRange(rows.Select(MapToModel));
        }

        public async Task<List<Patient>> GetAllAsync()
        {
            await EnsureCacheAsync();
            // 回傳快取的淺拷貝，避免外部誤改
            return _state.Patients.ToList();
        }

        public async Task<Patient?> GetByCodeAsync(string code)
        {
            // 先從快取找
            var p = _state.Patients.FirstOrDefault(x => x.Id == code);
            if (p is not null) return p;

            // 打 DB 補回快取
            var e = await _db.Patients.FirstOrDefaultAsync(x => x.PatientCode == code);
            if (e is null) return null;

            var mapped = MapToModel(e);
            _state.Patients.Add(mapped);
            return mapped;
        }

        public async Task<Patient> CreateAsync(Patient p)
        {
            if (string.IsNullOrWhiteSpace(p.Id))
                throw new ArgumentException("Patient.Id (PatientCode) is required.");

            var e = new PatientEntity
            {
                PatientCode = p.Id,
                Name = p.Name,
                DateOfBirth = p.DateOfBirth,
                Age = p.Age,
                Gender = p.Gender,
                BloodType = p.BloodType,
                HeightCm = p.Height is null ? null : (decimal?)p.Height,
                WeightKg = p.Weight is null ? null : (decimal?)p.Weight,
                AvatarUrl = p.AvatarUrl
            };
            _db.Patients.Add(e);
            await _db.SaveChangesAsync();

            var created = MapToModel(e);
            _state.Patients.Insert(0, created); // 加到快取最前面（你原本列表是新到舊）
            return created;
        }

        public async Task UpdateAsync(Patient p)
        {
            var e = await _db.Patients.FirstOrDefaultAsync(x => x.PatientCode == p.Id)
                ?? throw new KeyNotFoundException($"Patient {p.Id} not found");

            e.Name = p.Name;
            e.DateOfBirth = p.DateOfBirth;
            e.Age = p.Age;
            e.Gender = p.Gender;
            e.BloodType = p.BloodType;
            e.HeightCm = p.Height is null ? null : (decimal?)p.Height;
            e.WeightKg = p.Weight is null ? null : (decimal?)p.Weight;
            e.AvatarUrl = p.AvatarUrl;

            await _db.SaveChangesAsync();

            // 更新快取
            var idx = _state.Patients.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) _state.Patients[idx] = p;
        }

        public async Task DeleteByCodeAsync(string code)
        {
            var e = await _db.Patients.FirstOrDefaultAsync(x => x.PatientCode == code);
            if (e is null) return;
            _db.Patients.Remove(e);
            await _db.SaveChangesAsync();

            // 同步刪快取
            var idx = _state.Patients.FindIndex(x => x.Id == code);
            if (idx >= 0) _state.Patients.RemoveAt(idx);
        }

        private static Patient MapToModel(PatientEntity e) => new()
        {
            Id = e.PatientCode,
            Name = e.Name,
            DateOfBirth = e.DateOfBirth,
            Age = e.Age,
            Gender = e.Gender,
            BloodType = e.BloodType,
            Height = e.HeightCm is null ? null : (double?)e.HeightCm,
            Weight = e.WeightKg is null ? null : (double?)e.WeightKg,
            AvatarUrl = string.IsNullOrWhiteSpace(e.AvatarUrl) ? "images/病人_1.png" : e.AvatarUrl
        };
    }
}
