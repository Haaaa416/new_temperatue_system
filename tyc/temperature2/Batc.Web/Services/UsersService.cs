// Services/UsersService.cs
namespace Batc.Web.Services;

using Batc.Web.Models;
using Batc.Web.Models.Data;
using Microsoft.Data.SqlClient; // 這樣 catch 篩選器才找得到 SqlException
using Microsoft.EntityFrameworkCore;

public interface IUsersService
{
    Task<UserEntity> CreateAsync(UserDraft draft, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
    Task<UserEntity?> GetByLoginAndRoleAsync(int loginId, int roleId, CancellationToken ct = default);
}

public class UsersService : IUsersService
{
    private readonly AppDbContext _db;
    public UsersService(AppDbContext db) => _db = db;

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
    {
        return await _db.Users.AnyAsync(u => u.Username == username, ct);
    }

    public async Task<UserEntity> CreateAsync(UserDraft draft, CancellationToken ct = default)
    {

        Console.WriteLine($"UsersService.CreateAsync ENTER LoginID={draft.LoginID}, Username={draft.Username}, RoleID={draft.RoleID}");

        // ---- 基本驗證 ----
        if (draft.LoginID is null || draft.LoginID <= 0)
            throw new ArgumentException("LoginID 必須是正整數。");

        if (string.IsNullOrWhiteSpace(draft.Username))
            throw new ArgumentException("Username 不可空白。");

        if (draft.RoleID is < 1 or > 3)
            throw new ArgumentException("RoleID 不合法（應為 1~3）。");

        var username = draft.Username.Trim();

        // ---- 邏輯驗證（避免 DB 例外）----
        if (await _db.Users.AnyAsync(u => u.LoginID == draft.LoginID.Value, ct))
            throw new InvalidOperationException($"LoginID 已存在：{draft.LoginID}");

        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
            throw new InvalidOperationException($"Username 已存在：{username}");

        // ---- 映射（欄位對欄位）----
        var entity = new UserEntity
        {
            LoginID = draft.LoginID.Value, // ★ 手動主鍵
            Username = username,
            RoleID = draft.RoleID,
            // CreatedAt 建議交給 DB DEFAULT (SYSUTCDATETIME)
        };

        _db.Users.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql &&
                                        (sql.Number == 2627 || sql.Number == 2601))
        {
            // 雙保險：就算上面先查過，也可能併發撞鍵
            throw new InvalidOperationException("LoginID 或 Username 已存在，請更換。", ex);
        }

        Console.WriteLine("UsersService AFTER  SaveChanges");

        return entity; // 用於顯示 CreatedAt（DB default 回填）或後續流程
    }
    public async Task<UserEntity?> GetByLoginAndRoleAsync(int loginId, int roleId, CancellationToken ct = default)
    {
        // 單一查詢同時比對 ID + 角色
        return await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.LoginID == loginId && u.RoleID == roleId, ct);
    }

}
