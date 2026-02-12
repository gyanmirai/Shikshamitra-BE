using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse

namespace ShikshaMitra.Application.Contracts.Finance.Setting.FeeStructure;

public interface IFeeStructureAppService : IApplicationService
{
	Task<FeeStructureDetailsResponse> CreateAsync(Guid academicYearId, FeeStructureCreateRequest input);
	Task<FeeStructureListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
	Task<FeeStructureDetailsResponse> GetAsync(Guid feeStructureId);
	Task<MessageResponse> UpdateAsync(Guid feeStructureId, FeeStructureUpdateRequest input);
	Task<MessageResponse> UpdateStatusAsync(Guid feeStructureId, FeeStructureStatusUpdateRequest input);
	Task<MessageResponse> SaveStandardFeeStructuresAsync(Guid feeStructureId, List<StandardFeeStructureSaveRequest> input);
	Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid feeStructureId, Guid standardId);
}
