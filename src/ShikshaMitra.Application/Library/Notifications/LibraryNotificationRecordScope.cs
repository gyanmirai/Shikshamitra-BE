using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationRecordScope(LibraryStoredLoanScope loanScope, LibraryStoredReservationScope requestScope,
	LibraryAssessmentSourceScopeQuery assessmentScope,
	IRepository<LibraryMember, Guid> memberships, ILibraryAccessService access, IAsyncQueryableExecuter queries,
	ICurrentTenant tenant) : ITransientDependency
{
	public async Task<IQueryable<LibraryNotificationHistory>> ApplyAsync(IQueryable<LibraryNotificationHistory> history, UserScopeContext scope)
	{
		var query = history.Where(row => row.TenantId == tenant.Id);
		if (!tenant.Id.HasValue || scope.TenantId != tenant.Id || !scope.HasAnyAccess) return query.Where(_ => false);
		if (scope.HasTenantWideScope()) return query;
		query = LibraryNotificationSource.Apply(query);
		var loans = await loanScope.GetReadableLoansAsync(scope);
		var requests = await requestScope.GetReadableRequestsAsync(scope);
		var assessments = await assessmentScope.GetQueryAsync(scope);
		var aliases = (await memberships.GetQueryableAsync()).Where(member => member.TenantId == tenant.Id);
		var currentMembers = (await access.GetMemberIdsAsync(scope)).ToArray();
		return query.Where(row => row.RecipientMemberId.HasValue &&
			(row.RelatedEntityType == nameof(BookIssue) ?
				row.RelatedEntityId.HasValue && loans.Any(loan => loan.Id == row.RelatedEntityId.Value &&
					(loan.MemberId == row.RecipientMemberId.Value || aliases.Any(member => member.Id == loan.MemberId && member.StudentId == row.RecipientMemberId.Value))) :
			 row.RelatedEntityType == nameof(BookReservation) ?
				row.RelatedEntityId.HasValue && requests.Any(request => request.Id == row.RelatedEntityId.Value &&
					(request.CanonicalMemberId.HasValue ? request.CanonicalMemberId.Value == row.RecipientMemberId.Value :
					 request.MemberId == row.RecipientMemberId.Value || aliases.Any(member => member.Id == request.MemberId && member.StudentId == row.RecipientMemberId.Value))) :
			 row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RelatedEntityType == nameof(LibraryCharge) ?
				row.RelatedEntityId.HasValue && assessments.Any(source => source.SourceId == row.RelatedEntityId.Value &&
					(source.StudentId == row.RecipientMemberId || source.EmployeeId == row.RecipientMemberId)) :
			 Enumerable.Contains(currentMembers, row.RecipientMemberId.Value)));
	}

	public async Task<bool> CanReadAsync(LibraryNotificationHistory row, UserScopeContext scope)
	{
		if (!tenant.Id.HasValue || row.TenantId != tenant.Id || scope.TenantId != tenant.Id || !scope.HasAnyAccess) return false;
		if (row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RelatedEntityType == nameof(LibraryCharge))
			return row.RelatedEntityId.HasValue && row.RecipientMemberId.HasValue &&
				await queries.AnyAsync((await assessmentScope.GetQueryAsync(scope)).Where(source => source.SourceId == row.RelatedEntityId.Value &&
					(source.StudentId == row.RecipientMemberId || source.EmployeeId == row.RecipientMemberId)));
		if (scope.HasTenantWideScope()) return true;
		if (!LibraryNotificationSource.IsConsistent(row.EventType, row.RelatedEntityType, row.RelatedEntityId)) return false;
		if (!row.RecipientMemberId.HasValue) return false;
		var memberId = row.RecipientMemberId.Value;
		var aliases = (await memberships.GetQueryableAsync()).Where(member => member.TenantId == tenant.Id);
		if (row.RelatedEntityType == nameof(BookIssue))
			return row.RelatedEntityId.HasValue && await queries.AnyAsync((await loanScope.GetReadableLoansAsync(scope)).Where(loan =>
				loan.Id == row.RelatedEntityId.Value && (loan.MemberId == memberId || aliases.Any(member => member.Id == loan.MemberId && member.StudentId == memberId))));
		if (row.RelatedEntityType == nameof(BookReservation))
			return row.RelatedEntityId.HasValue && await queries.AnyAsync((await requestScope.GetReadableRequestsAsync(scope)).Where(request =>
				request.Id == row.RelatedEntityId.Value && (request.CanonicalMemberId.HasValue ? request.CanonicalMemberId.Value == memberId :
				 request.MemberId == memberId || aliases.Any(member => member.Id == request.MemberId && member.StudentId == memberId))));
		return (await access.GetMemberIdsAsync(scope)).Contains(memberId);
	}
}
