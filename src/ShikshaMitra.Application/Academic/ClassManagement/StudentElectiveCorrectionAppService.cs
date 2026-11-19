using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add)]
public class StudentElectiveCorrectionAppService(
	AdminPermissionAccess admins, StudentElectiveCorrectionReader reader,
	IRepository<StudentElectiveSelection, Guid> selections,
	IRepository<StudentElectiveCorrection, Guid> corrections,
	IDataProtectionProvider protection, IUnitOfWorkManager units) : ApplicationService, IStudentElectiveCorrectionAppService
{
	private IDataProtector Protector => protection.CreateProtector("Academic.LockedElectiveCorrection.v1");

	public async Task<StudentElectiveCorrectionReviewDto> PreviewAsync(StudentElectiveCorrectionInput input)
	{
		await RequireAdminAsync();
		var prepared = await reader.PrepareAsync(input);
		prepared.Review.ExpiresAt = Clock.Now.AddMinutes(10);
		prepared.Review.ReviewToken = Protector.Protect(JsonSerializer.Serialize(new ReviewAssertion(
			CurrentTenant.Id!.Value, CurrentUser.Id!.Value, Fingerprint(input, prepared), prepared.Review.ExpiresAt)));
		return prepared.Review;
	}

	public async Task ConfirmAsync(ConfirmStudentElectiveCorrectionInput input)
	{
		using var unit = units.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await RequireAdminAsync();
		ReviewAssertion assertion;
		try
		{
			assertion = JsonSerializer.Deserialize<ReviewAssertion>(Protector.Unprotect(input.ReviewToken)) ?? throw ReviewChanged();
		}
		catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
		{
			throw ReviewChanged();
		}
		if (assertion.TenantId != CurrentTenant.Id || assertion.UserId != CurrentUser.Id || assertion.ExpiresAt <= Clock.Now) throw ReviewChanged();
		var prepared = await reader.PrepareAsync(input);
		if (assertion.Fingerprint != Fingerprint(input, prepared)) throw ReviewChanged();
		var before = JsonSerializer.Serialize(new { prepared.Selection.SelectedSubjectId, prepared.Selection.StudentPreference, prepared.Selection.PreferenceOverrideSubjectId });
		prepared.Selection.CorrectLockedSelection(input.SelectedSubjectId, input.PreferredSubjectId, prepared.Review.PreferredSubject);
		await selections.UpdateAsync(prepared.Selection);
		await corrections.InsertAsync(new StudentElectiveCorrection(GuidGenerator.Create(), CurrentTenant.Id!.Value,
			input.StudentId, input.ElectiveGroupId, CurrentUser.Id!.Value, before,
			JsonSerializer.Serialize(new { prepared.Selection.SelectedSubjectId, prepared.Selection.StudentPreference, prepared.Selection.PreferenceOverrideSubjectId }),
			JsonSerializer.Serialize(prepared.Review.Impacts), input.Reason?.Trim()));
		await unit.CompleteAsync();
	}

	private async Task RequireAdminAsync()
	{
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || !await admins.IsAdminAsync(CurrentUser.Id.Value) ||
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add))
			throw new AbpAuthorizationException();
	}

	private static string Fingerprint(StudentElectiveCorrectionInput input, PreparedElectiveCorrection prepared) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
		{
			input.AcademicMediumId, input.ClassId, input.DivisionId, input.ElectiveGroupId, input.StudentId,
			input.SelectedSubjectId, input.PreferredSubjectId, input.Reason,
			prepared.Selection.ConcurrencyStamp, CurrentSelectedSubjectId = prepared.Selection.SelectedSubjectId, prepared.Selection.StudentPreference,
			prepared.Selection.PreferenceOverrideSubjectId, prepared.StateFingerprint
		}))));

	private static BusinessException ReviewChanged() => new("ClassManagement:ElectiveCorrectionReviewChanged");
	private sealed record ReviewAssertion(Guid TenantId, Guid UserId, string Fingerprint, DateTime ExpiresAt);
}
