using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionParentRegistrationAppService
{
	public Task<AdmissionParentStaffContextDto> ChangeEmailAsync(Guid applicationId, AdmissionParentEmailInput input) =>
		StaffMutationAsync(applicationId, input, async application =>
		{
			var email = input.Email?.Trim();
			if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || email.Contains('\r') || email.Contains('\n') ||
				!MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase) ||
				input.ExpectedEmail?.Length > 256 || input.Reason?.Length > 512)
				throw AdmissionParentRegistrationData.Invalid("Enter a valid parent email address.");
			var eventId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"parent-registration-email:{CurrentTenant.Id}:{applicationId}:{input.IdempotencyKey}"))[..16]);
			var fingerprint = AdmissionParentRegistration.Hash(JsonSerializer.Serialize(input));
			var previous = await data.Repo<AdmissionFormChangeHistory>().FindAsync(eventId);
			if (previous != null)
			{
				EmailChangeAudit? audit;
				try { audit = JsonSerializer.Deserialize<EmailChangeAudit>(previous.ChangedFieldsJson ?? "null"); }
				catch (JsonException) { throw AdmissionParentRegistrationData.Conflict(); }
				if (previous.ApplicationId != applicationId || previous.ChangeType != "ParentEmailChanged" || audit?.Fingerprint != fingerprint)
					throw AdmissionParentRegistrationData.Conflict();
				return;
			}
			EnsureStamp(application, input);
			var applicant = await data.Repo<AdmissionApplicant>().GetAsync(application.ApplicantId);
			var recipient = AdmissionParentRegistrationData.Recipients(applicant).SingleOrDefault(item => item.Relationship == input.RecipientRelationship)
				?? throw AdmissionParentRegistrationData.Invalid("Choose a saved parent or guardian.");
			var blocked = EmailChangeBlockedReason(application, applicant, recipient, await data.IsInitialAllowedAsync(application), true);
			if (blocked != null) throw AdmissionParentRegistrationData.Invalid(blocked);
			await LazyServiceProvider.LazyGetRequiredService<AdmissionApplicantScopeAccessService>().EnsureCanUpdateProfileAsync(applicant.Id, includesGuardian: false);
			if (!string.Equals(AdmissionSiblingParentSelection.NormalizeEmail(recipient.Email), AdmissionSiblingParentSelection.NormalizeEmail(input.ExpectedEmail), StringComparison.Ordinal))
				throw AdmissionParentRegistrationData.Conflict();
			var form = await data.FormAsync(application);
			var normalized = AdmissionSiblingParentSelection.NormalizeEmail(email)!;
			var prefix = input.RecipientRelationship == "father" ? "Father" : input.RecipientRelationship == "mother" ? "Mother" : "Guardian";
			var selectedFirstParent = AdmissionFirstParentContactMapping.CanMap(applicant) &&
				(AdmissionSiblingParentSelection.ReadRelationship(applicant) ?? AdmissionPrimaryContactSelection.ReadRelationship(applicant)) == input.RecipientRelationship;
			var values = form.Values.ToDictionary(value => value.FieldCode, value => AdmissionApplicationValueConverter.Unwrap(value.ValueJson), StringComparer.OrdinalIgnoreCase);
			var visible = AdmissionEffectiveFormResolver.VisibleFieldCodes(form.Definition, values,
				LazyServiceProvider.LazyGetRequiredService<AdmissionApplicationFormValidator>());
			var codes = form.Definition.Sections.Where(section => !section.IsRepeatable).SelectMany(section => section.Fields)
				.Where(field => !field.ReadOnly && visible.Contains(field.Code) && field.BindingType == AdmissionFormBindingType.System &&
					(field.BindingKey == prefix + ".Email" && prefix != "Guardian" || selectedFirstParent && field.BindingKey == "Guardian.Email"))
				.Select(field => field.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			foreach (var code in codes)
			{
				var saved = await data.Repo<AdmissionFormFieldValue>().FindAsync(row => row.FormInstanceId == form.Instance.Id && row.FieldCode == code);
				if (saved == null) await data.Repo<AdmissionFormFieldValue>().InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id, form.Instance.Id, code, JsonSerializer.Serialize(normalized)), false);
				else { saved.SetValue(JsonSerializer.Serialize(normalized)); await data.Repo<AdmissionFormFieldValue>().UpdateAsync(saved, false); }
			}
			AdmissionFamilyContactCapture.ChangeEmail(applicant, input.RecipientRelationship, normalized);
			await data.Repo<AdmissionApplicant>().UpdateAsync(applicant, false);
			if (application.InquiryId.HasValue)
			{
				var inquiry = await data.Repo<AdmissionInquiry>().FindAsync(application.InquiryId.Value);
				if (inquiry?.ApplicantId == applicant.Id) { AdmissionFamilyContactCapture.CopyToInquiry(applicant, inquiry); await data.Repo<AdmissionInquiry>().UpdateAsync(inquiry, false); }
			}
			foreach (var grant in await data.Repo<AdmissionParentRegistration>().GetListAsync(row => row.ApplicationId == applicationId))
				if (grant.CanRead(Clock.Now)) { grant.Close("Revoked"); await data.Repo<AdmissionParentRegistration>().UpdateAsync(grant, false); }
			application.SaveDraft(); form.Instance.MarkSaved();
			await data.Repo<AdmissionFormInstance>().UpdateAsync(form.Instance, false);
			await data.Repo<AdmissionApplication>().UpdateAsync(application, false);
			var metadata = JsonSerializer.Serialize(new EmailChangeAudit(1, input.RecipientRelationship, codes, fingerprint));
			await data.Repo<AdmissionFormChangeHistory>().InsertAsync(new(eventId, CurrentTenant.Id, form.Instance.Id, application.Id, "ParentEmailChanged", metadata, CurrentUser.Id), false);
			await data.Repo<AdmissionActivity>().InsertAsync(LazyServiceProvider.LazyGetRequiredService<StudentJourneyAuditWriter>().CreateActivity(
				"AdmissionParentEmailChanged", "Saved parent email updated", applicant.Id, application.Id, application.InquiryId, metadata), false);
		});

	private static string? EmailChangeBlockedReason(AdmissionApplication application, AdmissionApplicant applicant,
		AdmissionParentRecipientDto recipient, bool initial, bool canEdit)
	{
		if (!canEdit) return "Application Add/Update permission is required to change a saved parent email.";
		if (application.SubmittedAt.HasValue) return "Use Edit form and record an audited correction to change a submitted application's contact details.";
		if (!initial) return "Change the saved parent email from the current draft Registration Form.";
		if (applicant.ExistingStudentId.HasValue || AdmissionSiblingParentSelection.ReadConfirmation(applicant) != null)
			return "Update linked family contact details through the existing Student or family contact workflow.";
		return string.IsNullOrWhiteSpace(recipient.FullName) || recipient.Relationship == "guardian" && string.IsNullOrWhiteSpace(recipient.RelationshipLabel)
			? "Save this adult's name and relationship before changing their email." : null;
	}

	private sealed record EmailChangeAudit(int Version, string Relationship, string[] FieldCodes, string Fingerprint);
}
