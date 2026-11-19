using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionReviews;
using ApplicationPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionApplications;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewCommandService(AdmissionReviewAccessService access, AdmissionReviewStateService states,
	AdmissionReviewPeopleService people, AdmissionReviewRuntimeService runtime, AdmissionReviewDecisionGuard decisionGuard,
	AdmissionApplicationFormService forms, IRepository<AdmissionReviewRecord, Guid> records,
	IRepository<AdmissionReviewWorkItem, Guid> workItems, IRepository<AdmissionApplication, Guid> applications,
	IRepository<AdmissionActivity, Guid> activities, IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task SaveAsync(Guid applicationId, Guid stageId, AdmissionReviewSaveDto input, bool submit)
	{
		var context = await access.GetContextAsync(applicationId, stageId, true);
		AdmissionReviewAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		var state = await states.ReadAsync(context);
		if (!await people.CanReviewAsync(state, submit ? Permissions.Submit : Permissions.Edit)) throw new BusinessException(AdmissionReviewErrorCodes.ReviewerNotAllowed);
		var checklist = Normalize(context.Policy!, input.Checklist);
		if (submit && (checklist.Any(item => item.Required && !item.Checked) || string.IsNullOrWhiteSpace(input.Reason) || input.Recommendation == null))
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		if (submit && (state.Evidence?.IsKnown != true || input.Recommendation == "approve" && !state.Evidence.Passed))
			throw new BusinessException(AdmissionReviewErrorCodes.EvidenceRequired);
		var work = await runtime.EnsureWorkItemAsync(context);
		await AppendAsync(state, work, submit ? "submitted" : "draft", input.Recommendation, input.Reason, input.Notes, checklist);
	}
	public async Task AssignAsync(Guid applicationId, Guid stageId, AdmissionReviewAssignmentDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		if (context.Policy == null || !context.CanMutate && !(context.IsActive && context.Application.Status == AdmissionApplicationStatus.Draft))
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidState);
		AdmissionReviewAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (!await permissions.IsGrantedAsync(Permissions.Assign)) throw new BusinessException(AdmissionReviewErrorCodes.ReviewerNotAllowed);
		await people.EnsureAssigneeAsync(input.AssignedReviewerId, context);
		var state = await states.ReadAsync(context); var work = await runtime.EnsureWorkItemAsync(context);
		var checklist = state.Latest == null ? Normalize(context.Policy!, []) :
			JsonSerializer.Deserialize<List<AdmissionReviewChecklistDto>>(state.Latest.ChecklistSnapshotJson, AdmissionReviewEvidenceService.JsonOptions) ?? [];
		if (string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		work.Assign(input.AssignedReviewerId, input.DueAt);
		await AppendAsync(state, work, "assignment", state.Latest?.Recommendation, input.Reason, state.Latest?.Notes, checklist);
	}
	public async Task RequestCorrectionAsync(Guid applicationId, Guid stageId, AdmissionReviewCorrectionDto input)
	{
		var context = await access.GetContextAsync(applicationId, stageId, true);
		AdmissionReviewAccessService.EnsureConcurrency(context.Application, input.ConcurrencyStamp);
		if (!await permissions.IsGrantedAsync(ApplicationPermissions.Reopen) || string.IsNullOrWhiteSpace(input.Reason) || !context.IsActive ||
			context.Application.Status is not (AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Approved))
			throw new BusinessException(AdmissionReviewErrorCodes.DecisionNotAllowed);
		var state = await decisionGuard.EnsureDecisionAsync(context.Application, "requestCorrection");
		await decisionGuard.RecordDecisionAsync(state, "requestCorrection", input.Reason);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await forms.ReopenAsync(applicationId);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("AdmissionReviewCorrectionRequested",
			input.Reason, context.Application.ApplicantId, applicationId), false);
		await CurrentUnitOfWork.SaveChangesAsync();
	}
	private async Task AppendAsync(AdmissionReviewState state, AdmissionReviewWorkItem work, string status,
		string? recommendation, string? reason, string? notes, List<AdmissionReviewChecklistDto> checklist)
	{
		var evidence = state.Evidence ?? throw new BusinessException(AdmissionReviewErrorCodes.InvalidPolicy);
		await records.InsertAsync(new AdmissionReviewRecord(GuidGenerator.Create(), CurrentTenant.Id, state.Context.Application.Id,
			state.Context.Stage.Id, (state.Latest?.SequenceNumber ?? 0) + 1, status, recommendation, reason, notes, work.AssignedReviewerId,
			work.DueAt, evidence.Fingerprint, evidence.PolicyJson, JsonSerializer.Serialize(evidence.Items, AdmissionReviewEvidenceService.JsonOptions),
			JsonSerializer.Serialize(checklist, AdmissionReviewEvidenceService.JsonOptions), CurrentUser.Id!.Value, DateTime.UtcNow), false);
		work.Record(status == "assignment" ? "draft" : status);
		await workItems.UpdateAsync(work, false);
		await applications.UpdateAsync(state.Context.Application, false);
		await activities.InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity("AdmissionReviewRecorded",
			$"Review {status} recorded for {state.Context.Stage.Name}.", state.Context.Application.ApplicantId, state.Context.Application.Id), false);
		await CurrentUnitOfWork!.SaveChangesAsync();
	}
	private static List<AdmissionReviewChecklistDto> Normalize(AdmissionReviewPolicy policy, List<AdmissionReviewChecklistDto>? values)
	{
		if (values == null || values.Count > 30 || values.Any(item => item == null || string.IsNullOrWhiteSpace(item.Code) || item.Notes?.Length > 1024) ||
			values.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count ||
			values.Any(item => !policy.Checklist.Any(defined => defined.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))))
			throw new BusinessException(AdmissionReviewErrorCodes.InvalidInput);
		return policy.Checklist.Select(item =>
		{
			var supplied = values.SingleOrDefault(value => value.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase));
			return new AdmissionReviewChecklistDto { Code = item.Code, Name = item.Name, Required = item.Required, Checked = supplied?.Checked == true, Notes = supplied?.Notes?.Trim() };
		}).ToList();
	}
}
