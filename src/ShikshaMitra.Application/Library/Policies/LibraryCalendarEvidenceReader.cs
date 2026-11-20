using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Policies;

public sealed record LibraryCalendarEvidence(LibraryCalendarSettingsDto Settings,
	IReadOnlyCollection<DayOfWeek> OpenWeekdays, IReadOnlyCollection<LibraryClosedDateRange> ClosedDates, string Revision)
{
	public DateOnly? ConfirmedSchoolDateAt(DateTimeOffset instant) => Settings.Source == "Library" && !Settings.NeedsConfiguration
		? LibraryCalendarCalculator.SchoolDate(instant, LibraryCalendarService.ResolveZone(Settings.TimeZoneId)) : null;
}

public class LibraryCalendarEvidenceReader(LibraryCalendarService calendar, ICurrentTenant tenant,
	IRepository<LibraryTiming, Guid> timings, IRepository<LibraryCalendarClosure, Guid> closures) : ITransientDependency
{
	public async Task<LibraryCalendarEvidence> ReadAsync(Guid academicYearId, Guid? sectionId)
	{
		var settings = await calendar.GetSettingsAsync();
		var hours = (await timings.GetListAsync(x => x.TenantId == tenant.Id && x.AcademicYearId == academicYearId))
			.OrderBy(x => x.Id).Select(x => new { x.Id, x.Day, x.StartTime, x.EndTime, x.Status, x.ConcurrencyStamp }).ToList();
		var closed = (await closures.GetListAsync(x => x.TenantId == tenant.Id && (x.SectionId == null || x.SectionId == sectionId)))
			.OrderBy(x => x.Id).ToList();
		var openDays = await calendar.GetOpenWeekdaysAsync(academicYearId);
		var data = JsonSerializer.Serialize(new
		{
			AcademicYearId = academicYearId, SectionId = sectionId, Settings = settings, Hours = hours,
			Closures = closed.Select(x => new { x.Id, x.FromDate, x.ToDate, x.SectionId, x.Reason, x.ConcurrencyStamp })
		});
		return new(settings, openDays, closed.Select(x => new LibraryClosedDateRange(x.FromDate, x.ToDate)).ToList(),
			Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))));
	}
}
