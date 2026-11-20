using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Shared;

[RemoteService(false)]
public class LibraryMemberStatisticsReader(IRepository<LibraryMember, Guid> memberships,
	LibraryStoredLoanScope loans, LibraryStoredReservationScope requests, IUserScopeAccessService scopeAccess,
	LibraryCalendarService calendar, IClock clock) : ApplicationService, ITransientDependency
{
	public async Task<Dictionary<Guid, LibraryMemberStatisticsDto>> ReadAsync(IEnumerable<Guid> personIds)
	{
		var people = personIds.Distinct().ToList();
		var result = people.ToDictionary(id => id, _ => new LibraryMemberStatisticsDto());
		if (people.Count == 0) return result;
		var scope = await scopeAccess.GetCurrentUserScopeAsync();
		var aliases = await AsyncExecuter.ToListAsync((await memberships.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && people.Contains(x.StudentId)).Select(x => new { x.Id, x.StudentId }));
		var canonicalIds = aliases.ToDictionary(x => x.Id, x => x.StudentId);
		var memberIds = people.Concat(aliases.Select(x => x.Id)).Distinct().ToList();
		var today = await ConfirmedSchoolDayAsync();
		var loanCounts = await AsyncExecuter.ToListAsync((await loans.GetReadableLoansAsync(scope))
			.Where(x => memberIds.Contains(x.MemberId) && x.Status == BookIssueStatus.Issued)
			.GroupBy(x => x.MemberId).Select(group => new
			{
				MemberId = group.Key, Count = group.Count(), Overdue = group.Count(x => today.HasValue &&
					(x.DueDateProvenance == LibraryCivilDateProvenance.ExplicitSchoolDate || x.DueDateProvenance == LibraryCivilDateProvenance.ReviewedLegacy) &&
					x.SchoolDueDate < today),
				UnknownDue = group.Count(x => !x.SchoolDueDate.HasValue ||
					(x.DueDateProvenance != LibraryCivilDateProvenance.ExplicitSchoolDate && x.DueDateProvenance != LibraryCivilDateProvenance.ReviewedLegacy))
			}));
		foreach (var group in loanCounts)
		{
			var stats = result[canonicalIds.GetValueOrDefault(group.MemberId, group.MemberId)];
			stats.CurrentIssue += group.Count;
			stats.OverdueReturnBooks += group.Overdue;
			stats.UnknownDueDateCount += group.UnknownDue;
		}
		var holds = await AsyncExecuter.ToListAsync((await requests.GetReadableRequestsAsync(scope))
			.Where(x => x.State == LibraryReservationState.Waiting || x.State == LibraryReservationState.Ready)
			.Select(x => new { MemberId = x.CanonicalMemberId ?? x.MemberId })
			.Where(x => memberIds.Contains(x.MemberId)).GroupBy(x => x.MemberId)
			.Select(group => new { MemberId = group.Key, Count = group.Count() }));
		foreach (var group in holds)
			result[canonicalIds.GetValueOrDefault(group.MemberId, group.MemberId)].ReservedBooks += group.Count;
		foreach (var stats in result.Values)
		{
			stats.OverdueAsOfDate = today;
			stats.OverdueDateSource = today.HasValue ? "ConfirmedLibraryClock" : "Unavailable";
			stats.OverdueClassificationAvailable = today.HasValue && stats.UnknownDueDateCount == 0;
			var reasons = new List<string>();
			if (!today.HasValue) reasons.Add("Confirm the Library timezone before classifying overdue loans.");
			if (stats.UnknownDueDateCount > 0) reasons.Add("Some open loans have unconfirmed due dates. Review them before treating this total as complete.");
			stats.OverdueUnavailableReason = reasons.Count > 0 ? string.Join(" ", reasons) : null;
		}
		return result;
	}

	private async Task<DateOnly?> ConfirmedSchoolDayAsync()
	{
		var settings = await calendar.GetSettingsAsync();
		return settings.Source == "Library" && !settings.NeedsConfiguration
			? LibraryCalendarCalculator.SchoolDate(new DateTimeOffset(clock.Now), LibraryCalendarService.ResolveZone(settings.TimeZoneId))
			: null;
	}
}
