using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Notifications;

[RemoteService(false)]
public class LibraryNotificationRecipientRecoveryReader(IRepository<LibraryMember, Guid> memberships,
	IRepository<BookIssue, Guid> issues, IRepository<LibraryCharge, Guid> charges) : ApplicationService, ITransientDependency
{
	public async Task<HashSet<Guid>> ReadAsync(IReadOnlyCollection<Guid> canonicalIds)
	{
		if (canonicalIds.Count == 0) return [];
		var people = canonicalIds.ToArray();
		var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(member => member.TenantId == CurrentTenant.Id && Enumerable.Contains(people, member.StudentId))
			.Select(member => new { member.Id, member.StudentId }));
		var canonical = aliases.ToDictionary(member => member.Id, member => member.StudentId);
		var memberIds = people.Concat(aliases.Select(member => member.Id)).Distinct().ToArray();
		var loans = await AsyncExecuter.ToListAsync((await issues.GetQueryableAsync())
			.Where(issue => issue.TenantId == CurrentTenant.Id && Enumerable.Contains(memberIds, issue.MemberId) && issue.Status == BookIssueStatus.Issued)
			.Select(issue => issue.MemberId).Distinct());
		var assessments = await AsyncExecuter.ToListAsync((await charges.GetQueryableAsync())
			.Where(charge => charge.TenantId == CurrentTenant.Id && Enumerable.Contains(memberIds, charge.MemberId) &&
				charge.PaymentStatus == ChargePaymentStatus.Pending && charge.Amount > 0)
			.Select(charge => charge.MemberId).Distinct());
		return loans.Concat(assessments).Select(id => canonical.GetValueOrDefault(id, id)).ToHashSet();
	}
}
