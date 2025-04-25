using DevHabit.Api.Database;
using DevHabit.Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DevHabit.Api.Services;


public sealed class UserContext(
    IHttpContextAccessor httpContextAccessor,
    ApplicationDbContext dbContext,
    IMemoryCache memoryCache)
{
    //创建缓存前缀和缓存过期时间
    private const string CacheKeyPrefix = "users:id:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    
    public async Task<string?> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        //通过自定义的扩展方法获取当前用户的IdentityId
        string? identityId = httpContextAccessor.HttpContext?.User.GetIdentityId();
        //无用户信息则返回null
        if (identityId is null)
        {
            return null;
        }
        //生成缓存的key
        string cacheKey = CacheKeyPrefix + identityId;

        //使用内存缓存来缓存用户Id,
        string? userId = await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            //如果 30 分钟内有访问，则重新计时，避免频繁失效。
            entry.SetSlidingExpiration(CacheDuration);

            string? userId = await dbContext.Users
                .Where(u => u.IdentityId == identityId)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return userId;
        });

        return userId;
    }
}
