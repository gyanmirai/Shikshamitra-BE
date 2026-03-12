using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MutationResponse/MessageResponse if reused, otherwise standard DTO

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ConcessionType;

public interface IConcessionTypeAppService : IApplicationService
{
    Task<MutationResponse> CreateAsync(Guid academicYearId, ConcessionTypeCreateRequest input);
    Task<ConcessionTypePaginatedResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
    Task<ConcessionTypeResponse> GetAsync(Guid id);
    Task<MutationResponse> UpdateAsync(Guid id, ConcessionTypeUpdateRequest input);
    Task<MutationResponse> DeleteAsync(Guid id);
}

// Defining MutationResponse locally if not available commonly,
// but it seems used in other places. I'll define it here if needed or assume it exists.
// Based on previous tasks, it seems standard.
// Just in case, I'll add a simple definition or reuse if I find where it is.
// Actually, I saw it in the spec. I'll define it in Responses if not already there or use a common one.
// Let's check FeeCategory DTOs for MessageResponse/MutationResponse availability.
