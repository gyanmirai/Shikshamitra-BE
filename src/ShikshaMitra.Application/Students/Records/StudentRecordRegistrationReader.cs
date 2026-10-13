using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.Portal;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

/// <summary>Reads submitted Registration evidence, never mutable drafts or Inquiry answers.</summary>
[Volo.Abp.RemoteService(false)]
public class StudentRecordRegistrationReader(
	IRepository<AdmissionApplication, Guid> applications, IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<AdmissionFormInstance, Guid> instances, IRepository<AdmissionFormSubmission, Guid> submissions,
	IRepository<AdmissionFormDefinition, Guid> forms, IRepository<AdmissionFormSectionDefinition, Guid> sections,
	IRepository<AdmissionFormFieldDefinition, Guid> fields, IRepository<StudentOnboardingServiceHandoff, Guid> handoffs,
	IRepository<StudentCredentialEmailRequest, Guid> emails, OnboardingConfigurationManager configuration,
	IRepository<StudentEnrollment, Guid> allEnrollments,
	IRepository<StudentOnboardingEvent, Guid> events,
	StudentRecordsEnrollmentScopeService enrollmentScope) : ApplicationService, ITransientDependency
{
	private async Task<IQueryable<RegistrationRow>> GetSourceRowsAsync(Guid studentId, bool scoped)
	{
		var enrollmentQuery = scoped ? await enrollmentScope.GetQueryableAsync() : await allEnrollments.GetQueryableAsync();
		var caseQuery = await cases.GetQueryableAsync();
		var applicationQuery = await applications.GetQueryableAsync();
		var instanceQuery = await instances.GetQueryableAsync();
		var formQuery = await forms.GetQueryableAsync();
		var submissionQuery = await submissions.GetQueryableAsync();
		var eventQuery = await events.GetQueryableAsync();
		return from enrollment in enrollmentQuery
			join onboarding in caseQuery on (Guid?)enrollment.Id equals onboarding.StudentEnrollmentId
			join application in applicationQuery on onboarding.ApplicationId equals application.Id
			join instance in instanceQuery on application.Id equals instance.ApplicationId
			join form in formQuery on instance.FormDefinitionId equals form.Id
			where enrollment.StudentId == studentId && enrollment.TenantId == CurrentTenant.Id &&
				onboarding.TenantId == CurrentTenant.Id && onboarding.StudentId == studentId &&
				onboarding.AcademicYearId == enrollment.AcademicYearId && onboarding.Status != StudentOnboardingStatus.Cancelled &&
				application.TenantId == CurrentTenant.Id && (application.StudentId == studentId || application.EnrolledStudentId == studentId) &&
				(application.Status == AdmissionApplicationStatus.AdmissionFinalized || application.Status == AdmissionApplicationStatus.Enrolled) &&
				instance.TenantId == CurrentTenant.Id && form.TenantId == CurrentTenant.Id &&
				form.Purpose == AdmissionFormPurpose.Registration && form.Version == instance.FormVersion &&
				submissionQuery.Any(s => s.TenantId == CurrentTenant.Id && s.ApplicationId == application.Id && s.FormInstanceId == instance.Id &&
					(!application.FinalizedAt.HasValue || s.SubmittedAt <= application.FinalizedAt.Value))
			orderby onboarding.CreationTime, onboarding.Id
			select new RegistrationRow
			{
				ApplicationId = application.Id, ApplicationNumber = application.ApplicationNumber,
				AcademicYearId = enrollment.AcademicYearId, FormInstanceId = instance.Id,
				FormDefinitionId = form.Id, FinalizedAt = application.FinalizedAt, OnboardingCaseId = onboarding.Id,
				ActivatedAt = eventQuery.Where(e => e.TenantId == CurrentTenant.Id && e.OnboardingCaseId == onboarding.Id && e.EventType == "Activated")
					.OrderBy(e => e.CreationTime).Select(e => (DateTime?)e.CreationTime).FirstOrDefault() ??
					(enrollment.OnboardingCaseId == onboarding.Id ? (DateTime?)enrollment.CreationTime : onboarding.CompletedAt),
				OnboardingStatus = onboarding.Status, CompletedAt = onboarding.CompletedAt
			};
	}
	internal async Task<List<Guid>> GetSourceYearIdsAsync(Guid studentId) =>
		await AsyncExecuter.ToListAsync((await GetSourceRowsAsync(studentId, false)).Select(x => x.AcademicYearId).Distinct());
	internal async Task<Guid?> GetBaselineSubmissionIdAsync(Guid studentId)
	{
		var row = await AsyncExecuter.FirstOrDefaultAsync(await GetSourceRowsAsync(studentId, false));
		if (row == null) return null;
		return await AsyncExecuter.FirstOrDefaultAsync((await submissions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			x.ApplicationId == row.ApplicationId && x.FormInstanceId == row.FormInstanceId && (!row.FinalizedAt.HasValue || x.SubmittedAt <= row.FinalizedAt))
			.OrderByDescending(x => x.AttemptNumber).ThenByDescending(x => x.SubmittedAt).Select(x => (Guid?)x.Id));
	}
	internal async Task<(bool BaselineReadable, HashSet<Guid> SubmissionIds)> GetSourceVisibilityAsync(Guid studentId)
	{
		var rows = await AsyncExecuter.ToListAsync((await GetSourceRowsAsync(studentId, true)).Select(x => new { x.ApplicationId, x.FormInstanceId, x.FinalizedAt }));
		var baseline = await AsyncExecuter.FirstOrDefaultAsync((await GetSourceRowsAsync(studentId, false)).Select(x => (Guid?)x.ApplicationId));
		var ids = rows.Select(x => x.FormInstanceId).Distinct().ToArray();
		var submitted = await AsyncExecuter.ToListAsync((await submissions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.FormInstanceId))
			.Select(x => new { x.Id, x.FormInstanceId, x.ApplicationId, x.AttemptNumber, x.SubmittedAt }));
		return (!baseline.HasValue || rows.Any(x => x.ApplicationId == baseline), rows.Select(row => submitted.Where(x => x.FormInstanceId == row.FormInstanceId &&
			x.ApplicationId == row.ApplicationId && (!row.FinalizedAt.HasValue || x.SubmittedAt <= row.FinalizedAt)).OrderByDescending(x => x.AttemptNumber)
			.ThenByDescending(x => x.SubmittedAt).Select(x => x.Id).FirstOrDefault()).Where(id => id != Guid.Empty).ToHashSet());
	}
	public async Task<List<StudentRecordRegistrationSourceDto>> GetAsync(Guid studentId)
	{
		var rows = await AsyncExecuter.ToListAsync(await GetSourceRowsAsync(studentId, true));
		if (rows.Count == 0) return [];
		// A narrower user's visible returning admission must never become a new baseline.
		var baseline = await AsyncExecuter.FirstOrDefaultAsync((await GetSourceRowsAsync(studentId, false)).Select(x => (Guid?)x.ApplicationId));
		var instanceIds = rows.Select(x => x.FormInstanceId).Distinct().ToArray();
		var submissionRows = await submissions.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(instanceIds, x.FormInstanceId));
		var definitionIds = rows.Select(x => x.FormDefinitionId).Distinct().ToArray();
		var definitions = (await forms.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(definitionIds, x.Id))).ToDictionary(x => x.Id);
		// Tenant-bounded scalar ancestry resolves real version families; labels and reused codes are not identity.
		var lineage = await AsyncExecuter.ToListAsync((await forms.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id).Select(x => new { x.Id, x.ParentVersionId }));
		var parents = lineage.ToDictionary(x => x.Id, x => x.ParentVersionId);
		var sectionRows = await sections.GetListAsync(x => Enumerable.Contains(definitionIds, x.FormDefinitionId));
		var fieldRows = await fields.GetListAsync(x => Enumerable.Contains(definitionIds, x.FormDefinitionId));
		var result = new List<StudentRecordRegistrationSourceDto>();
		foreach (var row in rows.DistinctBy(x => x.ApplicationId))
		{
			var submission = submissionRows.Where(x => x.FormInstanceId == row.FormInstanceId && x.ApplicationId == row.ApplicationId &&
				(!row.FinalizedAt.HasValue || x.SubmittedAt <= row.FinalizedAt.Value)).OrderByDescending(x => x.AttemptNumber).ThenByDescending(x => x.SubmittedAt).FirstOrDefault();
			if (submission == null) continue;
			var definition = definitions[row.FormDefinitionId];
			var dto = AdmissionApplicationFormService.MapDefinition(definition,
				sectionRows.Where(x => x.FormDefinitionId == definition.Id), fieldRows.Where(x => x.FormDefinitionId == definition.Id).ToList());
			var family = definition.Id;
			var seen = new HashSet<Guid> { family };
			while (parents.TryGetValue(family, out var parent) && parent.HasValue && parents.ContainsKey(parent.Value) && seen.Add(parent.Value)) family = parent.Value;
			result.Add(new()
			{
				ApplicationId = row.ApplicationId, ApplicationNumber = row.ApplicationNumber, AcademicYearId = row.AcademicYearId,
				FormInstanceId = row.FormInstanceId, SubmissionId = submission.Id, SubmittedAt = submission.SubmittedAt,
				FinalizedAt = row.FinalizedAt, OnboardingCaseId = row.OnboardingCaseId, ActivatedAt = row.ActivatedAt,
				OnboardingStatus = row.OnboardingStatus.ToString(), FormDefinitionId = definition.Id, FormFamilyId = family,
				FormVersion = definition.Version, FormName = definition.Name, Definition = dto,
				OriginalValues = ReadValues(dto, submission.SnapshotJson), IsBaseline = row.ApplicationId == baseline
			});
		}
		return result;
	}

	public async Task<List<StudentRecordAdmissionOnboardingDto>> GetSummaryAsync(Guid studentId, Guid? yearId = null)
	{
		var enrollments = await enrollmentScope.GetQueryableAsync();
		var onboarding = await cases.GetQueryableAsync();
		var admission = await applications.GetQueryableAsync();
		var eventQuery = await events.GetQueryableAsync();
		var query = from e in enrollments
			join c in onboarding on (Guid?)e.Id equals c.StudentEnrollmentId
			join a in admission on c.ApplicationId equals a.Id
			where e.StudentId == studentId && e.TenantId == CurrentTenant.Id && c.TenantId == CurrentTenant.Id &&
				c.StudentId == studentId && c.AcademicYearId == e.AcademicYearId && c.Status != StudentOnboardingStatus.Cancelled &&
				a.TenantId == CurrentTenant.Id && (a.StudentId == studentId || a.EnrolledStudentId == studentId) &&
				(a.Status == AdmissionApplicationStatus.AdmissionFinalized || a.Status == AdmissionApplicationStatus.Enrolled) &&
				(!yearId.HasValue || e.AcademicYearId == yearId)
			orderby c.CreationTime descending, c.Id
			select new StudentRecordAdmissionOnboardingDto { ApplicationId = a.Id, ApplicationNumber = a.ApplicationNumber,
				AcademicYearId = e.AcademicYearId, FinalizedAt = a.FinalizedAt, OnboardingCaseId = c.Id,
				Status = c.Status.ToString(), ActivatedAt = eventQuery.Where(x => x.TenantId == CurrentTenant.Id && x.OnboardingCaseId == c.Id && x.EventType == "Activated")
					.OrderBy(x => x.CreationTime).Select(x => (DateTime?)x.CreationTime).FirstOrDefault() ??
					(e.OnboardingCaseId == c.Id ? (DateTime?)e.CreationTime : c.CompletedAt), CompletedAt = c.CompletedAt };
		var result = await AsyncExecuter.ToListAsync(query);
		var caseIds = result.Select(x => x.OnboardingCaseId).ToArray();
		var accountHandoffs = await handoffs.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(caseIds, x.OnboardingCaseId) &&
			x.Required && (x.ServiceKey == "student-account" || x.ServiceKey == "guardian-account"));
		var emailRequests = await emails.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(caseIds, x.CaseId));
		var sourceCases = await cases.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(caseIds, x.Id));
		var settings = new Dictionary<Guid, JsonElement?>();
		foreach (var item in result)
		{
			var source = sourceCases.First(x => x.Id == item.OnboardingCaseId);
			JsonElement? rules;
			if (source.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var snapshot) && snapshot is string text)
				rules = JsonSerializer.Deserialize<JsonElement>(text);
			else
			{
				if (!settings.TryGetValue(source.AcademicYearId, out rules))
					settings[source.AcademicYearId] = rules = (await configuration.GetAsync(source.AcademicYearId))?.Readiness(source);
			}
			var expectedEmails = new[] { "student", "guardian" }.Where(type => rules.HasValue &&
				OnboardingRuleConfiguration.Boolean(rules.Value, type == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail")).ToList();
			var accounts = accountHandoffs.Where(x => x.OnboardingCaseId == item.OnboardingCaseId).ToList();
			var requests = emailRequests.Where(x => x.CaseId == item.OnboardingCaseId).ToList();
			var pending = accounts.Any(x => x.Status != StudentOnboardingServiceHandoffStatus.Acknowledged) ||
				expectedEmails.Any(type => !requests.Any(x => x.AccountType == type && x.Status == "Sent"));
			item.CredentialsStatus = accounts.Count == 0 && expectedEmails.Count == 0 ? "NotRequired" : !pending ? "Completed" :
				accounts.Any(x => x.Status == StudentOnboardingServiceHandoffStatus.Failed) || requests.Any(x => x.Status == "Failed") ? "Failed" : "Pending";
		}
		return result;
	}

	internal static Dictionary<string, JsonElement> ReadValues(AdmissionFormDto definition, string snapshot)
	{
		var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(snapshot) ?? [];
		return definition.Sections.SelectMany(x => x.Fields).Where(x => raw.ContainsKey(x.Code))
			.ToDictionary(x => x.Code, x => ToTypedValue(raw[x.Code], x.FieldType, definition.Sections.First(s => s.Id == x.SectionId).IsRepeatable), StringComparer.OrdinalIgnoreCase);
	}
	internal static JsonElement ToTypedValue(string value, AdmissionFormFieldType type, bool repeatable = false)
	{
		if (repeatable)
		{
			try
			{
				var array = JsonSerializer.Deserialize<JsonElement>(value);
				return array.ValueKind == JsonValueKind.Array ? JsonSerializer.SerializeToElement(array.EnumerateArray().Select(item =>
					ToTypedValue(item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.GetRawText(), type)).ToArray()) : JsonSerializer.SerializeToElement(value);
			}
			catch (JsonException) { return JsonSerializer.SerializeToElement(value); }
		}
		if (type is AdmissionFormFieldType.MultiSelect or AdmissionFormFieldType.AddressGroup)
		{
			try { return JsonSerializer.Deserialize<JsonElement>(value); }
			catch (JsonException) { return JsonSerializer.SerializeToElement(value); }
		}
		if (type == AdmissionFormFieldType.Checkbox && bool.TryParse(value, out var boolean)) return JsonSerializer.SerializeToElement(boolean);
		if (type == AdmissionFormFieldType.Number && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return JsonSerializer.SerializeToElement(number);
		return JsonSerializer.SerializeToElement(value);
	}
	private sealed class RegistrationRow
	{
		public Guid ApplicationId { get; set; }
		public string ApplicationNumber { get; set; } = "";
		public Guid AcademicYearId { get; set; }
		public Guid FormInstanceId { get; set; }
		public Guid FormDefinitionId { get; set; }
		public DateTime? FinalizedAt { get; set; }
		public Guid OnboardingCaseId { get; set; }
		public DateTime? ActivatedAt { get; set; }
		public DateTime? CompletedAt { get; set; }
		public StudentOnboardingStatus OnboardingStatus { get; set; }
	}
}
