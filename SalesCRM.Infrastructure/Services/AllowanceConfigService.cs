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
        var configs = await _uow.AllowanceConfigs.Query()
            .Include(a => a.SetBy)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        // For User-scoped rows resolve the role + display name; for Zone/Region resolve the area name.
        var userIds = configs.Where(c => c.Scope == AllowanceScope.User && c.ScopeId.HasValue).Select(c => c.ScopeId!.Value).Distinct().ToList();
        var zoneIds = configs.Where(c => c.Scope == AllowanceScope.Zone && c.ScopeId.HasValue).Select(c => c.ScopeId!.Value).Distinct().ToList();
        var regionIds = configs.Where(c => c.Scope == AllowanceScope.Region && c.ScopeId.HasValue).Select(c => c.ScopeId!.Value).Distinct().ToList();

        var userMap = new Dictionary<int, (string Name, string Role)>();
        if (userIds.Count > 0)
        {
            var users = await _uow.Users.Query().Where(u => userIds.Contains(u.Id)).ToListAsync();
            foreach (var u in users) userMap[u.Id] = (u.Name, u.Role.ToString());
        }

        var zoneMap = new Dictionary<int, string>();
        if (zoneIds.Count > 0)
        {
            var zones = await _uow.Zones.Query().Where(z => zoneIds.Contains(z.Id)).ToListAsync();
            foreach (var z in zones) zoneMap[z.Id] = z.Name;
        }

        var regionMap = new Dictionary<int, string>();
        if (regionIds.Count > 0)
        {
            var regions = await _uow.Regions.Query().Where(r => regionIds.Contains(r.Id)).ToListAsync();
            foreach (var r in regions) regionMap[r.Id] = r.Name;
        }

        return configs.Select(a =>
        {
            string? scopeName = null;
            string? targetRole = null;
            if (a.Scope == AllowanceScope.User && a.ScopeId.HasValue && userMap.TryGetValue(a.ScopeId.Value, out var u))
            {
                scopeName = u.Name;
                targetRole = u.Role;
            }
            else if (a.Scope == AllowanceScope.Zone && a.ScopeId.HasValue && zoneMap.TryGetValue(a.ScopeId.Value, out var zn))
                scopeName = zn;
            else if (a.Scope == AllowanceScope.Region && a.ScopeId.HasValue && regionMap.TryGetValue(a.ScopeId.Value, out var rn))
                scopeName = rn;
            else if (a.Scope == AllowanceScope.Role && a.ScopeId.HasValue && Enum.IsDefined(typeof(UserRole), a.ScopeId.Value))
            {
                var roleEnum = (UserRole)a.ScopeId.Value;
                scopeName = roleEnum.ToString();
                targetRole = roleEnum.ToString();
            }

            return new AllowanceConfigDto
            {
                Id = a.Id, Scope = a.Scope.ToString(), ScopeId = a.ScopeId, ScopeName = scopeName, TargetRole = targetRole,
                VehicleType = a.VehicleType?.ToString(),
                RatePerKm = a.RatePerKm, MaxDailyAllowance = a.MaxDailyAllowance,
                MinDistanceForAllowance = a.MinDistanceForAllowance,
                EffectiveFrom = a.EffectiveFrom, EffectiveTo = a.EffectiveTo,
                SetByName = a.SetBy?.Name, CreatedAt = a.CreatedAt
            };
        }).ToList();
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

    public async Task<AllowanceConfigDto?> UpdateConfigAsync(int id, UpdateAllowanceConfigRequest request)
    {
        var config = await _uow.AllowanceConfigs.GetByIdAsync(id);
        if (config == null) return null;

        if (request.RatePerKm.HasValue) config.RatePerKm = request.RatePerKm.Value;
        if (request.MaxDailyAllowance.HasValue) config.MaxDailyAllowance = request.MaxDailyAllowance;
        if (request.MinDistanceForAllowance.HasValue) config.MinDistanceForAllowance = request.MinDistanceForAllowance;
        if (!string.IsNullOrEmpty(request.VehicleType) && Enum.TryParse<VehicleType>(request.VehicleType, true, out var vt))
            config.VehicleType = vt;
        if (request.EffectiveFrom.HasValue)
            config.EffectiveFrom = DateTime.SpecifyKind(request.EffectiveFrom.Value, DateTimeKind.Utc);
        if (request.EffectiveTo.HasValue)
            config.EffectiveTo = DateTime.SpecifyKind(request.EffectiveTo.Value, DateTimeKind.Utc);
        config.UpdatedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync();

        var configs = await GetAllConfigsAsync();
        return configs.FirstOrDefault(c => c.Id == id);
    }

    public async Task<bool> DeleteConfigAsync(int id)
    {
        var config = await _uow.AllowanceConfigs.GetByIdAsync(id);
        if (config == null) return false;
        await _uow.AllowanceConfigs.DeleteAsync(config);
        await _uow.SaveChangesAsync();
        return true;
    }

    private const decimal DefaultRatePerKm = 10m; // Hard floor: shown when nothing else is configured

    public async Task<ResolvedAllowanceDto> ResolveForUserAsync(int userId)
    {
        var user = await _uow.Users.Query()
            .Include(u => u.Zone).Include(u => u.Region)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new ResolvedAllowanceDto { RatePerKm = DefaultRatePerKm, ResolvedFrom = "Default" };

        var now = DateTime.UtcNow;
        var configs = await _uow.AllowanceConfigs.Query()
            .Where(a => a.EffectiveFrom <= now && (a.EffectiveTo == null || a.EffectiveTo >= now))
            .ToListAsync();

        // Resolution order: User > Role > Zone > Region > Global
        var userConfig = configs.FirstOrDefault(c => c.Scope == AllowanceScope.User && c.ScopeId == userId);
        if (userConfig != null) return Resolve(userConfig, "User");

        var roleConfig = configs.FirstOrDefault(c => c.Scope == AllowanceScope.Role && c.ScopeId == (int)user.Role);
        if (roleConfig != null) return Resolve(roleConfig, "Role");

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

        // Final fallback: the user's static field, or the hard floor if it's 0/missing — never blank.
        var fallbackRate = user.TravelAllowanceRate > 0 ? user.TravelAllowanceRate : DefaultRatePerKm;
        return new ResolvedAllowanceDto { RatePerKm = fallbackRate, ResolvedFrom = "UserDefault" };
    }

    private static ResolvedAllowanceDto Resolve(AllowanceConfig c, string from) => new()
    {
        RatePerKm = c.RatePerKm, MaxDailyAllowance = c.MaxDailyAllowance,
        MinDistanceForAllowance = c.MinDistanceForAllowance, ResolvedFrom = from
    };
}
