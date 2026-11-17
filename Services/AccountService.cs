using Microsoft.AspNetCore.Identity;
using para.Models;
using para.Repositories;

namespace para.Services;

public class AccountService(IRepository<User> users, IRepository<Role> roles, IStoreRules rules,
    IPasswordHasher<User> passwords) : IAccountService
{
    public async Task<User?> LoginAsync(string login, string password, CancellationToken ct = default)
    {
        var user = await users.FirstAsync(u => u.Login == login && u.IsActive, ct);
        if (user == null) return null;
        var result = Verify(user, password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwords.HashPassword(user, password);
            await users.SaveChangesAsync(ct);
        }
        return user;
    }

    private PasswordVerificationResult Verify(User user, string password)
    {
        try
        {
            if (user.PasswordHash.StartsWith("$2", StringComparison.Ordinal))
                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)
                    ? PasswordVerificationResult.SuccessRehashNeeded
                    : PasswordVerificationResult.Failed;
            return passwords.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or FormatException or ArgumentException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

    public async Task<User> RegisterAsync(string login, string password, string fullName, CancellationToken ct = default)
    {
        var role = await roles.FirstAsync(r => r.RoleName == "User", ct)
            ?? throw new StoreException(StoreError.NotFound, "Роль пользователя не найдена.");
        var user = new User
        {
            Login = login,
            FullName = fullName, RoleID = role.RoleID, Role = role, IsActive = true
        };
        user.PasswordHash = passwords.HashPassword(user, password);
        await rules.ValidateAsync(user, ct);
        users.Add(user);
        await users.SaveChangesAsync(ct);
        return user;
    }
}
