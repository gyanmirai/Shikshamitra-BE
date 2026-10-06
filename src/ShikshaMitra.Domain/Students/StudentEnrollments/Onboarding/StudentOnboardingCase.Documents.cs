using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public record StudentOnboardingDocumentOverride(Guid DocumentTypeId, string Reason, Guid StaffUserId, DateTime OverriddenAt);

public partial class StudentOnboardingCase
{
	private const string DocumentOverrideProperty = "RequiredDocumentOverrides";

	public IReadOnlyList<StudentOnboardingDocumentOverride> GetDocumentOverrides() =>
		ExtraProperties.TryGetValue(DocumentOverrideProperty, out var value) && !string.IsNullOrWhiteSpace(value?.ToString())
			? JsonSerializer.Deserialize<List<StudentOnboardingDocumentOverride>>(value.ToString()!) ?? [] : [];

	public void OverrideRequiredDocuments(IEnumerable<Guid> documentTypeIds, string reason, Guid staffUserId, DateTime at)
	{
		if (Status is StudentOnboardingStatus.Completed or StudentOnboardingStatus.Cancelled)
			throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		var note = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000).Trim();
		Check.NotDefaultOrNull<Guid>(staffUserId, nameof(staffUserId));
		var ids = documentTypeIds.Distinct().ToArray();
		if (ids.Length == 0 || ids.Contains(Guid.Empty))
			throw OnboardingRuleConfiguration.Invalid("Select at least one current required document.");
		var overrides = GetDocumentOverrides().Where(item => !ids.Contains(item.DocumentTypeId)).ToList();
		overrides.AddRange(ids.Select(id => new StudentOnboardingDocumentOverride(id, note, staffUserId, at.ToUniversalTime())));
		ExtraProperties[DocumentOverrideProperty] = JsonSerializer.Serialize(overrides);
		if (!StudentEnrollmentId.HasValue) RecordProgress(at);
	}
}
