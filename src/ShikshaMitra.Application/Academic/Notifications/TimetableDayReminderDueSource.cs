using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Timetable;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), ExposeServices(typeof(INotificationDueSource))]
public sealed class TimetableDayReminderDueSource(ICurrentTenant tenant, ITimetableRepository timetables, TimetableNotificationSource source,
	IRepository<NotificationRule, Guid> rules, NotificationEffectiveRuleResolver resolver, NotificationRuleConfigurationCodec configuration,
	NotificationSchoolTimeZone schoolZone, TimetableDayReminder reminder, IAsyncQueryableExecuter queries)
	: INotificationDueSource, ITransientDependency
{
	public string SourceType => TimetableNotificationSource.Type;
	public string ScanKey => TimetableDayReminder.EventKey;
	public IReadOnlyCollection<string> ScheduledEventKeys { get; } = Array.AsReadOnly(new[] { TimetableDayReminder.EventKey });

	public async Task<NotificationDueSourcePage> ReadPageAsync(NotificationDueScanRequest request, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (request.TenantId == Guid.Empty || request.TenantId != tenant.Id) return new([], null);
		if (request.PageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request));
		var enabled = (await rules.GetQueryableAsync()).Where(x => x.TenantId == request.TenantId && !x.IsDeleted &&
			x.EventKey == TimetableDayReminder.EventKey && x.IsEnabled);
		if (!await queries.AnyAsync(enabled, token)) return new([], null);
		var zone = await schoolZone.GetAsync(token);
		var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(request.Now, zone).DateTime).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
		var published = (await timetables.GetQueryableAsync()).Where(x => x.Status == TimetableStatus.Published);
		if (request.Continuation is { } cursor)
		{
			if (!Guid.TryParseExact(cursor, "N", out var lastId) || lastId == Guid.Empty)
				throw new BusinessException(NotificationErrorCodes.StateConflict);
			published = published.Where(x => x.Id.CompareTo(lastId) > 0);
		}
		var scoped = (await source.ScopedRowsAsync(request.TenantId, published)).Where(row =>
			row.AcademicYearEnd >= date && row.AcademicYearStart < date.AddDays(1) &&
			enabled.Any(rule => (!rule.AcademicYearId.HasValue || rule.AcademicYearId == row.YearId) &&
				(!rule.BoardId.HasValue || rule.BoardId == row.BoardId) &&
				(!rule.MediumId.HasValue || rule.MediumId == row.MediumId) &&
				(!rule.ClassId.HasValue || rule.ClassId == row.StandardId) &&
				(!rule.DivisionId.HasValue || rule.DivisionId == row.DivisionId)));
		var rows = await queries.ToListAsync(scoped.OrderBy(x => x.Id).Take(request.PageSize + 1), token);
		var definition = AcademicNotificationCatalogue.GetDefinition(TimetableDayReminder.EventKey);
		var candidates = new List<NotificationDueSourceCandidate>();
		foreach (var row in rows.Take(request.PageSize))
		{
			var rule = await resolver.ResolveAsync(definition.EventKey, row.RuleScope, token);
			if (rule is not { IsEnabled: true }) continue;
			NotificationRuleTimingConfiguration timing;
			try { timing = configuration.Deserialize(rule.ConfigurationJson, definition).Timing; }
			catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuleConfigurationInvalid) { continue; }
			var timetable = await timetables.FindByStandardAndDivisionAsync(row.StandardId, row.DivisionId);
			if (timetable == null || timetable.Id != row.Id || timetable.TenantId != request.TenantId ||
				timetable.Status != TimetableStatus.Published) continue;
			var candidate = await reminder.FindCandidateAsync(timetable, row, timing, request.Now, zone, token);
			if (candidate != null) candidates.Add(candidate);
		}
		return new(candidates, rows.Count > request.PageSize ? rows[request.PageSize - 1].Id.ToString("N") : null);
	}
}
