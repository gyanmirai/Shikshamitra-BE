using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance;

[RemoteService(false)]
public class FinanceWorkContextAppService(
	FinanceScopeAccessService scopeAccess,
	IUserScopeService userScopes,
	IPermissionChecker permissions,
	IReadOnlyRepository<Student, Guid> students,
	IAcademicYearRepository years,
	IIdentityUserRepository identities) : ApplicationService, IFinanceWorkContextAppService
{
	public async Task<FinanceWorkContextDto> GetAsync(Guid academicYearId)
	{
		if (!CurrentUser.Id.HasValue || !CurrentTenant.Id.HasValue || academicYearId == Guid.Empty ||
			!await years.AnyAsync(year => year.Id == academicYearId && year.TenantId == CurrentTenant.Id))
			throw new AbpAuthorizationException("Finance work context is unavailable.");
		var identity = await identities.FindAsync(CurrentUser.Id.Value);
		if (identity == null || identity.TenantId != CurrentTenant.Id || !identity.IsActive)
			throw new AbpAuthorizationException("Finance work context is unavailable.");

		var capabilityNames = new Dictionary<string, string[]>(StringComparer.Ordinal)
		{
			["charge"] = [ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add, string.Empty,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Reject],
			["concession"] = [ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Add,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Submit,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Approve,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.Reject],
			["refund"] = [ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Submit,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject],
			["adjustment"] = [ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add,
				string.Empty,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject]
		};
		var result = new FinanceWorkContextDto { AcademicYearId = academicYearId };
		foreach (var (module, names) in capabilityNames)
		{
			var granted = new bool[names.Length];
			for (var i = 0; i < names.Length; i++)
				granted[i] = !string.IsNullOrEmpty(names[i]) && await permissions.IsGrantedAsync(names[i]);
			result.RequestCapabilities[module] = new FinanceRequestCapabilityDto
			{
				View = granted[0], Add = granted[1], Submit = granted[2],
				Approve = granted[3], Reject = granted[4]
			};
		}

		result.CanViewAllTenant = await userScopes.IsAdminUserAsync(CurrentUser.Id.Value);
		var isTeacher = await scopeAccess.IsClassTeacherRequestUserAsync();
		result.ScopeLabel = result.CanViewAllTenant ? "Tenant-wide" :
			isTeacher ? "Assigned classes and divisions" : "Accessible scope";
		var visiblePermission = capabilityNames.FirstOrDefault(pair =>
			result.RequestCapabilities[pair.Key].View).Value;
		if (visiblePermission == null) return result;

		var studentIds = await scopeAccess.GetRequestScopedStudentIdsQueryAsync(academicYearId, visiblePermission[0]);
		var scopedStudents = (await students.GetQueryableAsync()).Where(student => studentIds.Contains(student.Id));
		result.ScopeOptions = await AsyncExecuter.ToListAsync(scopedStudents
			.Where(student => student.AcademicBoardId.HasValue && student.AcademicMediumStageId.HasValue &&
				student.StandardId.HasValue && student.DivisionId.HasValue)
			.Select(student => new FinanceScopeOptionDto
			{
				BoardId = student.AcademicBoardId!.Value, MediumId = student.AcademicMediumId,
				StageId = student.AcademicMediumStageId!.Value,
				StandardId = student.StandardId!.Value, DivisionId = student.DivisionId!.Value
			}).Distinct());
		return result;
	}
}
