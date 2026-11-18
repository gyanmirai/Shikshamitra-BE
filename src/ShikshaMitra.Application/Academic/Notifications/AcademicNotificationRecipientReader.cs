using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationRecipientReader(ICurrentTenant tenant, IStudentRepository students,
	IRepository<StudentFamilyDetail, Guid> families, IRepository<StudentPortalAccountLink, Guid> links,
	IRepository<StudentGuardianAccessReview, Guid> reviews, IRepository<IdentityUser, Guid> users,
	IRepository<Employee, Guid> employees, IAsyncQueryableExecuter executer)
	: IAcademicNotificationRecipientProjection, ITransientDependency
{
	public async Task<AcademicNotificationRecipientData> LoadAsync(Guid tenantId,
		IReadOnlyList<NotificationRecipientRefDto> sourceRecipients, CancellationToken cancellationToken)
	{
		if (tenantId == Guid.Empty || tenant.Id != tenantId) throw new AbpAuthorizationException();
		if (sourceRecipients.Count > 10000) throw new BusinessException(NotificationErrorCodes.RecipientIneligible);
		var studentIds = sourceRecipients.Where(x => x.Kind == "Student" && x.StudentId.HasValue)
			.Select(x => x.StudentId!.Value).Distinct().ToArray();
		var studentRows = studentIds.Length == 0 ? [] : await executer.ToListAsync(
			(await students.GetQueryableWithoutAutoIncludesAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
				Enumerable.Contains(studentIds, x.Id)), cancellationToken);
		var familyRows = studentIds.Length == 0 ? [] : await executer.ToListAsync((await families.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && Enumerable.Contains(studentIds, x.Id)), cancellationToken);
		var linkRows = studentIds.Length == 0 ? [] : await executer.ToListAsync((await links.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && Enumerable.Contains(studentIds, x.StudentId)), cancellationToken);
		var pendingReviews = studentIds.Length == 0 ? [] : await executer.ToListAsync((await reviews.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Status == "Pending" && Enumerable.Contains(studentIds, x.StudentId))
			.Select(x => x.StudentId).Distinct(), cancellationToken);
		var userIds = linkRows.Select(x => x.UserId).Concat(sourceRecipients.Where(x => x.UserId.HasValue)
			.Select(x => x.UserId!.Value)).Distinct().ToArray();
		var userRows = userIds.Length == 0 ? [] : await executer.ToListAsync((await users.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && Enumerable.Contains(userIds, x.Id)), cancellationToken);
		var userById = userRows.ToDictionary(x => x.Id);
		var employeeIds = sourceRecipients.Where(x => x.EmployeeId.HasValue).Select(x => x.EmployeeId!.Value).Distinct().ToArray();
		var employeeRows = await executer.ToListAsync((await employees.GetQueryableAsync())
			.Where(x => x.TenantId == tenantId && !x.IsDeleted && Enumerable.Contains(employeeIds, x.Id))
			.Select(x => new { x.Id, x.UserId, x.EmployeeStatusType }), cancellationToken);
		var familyById = familyRows.ToDictionary(x => x.Id);
		var accountsByStudent = linkRows.Where(x => userById.ContainsKey(x.UserId))
			.ToLookup(x => x.StudentId, x => new AcademicNotificationAccount(x, userById[x.UserId]));
		var children = studentRows.Select(student => new AcademicNotificationStudent(student,
			familyById.GetValueOrDefault(student.Id), pendingReviews.Contains(student.Id), accountsByStudent[student.Id].ToArray())).ToArray();
		var staff = sourceRecipients.Where(x => x.Kind != "Student" && x.UserId.HasValue).Distinct()
			.Select(x => {
				var user = userById.GetValueOrDefault(x.UserId!.Value);
				var active = user is { IsActive: true } && (!user.LockoutEnabled || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow) &&
					(!x.EmployeeId.HasValue || employeeRows.Any(e => e.Id == x.EmployeeId && e.UserId == x.UserId && e.EmployeeStatusType == EmployeeStatusType.Active));
				return new AcademicNotificationStaff(x.UserId.Value, x.EmployeeId, active, user?.Email, user?.PhoneNumber);
			}).ToArray();
		return new(children, staff);
	}
}
