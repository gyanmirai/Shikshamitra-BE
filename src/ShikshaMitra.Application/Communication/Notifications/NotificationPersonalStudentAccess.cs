using System;
using System.Collections.Generic;
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
	IAsyncQueryableExecuter executer, StudentAppAccessEnforcement appAccess) : ITransientDependency
{
	public async Task<IQueryable<Guid>> GetStudentIdsQueryAsync(NotificationSelfContext owner, string? accountType = null)
	{
		var pending = (await reviews.GetQueryableAsync()).Where(x => x.TenantId == owner.TenantId && !x.IsDeleted && x.Status == "Pending");
		var off = await appAccess.GetOffRestrictionsAsync(owner.TenantId);
		return from link in await links.GetQueryableAsync()
			join student in await students.GetQueryableWithoutAutoIncludesAsync() on link.StudentId equals student.Id
			where link.TenantId == owner.TenantId && !link.IsDeleted && link.UserId == owner.UserId &&
				student.TenantId == owner.TenantId && !student.IsDeleted && student.Status == StudentLifeCycleStatusEnum.Enrolled &&
				(accountType == null || link.AccountType == accountType) &&
				!off.Any(x => x.StudentId == student.Id &&
					(link.AccountType == "student" && x.UserId == null ||
						(link.Relationship == "father" || link.Relationship == "mother") && x.UserId == owner.UserId)) &&
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

	internal async Task<IReadOnlyList<NotificationPreferenceStudentDto>> GetPreferenceOptionsAsync(
		NotificationSelfContext owner, CancellationToken cancellationToken)
	{
		var linked = await GetStudentIdsQueryAsync(owner);
		var records = await executer.ToListAsync((await students.GetQueryableWithoutAutoIncludesAsync())
			.Where(x => linked.Contains(x.Id))
			.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ThenBy(x => x.Id)
			.Select(x => new { x.Id, x.FirstName, x.MiddleName, x.LastName }), cancellationToken);
		return records.Select(x =>
		{
			var name = string.Join(" ", new[] { x.FirstName, x.MiddleName, x.LastName }
				.OfType<string>().Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
			return new NotificationPreferenceStudentDto(x.Id, name.Length == 0 ? "Student" : name);
		}).ToArray();
	}
}
