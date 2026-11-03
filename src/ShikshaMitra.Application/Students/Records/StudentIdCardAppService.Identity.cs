using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	private readonly record struct IdentityReadKey(Guid? TenantId, Guid? BoardId, Guid? MediumId, Guid? AcademicYearId, Guid? OrganizationId, bool Signature);

	private async Task<StudentIdCardTemplateDto> CaptureIdentityTemplateAsync(StudentIdCardTemplateDto template, StudentDocumentSnapshot student,
		Dictionary<IdentityReadKey, SchoolIdentityDocumentSnapshotDto> identitySnapshots)
	{
		var frozen = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(JsonSerializer.Serialize(template, JsonOptions), JsonOptions)!;
		// Issued cards retain their design, never a live template's other student assignments.
		frozen.Selection = null;
		frozen.SelectionRestricted = false;
		var key = new IdentityReadKey(CurrentTenant.Id, student.BoardId, student.MediumId, student.AcademicYearId, student.OrganizationId, false);
		if (!identitySnapshots.TryGetValue(key, out var identity))
			identitySnapshots[key] = identity = await schoolIdentity.CaptureAsync(student.BoardId, student.MediumId, student.AcademicYearId, signature: false, organizationId: student.OrganizationId);
		// Each card owns its mutable template overrides; the operation retains only raw persisted metadata.
		frozen.SchoolIdentity = JsonSerializer.Deserialize<SchoolIdentityDocumentSnapshotDto>(JsonSerializer.Serialize(identity, JsonOptions), JsonOptions)!;
		SchoolIdentityTemplateAssetService.Apply(frozen.SchoolIdentity, frozen.Logo, SchoolIdentityAssetKind.Logo);
		frozen.SchoolIdentity.BannerAssetId = null;
		return frozen;
	}
}
