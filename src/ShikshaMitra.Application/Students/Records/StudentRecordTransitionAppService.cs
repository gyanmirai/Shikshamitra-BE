using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.View)]
public partial class StudentRecordTransitionAppService(IRepository<StudentRecordTransition, Guid> transitions,
	IRepository<StudentRecordTransitionBatch, Guid> batches, IRepository<StudentRecordTransitionEvent, Guid> events,
	IRepository<StudentRecordRenewalCase, Guid> renewalCases, IRepository<StudentEnrollment, Guid> enrollments,
	IStudentRepository students, StudentScopeAccessService scope, StudentRecordsAccessService access,
	StudentRecordsPolicyResolver policies, StudentRecordTransitionReadinessService readiness,
	StudentRecordTransitionOptionsService options, StudentRecordTransitionApplication application,
	StudentRecordRenewalCoordinator renewals, StudentRecordTransitionCorrectionService corrections,
	StudentRecordReturnCoordinator returns, StudentRecordsEnrollmentScopeService enrollmentScope,
	StudentRenewalPortalCoordinator renewalPortals, StudentRecordAdmissionReturnService admissionReturns,
	IRepository<StudentRecordReturnHandoff, Guid> returnHandoffs) : ApplicationService, IStudentRecordTransitionAppService
{
	public Task<StudentRecordTransitionOptionsDto> GetOptionsAsync(Guid? academicYearId, Guid? studentId, Guid? admissionOfferingId = null) => options.GetAsync(academicYearId, studentId, admissionOfferingId);
	public async Task<PagedResultDto<StudentRecordTransitionDto>> GetListAsync(StudentRecordTransitionQueryDto input)
	{
		var allowed = await enrollmentScope.ApplyStudentScopeAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(x => x.TenantId == CurrentTenant.Id));
		var sourceEnrollments = await enrollmentScope.GetQueryableAsync();
		var handoffQuery = await returnHandoffs.GetQueryableAsync();
		var query = (await transitions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && allowed.Any(s => s.Id == x.StudentId) &&
			(sourceEnrollments.Any(e => e.Id == x.SourceEnrollmentId) || !x.SourceEnrollmentId.HasValue && x.Outcome == "Return" && handoffQuery.Any(h => h.TenantId == CurrentTenant.Id && h.TransitionId == x.Id && h.StudentId == x.StudentId)));
		if (input.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (input.StudentId.HasValue) query = query.Where(x => x.StudentId == input.StudentId);
		if (!string.IsNullOrWhiteSpace(input.Status)) query = query.Where(x => x.Status == input.Status);
		// Target placement is part of the request, not merely the student's current school scope.
		var visible = await GetVisiblePlacementIdsAsync();
		query = query.Where(x => (!x.TargetStandardId.HasValue || Enumerable.Contains(visible.Standards, x.TargetStandardId.Value)) && (!x.TargetDivisionId.HasValue || Enumerable.Contains(visible.Divisions, x.TargetDivisionId.Value)));
		var total = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var result = new List<StudentRecordTransitionDto>();
		foreach (var item in items) result.Add(await MapAsync(item));
		return new(total, result);
	}
	public async Task<StudentRecordTransitionDto> GetAsync(Guid id) => await MapAsync(await GetScopedAsync(id));
	private async Task<StudentRecordTransition> GetScopedAsync(Guid id)
	{
		var item = await transitions.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id);
		await access.EnsureReadableAsync(item.StudentId);
		if (!item.SourceEnrollmentId.HasValue)
		{
			if (!item.ReturnHandoffId.HasValue) throw new AbpAuthorizationException();
			await admissionReturns.GetHandoffAsync(item); await admissionReturns.RequireLegacyAuthorityAsync(item.StudentId);
		}
		else
		{
			if (!await AsyncExecuter.AnyAsync((await enrollmentScope.GetQueryableAsync()).Where(x => x.Id == item.SourceEnrollmentId))) throw new AbpAuthorizationException();
			var source = await enrollments.GetAsync(x => x.Id == item.SourceEnrollmentId && x.TenantId == CurrentTenant.Id);
			if (source.StandardId.HasValue) await scope.EnsureCanAccessStandardAsync(source.StandardId.Value, "transition source class");
			if (source.DivisionId.HasValue) await scope.EnsureCanAccessDivisionAsync(source.DivisionId.Value, "transition source division");
		}
		var placement = JsonSerializer.Deserialize<StudentRecordTransitionInput>(item.InputJson)!;
		if (placement.TargetStandardId.HasValue) await scope.EnsureCanAccessStandardAsync(placement.TargetStandardId.Value, "transition target class");
		if (placement.TargetDivisionId.HasValue) await scope.EnsureCanAccessDivisionAsync(placement.TargetDivisionId.Value, "transition target division");
		return item;
	}
	private async Task<StudentRecordTransitionDto> MapAsync(StudentRecordTransition item)
	{
		var record = await access.EnsureReadableAsync(item.StudentId);
		var yearPolicy = (await policies.ResolveAsync(item.AcademicYearId)).Policy;
		var roles = await access.GetDatabaseRoleNamesAsync();
		var masks = StudentRecordsPolicy.SensitiveGroups.Where(group => record.Policy.IsMasked(group, roles) || yearPolicy.IsMasked(group, roles)).ToList();
		var privateText = masks.Count > 0;
		var input = JsonSerializer.Deserialize<StudentRecordTransitionInput>(item.InputJson)!;
		var history = await events.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.TransitionId == item.Id);
		var canStage = await IsStageApproverAsync(item);
		var handoff = item.ReturnHandoffId.HasValue ? await admissionReturns.GetHandoffAsync(item) : null;
		return new()
		{
			AdmissionApplicationId = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View) ? handoff?.AdmissionApplicationId : null,
			AdmissionInquiryId = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View) ? handoff?.AdmissionInquiryId : null,
			Id = item.Id, BatchId = item.BatchId, StudentId = item.StudentId, AcademicYearId = item.AcademicYearId,
			StudentName = masks.Contains("Identity") ? null : string.Join(" ", new[] { record.Student.FirstName, record.Student.MiddleName, record.Student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			Outcome = item.Outcome, Status = item.Status, Reason = privateText ? "" : item.Reason, EffectiveDate = item.EffectiveDate,
			TargetAcademicYearId = input.TargetAcademicYearId, TargetStandardId = input.TargetStandardId, TargetDivisionId = input.TargetDivisionId, RollNumber = input.RollNumber,
			CurrentApprovalStage = item.CurrentApprovalStage, ApprovalStageCount = item.ApprovalStageCount, ConcurrencyStamp = item.ConcurrencyStamp,
			RenewalCaseId = item.RenewalCaseId, CorrectionOfId = item.CorrectionOfId, LastError = item.LastError,
			CanApprove = canStage,
			CanCancel = ((item.Status is "Submitted" or "Approved" or "Scheduled" or "Exception") || await renewals.CanCancelAsync(item)) && CurrentUser.Id == item.RequestedBy && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create),
			CanApply = (item.Status is "Approved" or "Scheduled" or "Exception") && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply),
			CanCorrect = item.Status == "Applied" && !item.ReturnHandoffId.HasValue && !item.CorrectionOfId.HasValue && record.Access == "Full" && record.PlacementEditable && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create) && (await corrections.PreviewAsync(item)).Ready,
			History = history.OrderBy(x => x.CreationTime).Select(x => new StudentRecordTransitionEventDto { Action = x.Action, ActorId = x.ActorId, Stage = x.Stage, Remarks = privateText ? "" : x.Remarks, OccurredAt = x.CreationTime }).ToList()
		};
	}
	private async Task<bool> IsStageApproverAsync(StudentRecordTransition item)
	{
		if (item.Status != "Submitted" || CurrentUser.Id == item.RequestedBy || !await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve)) return false;
		var rule = JsonSerializer.Deserialize<StudentRecordsChangeRule>(item.ApprovalRuleJson)!;
		return rule.Stages[item.CurrentApprovalStage].RoleNames.Intersect(await access.GetDatabaseRoleNamesAsync(), StringComparer.OrdinalIgnoreCase).Any();
	}
	private static void Match(StudentRecordTransition item, string stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || item.ConcurrencyStamp != stamp) throw new BusinessException("Students:Records:ConcurrencyConflict");
	}
	public async Task<PagedResultDto<StudentRecordRenewalDto>> GetRenewalsAsync(StudentRecordTransitionQueryDto input)
	{
		var allowed = await enrollmentScope.ApplyStudentScopeAsync((await students.GetQueryableWithoutAutoIncludesAsync()).Where(x => x.TenantId == CurrentTenant.Id));
		var query = (await renewalCases.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && allowed.Any(s => s.Id == x.StudentId));
		if (input.AcademicYearId.HasValue) query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (input.StudentId.HasValue) query = query.Where(x => x.StudentId == input.StudentId);
		if (!string.IsNullOrWhiteSpace(input.Status)) query = query.Where(x => x.Status == input.Status);
		var targetIds = await GetVisiblePlacementIdsAsync();
		query = query.Where(x => Enumerable.Contains(targetIds.Standards, x.StandardId) && Enumerable.Contains(targetIds.Divisions, x.DivisionId));
		var count = await AsyncExecuter.CountAsync(query); var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var mapped = new List<StudentRecordRenewalDto>(); foreach (var item in rows) mapped.Add(await renewals.GetAsync(item.Id));
		return new(count, mapped);
	}
	public Task<StudentRecordRenewalDto> GetRenewalAsync(Guid id) => renewals.GetAsync(id);
	private async Task<(Guid[] Standards, Guid[] Divisions)> GetVisiblePlacementIdsAsync()
	{
		var standards = new List<Guid>(); var divisions = new List<Guid>();
		foreach (var year in (await options.GetAsync(null, null)).AcademicYears)
		{
			var placement = await options.GetAsync(year.Id, null); standards.AddRange(placement.Standards.Select(x => x.Id)); divisions.AddRange(placement.Divisions.Select(x => x.Id));
		}
		return (standards.ToArray(), divisions.ToArray());
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<StudentRecordRenewalDto> ConfirmRenewalAsync(Guid id, StudentRecordRenewalDecisionDto input)
	{
		await renewals.ConfirmAsync(id, input); return await renewals.GetAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply)]
	public async Task<StudentRecordRenewalDto> ActivateRenewalAsync(Guid id, StudentRecordRenewalDecisionDto input)
	{
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
			await renewals.ActivateAsync(id, input); await work.CompleteAsync();
		}
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{
			throw new BusinessException("Students:Records:ConcurrencyConflict");
		}
		return await renewals.GetAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public Task<StudentRecordReturnDto> ReturnAsync(StudentRecordReturnInput input) => returns.ReturnAsync(input, SubmitBatchAsync);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public Task<StudentRecordTransitionPreviewRowDto> PreviewReturnAsync(StudentRecordReturnInput input) => returns.PreviewAsync(input);
}
