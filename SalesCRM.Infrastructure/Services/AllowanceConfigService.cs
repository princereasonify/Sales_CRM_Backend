using Microsoft.EntityFrameworkCore;
using SalesCRM.Core.DTOs.Allowance;
using SalesCRM.Core.Entities;
using SalesCRM.Core.Enums;
using SalesCRM.Core.Interfaces;

namespace SalesCRM.Infrastructure.Services;

public class AllowanceConfigService : IAllowanceConfigService
{
    private readonly IUnitOfWork _uow;
    public AllowanceConfigService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<AllowanceConfigDto>> GetAllConfigsAsync()
    {
        return await _uow.AllowanceConfigs.Query()
            .Include(a => a.SetBy)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AllowanceConfigDto
            {
                Id = a.Id, Scope = a.Scope.ToString(), ScopeId = a.ScopeId,
                VehicleType = a.VehicleType.HasValue ? a.VehicleType.Value.ToString() : null,
                RatePerKm = a.RatePerKm, MaxDailyAllowance = a.MaxDailyAllowance,
                MinDistanceForAllowance = a.MinDistanceForAllowance,
                EffectiveFrom = a.EffectiveFrom, EffectiveTo = a.EffectiveTo,
                SetByName = a.SetBy.Name, CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<AllowanceConfigDto> CreateConfigAsync(CreateAllowanceConfigRequest request, int setById)
    {
        Enum.TryParse<AllowanceScope>(request.Scope, true, out var scope);
        VehicleType? vehicleType = null;
        if (!string.IsNullOrEmpty(request.VehicleType) && Enum.TryParse<VehicleType>(request.VehicleType, true, out var vt))
            vehicleType = vt;

        var config = new AllowanceConfig
        {
            Scope = scope, ScopeId = request.ScopeId, VehicleType = vehicleType, RatePerKm = request.RatePerKm,
            MaxDailyAllowance = request.MaxDailyAllowance,
            MinDistanceForAllowance = request.MinDistanceForAllowance,
            EffectiveFrom = DateTime.SpecifyKind(request.EffectiveFrom, DateTimeKind.Utc),
            EffectiveTo = request.EffectiveTo.HasValue ? DateTime.SpecifyKind(request.EffectiveTo.Value, DateTimeKind.Utc) : null,
            SetById = setById
        };
        await _uow.AllowanceConfigs.AddAsync(config);
        await _uow.SaveChangesAsync();

        return new AllowanceConfigDto
        {
            Id = config.Id, Scope = config.Scope.ToString(), ScopeId = config.ScopeId,
            VehicleType = config.VehicleType?.ToString(),
            RatePerKm = config.RatePerKm, MaxDailyAllowance = config.MaxDailyAllowance,
            MinDistanceForAllowance = config.MinDistanceForAllowance,
            EffectiveFrom = config.EffectiveFrom, EffectiveTo = config.EffectiveTo,
            CreatedAt = config.CreatedAt
        };
    }

    public async Task<ResolvedAllowanceDto> ResolveForUserAsync(int userId)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new ResolvedAllowanceDto { RatePerKm = 10, ResolvedFrom = "Default" };

        var now = DateTime.UtcNow;
        var configs = await _uow.AllowanceConfigs.Query()
            .Where(a => a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo >= now))
            .ToListAsync();

        // Resolution order: User > Zone > Region > Global
        var userConfig = configs.FirstOrDefault(c => c.Scope == AllowanceScope.User && c.ScopeId == userId);
        if (userConfig != null) return Resolve(userConfig, "User");

        if (user.ZoneId.HasValue)
        {
            var zoneConfig = configs.FirstOrDefault(c => c.Scope == AllowanceScope.Zone && c.ScopeId == user.ZoneId);
            if (zoneConfig != null) return Resolve(zoneConfig, "Zone");
        }

        if (user.RegionId.HasValue)
        {
            var regionConfig = configs.FirstOrDefault(c => c.Scope == AllowanceScope.Region && c.ScopeId == user.RegionId);
            if (regionConfig != null) return Resolve(regionConfig, "Region");
        }

        var globalConfig = configs.FirstOrDefault(c => c.Scope == AllowanceScope.Global);
        if (globalConfig != null) return Resolve(globalConfig, "Global");

        return new ResolvedAllowanceDto { RatePerKm = user.TravelAllowanceRate, ResolvedFrom = "UserDefault" };
    }

    private static ResolvedAllowanceDto Resolve(AllowanceConfig c, string from) => new()
    {
        RatePerKm = c.RatePerKm, MaxDailyAllowance = c.MaxDailyAllowance,
        MinDistanceForAllowance = c.MinDistanceForAllowance, ResolvedFrom = from
    };
}
