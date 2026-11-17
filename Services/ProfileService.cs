using AutoMapper;
using para.DTOs;
using para.Models;
using para.Repositories;

namespace para.Services;

public interface IProfileService
{
    Task<UserProfileDTO?> GetAsync(int id, CancellationToken ct);
    Task<UserProfileDTO?> UpdateAsync(int id, UpdateProfileDTO dto, CancellationToken ct);
}

public sealed class ProfileService(IRepository<User> users, IMapper mapper) : IProfileService
{
    public async Task<UserProfileDTO?> GetAsync(int id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        return user == null ? null : mapper.Map<UserProfileDTO>(user);
    }

    public async Task<UserProfileDTO?> UpdateAsync(int id, UpdateProfileDTO dto, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        if (user == null) return null;
        mapper.Map(dto, user);
        await users.SaveChangesAsync(ct);
        return mapper.Map<UserProfileDTO>(user);
    }
}
