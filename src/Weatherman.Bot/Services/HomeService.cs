using System.Text.Json;
using Weatherman.Bot.Data;
using Weatherman.Bot.Data.Models;
using Weatherman.Bot.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Weatherman.Bot.Services;

public class HomeService
{
    private readonly DbContextHelper _dbContextHelper;
    private readonly IFusionCache _cache;

    private const string _cacheKeyPrefix = "userhomev2";

    private readonly TimeSpan _userHomeCacheExpiration = TimeSpan.FromHours(1);

    public HomeService(DbContextHelper dbContextHelper, IFusionCache cache)
    {
        _dbContextHelper = dbContextHelper;
        _cache = cache;
    }

    public async Task SetHomeAsync(ulong userId, LocationDetails location)
    {
        var homeLocation = JsonSerializer.Serialize(location);

        using var dbContext = _dbContextHelper.GetDbContext();
        var userProfile = await dbContext.UserProfiles.FindAsync(userId.ToString());
        if (userProfile != null)
        {
            userProfile.HomeLocation = homeLocation;
            userProfile.HomeLocationChangedDate = DateTime.UtcNow;

            dbContext.Update(userProfile);
            await dbContext.SaveChangesAsync();
        }
        else
        {
            userProfile = new UserProfile
            {
                Id = userId.ToString(),
                HomeLocation = homeLocation,
                HomeLocationChangedDate = DateTime.UtcNow
            };

            dbContext.Add(userProfile);
            await dbContext.SaveChangesAsync();
        }

        var cacheKey = $"{_cacheKeyPrefix}-{userId}";
        await _cache.RemoveAsync(cacheKey);
    }

    public async Task<LocationDetails> GetHomeAsync(ulong userId)
    {
        return await _cache.GetOrSetAsync(
            $"{_cacheKeyPrefix}-{userId}",
            _ => GetHomeFromDbAsync(userId),
            _userHomeCacheExpiration);
    }

    private async Task<LocationDetails> GetHomeFromDbAsync(ulong userId)
    {
        using var dbContext = _dbContextHelper.GetDbContext();
        var userProfile = await dbContext.UserProfiles.FindAsync(userId.ToString());
        if (userProfile == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<LocationDetails>(userProfile.HomeLocation);
    }

    public async Task RemoveHomeAsync(ulong userId)
    {
        using var dbContext = _dbContextHelper.GetDbContext();
        var userProfile = await dbContext.UserProfiles.FindAsync(userId.ToString());
        if (userProfile == null)
        {
            return;
        }

        var cacheKey = $"{_cacheKeyPrefix}-{userId}";
        await _cache.RemoveAsync(cacheKey);

        dbContext.Remove(userProfile);
        await dbContext.SaveChangesAsync();
    }
}
