using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeStructureMetadataAppService : IApplicationService
{
	Task<FeeStructureMetadataListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, string? filters = null);
	Task GenerateFeeStructuresAsync(Guid academicYearId);
}
