using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Uow;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[Authorize, RemoteService(false)]
public partial class AdmissionParentRegistrationAppService(AdmissionParentRegistrationData data,
	AdmissionParentRegistrationAccess access, AdmissionParentRegistrationDocuments documents, IDataProtectionProvider protection,
	IBackgroundJobManager jobs, AdmissionApplicationFormService formOwner) : ApplicationService, IAdmissionParentRegistrationAppService
{
	public async Task<AdmissionParentStaffContextDto> GetAsync(Guid applicationId)
	{
		var application = await access.StaffAsync(applicationId, false);
		var latest = (await data.Repo<AdmissionParentRegistration>().GetListAsync(row => row.ApplicationId == applicationId))
			.OrderByDescending(row => row.CreationTime).ThenByDescending(row => row.Id).FirstOrDefault();
		var applicant = await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
		var canEdit = await AuthorizationService.IsGrantedAsync(Permissions.AdmissionApplications.Edit);
		var canDocuments = await AuthorizationService.IsGrantedAsync(Permissions.AdmissionDocuments.Upload);
		var open = AdmissionParentRegistrationData.IsOpen(application);
		var initial = await data.IsInitialAllowedAsync(application);
		var form = await data.FormAsync(application);
		var documentItems = await documents.ReadAsync(application);
		var documentStage = await data.IsDocumentStageAllowedAsync(application);
		var canRequestDocuments = canEdit && canDocuments && documentStage && documentItems.Any(item => item.Dto.StageInstanceId == application.CurrentStageInstanceId);
		var availableDocuments = canDocuments ? documentItems.Select(item => item.Dto).ToList() : [];
		var hasDocuments = documentItems.Count > 0;
		var dto = latest == null ? null : Map(latest);
		var grantStageAllowed = latest == null || (latest.Purpose == "initial" ? initial : application.SubmittedAt.HasValue
			? application.CanCorrectForm : initial || await data.IsDocumentCorrectionAllowedAsync(application, latest));
		if (latest != null && grantStageAllowed) grantStageAllowed = await data.AreDocumentCorrectionStagesOpenAsync(application, latest);
		var recipients = AdmissionParentRegistrationData.Recipients(applicant);
		foreach (var recipient in recipients)
		{
			recipient.EmailChangeBlockedReason = EmailChangeBlockedReason(application, applicant, recipient, initial, canEdit);
			recipient.CanChangeEmail = recipient.EmailChangeBlockedReason == null;
		}
		if (dto != null && latest!.CanRead(Clock.Now))
		{
			var savedRecipient = AdmissionParentRegistrationData.Recipients(applicant).FirstOrDefault(item => item.Relationship == dto.Recipient.Relationship && item.CanSend);
			if (!open || !grantStageAllowed || savedRecipient == null ||
				AdmissionParentRegistrationData.RecipientFingerprint(applicant, savedRecipient) != latest.RecipientFingerprint) dto.Status = "Closed";
		}
		var live = latest?.CanRead(Clock.Now) == true && dto?.Status != "Closed";
		var deliveryBlocked = await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Application.Communication.CommunicationAppService>().ParentRegistrationDeliveryBlockedReasonAsync();
		return new() { ApplicationId = applicationId, ConcurrencyStamp = application.ConcurrencyStamp,
			Recipients = recipients, Request = dto,
			CanSend = canEdit && initial && (!hasDocuments || canDocuments), CanRequestCorrections = canEdit && open && (application.SubmittedAt.HasValue ? application.CanCorrectForm : initial) || canRequestDocuments,
			CanRequestDocumentCorrections = canRequestDocuments,
			CanRevoke = canEdit && live, CanTakeover = canEdit && live,
			CanApply = canEdit && application.CanCorrectForm && open && latest?.Status == "ReadyForReview" && latest.ProposalBaseline != null && dto!.ProposedValues.Count > 0,
			CanRetryDelivery = canEdit && live && latest!.DeliveryStatus is "Failed" or "PendingApproval",
			EmailDeliveryBlockedReason = deliveryBlocked,
			BlockedReason = !open ? "This admission is closed." : application.SubmittedAt.HasValue && !application.CanCorrectForm ? "Complete the reopened draft through the staff form before requesting parent corrections." : !initial && !application.SubmittedAt.HasValue && !canRequestDocuments ? "The Registration Form must be the current stage." :
				hasDocuments && !canDocuments ? "Document upload permission is required to invite a parent for these requirements." : null,
			AvailableFields = form.Definition.Sections.SelectMany(section => section.Fields).Select(field => new AdmissionParentFieldDto
				{ Code = field.Code, Label = field.Label, AllowedRecipientRelationships = recipients.Where(recipient => recipient.CanSend && AdmissionParentRegistrationSchema.CanWriteFor(field, recipient.Relationship, applicant))
					.Select(recipient => recipient.Relationship).ToList() }).Where(field => field.AllowedRecipientRelationships.Count > 0).ToList(), AvailableDocuments = availableDocuments };
	}
	public Task<AdmissionParentStaffContextDto> SendAsync(Guid applicationId, AdmissionParentSendInput input) => CreateAsync(applicationId, input, null);
	public Task<AdmissionParentStaffContextDto> RequestCorrectionsAsync(Guid applicationId, AdmissionParentCorrectionInput input) => CreateAsync(applicationId, input, input);
	private async Task<AdmissionParentStaffContextDto> CreateAsync(Guid applicationId, AdmissionParentSendInput input, AdmissionParentCorrectionInput? correction)
	{
		return await StaffMutationAsync(applicationId, input, async application =>
		{
			var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"parent-registration:{CurrentTenant.Id}:{applicationId}:{input.IdempotencyKey}"))[..16]);
			var fingerprint = AdmissionParentRegistration.Hash(JsonSerializer.Serialize((object?)correction ?? input));
			var previous = await data.Repo<AdmissionParentRegistration>().FindAsync(id);
			if (previous != null)
			{
				if (previous.RequestFingerprint != fingerprint) throw AdmissionParentRegistrationData.Conflict();
				return;
			}
			EnsureStamp(application, input);
			var documentOnly = correction != null && correction.FieldCodes.Count == 0 && correction.Documents.Count > 0 &&
				await data.IsDocumentStageAllowedAsync(application) && correction.Documents.All(item => item.StageInstanceId == application.CurrentStageInstanceId);
			if (!AdmissionParentRegistrationData.IsOpen(application) || !documentOnly && (correction == null || !application.SubmittedAt.HasValue) && !await data.IsInitialAllowedAsync(application))
				throw AdmissionParentRegistrationData.Invalid("Invite a parent from the current draft Registration Form, or request a correction on an open application.");
			if (correction != null && application.SubmittedAt.HasValue && !application.CanCorrectForm)
				throw AdmissionParentRegistrationData.Invalid("Complete the reopened draft through the staff form before requesting parent corrections.");
			var applicant = await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
			var recipient = AdmissionParentRegistrationData.Recipients(applicant).SingleOrDefault(item => item.Relationship == input.RecipientRelationship && item.CanSend)
				?? throw AdmissionParentRegistrationData.Invalid("Save a valid email and relationship for this adult before sending.");
			var form = await data.FormAsync(application);
			var available = await documents.ReadAsync(application);
			var selected = correction?.Documents ?? available.Select(item => new AdmissionParentDocumentSelection
				{ StageInstanceId = item.Dto.StageInstanceId, RequirementCode = item.Dto.Code }).ToList();
			var selectedFields = correction?.FieldCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
			var writable = form.Definition.Sections.SelectMany(section => section.Fields).Where(field => AdmissionParentRegistrationSchema.CanWriteFor(field, recipient.Relationship, applicant))
				.Select(field => field.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (correction != null && (string.IsNullOrWhiteSpace(correction.Notes) || correction.Notes.Length > 1024 || selectedFields.Count + selected.Count == 0 ||
				selectedFields.Any(code => !writable.Contains(code)) || selected.Any(selection => !available.Any(item => item.Dto.StageInstanceId == selection.StageInstanceId && item.Dto.Code == selection.RequirementCode))))
				throw AdmissionParentRegistrationData.Invalid("Choose editable fields or applicable documents and record the requested correction.");
			if (correction != null)
			{
				AdmissionParentRegistrationSchema.EnsureCorrectionReachable(form.Definition, form.Values, selectedFields, writable,
					LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationFormValidator>());
				selectedFields = AdmissionParentRegistrationSchema.ExpandCorrectionFields(form.Definition, selectedFields, writable);
				selected = selected.Select(selection => new AdmissionParentDocumentSelection { StageInstanceId = selection.StageInstanceId, RequirementCode = selection.RequirementCode,
					BaselineVersionNumber = available.Single(item => item.Dto.StageInstanceId == selection.StageInstanceId && item.Dto.Code == selection.RequirementCode).Evaluation.Latest?.VersionNumber ?? 0 }).ToList();
			}
			var allowDocuments = correction == null ? available.Count > 0 : selected.Count > 0;
			if (allowDocuments) await AuthorizationService.CheckAsync(Permissions.AdmissionDocuments.Upload);
			foreach (var old in await data.Repo<AdmissionParentRegistration>().GetListAsync(row => row.ApplicationId == applicationId))
				if (old.CanRead(Clock.Now)) { old.Close("Revoked"); await data.Repo<AdmissionParentRegistration>().UpdateAsync(old, false); }
			var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
			var grant = new AdmissionParentRegistration(id, CurrentTenant.Id ?? throw AdmissionParentRegistrationData.Invalid("A school is required."), applicationId,
				CurrentUser.Id ?? throw AdmissionParentRegistrationData.Unavailable(), AdmissionParentRegistration.Hash(token),
				protection.CreateProtector("AdmissionParentRegistration.v1").Protect(token), JsonSerializer.Serialize(recipient),
				AdmissionParentRegistrationData.RecipientFingerprint(applicant, recipient), correction == null ? "initial" : "correction", Clock.Now,
				fingerprint, allowDocuments, JsonSerializer.Serialize(selectedFields), JsonSerializer.Serialize(selected), correction?.Notes.Trim());
			await data.Repo<AdmissionParentRegistration>().InsertAsync(grant, false);
			await jobs.EnqueueAsync(new AdmissionParentRegistrationDeliveryArgs { TenantId = CurrentTenant.Id, RegistrationId = id });
			await data.Repo<AdmissionApplication>().UpdateAsync(application, false);
		});
	}
	private async Task<AdmissionParentStaffContextDto> StaffMutationAsync(Guid applicationId, AdmissionParentCommand input, Func<AdmissionApplication, Task> action)
	{
		if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 128) throw AdmissionParentRegistrationData.Invalid("A valid retry key is required.");
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
			var application = await access.StaffAsync(applicationId, true);
			await action(application);
			await work.SaveChangesAsync();
			var result = await GetAsync(applicationId);
			await work.CompleteAsync();
			return result;
		}
		catch (AbpDbConcurrencyException) { throw AdmissionParentRegistrationData.Conflict(); }
		catch (Exception error) when (AdmissionParentRegistrationData.IsDatabaseConflict(error)) { throw AdmissionParentRegistrationData.Conflict(); }
	}
	private static void EnsureStamp(AdmissionApplication application, AdmissionParentCommand input)
	{ if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != application.ConcurrencyStamp) throw AdmissionParentRegistrationData.Conflict(); }
	private AdmissionParentRequestDto Map(AdmissionParentRegistration grant) => new()
	{
		Id = grant.Id, Purpose = grant.Purpose, Status = grant.CanRead(Clock.Now) ? grant.Status : grant.ExpiresAt <= Clock.Now && grant.Status is "Invited" or "InProgress" ? "Expired" : grant.Status,
		Recipient = JsonSerializer.Deserialize<AdmissionParentRecipientDto>(grant.RecipientJson)!, ExpiresAt = grant.ExpiresAt,
		LastSavedAt = grant.LastSavedAt, HandedInAt = grant.HandedInAt, DeliveryStatus = grant.DeliveryStatus, DeliveryError = grant.DeliveryError,
		Notes = grant.Notes, FieldCodes = JsonSerializer.Deserialize<List<string>>(grant.FieldCodesJson)!, Documents = JsonSerializer.Deserialize<List<AdmissionParentDocumentSelection>>(grant.DocumentsJson)!,
		ProposedValues = JsonSerializer.Deserialize<List<AdmissionFormValueDto>>(grant.ProposedValuesJson)!
	};
}
