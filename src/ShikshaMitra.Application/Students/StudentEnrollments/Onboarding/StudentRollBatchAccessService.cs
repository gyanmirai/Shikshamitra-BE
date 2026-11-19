using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Records;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[RemoteService(false)]
public class StudentRollBatchAccessService(StudentScopeAccessService scope, StudentRecordsActorService actors,
	IUserScopeAccessService scopes, IRepository<Division, Guid> divisions, IRepository<Standard, Guid> standards,
	StudentRecordsAccessService records, IStudentOnboardingAppService onboarding) : ApplicationService, ITransientDependency
{
	public async Task EnsureScopeAsync(StudentRollScopeInput input)
	{
		await actors.EnsureActiveAsync();
		if (input.AcademicYearId == Guid.Empty || input.StandardId == Guid.Empty || input.DivisionId == Guid.Empty) throw new BusinessException(StudentRollNumberErrorCodes.InvalidBatch);
		if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View) &&
			!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)) throw new AbpAuthorizationException();
		await scope.EnsureCanAccessDivisionAsync(input.DivisionId, "roll number batch");
		var division = await AsyncExecuter.SingleOrDefaultAsync((await divisions.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.Id == input.DivisionId).Select(x => new { x.StandardId }));
		var standard = await AsyncExecuter.SingleOrDefaultAsync((await standards.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.Id == input.StandardId)
			.Select(x => new { YearId = x.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId }));
		if (division?.StandardId != input.StandardId || standard?.YearId != input.AcademicYearId) throw new BusinessException(StudentOnboardingErrorCodes.PlacementInvalid);
	}
	public async Task<bool> IsAdminAsync() => (await scopes.GetCurrentUserScopeAsync()).IsAdmin;
	public async Task EnsureTargetsAsync(PreviewStudentRollBatchInput input)
	{
		if (input.CaseIds == null) return;
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View);
		foreach (var id in input.CaseIds) await onboarding.EnsureCaseAccessAsync(id);
	}
	public async Task EnsureRowsAsync(StudentRollNumberBatch item, bool write)
	{
		if (write && !await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Add))
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Add);
		await EnsureScopeAsync(new() { AcademicYearId = item.AcademicYearId, StandardId = item.StandardId, DivisionId = item.DivisionId });
		var rows = item.Rows();
		if (rows.Any(x => x.EnrollmentId.HasValue))
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View);
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View);
			if (write) await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create);
			var roles = await records.GetDatabaseRoleNamesAsync();
			foreach (var row in rows.Where(x => x.EnrollmentId.HasValue))
			{
				// Read/write capability projection repeats within one request. Never cache across
				// requests or principals: a later command must recheck live Records eligibility.
				var key = $"RollRecordsAccess:{CurrentTenant.Id}:{CurrentUser.Id}:{row.StudentId}";
				var allowed = CurrentUnitOfWork?.Items.TryGetValue(key, out var saved) == true && saved is StudentRecordsAccess cached
					? cached : await records.EnsureReadableAsync(row.StudentId);
				if (CurrentUnitOfWork != null) CurrentUnitOfWork.Items[key] = allowed;
				if (write && (allowed.Access != "Full" || !allowed.PlacementEditable)) throw new BusinessException("Students:Records:ReadOnly");
				if (allowed.Policy.IsMasked("Identity", roles)) throw new BusinessException("Students:Records:RestrictedFields");
			}
		}
		if (rows.Any(x => x.CaseId.HasValue && !x.EnrollmentId.HasValue))
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.View);
			if (write) await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.AllocateIdentifier);
			foreach (var row in rows.Where(x => x.CaseId.HasValue && !x.EnrollmentId.HasValue)) await onboarding.EnsureCaseAccessAsync(row.CaseId!.Value);
		}
	}
}
