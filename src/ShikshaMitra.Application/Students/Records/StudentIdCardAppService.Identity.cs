using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.SchoolIdentity;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	private readonly record struct IdentityReadKey(Guid? TenantId, Guid? BoardId, Guid? MediumId, Guid? AcademicYearId, Guid? OrganizationId, bool Signature);

	private async Task<bool> CanReadSignatureConfigurationAsync() =>
		await AuthorizationService.IsGrantedAsync(SchoolIdentityPermissions.View) &&
		await AuthorizationService.IsGrantedAsync(SchoolIdentityPermissions.AccessSignature);

	private async Task<List<StudentIdCardDto>> RedactCardSignaturesAsync(List<StudentIdCardDto> cards)
	{
		if (cards.Any(card => card.TemplateSnapshot.Signature != null) && !await CanReadSignatureConfigurationAsync())
			foreach (var card in cards)
			{
				card.TemplateSnapshot.Signature = null;
				if (card.TemplateSnapshot.SchoolIdentity is not { } identity) continue;
				identity.SignatureAssetId = null;
				identity.PrincipalName = null;
				identity.Designation = null;
			}
		return cards;
	}

	private async Task<StudentIdCardTemplateDto> CaptureIdentityTemplateAsync(StudentIdCardTemplateDto template, StudentDocumentSnapshot student,
		Dictionary<IdentityReadKey, SchoolIdentityDocumentSnapshotDto> identitySnapshots)
	{
		var frozen = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(JsonSerializer.Serialize(template, JsonOptions), JsonOptions)!;
		// Issued cards retain their design, never a live template's other student assignments.
		frozen.Selection = null;
		frozen.SelectionRestricted = false;
		var inheritSignature = template.SchemaVersion == 2 && template.Signature?.Mode == SchoolIdentityAssetMode.Inherit;
		var key = new IdentityReadKey(CurrentTenant.Id, student.BoardId, student.MediumId, student.AcademicYearId, student.OrganizationId, inheritSignature);
		if (!identitySnapshots.TryGetValue(key, out var identity))
			identitySnapshots[key] = identity = await schoolIdentity.CaptureAsync(student.BoardId, student.MediumId, student.AcademicYearId, signature: inheritSignature, organizationId: student.OrganizationId);
		// Each card owns its mutable template overrides; the operation retains only raw persisted metadata.
		frozen.SchoolIdentity = JsonSerializer.Deserialize<SchoolIdentityDocumentSnapshotDto>(JsonSerializer.Serialize(identity, JsonOptions), JsonOptions)!;
		SchoolIdentityTemplateAssetService.Apply(frozen.SchoolIdentity, frozen.Logo, SchoolIdentityAssetKind.Logo);
		if (frozen.SchemaVersion == 2 && frozen.Signature != null)
			SchoolIdentityTemplateAssetService.Apply(frozen.SchoolIdentity, frozen.Signature, SchoolIdentityAssetKind.Signature);
		frozen.SchoolIdentity.BannerAssetId = null;
		return frozen;
	}
}
