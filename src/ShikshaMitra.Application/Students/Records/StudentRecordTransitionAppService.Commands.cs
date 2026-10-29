using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public partial class StudentRecordTransitionAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<StudentRecordTransitionPreviewDto> PreviewAsync(StudentRecordTransitionBatchInput input)
	{
		ValidateBatch(input, requireKey: false);
		var policy = await policies.ResolveAsync(input.AcademicYearId);
		var result = new StudentRecordTransitionPreviewDto { PolicyVersionId = policy.VersionId, BatchExceptionMode = policy.Policy.Transitions.BatchExceptionMode };
		var proposed = new List<StudentRecordTransitionInput>();
		foreach (var command in await readiness.OrderCohortAsync(input.Items))
		{
			var ruleJson = command.TargetAcademicYearId.HasValue && command.TargetStandardId.HasValue && command.TargetDivisionId.HasValue ? await readiness.CaptureRollRuleAsync(command) : "null";
			var classScope = ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingRollRuleReader.Parse(ruleJson) is { } rule && ShikshaMitra.Students.StudentEnrollments.Onboarding.OnboardingRuleConfiguration.Text(rule, "numberingScope") == "class";
			var reserved = proposed.Where(x => x.TargetAcademicYearId == command.TargetAcademicYearId && x.TargetStandardId == command.TargetStandardId && (classScope || x.TargetDivisionId == command.TargetDivisionId)).Where(x => x.RollNumber.HasValue).Select(x => x.RollNumber!.Value);
			var row = await readiness.PreviewAsync(input.AcademicYearId, command, policy.Policy, false, cohortReservations: reserved);
			var changeRule = policy.Policy.Changes[command.Outcome is "Promote" or "Repeat" ? "Placement" : "Status"];
			var effectiveDays = (command.EffectiveDate.Date - AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now)).TotalDays;
			row.ApprovalRequired = row.OutstandingItems.Count > 0 || changeRule.RequiresApproval(effectiveDays < 0, effectiveDays > 0, command.Outcome == "Return");
			if (row.OutstandingItems.Count > 0 && (changeRule.Stages.Count == 0 || changeRule.Stages.Any(x => x.RoleNames.Count == 0)))
			{
				StudentRecordTransitionReadinessService.Add(row, "OutstandingApprovalNotConfigured", "Configure an authorized approval stage before requesting exit with outstanding items."); row.Ready = false;
			}
			result.Items.Add(row);
			var accepted = JsonSerializer.Deserialize<StudentRecordTransitionInput>(JsonSerializer.Serialize(command))!;
			accepted.RollNumber = row.ProposedRollNumber ?? command.RollNumber;
			if (row.Ready) proposed.Add(accepted);
		}
		await readiness.ValidateBatchCapacityAsync(proposed, result.Items);
		result.ReadyCount = result.Items.Count(x => x.Ready); result.ExceptionCount = result.Items.Count - result.ReadyCount;
		result.CanSubmit = result.ReadyCount > 0 && (result.BatchExceptionMode == "ReadyOnly" || result.ExceptionCount == 0);
		return result;
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<StudentRecordTransitionBatchResultDto> SubmitBatchAsync(StudentRecordTransitionBatchInput input)
	{
		try { return await SubmitBatchCoreAsync(input); }
		catch (Exception exception) when (StudentOnboardingRollConflictMapper.IsSerializationConflict(exception))
		{
			throw new BusinessException(input.Items?.Any(x => x.TargetAcademicYearId.HasValue) == true ? "Students:Records:RollProposalChanged" : "Students:Records:ConcurrencyConflict");
		}
	}
	private async Task<StudentRecordTransitionBatchResultDto> SubmitBatchCoreAsync(StudentRecordTransitionBatchInput input)
	{
		ValidateBatch(input, requireKey: true);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var json = JsonSerializer.Serialize(input);
		var batch = await batches.FindAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == input.IdempotencyKey.Trim());
		if (batch != null)
		{
			if (batch.RequestJson != json) throw new BusinessException("Students:Records:IdempotencyConflict");
			var replay = await BatchResultAsync(batch); await work.CompleteAsync(); return replay;
		}
		var policy = await policies.ResolveAsync(input.AcademicYearId);
		var preview = await PreviewAsync(input);
		foreach (var inputRow in input.Items)
		{
			var row = preview.Items.Single(x => x.StudentId == inputRow.StudentId);
			if (row.Ready && row.RollAssignmentMode == "automatic" && inputRow.RollNumber != row.ProposedRollNumber)
				throw new BusinessException("Students:Records:RollProposalChanged").WithData("StudentId", inputRow.StudentId).WithData("ProposedRollNumber", row.ProposedRollNumber);
			if (inputRow.StudentConcurrencyStamp != row.StudentConcurrencyStamp || inputRow.EnrollmentConcurrencyStamp != row.EnrollmentConcurrencyStamp)
			{
				StudentRecordTransitionReadinessService.Add(row, "StaleSnapshot", "Refresh the student preview before submitting."); row.Ready = false;
			}
		}
		if (!preview.Items.Any(x => x.Ready) || policy.Policy.Transitions.BatchExceptionMode == "HoldAll" && preview.Items.Any(x => !x.Ready)) throw new BusinessException("Students:Records:BatchHeld");
		batch = new(GuidGenerator.Create(), CurrentTenant.Id!.Value, input.AcademicYearId, input.IdempotencyKey, json, JsonSerializer.Serialize(preview.Items.Where(x => !x.Ready)));
		await batches.InsertAsync(batch);
		foreach (var command in input.Items.Where(x => preview.Items.Single(p => p.StudentId == x.StudentId).Ready))
		{
			var source = await readiness.GetSourceAsync(command.StudentId, input.AcademicYearId);
			if (StudentSchoolExitOutstanding.IsExit(command.Outcome) && await transitions.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == command.StudentId && x.AcademicYearId == input.AcademicYearId &&
				(x.Outcome == "Withdraw" || x.Outcome == "Transfer" || x.Outcome == "Graduate") && (x.Status == "Submitted" || x.Status == "Approved" || x.Status == "Scheduled" || x.Status == "Exception")))
				throw new BusinessException("Students:Records:ExitAlreadyPending");
			var rule = policy.Policy.Changes[command.Outcome is "Promote" or "Repeat" ? "Placement" : "Status"];
			var days = (command.EffectiveDate.Date - AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now)).TotalDays;
			var outstanding = preview.Items.Single(x => x.StudentId == command.StudentId).OutstandingItems.Count > 0;
			var stages = outstanding || rule.RequiresApproval(days < 0, days > 0, command.Outcome == "Return") ? rule.Stages.Count : 0;
			if (outstanding && (stages == 0 || rule.Stages.Any(x => x.RoleNames.Count == 0))) throw new BusinessException("Students:Records:OutstandingApprovalNotConfigured");
			var item = new StudentRecordTransition(GuidGenerator.Create(), CurrentTenant.Id.Value, batch.Id, source.Student.Id, input.AcademicYearId,
				source.Enrollment.Id, command.Outcome, command.Reason, command.EffectiveDate, JsonSerializer.Serialize(command),
				StudentRecordTransitionReadinessService.Capture(source.Student, source.Enrollment), policy.VersionId, policy.Policy.ToJson(), JsonSerializer.Serialize(rule),
				CurrentUser.Id ?? throw new BusinessException("Students:Records:UserRequired"), stages);
			if (command.TargetAcademicYearId.HasValue) item.CaptureOnboardingRollRule(await readiness.CaptureRollRuleAsync(command));
			await transitions.InsertAsync(item);
			if (command.TargetAcademicYearId.HasValue && command.TargetStandardId.HasValue && command.TargetDivisionId.HasValue && command.RollNumber.HasValue)
				await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentEnrollments.Onboarding.StudentOnboardingRollRuleReader>()
					.RecordIssuedAsync(command.TargetAcademicYearId.Value, command.TargetStandardId.Value, command.TargetDivisionId.Value, command.RollNumber.Value);
			await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id.Value, item.Id, CurrentUser.Id.Value, "Submitted", 0, command.Reason));
		}
		await work.CompleteAsync();
		await ApplyBatchAsync(batch.Id);
		// Application commits in its own transaction. Do not return the submitting
		// context's tracked Approved row or its pre-application concurrency stamp.
		using var read = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var result = await BatchResultAsync(batch); await read.CompleteAsync(); return result;
	}
	private async Task<StudentRecordTransitionBatchResultDto> BatchResultAsync(StudentRecordTransitionBatch batch)
	{
		var result = new StudentRecordTransitionBatchResultDto { BatchId = batch.Id, Exceptions = JsonSerializer.Deserialize<List<StudentRecordTransitionPreviewRowDto>>(batch.ExceptionRowsJson)! };
		foreach (var item in await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.BatchId == batch.Id)) result.Items.Add(await MapAsync(await GetScopedAsync(item.Id)));
		foreach (var row in result.Exceptions)
		{
			var record = await access.EnsureReadableAsync(row.StudentId); var roles = await access.GetDatabaseRoleNamesAsync();
			if (record.Policy.IsMasked("Identity", roles) || (await policies.ResolveAsync(batch.AcademicYearId)).Policy.IsMasked("Identity", roles)) row.StudentName = null;
		}
		return result;
	}
	private static void ValidateBatch(StudentRecordTransitionBatchInput input, bool requireKey)
	{
		if (input.AcademicYearId == Guid.Empty || input.Items == null || input.Items.Count is < 1 or > 100 || input.Items.Any(x => x.StudentId == Guid.Empty) || input.Items.Select(x => x.StudentId).Distinct().Count() != input.Items.Count ||
			(requireKey && (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128))) throw new BusinessException("Students:Records:InvalidPayload");
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve)]
	public async Task<StudentRecordTransitionDto> ApproveAsync(Guid id, StudentRecordTransitionDecisionDto input)
	{
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetScopedAsync(id); Match(item, input.ConcurrencyStamp);
		if (!await IsStageApproverAsync(item)) throw new AbpAuthorizationException();
		StudentRecordTransitionPreviewRowDto? outstanding = null;
		if (StudentSchoolExitOutstanding.IsExit(item.Outcome))
		{
			outstanding = await readiness.PreviewAsync(item.AcademicYearId, JsonSerializer.Deserialize<StudentRecordTransitionInput>(item.InputJson)!, StudentRecordsPolicy.FromJson(item.PolicyJson), false, validateDate: false);
			if (!outstanding.Ready) throw new BusinessException(outstanding.Issues[0].Code);
			if (outstanding.OutstandingItems.Count > 0 && (string.IsNullOrWhiteSpace(input.OutstandingReason) || input.OutstandingReason.Length > 1000)) throw new BusinessException("Students:Records:OutstandingReasonRequired");
		}
		item.Approve(CurrentUser.Id!.Value); await transitions.UpdateAsync(item);
		if (outstanding?.OutstandingItems.Count > 0)
		{
			item.CaptureOutstandingApproval(outstanding.OutstandingEvidence);
			await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, id, CurrentUser.Id.Value, "OutstandingApproved", item.CurrentApprovalStage, input.OutstandingReason.Trim()));
		}
		await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, id, CurrentUser.Id.Value, "Approved", item.CurrentApprovalStage, input.Remarks));
		await work.CompleteAsync();
		await ApplyBatchAsync(item.BatchId); return await ReadCommittedAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Approve)]
	public async Task<StudentRecordTransitionDto> RejectAsync(Guid id, StudentRecordTransitionDecisionDto input)
	{
		var item = await GetScopedAsync(id); Match(item, input.ConcurrencyStamp);
		if (!await IsStageApproverAsync(item)) throw new AbpAuthorizationException();
		if (string.IsNullOrWhiteSpace(input.Remarks)) throw new BusinessException("Students:Records:ReasonRequired");
		item.Reject(); await transitions.UpdateAsync(item, true);
		await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, id, CurrentUser.Id!.Value, "Rejected", item.CurrentApprovalStage, input.Remarks)); return await GetAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add)]
	public async Task<StudentRecordTransitionDto> CancelAsync(Guid id, StudentRecordTransitionDecisionDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Add);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var item = await GetScopedAsync(id); Match(item, input.ConcurrencyStamp);
		if (item.RequestedBy != CurrentUser.Id) throw new AbpAuthorizationException();
		if (string.IsNullOrWhiteSpace(input.Remarks)) throw new BusinessException("Students:Records:ReasonRequired");
		var pendingRenewal = item.Status == "AwaitingRenewal";
		if (pendingRenewal) await renewals.CancelAsync(item);
		item.Cancel(pendingRenewal); await transitions.UpdateAsync(item, true);
		await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id!.Value, id, CurrentUser.Id!.Value, "Cancelled", item.CurrentApprovalStage, input.Remarks));
		await work.CompleteAsync(); return await ReadCommittedAsync(id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Apply)]
	public async Task<StudentRecordTransitionDto> ApplyAsync(Guid id, StudentRecordTransitionDecisionDto input)
	{
		var item = await GetScopedAsync(id); Match(item, input.ConcurrencyStamp);
		await application.ApplyAsRecordedActorAsync(id); return await ReadCommittedAsync(id);
	}
	private async Task<StudentRecordTransitionDto> ReadCommittedAsync(Guid id)
	{
		using var read = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var result = await GetAsync(id); await read.CompleteAsync(); return result;
	}
	private async Task ApplyBatchAsync(Guid batchId)
	{
		foreach (var item in await transitions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.BatchId == batchId && (x.Status == "Approved" || x.Status == "Scheduled"))) await application.ApplyAsRecordedActorAsync(item.Id);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create)]
	public async Task<StudentRecordTransitionPreviewRowDto> CorrectAsync(Guid id, StudentRecordTransitionCorrectionDto input)
	{
		var original = await GetScopedAsync(id); Match(original, input.ConcurrencyStamp);
		if (!input.PreviewOnly)
		{
			var replay = await batches.FindAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == input.IdempotencyKey);
			if (replay != null)
			{
				var existing = await transitions.FindAsync(x => x.TenantId == CurrentTenant.Id && x.BatchId == replay.Id && x.CorrectionOfId == id && x.RequestedBy == CurrentUser.Id && x.Reason == input.Reason);
				if (existing == null) throw new BusinessException("Students:Records:IdempotencyConflict");
				return new() { StudentId = original.StudentId, Outcome = "Correction", TransitionId = existing.Id, Ready = true };
			}
		}
		var row = await corrections.PreviewAsync(original);
		if (input.PreviewOnly || !row.Ready) return row;
		if (string.IsNullOrWhiteSpace(input.Reason) || string.IsNullOrWhiteSpace(input.IdempotencyKey)) throw new BusinessException("Students:Records:ReasonRequired");
		var batchInput = new StudentRecordTransitionBatchInput { AcademicYearId = original.AcademicYearId, IdempotencyKey = input.IdempotencyKey, Items = [new() { StudentId = original.StudentId, Outcome = "Correction", Reason = input.Reason, EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now) }] };
		using var work = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		if (await batches.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.IdempotencyKey == input.IdempotencyKey)) throw new BusinessException("Students:Records:IdempotencyConflict");
		var policy = await policies.ResolveAsync(original.AcademicYearId); var rule = policy.Policy.Changes["Correction"];
		var batch = new StudentRecordTransitionBatch(GuidGenerator.Create(), CurrentTenant.Id!.Value, original.AcademicYearId, input.IdempotencyKey, JsonSerializer.Serialize(batchInput), "[]"); await batches.InsertAsync(batch);
		if (original.ReturnHandoffId.HasValue || !original.SourceEnrollmentId.HasValue) throw new BusinessException("Students:Records:DownstreamDependency");
		var correction = new StudentRecordTransition(GuidGenerator.Create(), CurrentTenant.Id.Value, batch.Id, original.StudentId, original.AcademicYearId, original.SourceEnrollmentId.Value,
			"Correction", input.Reason, AdmissionCycleWindowEvaluator.GetInstitutionDate(Clock.Now), JsonSerializer.Serialize(batchInput.Items[0]), original.AfterJson!, policy.VersionId, policy.Policy.ToJson(), JsonSerializer.Serialize(rule), CurrentUser.Id!.Value,
			rule.RequiresApproval(false, false, false) ? rule.Stages.Count : 0, original.Id);
		await transitions.InsertAsync(correction); await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id.Value, correction.Id, CurrentUser.Id.Value, "CorrectionRequested", 0, input.Reason));
		await work.CompleteAsync(); await application.ApplyAsRecordedActorAsync(correction.Id); row.TransitionId = correction.Id; return row;
	}
}
