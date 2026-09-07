using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Domain.Finance.Policies;

public interface ICollectionPolicyAppService : IApplicationService
{
	Task<PagedResultDto<CollectionPolicyDto>> GetListAsync(CollectionPolicyListRequestDto input);
	Task<CollectionPolicyDto> GetAsync(Guid id);
	Task<CollectionPolicyDto> CreateAsync(CreateUpdateCollectionPolicyDto input);
	Task<CollectionPolicyDto> UpdateAsync(Guid id, CreateUpdateCollectionPolicyDto input);
	Task<CollectionPolicyDto> CloneAsync(Guid id, CollectionPolicyCloneRequestDto input);
	Task ArchiveAsync(Guid id);
	Task<CollectionPolicyDto> UnarchiveAsync(Guid id);
	Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, FinancePolicyVersionListRequestDto input);
	Task<CollectionPolicySimulateResponseDto> SimulateAsync(Guid id, CollectionPolicySimulateRequestDto input);
}

public interface IConcessionPolicyAppService : IApplicationService
{
	Task<PagedResultDto<ConcessionPolicyDto>> GetListAsync(ConcessionPolicyListRequestDto input);
	Task<ConcessionPolicyDto> GetAsync(Guid id);
	Task<ConcessionPolicyDto> CreateAsync(CreateUpdateConcessionPolicyDto input);
	Task<ConcessionPolicyDto> UpdateAsync(Guid id, CreateUpdateConcessionPolicyDto input);
	Task<ConcessionPolicyDto> CloneAsync(Guid id, ConcessionPolicyCloneRequestDto input);
	Task ArchiveAsync(Guid id);
	Task<ConcessionPolicyDto> UnarchiveAsync(Guid id);
	Task<PagedResultDto<FinancePolicyVersionDto>> GetVersionsAsync(Guid id, FinancePolicyVersionListRequestDto input);
	Task<ConcessionPolicySimulateResponseDto> SimulateAsync(Guid id, ConcessionPolicySimulateRequestDto input);
}

public interface IStudentFeePolicyOverrideAppService : IApplicationService
{
	Task<EffectiveCollectionRulesDto> GetEffectiveRulesAsync(Guid studentId, Guid feeStructureId);
	Task<StudentFeePolicyOverrideDto?> GetOverrideAsync(Guid studentId, Guid feeStructureId);
	Task<StudentFeePolicyOverrideDto> SetOverrideAsync(Guid studentId, Guid feeStructureId, CreateUpdateStudentFeePolicyOverrideDto input);
}

public interface ICollectionPolicyExceptionWindowAppService : IApplicationService
{
	Task<CollectionPolicyExceptionWindowDto> CreateAsync(CreateCollectionPolicyExceptionWindowDto input);
	Task<List<CollectionPolicyExceptionWindowDto>> GetListAsync(CollectionPolicyExceptionWindowListRequestDto input);
	Task DeleteAsync(Guid id);
}

public interface IBulkWaiverRequestAppService : IApplicationService
{
	Task<BulkWaiverPreviewResponseDto> PreviewAsync(BulkWaiverPreviewRequestDto input);
	Task<BulkWaiverRequestDto> CreateAsync(CreateBulkWaiverRequestDto input);
	Task<List<BulkWaiverRequestDto>> GetListAsync(Guid academicYearId);
	Task<BulkWaiverRequestDto> ApproveAsync(Guid id);
	Task<BulkWaiverRequestDto> RejectAsync(Guid id);
	Task<BulkWaiverApplyResultDto> ApplyAsync(Guid id);
	Task<List<BulkWaiverRollupItemDto>> GetRollupAsync(Guid id);
}
