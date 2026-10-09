using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Uow;
using System.Text.Json;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
    private static string IdentifierGroupLockKey(Guid yearId, string type) => $"IdentifierGroupLocked:{yearId:N}:{type}";
    private async Task<bool> IsDivisionIdentifierGroupLockedAsync(Guid? divisionId, Guid yearId, string type)
    {
        if (!divisionId.HasValue) return false;
        var division = await divisionRepository.FindAsync(divisionId.Value);
        return division != null && division.ExtraProperties.TryGetValue(IdentifierGroupLockKey(yearId, type), out var value) && bool.TryParse(value?.ToString(), out var locked) && locked;
    }

	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier)]
	public async Task SetIdentifierLockAsync(SetStudentIdentifierLockDto input)
	{
		if (input.IdentifierType is not ("gr" or "roll") || input.DivisionId == Guid.Empty || input.AcademicYearId == Guid.Empty)
			throw OnboardingRuleConfiguration.Invalid("Select a division and identifier type.");
		if (!CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		if (!input.IsLocked)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Repair);
			if (!scope.IsAdmin) throw new AbpAuthorizationException();
			if (string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessException(StudentOnboardingErrorCodes.WaiverReasonRequired);
		}
		using var work = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var cases = await caseRepository.GetListAsync(c => c.AcademicYearId == input.AcademicYearId && c.DivisionId == input.DivisionId && c.Status != StudentOnboardingStatus.Cancelled);
		var access = LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentScopeAccessService>();
        await access.EnsureCanAccessDivisionAsync(input.DivisionId, nameof(SetIdentifierLockAsync));
        var division = await divisionRepository.GetAsync(input.DivisionId);
        var standard = await standardRepository.GetAsync(division.StandardId);
        var stage = await mediumStageRepository.GetAsync(standard.EducationMediumStageId);
        var medium = await mediumRepository.GetAsync(stage.EducationMediumId);
        var board = await boardRepository.GetAsync(medium.EducationBoardId);
        if (board.AcademicYearId != input.AcademicYearId) throw OnboardingRuleConfiguration.Invalid("The division must belong to the selected academic year.");
        division.ExtraProperties[IdentifierGroupLockKey(input.AcademicYearId, input.IdentifierType)] = input.IsLocked;
        division.ExtraProperties[$"{IdentifierGroupLockKey(input.AcademicYearId, input.IdentifierType)}:Audit"] = JsonSerializer.Serialize(new
        { isLocked = input.IsLocked, changedBy = CurrentUser.Id, changedAt = Clock.Now, reason = input.Reason });
        await divisionRepository.UpdateAsync(division, false);
		foreach (var item in cases)
		{
			var student = await studentRepository.GetAsync(item.StudentId);
			// A completed admission case can refer to a previous placement. Only the current Records group owns its identifier lock.
			if (item.StudentEnrollmentId.HasValue && (student.AcademicYearId != input.AcademicYearId || student.DivisionId != input.DivisionId)) continue;
			await EnsureCaseAccessAsync(item.Id);
			item.SetIdentifierLocked(input.IdentifierType, input.IsLocked); await caseRepository.UpdateAsync(item, false);
			student.SetIdentifierLocked(input.IdentifierType, input.IsLocked); await studentRepository.UpdateAsync(student, false);
			await AddEventAsync(item.Id, input.IsLocked ? "IdentifierLocked" : "IdentifierUnlocked", $"{input.IdentifierType} number {(input.IsLocked ? "locked" : "unlocked")} for division group. {input.Reason}");
		}
		// Include active Records students in the selected group. Identifiers are locked, never placement.
		foreach (var student in await studentRepository.GetListAsync(s => s.AcademicYearId == input.AcademicYearId && s.DivisionId == input.DivisionId))
		{
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.StudentScopeAccessService>().EnsureCanAccessStudentAsync(student, nameof(SetIdentifierLockAsync));
			student.SetIdentifierLocked(input.IdentifierType, input.IsLocked); await studentRepository.UpdateAsync(student, false);
		}
		await CurrentUnitOfWork!.SaveChangesAsync(); await work.CompleteAsync();
	}
}
