using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationPersonalStudentAccess(IRepository<StudentPortalAccountLink, Guid> links,
	IStudentRepository students, IRepository<StudentGuardianAccessReview, Guid> reviews,
	IAsyncQueryableExecuter executer) : ITransientDependency
{
	public async Task<IQueryable<Guid>> GetStudentIdsQueryAsync(NotificationSelfContext owner, string? accountType = null)
	{
		var pending = (await reviews.GetQueryableAsync()).Where(x => x.TenantId == owner.TenantId && !x.IsDeleted && x.Status == "Pending");
		return from link in await links.GetQueryableAsync()
			join student in await students.GetQueryableWithoutAutoIncludesAsync() on link.StudentId equals student.Id
			where link.TenantId == owner.TenantId && !link.IsDeleted && link.UserId == owner.UserId &&
				student.TenantId == owner.TenantId && !student.IsDeleted && student.Status == StudentLifeCycleStatusEnum.Enrolled &&
				(accountType == null || link.AccountType == accountType) &&
				(link.AccountType == "student" && link.Relationship == "self" || link.AccountType == "guardian" &&
					(link.Relationship == "father" || link.Relationship == "mother" || link.Relationship == "guardian") &&
					!pending.Any(x => x.StudentId == student.Id))
			select student.Id;
	}

	public async Task RequireAsync(NotificationSelfContext owner, Guid studentId, CancellationToken cancellationToken)
	{
		if (studentId == Guid.Empty || !await executer.AnyAsync((await GetStudentIdsQueryAsync(owner)).Where(x => x == studentId), cancellationToken))
			throw new AbpAuthorizationException();
	}
}
