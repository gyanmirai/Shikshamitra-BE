using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Shared;

public class LibrarySchoolContextReader(IRepository<AcademicYear, Guid> academicYears, LibraryCalendarService calendar,
	IClock clock, ICurrentTenant tenant, IAsyncQueryableExecuter queries) : ITransientDependency
{
	public async Task<LibraryCirculationSchoolContextDto> ReadAsync(LibraryAcademicContext context)
	{
		if (!tenant.Id.HasValue || context.UserScope.TenantId != tenant.Id || !context.UserScope.HasAnyAccess)
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var allowed = context.AllowedAcademicYearIds.ToArray();
		var years = await queries.ToListAsync((await academicYears.GetQueryableAsync())
			.Where(row => row.TenantId == tenant.Id && Enumerable.Contains(allowed, row.Id))
			.OrderByDescending(row => row.StartDate).ThenBy(row => row.Id)
			.Select(row => new NamedIdDto { Id = row.Id, Name = row.Name }));
		var settings = await calendar.GetSettingsAsync();
		if (settings.Source != "Library" || settings.NeedsConfiguration)
			return new()
			{
				AcademicYears = years,
				UnavailableReason = settings.Source == "Library"
					? "The Library timezone is invalid. Enter the school date explicitly."
					: "The school has not confirmed a Library timezone. Enter the school date explicitly."
			};

		return new()
		{
			SchoolToday = LibraryCalendarCalculator.SchoolDate(new DateTimeOffset(clock.Now), LibraryCalendarService.ResolveZone(settings.TimeZoneId)),
			ConfirmedTimeZoneId = settings.TimeZoneId,
			IsConfirmed = true,
			RequiresExplicitDate = false,
			AcademicYears = years
		};
	}
}
