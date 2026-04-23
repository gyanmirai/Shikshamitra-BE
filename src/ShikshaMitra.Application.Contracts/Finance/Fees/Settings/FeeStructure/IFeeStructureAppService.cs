using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.Fees.Settings.FeeCategory.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructure.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructure;

public interface IFeeStructureAppService : IApplicationService
{
	Task<FeeStructureDetailsResponse> CreateAsync(Guid academicYearId, FeeStructureCreateRequest input);
	Task<FeeStructureListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null);
	Task<MessageResponse> UpdateAsync(Guid feeStructureId, FeeStructureUpdateRequest input);
	Task UpdateStatusAsync(Guid feeStructureId, FeeStructureStatusUpdateRequest input);
	Task SaveStandardFeeStructuresAsync(Guid feeStructureId, StandardFeeStructureSaveRequest input);
	Task<StandardFeeStructureResponse> GetStandardFeeStructureAsync(Guid standardId);
}
