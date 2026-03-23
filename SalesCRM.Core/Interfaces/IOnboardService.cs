using SalesCRM.Core.DTOs.Onboarding;

namespace SalesCRM.Core.Interfaces;

public interface IOnboardService
{
    Task<(List<OnboardAssignmentDto> Items, int Total)> GetOnboardingsAsync(string? status, int? assignedToId, int page, int limit);
    Task<OnboardAssignmentDto?> GetOnboardingByIdAsync(int id);
    Task<OnboardAssignmentDto> CreateOnboardingAsync(CreateOnboardRequest request, int assignedById);
    Task<OnboardAssignmentDto?> UpdateOnboardingAsync(int id, UpdateOnboardRequest request);
}
