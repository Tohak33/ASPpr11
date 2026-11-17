using AutoMapper;
using Microsoft.AspNetCore.Identity;
using para.Models;
using para.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace para.Services;

public class EntityService<T>(IRepository<T> repository, IStoreRules rules, IMapper mapper, IMemoryCache cache,
    IPasswordHasher<User> passwords) : IEntityService<T> where T : class
{
    public Task<List<T>> ListAsync() => repository.ListAsync();
    public Task<T?> GetAsync(int id) => repository.GetByIdAsync(id);

    public async Task CreateAsync(T input)
    {
        var entity = mapper.Map<T>(input);
        if (entity is Product p) p.CreatedAt = DateTime.UtcNow;
        if (entity is User u) u.PasswordHash = passwords.HashPassword(u, u.PasswordHash);
        await rules.ValidateAsync(entity);
        repository.Add(entity);
        await repository.SaveChangesAsync();
        if (entity is Category) cache.Remove(CatalogService.CategoryCacheKey);
    }

    public async Task<bool> UpdateAsync(int id, T input)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return false;
        var oldHash = (entity as User)?.PasswordHash;
        mapper.Map(input, entity);
        if (entity is User u)
            u.PasswordHash = input is User inputUser && !string.IsNullOrWhiteSpace(inputUser.PasswordHash)
                ? passwords.HashPassword(u, inputUser.PasswordHash)
                : oldHash!;
        await rules.ValidateAsync(entity);
        await repository.SaveChangesAsync();
        if (entity is Category) cache.Remove(CatalogService.CategoryCacheKey);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null) return false;
        await rules.ValidateDeleteAsync(entity);
        repository.Remove(entity);
        await repository.SaveChangesAsync();
        if (entity is Category) cache.Remove(CatalogService.CategoryCacheKey);
        return true;
    }
}
