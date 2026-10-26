using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Finance.Mobile;

public record MobileFinanceAccessContext(Guid StudentId, Guid? AcademicYearId, bool IsGuardian, string StudentName,
	Guid UserId, Student Student, IReadOnlyList<AcademicYear> AcademicYears);

/// <summary>Family authorization uses persisted portal associations on every call, independently of staff scopes.</summary>
[Authorize]
[RemoteService(false)]
public class MobileFinanceAccessService(StudentPortalAccessService portal, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<AcademicYear, Guid> years, IDataFilter filter, IClock clock, IPermissionChecker permissions) : ApplicationService, ITransientDependency
{
	public async Task<MobileFinanceAccessContext> RequireAsync(Guid studentId, Guid? academicYearId = null,
		bool guardianOnly = false, string? permission = null, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var requiredPermission = permission ?? ShikshaMitraSubmodulePermissions.Finance.SelfService.View;
		if (!ShikshaMitraSubmodulePermissions.Finance.SelfService.All.Contains(requiredPermission, StringComparer.Ordinal) ||
			!await permissions.IsGrantedAsync(requiredPermission)) throw new AbpAuthorizationException();
		var accounts = (await portal.GetMyStudentsAsync()).Where(x => x.StudentId == studentId).ToList();
		if (accounts.Count == 0 || !CurrentUser.Id.HasValue) throw new AbpAuthorizationException();
		var student = await portal.ResolveStudentAsync(studentId);
		if (student.Status is StudentLifeCycleStatusEnum.InActive or StudentLifeCycleStatusEnum.Left) throw new AbpAuthorizationException();
		var guardian = IsGuardianOnlyAssociation(student.UserId == CurrentUser.Id || accounts.Any(x => x.AccountType == "student" && x.Relationship == "self"),
			accounts.Any(x => x.AccountType == "guardian"));
		var requestPermission = requiredPermission is ShikshaMitraSubmodulePermissions.Finance.SelfService.RequestConcession or
			ShikshaMitraSubmodulePermissions.Finance.SelfService.RequestRefund or ShikshaMitraSubmodulePermissions.Finance.SelfService.RequestAdjustment;
		if ((guardianOnly || requestPermission) && !guardian) throw new AbpAuthorizationException();
		List<StudentEnrollment> history;
		using (filter.Disable<ISoftDelete>())
			history = await enrollments.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId, cancellationToken: cancellationToken);
		var today = clock.Now.Date;
		var ids = history.Where(x => !x.IsDeleted).Select(x => x.AcademicYearId).Distinct().ToList();
		if (student.AcademicYearId.HasValue && !history.Any(x => x.AcademicYearId == student.AcademicYearId)) ids.Add(student.AcademicYearId.Value);
		var available = (await years.GetListAsync(x => x.TenantId == CurrentTenant.Id && ids.Contains(x.Id), cancellationToken: cancellationToken))
			.Where(y => !history.Any(x => x.AcademicYearId == y.Id) || history.Any(x => x.AcademicYearId == y.Id && !x.IsDeleted && (x.IsActive || y.EndDate.Date < today)))
			.OrderByDescending(x => x.StartDate).ToList();
		if (academicYearId.HasValue && (academicYearId == Guid.Empty || available.All(x => x.Id != academicYearId))) throw new AbpAuthorizationException();
		return new(studentId, academicYearId, guardian, string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))), CurrentUser.Id.Value, student, available);
	}

	public static bool IsGuardianOnlyAssociation(bool hasStudentSelfAssociation, bool hasGuardianAssociation) =>
		hasGuardianAssociation && !hasStudentSelfAssociation;
}
