using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentRollNumberBatchAppService
{
	public Task<StudentRollNumberBatchDto> SubmitAsync(Guid id, SubmitStudentRollBatchInput input) => GuardConflictAsync(() => SubmitCoreAsync(id, input));
	public Task<StudentRollNumberBatchDto> DecideAsync(Guid id, DecideStudentRollBatchInput input) => GuardConflictAsync(() => DecideCoreAsync(id, input));
	public Task<StudentRollNumberBatchDto> ApplyAsync(Guid id, SubmitStudentRollBatchInput input) => GuardConflictAsync(() => ApplyCoreAsync(id, input));
	private static async Task<StudentRollNumberBatchDto> GuardConflictAsync(Func<Task<StudentRollNumberBatchDto>> command)
	{
		try { return await command(); }
		catch (Exception exception) when (IsRollBatchConflict(exception))
		{ throw new BusinessException(StudentRollNumberErrorCodes.StalePreview); }
	}
	private static bool IsRollBatchConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current is Volo.Abp.Data.AbpDbConcurrencyException || current is System.Data.Common.DbException { SqlState: "40001" or "40P01" or "23505" }) return true;
		return false;
	}
	private async Task<StudentRollNumberBatchDto> SubmitCoreAsync(Guid id, SubmitStudentRollBatchInput input)
	{
		if (!(await new SubmitStudentRollBatchInputValidator().ValidateAsync(input)).IsValid) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetScopedAsync(id); await access.EnsureRowsAsync(item, true);
		if (item.RequestedBy != CurrentUser.Id) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		if (item.SubmissionKey != null)
		{
			if (item.SubmissionKey != input.IdempotencyKey || item.Reason != input.Reason.Trim()) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
			var replay = await ProjectAsync(item, 0, 25); await work.CompleteAsync(); return replay;
		}
		Stamp(item, input.ConcurrencyStamp); await EnsureFreshAsync(item);
		var stages = new List<StudentRecordsApprovalStage>();
		if (item.Rows().Any(x => x.EnrollmentId.HasValue))
		{
			var policy = StudentRecordsPolicy.FromJson(item.PolicyJson);
			if (policy.Changes["Placement"].RequireReason && string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
			if (policy.Changes["Placement"].RequiresApproval(false, false, false)) stages = policy.Changes["Placement"].Stages;
		}
		item.Submit(input.IdempotencyKey, input.Reason, JsonSerializer.Serialize(stages), Now());
		if (item.Status == "Approved") await application.ApplyAsync(item);
		await batches.UpdateAsync(item, false); await CurrentUnitOfWork!.SaveChangesAsync();
		var result = await ProjectAsync(item, 0, 25); await work.CompleteAsync(); return result;
	}
	private async Task<StudentRollNumberBatchDto> DecideCoreAsync(Guid id, DecideStudentRollBatchInput input)
	{
		if (!(await new DecideStudentRollBatchInputValidator().ValidateAsync(input)).IsValid) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetScopedAsync(id); Stamp(item, input.ConcurrencyStamp);
		if (!await CanDecideAsync(item)) throw new BusinessException(StudentRollNumberErrorCodes.ApproverRequired);
		if (input.Approve) await EnsureFreshAsync(item);
		item.Decide(CurrentUser.Id!.Value, input.Approve, input.Reason, Now()); await batches.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync(); var result = await ProjectAsync(item, 0, 25); await work.CompleteAsync(); return result;
	}
	private async Task<StudentRollNumberBatchDto> ApplyCoreAsync(Guid id, SubmitStudentRollBatchInput input)
	{
		if (!(await new SubmitStudentRollBatchInputValidator().ValidateAsync(input)).IsValid) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetScopedAsync(id); await access.EnsureRowsAsync(item, true);
		if (item.SubmissionKey != input.IdempotencyKey) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		if (item.Status == "Applied") { var replay = await ProjectAsync(item, 0, 25); await work.CompleteAsync(); return replay; }
		Stamp(item, input.ConcurrencyStamp); await EnsureFreshAsync(item); await EnsureApproversCurrentAsync(item);
		await application.ApplyAsync(item); await batches.UpdateAsync(item, false); await CurrentUnitOfWork!.SaveChangesAsync();
		var result = await ProjectAsync(item, 0, 25); await work.CompleteAsync(); return result;
	}
	private async Task EnsureApproversCurrentAsync(StudentRollNumberBatch item)
	{
		var stages = JsonSerializer.Deserialize<List<StudentRecordsApprovalStage>>(item.ApprovalStagesJson)!;
		if (item.Status != "Approved" || item.Decisions().Count != stages.Count) throw new BusinessException(StudentRollNumberErrorCodes.InvalidState);
		foreach (var decision in item.Decisions())
		{
			var actor = await users.FindByIdAsync(decision.ActorId.ToString());
			if (!decision.Approved || actor == null || actor.TenantId != CurrentTenant.Id || !actor.IsActive ||
				!(await users.GetRolesAsync(actor)).Intersect(stages[decision.Stage].RoleNames, StringComparer.OrdinalIgnoreCase).Any())
				throw new BusinessException(StudentRollNumberErrorCodes.ApproverRequired);
			var factory = LazyServiceProvider.LazyGetRequiredService<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<IdentityUser>>();
			using var principal = LazyServiceProvider.LazyGetRequiredService<ICurrentPrincipalAccessor>().Change(await factory.CreateAsync(actor));
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Approve);
			await access.EnsureScopeAsync(new() { AcademicYearId = item.AcademicYearId, StandardId = item.StandardId, DivisionId = item.DivisionId });
		}
	}
}
