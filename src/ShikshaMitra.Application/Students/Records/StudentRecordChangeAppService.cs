using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Data;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
public partial class StudentRecordChangeAppService(
	IRepository<StudentRecordChange, Guid> changes,
	IRepository<StudentRecordChangeDecision, Guid> decisions,
	IStudentRepository students, StudentRecordsEnrollmentScopeService recordScope, StudentRecordsAccessService access,
	StudentRecordsPolicyResolver policies, StudentRecordPayloadService payloads,
	StudentRecordChangeApplication application, IPermissionChecker permissions, IRepository<IdentityUser, Guid> actors)
	: ApplicationService, IStudentRecordChangeAppService
{
	public async Task<PagedResultDto<StudentRecordChangeDto>> GetListAsync(StudentRecordChangeQueryDto input)
	{
		var studentQuery = await recordScope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		studentQuery = studentQuery.Where(s => (!input.BoardId.HasValue || s.AcademicBoardId == input.BoardId) && (!input.MediumId.HasValue || s.AcademicMediumId == input.MediumId) && (!input.StandardId.HasValue || s.StandardId == input.StandardId) && (!input.DivisionId.HasValue || s.DivisionId == input.DivisionId));
		var query = (await changes.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && studentQuery.Any(s => s.Id == x.StudentId));
		if (input.StudentId.HasValue) query = query.Where(x => x.StudentId == input.StudentId);
		if (input.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrEmpty(input.Status)) query = query.Where(x => x.Status == input.Status);
		if (input.ApprovableOnly) query = await FilterApprovableAsync(query);
		var count = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var result = new List<StudentRecordChangeDto>();
		var roles = await access.GetDatabaseRoleNamesAsync();
		var studentIds = items.Select(x => x.StudentId).Distinct().ToArray();
		var pageStudents = (await AsyncExecuter.ToListAsync(studentQuery.Where(x => Enumerable.Contains(studentIds, x.Id)))).ToDictionary(x => x.Id);
		var itemIds = items.Select(x => x.Id).ToArray();
		var pageDecisions = await decisions.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(itemIds, x.ChangeId));
		var yearPolicies = new Dictionary<Guid, StudentRecordsPolicy>();
		foreach (var year in items.Select(x => x.AcademicYearId).Distinct()) yearPolicies[year] = (await policies.ResolveAsync(year)).Policy;
		var canApprove = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve);
		var canCancel = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Cancel);
		var actorNames = await GetActorNamesAsync(items.Select(x => x.RequestedBy).Concat(pageDecisions.Select(x => x.ActorId)).Distinct().ToArray());
		foreach (var item in items) result.Add(await MapAsync(item, includePayload: false, pageStudents[item.StudentId],
			yearPolicies[item.AcademicYearId], roles, pageDecisions.Where(x => x.ChangeId == item.Id).ToList(), canApprove, canCancel, actorNames));
		return new(count, result);
	}
	public async Task<StudentRecordChangeDto> GetAsync(Guid id) => await MapAsync(await GetScopedAsync(id), true);
	public async Task<StudentRecordSectionSnapshotDto> GetSnapshotAsync(Guid studentId, string section)
	{
		var allowed = await access.EnsureWritableAsync(studentId, section == "placement");
		await EnsureVisibleAsync(allowed.Policy, section);
		var json = await payloads.CaptureAsync(studentId, section);
		return new() { Section = section, Snapshot = StudentRecordPayloadService.Hash(json), Data = JsonSerializer.Deserialize<JsonElement>(json) };
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	public Task<StudentRecordChangeDto> CreateAsync(CreateStudentRecordChangeDto input) => CreateCoreAsync(input, false);

	/// <summary>Uses the same review validation and policy while joining admission finalization.
	/// Approved requests are picked up by the existing Records scheduling worker after commit.</summary>
	internal async Task<StudentRecordChangeDto> CreateAdmissionReviewAsync(CreateStudentRecordChangeDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
		if (CurrentUnitOfWork == null) throw new BusinessException("Students:Records:TransactionRequired");
		return await CreateCoreAsync(input, true);
	}

	private async Task<StudentRecordChangeDto> CreateCoreAsync(CreateStudentRecordChangeDto input, bool joinAdmissionWork)
	{
		try { return await CreateCoreInternalAsync(input, joinAdmissionWork); }
		catch (Exception exception) when (input.Section == "photo" && StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{ throw new BusinessException("Students:Records:ConcurrencyConflict"); }
	}

	private async Task<StudentRecordChangeDto> CreateCoreInternalAsync(CreateStudentRecordChangeDto input, bool joinAdmissionWork)
	{
		Guid id;
		using (var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true,
			IsolationLevel = input.Section == "photo" ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable }, requiresNew: !joinAdmissionWork))
		{
			if (input.Section == "photo") await LazyServiceProvider.LazyGetRequiredService<IStudentPhotoChangeLock>().AcquireAsync(CurrentTenant.Id, input.StudentId);
			var allowed = await access.EnsureWritableAsync(input.StudentId, input.Section == "placement");
			await EnsureVisibleAsync(allowed.Policy, input.Section);
			var existing = await changes.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == input.StudentId && x.ClientKey == input.ClientKey);
			var rawHash = input.Section == "custom-details" ? StudentRecordPayloadService.Hash(input.Payload.GetRawText()) : null;
			if (input.Section == "custom-details" && await payloads.IsCustomMaskedAsync(input.StudentId))
				throw new BusinessException("Students:Records:RestrictedFields");
			var json = existing != null && input.Section == "custom-details" && existing.GetProperty<string>("CustomDetailsRequestHash") == rawHash
				? StudentRecordPayloadService.Canonical(existing.PayloadJson)
				: await payloads.ValidateAndNormalizeAsync(input.Section, input.Payload, input.StudentId);
			if (existing != null)
			{
				if (input.Section == "photo" && existing.RequestedBy != CurrentUser.Id ||
					existing.Section != input.Section || StudentRecordPayloadService.Canonical(existing.PayloadJson) != json ||
					existing.CorrectsChangeId != input.CorrectsChangeId || existing.Reason != input.Reason.Trim() ||
					(input.EffectiveAt.HasValue && existing.EffectiveAt != input.EffectiveAt.Value.ToUniversalTime()))
					throw new BusinessException("Students:Records:IdempotencyConflict");
				id = existing.Id;
				await work.CompleteAsync();
			}
			else
			{
			if (input.Section == "photo" && await changes.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == input.StudentId && x.Section == "photo" &&
				(x.Status == "Pending" || x.Status == "Approved" || x.Status == "Scheduled"))) throw new BusinessException("Students:Records:PhotoChangePending");
			await payloads.ValidateImpactAsync(input.StudentId, input.Section, json);
			if (input.Section == "photo") await payloads.EnsurePhotoDraftAsync(input.StudentId, CurrentUser.GetId(), json);
			var before = await payloads.CaptureAsync(input.StudentId, input.Section);
			EnsureSectionBoundaries(input.Section, before, json);
			if (StudentRecordPayloadService.Hash(before) != input.ExpectedSnapshot)
				throw new BusinessException("Students:Records:StaleSnapshot");
			if (StudentRecordPayloadService.Canonical(before) == json) throw new BusinessException("Students:Records:NoChanges");
			var year = allowed.Student.AcademicYearId ?? throw new BusinessException("Students:Records:EnrollmentMissing");
			var snapshot = await policies.ResolveAsync(year);
			var now = new DateTimeOffset(Clock.Now.ToUniversalTime());
			var effective = input.EffectiveAt?.ToUniversalTime() ?? now;
			ValidateEffectiveDate(effective, now, snapshot.Policy.Transitions);
			var action = StudentRecordPayloadService.ActionFor(input.Section);
			if (input.CorrectsChangeId.HasValue)
			{
				var original = await GetScopedAsync(input.CorrectsChangeId.Value);
				if (original.StudentId != input.StudentId || original.Section != input.Section || original.Status != "Applied")
					throw new BusinessException("Students:Records:InvalidCorrection");
				action = "Correction";
			}
			var rule = snapshot.Policy.Changes[action];
			var effectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(effective.UtcDateTime);
			var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(now.UtcDateTime);
			var approval = rule.RequiresApproval(effectiveDate < today,
				effectiveDate > today, allowed.Student.Status == StudentLifeCycleStatusEnum.Left);
			var stages = approval ? rule.Stages : [];
			var item = new StudentRecordChange(GuidGenerator.Create(), CurrentTenant.Id, input.StudentId, year, CurrentUser.GetId(),
				input.Section, action, input.ClientKey, input.Reason, before, json, snapshot.Policy.ToJson(), snapshot.VersionId,
				JsonSerializer.Serialize(stages, StudentRecordPayloadService.JsonOptions), stages.Count, effective, input.CorrectsChangeId);
			if (rawHash != null) item.SetProperty("CustomDetailsRequestHash", rawHash);
			await changes.InsertAsync(item);
			id = item.Id;
			await work.CompleteAsync();
			}
		}
		if (joinAdmissionWork) await CurrentUnitOfWork!.SaveChangesAsync();
		else await application.ApplyScheduledAsync(id);
		return await GetAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)]
	public async Task<StudentRecordChangeDto> ApproveAsync(Guid id, StudentRecordDecisionDto input)
	{
		await DecideAsync(id, input, approve: true);
		await application.ApplyOrScheduleAsync(id);
		return await GetAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve)]
	public async Task<StudentRecordChangeDto> RejectAsync(Guid id, StudentRecordDecisionDto input)
	{
		await DecideAsync(id, input, approve: false);
		return await GetAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Cancel)]
	public async Task<StudentRecordChangeDto> CancelAsync(Guid id, StudentRecordDecisionDto input)
	{
		var item = await GetScopedAsync(id);
		EnsureStamp(item, input.ConcurrencyStamp);
		item.Cancel(CurrentUser.GetId());
		await changes.UpdateAsync(item, autoSave: true);
		return await MapAsync(item, true);
	}
	private async Task DecideAsync(Guid id, StudentRecordDecisionDto input, bool approve)
	{
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetScopedAsync(id);
		EnsureStamp(item, input.ConcurrencyStamp);
		await EnsureVisibleAsync((await policies.ResolveAsync(item.AcademicYearId)).Policy, item.Section);
		if (item.Section == "custom-details" && await payloads.IsCustomMaskedAsync(item.StudentId))
			throw new BusinessException("Students:Records:RestrictedFields");
		if (!await IsStageApproverAsync(item)) throw new BusinessException("Students:Records:ApproverRequired");
		var stage = item.ApprovalCount;
		if (approve) item.Approve(CurrentUser.GetId()); else item.Reject(CurrentUser.GetId());
		await decisions.InsertAsync(new StudentRecordChangeDecision(GuidGenerator.Create(), CurrentTenant.Id,
			item.Id, CurrentUser.GetId(), stage, approve ? "Approved" : "Rejected", input.Reason));
		await changes.UpdateAsync(item);
		await work.CompleteAsync();
	}
	private async Task<StudentRecordChange> GetScopedAsync(Guid id)
	{
		var item = await changes.FindAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id)
			?? throw new EntityNotFoundException(typeof(StudentRecordChange), id);
		await access.EnsureReadableAsync(item.StudentId);
		return item;
	}
	private async Task<bool> IsStageApproverAsync(StudentRecordChange item)
	{
		return IsStageApprover(item, await access.GetDatabaseRoleNamesAsync(), await decisions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.ChangeId == item.Id));
	}
	private bool IsStageApprover(StudentRecordChange item, List<string> roles, List<StudentRecordChangeDecision> history)
	{
		if (item.Status != "Pending" || item.RequestedBy == CurrentUser.Id) return false;
		var stages = JsonSerializer.Deserialize<List<StudentRecordsApprovalStage>>(item.ApprovalStagesJson, StudentRecordPayloadService.JsonOptions)!;
		if (item.ApprovalCount >= stages.Count) return false;
		if (!roles.Intersect(stages[item.ApprovalCount].RoleNames, StringComparer.OrdinalIgnoreCase).Any()) return false;
		return !history.Any(x => x.ActorId == CurrentUser.Id);
	}
	private static void EnsureSectionBoundaries(string section, string beforeJson, string proposedJson)
	{
		if (section != "general") return;
		using var before = JsonDocument.Parse(beforeJson);
		using var proposed = JsonDocument.Parse(proposedJson);
		foreach (var field in new[] { "aadharNo", "apparId", "pennNo", "profilePictureUrl", "academicMediumId", "standardId", "divisionId" })
			if (before.RootElement.GetProperty(field).GetRawText() != proposed.RootElement.GetProperty(field).GetRawText())
				throw new BusinessException("Students:Records:OwnedFieldChange").WithData("Field", field);
	}
	private async Task EnsureVisibleAsync(StudentRecordsPolicy policy, string section)
	{
		if (StudentRecordVisibility.IsMasked(policy, section, await access.GetDatabaseRoleNamesAsync()))
			throw new BusinessException("Students:Records:RestrictedFields");
	}
	private static void EnsureStamp(StudentRecordChange item, string stamp)
	{
		if (string.IsNullOrEmpty(stamp) || item.ConcurrencyStamp != stamp) throw new BusinessException("Students:Records:ConcurrencyConflict");
	}
	internal static void ValidateEffectiveDate(DateTimeOffset effective, DateTimeOffset now, StudentRecordsTransitionPolicy policy)
	{
		var days = (AdmissionCycleWindowEvaluator.GetInstitutionDate(effective.UtcDateTime) - AdmissionCycleWindowEvaluator.GetInstitutionDate(now.UtcDateTime)).Days;
		if ((days < 0 && (!policy.AllowPastDates || -days > policy.MaxPastDays)) ||
			(days > 0 && (!policy.AllowFutureDates || days > policy.MaxFutureDays)))
			throw new BusinessException("Students:Records:EffectiveDateNotAllowed");
	}
}
