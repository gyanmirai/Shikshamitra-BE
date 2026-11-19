using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Common;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

internal sealed record HomeworkNotificationTeacher(Guid? DivisionId, NotificationRecipientRefDto Reference);

[RemoteService(false)]
public sealed class HomeworkNotificationTeachers(IRepository<AssignedSubject, Guid> subjects,
	IRepository<TeacherSubjectAllocation, Guid> allocations, IRepository<Employee, Guid> employees,
	IRepository<IdentityUser, Guid> users, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<IReadOnlyList<HomeworkNotificationTeacher>?> FindAsync(Guid tenantId, HomeworkReminderRecord record,
		Guid[] divisions, CancellationToken cancellationToken)
	{
		var classId = record.Task.StandardId;
		var taskSubjectId = record.Task.SubjectId;
		var subjectId = record.Subject.Id;
		var classTeachers = from subject in await subjects.GetQueryableAsync()
			where subject.TenantId == tenantId && !subject.IsDeleted && subject.StandardId == classId &&
				(subject.Id == taskSubjectId || subject.SubjectId == taskSubjectId) && subject.Status == SubjectStatus.Published
			from teacher in subject.Teachers
			where teacher.TenantId == tenantId
			select new { teacher.TeacherId, DivisionId = (Guid?)null };
		var divisionTeachers = from allocation in await allocations.GetQueryableAsync()
			where !allocation.IsDeleted && allocation.StandardId == classId && allocation.SubjectId == subjectId &&
				Enumerable.Contains(divisions, allocation.DivisionId)
			from teacher in allocation.Teachers
			where !teacher.IsDeleted
			select new { teacher.TeacherId, DivisionId = (Guid?)allocation.DivisionId };
		var staff = from teacher in classTeachers.Concat(divisionTeachers)
			join employee in await employees.GetQueryableAsync() on teacher.TeacherId equals employee.Id
			join user in await users.GetQueryableAsync() on employee.UserId equals (Guid?)user.Id
			where employee.TenantId == tenantId && !employee.IsDeleted && employee.EmployeeStatusType == EmployeeStatusType.Active &&
				user.TenantId == tenantId && !user.IsDeleted && user.IsActive
			select new { UserId = user.Id, EmployeeId = employee.Id, teacher.DivisionId };
		var rows = await queries.ToListAsync(staff.Distinct().Take(10001), cancellationToken);
		if (rows.Count > 10000) return null;

		var classUserIds = rows.Where(x => !x.DivisionId.HasValue).Select(x => x.UserId).ToHashSet();
		return rows.Where(x => !x.DivisionId.HasValue || !classUserIds.Contains(x.UserId))
			.Select(x => new HomeworkNotificationTeacher(x.DivisionId, new("AssignedTeacher", UserId: x.UserId, EmployeeId: x.EmployeeId)))
			.ToArray();
	}
}
