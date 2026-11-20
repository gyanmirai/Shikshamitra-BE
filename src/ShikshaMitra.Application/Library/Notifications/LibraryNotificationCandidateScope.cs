using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationCandidateScope(LibraryStoredLoanScope loans, LibraryStoredReservationScope requests,
	IRepository<BookIssue, Guid> issues, IRepository<BookReservation, Guid> reservations,
	IRepository<LibraryMember, Guid> memberships, IUserScopeService scopes, ICurrentUser user,
	ICurrentTenant tenant, IAsyncQueryableExecuter queries) : ITransientDependency
{
	public static bool HasRecordedSource(string? type) => type is nameof(BookIssue) or nameof(BookReservation);

	public async Task<Guid> RequireManualRecipientAsync(string type, Guid? sourceId, Guid memberId)
	{
		if (!sourceId.HasValue || !user.Id.HasValue || !tenant.Id.HasValue) throw new AbpAuthorizationException();
		var scope = await scopes.GetUserScopeAsync(user.Id.Value);
		var aliases = (await memberships.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id);
		var canonicalId = await queries.FirstOrDefaultAsync(aliases.Where(row => row.Id == memberId).Select(row => (Guid?)row.StudentId)) ?? memberId;
		var allowed = type == nameof(BookIssue)
			? await queries.AnyAsync((await loans.GetReadableLoansAsync(scope)).Where(row => row.Id == sourceId.Value &&
				(row.MemberId == canonicalId || aliases.Any(alias => alias.Id == row.MemberId && alias.StudentId == canonicalId))))
			: type == nameof(BookReservation) && await queries.AnyAsync((await requests.GetReadableRequestsAsync(scope)).Where(row =>
				row.Id == sourceId.Value && (row.CanonicalMemberId.HasValue ? row.CanonicalMemberId.Value == canonicalId :
					row.MemberId == canonicalId || aliases.Any(alias => alias.Id == row.MemberId && alias.StudentId == canonicalId))));
		if (!allowed) throw new AbpAuthorizationException();
		return canonicalId;
	}

	public async Task<IQueryable<BookIssue>> GetLoansAsync(LibraryNotificationOrigin origin)
	{
		if (IsScheduled(origin)) return (await issues.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id && tenant.Id.HasValue);
		if (!origin.ActorUserId.HasValue || origin.IsSystem) return (await issues.GetQueryableAsync()).Where(_ => false);
		return await loans.GetReadableLoansAsync(await scopes.GetUserScopeAsync(origin.ActorUserId.Value));
	}

	public async Task<IQueryable<BookReservation>> GetRequestsAsync(LibraryNotificationOrigin origin)
	{
		if (IsScheduled(origin)) return (await reservations.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id && tenant.Id.HasValue);
		if (!origin.ActorUserId.HasValue || origin.IsSystem) return (await reservations.GetQueryableAsync()).Where(_ => false);
		return await requests.GetReadableRequestsAsync(await scopes.GetUserScopeAsync(origin.ActorUserId.Value));
	}

	private static bool IsScheduled(LibraryNotificationOrigin origin) =>
		origin.IsSystem && !origin.ActorUserId.HasValue && string.IsNullOrWhiteSpace(origin.Action);
}
