using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

internal sealed record AdmissionParentForm(AdmissionFormInstance Instance, AdmissionFormDto Definition, List<AdmissionFormValueDto> Values);

[RemoteService(false)]
public class AdmissionParentRegistrationData : ApplicationService, ITransientDependency
{
	internal IRepository<T, Guid> Repo<T>() where T : class, Volo.Abp.Domain.Entities.IEntity<Guid> => LazyServiceProvider.LazyGetRequiredService<IRepository<T, Guid>>();
	internal async Task<AdmissionParentForm> FormAsync(AdmissionApplication application)
	{
		var instance = await Repo<AdmissionFormInstance>().FindAsync(row => row.ApplicationId == application.Id) ?? throw Invalid("The application form is unavailable.");
		var definition = AdmissionEffectiveFormStore.Read(instance)?.Definition;
		if (definition == null)
		{
			var legacy = await Repo<AdmissionFormDefinition>().GetAsync(instance.FormDefinitionId);
			AdmissionEffectiveFormStore.EnsureLegacyAllowed(legacy);
			definition = AdmissionApplicationFormService.MapDefinition(legacy,
				await Repo<AdmissionFormSectionDefinition>().GetListAsync(row => row.FormDefinitionId == legacy.Id),
				await Repo<AdmissionFormFieldDefinition>().GetListAsync(row => row.FormDefinitionId == legacy.Id));
		}
		var values = (await Repo<AdmissionFormFieldValue>().GetListAsync(row => row.FormInstanceId == instance.Id))
			.Select(row => new AdmissionFormValueDto { FieldCode = row.FieldCode, ValueJson = row.ValueJson }).ToList();
		return new(instance, definition, values);
	}
	internal static List<AdmissionParentRecipientDto> Recipients(AdmissionApplicant applicant)
	{
		var main = AdmissionFamilyContactCapture.ReadMainContact(applicant)?.Relationship;
		return new[] { "father", "mother", "guardian" }.Select(role =>
		{
			var prefix = role == "father" ? "Father" : role == "mother" ? "Mother" : "Guardian";
			var name = AdmissionStudentProfileMapping.Read(applicant, prefix + ".Name") ?? "";
			var email = AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email")?.Trim();
			var relationship = role == "guardian" ? applicant.GetProperty<string>(StudentFamilyContactSelection.GuardianRelationshipKey) : null;
			return new AdmissionParentRecipientDto { Relationship = role, FullName = name, Email = email, RelationshipLabel = relationship,
				IsMainContact = role == main, CanSend = !string.IsNullOrWhiteSpace(name) && MailAddress.TryCreate(email, out var address) &&
				address.Address == email && (role != "guardian" || !string.IsNullOrWhiteSpace(relationship)) };
		}).Where(item => !string.IsNullOrWhiteSpace(item.FullName) || item.Email != null).ToList();
	}
	internal static string RecipientFingerprint(AdmissionApplicant applicant, AdmissionParentRecipientDto recipient) => AdmissionParentRegistration.Hash(
		JsonSerializer.Serialize(new { applicant.Id, applicant.FamilyId, applicant.ExistingStudentId, recipient.Relationship, recipient.RelationshipLabel,
			recipient.FullName, recipient.Email, Confirmation = AdmissionSiblingParentSelection.ReadConfirmation(applicant) }));
	internal async Task<bool> IsInitialAllowedAsync(AdmissionApplication application)
	{
		if (application.Status != AdmissionApplicationStatus.Draft || application.SubmittedAt.HasValue || !application.CurrentStageInstanceId.HasValue) return false;
		var stage = await Repo<AdmissionApplicationStageInstance>().FindAsync(application.CurrentStageInstanceId.Value);
		var workflow = stage == null ? null : await Repo<AdmissionApplicationWorkflowInstance>().FindAsync(stage.WorkflowInstanceId);
		return stage?.StageType == AdmissionWorkflowStageType.Form && stage.Status == AdmissionRuntimeStageStatus.Active &&
			workflow?.ApplicationId == application.Id && workflow.Status == AdmissionWorkflowInstanceStatus.Active;
	}
	internal async Task<bool> IsDocumentStageAllowedAsync(AdmissionApplication application)
	{
		if (!IsOpen(application) || !application.CurrentStageInstanceId.HasValue || application.SubmittedAt.HasValue && !application.CanCorrectForm) return false;
		var stage = await Repo<AdmissionApplicationStageInstance>().FindAsync(application.CurrentStageInstanceId.Value);
		if (stage?.StageType != AdmissionWorkflowStageType.DocumentVerification || stage.Status != AdmissionRuntimeStageStatus.Active) return false;
		var workflow = await Repo<AdmissionApplicationWorkflowInstance>().FindAsync(stage.WorkflowInstanceId);
		return workflow?.ApplicationId == application.Id && workflow.Status == AdmissionWorkflowInstanceStatus.Active;
	}
	internal async Task<bool> IsDocumentCorrectionAllowedAsync(AdmissionApplication application, AdmissionParentRegistration grant)
	{
		if (grant.Purpose != "correction" || JsonSerializer.Deserialize<List<string>>(grant.FieldCodesJson)?.Count != 0 || !await IsDocumentStageAllowedAsync(application)) return false;
		var documents = JsonSerializer.Deserialize<List<AdmissionParentDocumentSelection>>(grant.DocumentsJson);
		return documents?.Count > 0 && documents.All(item => item.StageInstanceId == application.CurrentStageInstanceId);
	}
	internal async Task<bool> AreDocumentCorrectionStagesOpenAsync(AdmissionApplication application, AdmissionParentRegistration grant)
	{
		if (grant.Purpose != "correction" || JsonSerializer.Deserialize<List<string>>(grant.FieldCodesJson)?.Count != 0) return true;
		var selections = JsonSerializer.Deserialize<List<AdmissionParentDocumentSelection>>(grant.DocumentsJson);
		if (selections?.Count is not > 0) return true;
		foreach (var stageId in selections.Select(item => item.StageInstanceId).Distinct())
		{
			var stage = await Repo<AdmissionApplicationStageInstance>().FindAsync(stageId);
			if (stage?.StageType != AdmissionWorkflowStageType.DocumentVerification ||
				stage.Status is not (AdmissionRuntimeStageStatus.Pending or AdmissionRuntimeStageStatus.Active)) return false;
			var workflow = await Repo<AdmissionApplicationWorkflowInstance>().FindAsync(stage.WorkflowInstanceId);
			if (workflow?.ApplicationId != application.Id || workflow.Status != AdmissionWorkflowInstanceStatus.Active) return false;
		}
		return true;
	}
	internal static bool IsOpen(AdmissionApplication application) => application.Status is AdmissionApplicationStatus.Draft or AdmissionApplicationStatus.Submitted or
		AdmissionApplicationStatus.InReview or AdmissionApplicationStatus.Waitlisted or AdmissionApplicationStatus.Approved or AdmissionApplicationStatus.Offered or AdmissionApplicationStatus.OfferAccepted;
	internal async Task<string> BaselineAsync(AdmissionApplication application)
	{
		var form = await FormAsync(application);
		var applicant = await Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
		var evidence = await Repo<AdmissionDocumentEvidence>().GetListAsync(row => row.ApplicationId == application.Id);
		var reviews = await Repo<AdmissionDocumentReview>().GetListAsync(row => row.ApplicationId == application.Id);
		return AdmissionParentRegistration.Hash(JsonSerializer.Serialize(new { application.ConcurrencyStamp, Applicant = applicant.ConcurrencyStamp,
			Form = form.Instance.ConcurrencyStamp, Values = form.Values.OrderBy(value => value.FieldCode),
			Evidence = evidence.OrderBy(row => row.Id).Select(row => new { row.Id, row.VersionNumber }), Reviews = reviews.OrderBy(row => row.Id).Select(row => row.Id) }));
	}
	internal async Task<string> SnapshotAsync(AdmissionParentRegistration grant, AdmissionApplication application) =>
		AdmissionParentRegistration.Hash(grant.Id + ":" + grant.ConcurrencyStamp + ":" + await BaselineAsync(application));
	internal static bool IsDatabaseConflict(Exception error)
	{
		for (Exception? current = error; current != null; current = current.InnerException)
			if (current is System.Data.Common.DbException database && database.SqlState is "40001" or "40P01" or "23505") return true;
		return false;
	}
	internal static BusinessException Invalid(string reason) => new BusinessException(AdmissionParentRegistrationErrors.Invalid, reason).WithData("Reason", reason);
	internal static BusinessException Conflict() => new BusinessException(AdmissionParentRegistrationErrors.Conflict,
		"The application changed. Keep your answers and reload the latest saved version before trying again.");
	internal static BusinessException Unavailable() => new(AdmissionParentRegistrationErrors.Unavailable, "This registration link is unavailable. Ask the school for a new link.");
}
