using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Current saved owners; a guardian fallback never supplies a Student recipient.</summary>
[ExposeServices(typeof(StudentCredentialsRecipientReader), typeof(IStudentCredentialRecipientVerifier))]
public class StudentCredentialsRecipientReader(IStudentRepository students,
	IRepository<AdmissionApplicant, Guid> applicants, IRepository<StudentRecordChange, Guid> changes,
	IRepository<StudentOnboardingCase, Guid> cases, ICurrentTenant tenant,
	IRepository<StudentRecordCustomProfile, Guid> profiles) : ITransientDependency, IStudentCredentialRecipientVerifier
{
	public async Task<IReadOnlyList<StudentCredentialsRecipientDto>> GetAsync(StudentOnboardingCase item)
	{
		var student = await students.GetWithDetailsAsync(item.StudentId);
		if (student == null || student.TenantId != item.TenantId) return [];
		var email = Normalize(student.ContactDetail?.Email);
		var applicant = await applicants.FindAsync(item.ApplicantId);
		var capturedEmail = applicant != null && (applicant.ExistingStudentId == student.Id || applicant.ReservedStudentId == student.Id)
			? AdmissionStudentProfileMapping.Read(applicant, "Applicant.Email") : null;
		var studentEmailRecorded = SameEmail(email, capturedEmail);
		if (!studentEmailRecorded && email != null)
		{
			var applied = await changes.GetListAsync(change => change.StudentId == student.Id &&
				(change.Section == "general" || change.Section == "registration-form") && change.Status == "Applied");
			studentEmailRecorded = applied.Any(change => change.Section == "general" && ChangedToEmail(change.BeforeJson, change.PayloadJson, email));
			if (!studentEmailRecorded && applied.Any(change => change.Section == "registration-form"))
			{
				var retained = await profiles.FindAsync(profile => profile.StudentId == student.Id && profile.TenantId == item.TenantId);
				if (retained != null) studentEmailRecorded = applied.Any(change => change.Section == "registration-form" &&
					ChangedFormToEmail(change.BeforeJson, change.PayloadJson, email, retained.SourcesJson, retained.SchemaJson));
			}
		}
		var result = new List<StudentCredentialsRecipientDto>
		{
			new() { AccountType = "student", Relationship = "self",
				Name = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))),
				Email = studentEmailRecorded ? email : null,
				BlockedReason = !studentEmailRecorded ? "No Student email was captured or approved. Add or review the Student email in Student Records."
					: ValidEmail(email) ? null : "Correct the saved Student email in Student Records." }
		};
		foreach (var relationship in StudentPortalOwnerSelection.Relationships(student.FamilyDetail))
		{
			var person = relationship switch
			{
				"father" => student.FamilyDetail?.FatherDetails,
				"mother" => student.FamilyDetail?.MotherDetails,
				_ => student.FamilyDetail?.GuardianDetails
			};
			var parentEmail = Normalize(person?.Email);
			result.Add(new() { AccountType = "guardian", Relationship = relationship, Name = person?.FullName,
				Email = parentEmail, BlockedReason = ValidEmail(parentEmail) ? null
					: "Add or correct this parent's saved email in Student Records." });
		}
		return result;
	}

	public async Task<bool> IsCurrentOwnerAsync(Guid caseId, string accountType, string? relationship, string email)
	{
		var item = await cases.FindAsync(caseId);
		if (item == null || !tenant.Id.HasValue || item.TenantId != tenant.Id) return false;
		return (await GetAsync(item)).Any(owner => owner.AccountType == accountType && owner.Relationship == relationship &&
			owner.BlockedReason == null && SameEmail(owner.Email, email));
	}

	internal static bool ChangedToEmail(string beforeJson, string payloadJson, string currentEmail)
	{
		try
		{
			using var before = JsonDocument.Parse(beforeJson);
			using var after = JsonDocument.Parse(payloadJson);
			var previous = ReadEmail(before.RootElement);
			var next = ReadEmail(after.RootElement);
			return SameEmail(next, currentEmail) && !SameEmail(previous, next);
		}
		catch (JsonException) { return false; }
	}

	internal static bool ChangedFormToEmail(string beforeJson, string payloadJson, string currentEmail,
		string sourcesJson, string schemaJson)
	{
		try
		{
			var before = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(beforeJson, StudentRecordPayloadService.JsonOptions);
			var after = JsonSerializer.Deserialize<StudentRecordCustomDetailsPayload>(payloadJson, StudentRecordPayloadService.JsonOptions);
			var sources = JsonSerializer.Deserialize<List<StudentRecordRegistrationSourceDto>>(sourcesJson, StudentRecordPayloadService.JsonOptions);
			var schemas = JsonSerializer.Deserialize<List<StudentRecordRegistrationSourceDto>>(schemaJson, StudentRecordPayloadService.JsonOptions);
			if (before?.Sources == null || after?.Sources == null || sources == null || schemas == null) return false;
			foreach (var source in sources.Where(value => value.SubmissionId != Guid.Empty && value.ApplicationId != Guid.Empty && value.FormInstanceId != Guid.Empty))
			{
				var definition = schemas.FirstOrDefault(value => value.SubmissionId == source.SubmissionId)?.Definition;
				if (definition == null || definition.Id != source.FormDefinitionId) continue;
				var previous = before.Sources.FirstOrDefault(value => value.SubmissionId == source.SubmissionId &&
					value.ApplicationId == source.ApplicationId && value.FormInstanceId == source.FormInstanceId);
				var next = after.Sources.FirstOrDefault(value => value.SubmissionId == source.SubmissionId &&
					value.ApplicationId == source.ApplicationId && value.FormInstanceId == source.FormInstanceId);
				if (previous?.Values == null || next?.Values == null) continue;
				foreach (var field in definition.Sections.SelectMany(section => section.Fields).Where(field =>
					field.BindingType == AdmissionFormBindingType.System && field.BindingKey == "Applicant.Email"))
					if (previous.Values.TryGetValue(field.Code, out var oldValue) && next.Values.TryGetValue(field.Code, out var newValue) &&
						newValue.ValueKind == JsonValueKind.String && SameEmail(newValue.GetString(), currentEmail) &&
						!SameEmail(oldValue.ValueKind == JsonValueKind.String ? oldValue.GetString() : null, newValue.GetString())) return true;
			}
			return false;
		}
		catch (JsonException) { return false; }
	}

	private static string? ReadEmail(JsonElement payload)
	{
		if (payload.ValueKind != JsonValueKind.Object) return null;
		foreach (var property in payload.EnumerateObject())
			if (property.Name.Equals("email", StringComparison.OrdinalIgnoreCase))
				return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
		return null;
	}
	internal static bool SameEmail(string? first, string? second) => Normalize(first) is string normalized &&
		string.Equals(normalized, Normalize(second), StringComparison.OrdinalIgnoreCase);
	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	private static bool ValidEmail(string? email) => email is { Length: <= 256 } && MailAddress.TryCreate(email, out var address) &&
		string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Revalidates only explicit saved-recipient intents; legacy automatic behavior remains compatible.</summary>
public interface IStudentCredentialRecipientVerifier
{
	Task<bool> IsCurrentOwnerAsync(Guid caseId, string accountType, string? relationship, string email);
}
