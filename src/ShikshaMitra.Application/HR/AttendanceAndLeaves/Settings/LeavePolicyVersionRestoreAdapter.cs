using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyVersionRestoreAdapter(IRepository<AcademicYear, Guid> years,
	IRepository<LeavePolicy, Guid> policies, LeavePolicyManager manager,
	LeavePolicyLifecycle lifecycle, LeavePolicyCacheService cache) : ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyDetailResponse> RestoreAsync(HrPolicyVersion version, HrPolicyRestoreDraftRequestDto input)
	{
		if (version.TenantId != CurrentTenant.Id || version.PolicyType != "leave")
			throw new BusinessException(HRErrorCodes.HrPolicyVersionNotFound);
		LeavePolicySnapshotDto snapshot;
		try
		{
			snapshot = JsonSerializer.Deserialize<LeavePolicySnapshotDto>(version.SnapshotJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
				?? throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
		}
		catch (JsonException ex) { throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot, innerException: ex); }
		if (snapshot.SchemaVersion != 1 || snapshot.Id != version.PolicyId || snapshot.Version != version.VersionNumber ||
			snapshot.AcademicYearId != version.AcademicYearId || snapshot.Rules == null)
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
		var year = await years.GetAsync(version.AcademicYearId);
		if (year.TenantId != CurrentTenant.Id) throw new EntityNotFoundException(typeof(AcademicYear), year.Id);
		var request = new LeavePolicyCreateRequest
		{
			PolicyTitle = string.IsNullOrWhiteSpace(input.Name) ? $"{snapshot.PolicyTitle} v{snapshot.Version} Restore Draft" : input.Name.Trim(),
			Status = "Draft", IsDefault = false, Description = snapshot.Description,
			EffectiveFrom = input.EffectiveFrom?.Date ?? snapshot.EffectiveFrom, EffectiveTo = snapshot.EffectiveTo,
			Rules = snapshot.Rules.Where(x => x.IsActive).ToList(), ChangeReason = input.ChangeReason
		};
		if (!(await new LeavePolicyCreateRequestValidator().ValidateAsync(request)).IsValid)
			throw new BusinessException(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
		var context = await lifecycle.BeginAsync(null, year.Id);
		var draft = await manager.CreateAsync(year.Id, request.PolicyTitle, request.EffectiveFrom, request.EffectiveTo,
			HrPolicyStatus.Draft, false, request.Rules.Select(LeavePolicyProjection.ToRuleInput), request.Description);
		draft.SetProperty("SourceVersionId", version.Id);
		draft.SetProperty("SourcePolicyId", version.PolicyId);
		draft.SetProperty("SourceVersionNumber", version.VersionNumber);
		await policies.InsertAsync(draft, autoSave: true);
		await lifecycle.CompleteAsync(context, draft, "restore-draft", input.ChangeReason);
		await cache.ClearContractAsync(year.Id);
		return LeavePolicyProjection.ToDetail(draft);
	}
}
