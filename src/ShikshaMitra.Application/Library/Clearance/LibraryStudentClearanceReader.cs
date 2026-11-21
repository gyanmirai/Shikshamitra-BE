using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Clearance;

/// <summary>Physical loan evidence for a student already authorized by the Student Records owner.</summary>
public class LibraryStudentClearanceReader(IRepository<BookIssue, Guid> issues, IRepository<LibraryMember, Guid> members,
	IAsyncQueryableExecuter queries, ICurrentTenant tenant) : ITransientDependency
{
	internal async Task<int> CountOutstandingAsync(Guid studentId) =>
		await queries.CountAsync(await OutstandingLoansAsync(studentId));

	internal async Task<List<Guid>> GetOutstandingIssueIdsAsync(Guid studentId) =>
		await queries.ToListAsync((await OutstandingLoansAsync(studentId)).OrderBy(issue => issue.Id).Select(issue => issue.Id));

	private async Task<IQueryable<BookIssue>> OutstandingLoansAsync(Guid studentId)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty || studentId == Guid.Empty) throw new AbpAuthorizationException();
		var aliases = (await members.GetQueryableAsync()).Where(member => member.TenantId == tenantId && member.StudentId == studentId);
		return (await issues.GetQueryableAsync()).Where(issue => issue.TenantId == tenantId && issue.MemberRole == 0 &&
			issue.Status != BookIssueStatus.Returned && (issue.MemberId == studentId || aliases.Any(member => member.Id == issue.MemberId)));
	}
}
