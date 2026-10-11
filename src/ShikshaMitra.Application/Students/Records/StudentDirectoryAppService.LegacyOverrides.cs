using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public partial class StudentDirectoryAppService
{
	public async Task<PagedResultDto<StudentLegacyOverrideDto>> GetLegacyOverridesAsync(PagedResultRequestDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request))
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve);
		EnsureTenant();
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100) throw new BusinessException("Students:Records:InvalidDirectoryQuery");
		var studentQuery = await enrollmentScope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var query = (await overrides.GetQueryableAsync()).Where(o => o.TenantId == CurrentTenant.Id && studentQuery.Any(s => s.Id == o.StudentId));
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync((from o in query
			join s in studentQuery on o.StudentId equals s.Id
			orderby o.CreationTime descending, o.Id
			select new { Item = o, s.AcademicYearId }).Skip(input.SkipCount).Take(input.MaxResultCount));
		var roles = await access.GetDatabaseRoleNamesAsync();
		var masks = new Dictionary<Guid, bool>();
		var result = new List<StudentLegacyOverrideDto>();
		foreach (var row in rows)
		{
			var key = row.AcademicYearId ?? Guid.Empty;
			if (!masks.TryGetValue(key, out var masked)) masks[key] = masked = (await policies.ResolveAsync(row.AcademicYearId)).Policy.IsMasked("Identity", roles);
			var dto = MapOverride(row.Item);
			if (masked) { dto.Reason = "Restricted"; dto.EvidenceReference = null; dto.DecisionRemarks = null; }
			result.Add(dto);
		}
		return new(total, result);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request)]
	[UnitOfWork]
	public async Task<StudentLegacyOverrideDto> RequestLegacyOverrideAsync(Guid studentId, RequestStudentLegacyOverrideDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Request);
		EnsureTenant();
		if (!CurrentUser.Id.HasValue) throw new BusinessException("Students:Records:UserRequired");
		if (!await access.IsEligibleHistoricalStudentAsync(studentId)) throw new BusinessException("Students:Records:LegacyStudentNotEligible");
		if (await overrides.AnyAsync(o => o.TenantId == CurrentTenant.Id && o.StudentId == studentId && (o.Status == "Pending" || o.Status == "Approved")))
			throw new BusinessException("Students:Records:LegacyOverrideExists");
		var request = new StudentRecordLegacyOverride(GuidGenerator.Create(), CurrentTenant.Id!.Value, studentId, CurrentUser.Id.Value, input.Reason, input.EvidenceReference);
		return MapOverride(await overrides.InsertAsync(request, autoSave: true));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve)]
	[UnitOfWork]
	public Task<StudentLegacyOverrideDto> ApproveLegacyOverrideAsync(Guid id, DecideStudentLegacyOverrideDto input) => DecideOverrideAsync(id, input, true);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve)]
	[UnitOfWork]
	public Task<StudentLegacyOverrideDto> RejectLegacyOverrideAsync(Guid id, DecideStudentLegacyOverrideDto input) => DecideOverrideAsync(id, input, false);

	private async Task<StudentLegacyOverrideDto> DecideOverrideAsync(Guid id, DecideStudentLegacyOverrideDto input, bool approve)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Overrides.Approve);
		EnsureTenant();
		if (!CurrentUser.Id.HasValue) throw new BusinessException("Students:Records:UserRequired");
		var request = await AsyncExecuter.FirstOrDefaultAsync((await overrides.GetQueryableAsync()).Where(o => o.Id == id && o.TenantId == CurrentTenant.Id))
			?? throw new EntityNotFoundException(typeof(StudentRecordLegacyOverride), id);
		await access.GetScopedStudentAsync(request.StudentId);
		if (request.RequestedByUserId == CurrentUser.Id) throw new BusinessException("Students:Records:SelfApprovalDenied");
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != request.ConcurrencyStamp)
			throw new BusinessException("Students:Records:LegacyOverrideConcurrencyConflict");
		if (approve && !await access.IsEligibleHistoricalStudentAsync(request.StudentId)) throw new BusinessException("Students:Records:LegacyStudentNotEligible");
		request.Decide(CurrentUser.Id.Value, approve, input.Remarks, Clock.Now);
		return MapOverride(await overrides.UpdateAsync(request, autoSave: true));
	}

	private static StudentLegacyOverrideDto MapOverride(StudentRecordLegacyOverride item) => new()
	{
		Id = item.Id, StudentId = item.StudentId, RequestedByUserId = item.RequestedByUserId, DecidedByUserId = item.DecidedByUserId,
		Reason = item.Reason, EvidenceReference = item.EvidenceReference, DecisionRemarks = item.DecisionRemarks,
		Status = item.Status, CreationTime = item.CreationTime, DecidedAt = item.DecidedAt, ConcurrencyStamp = item.ConcurrencyStamp
	};
}
