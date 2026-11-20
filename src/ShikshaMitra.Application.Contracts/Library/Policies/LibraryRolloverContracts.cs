using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Policies;

public interface ILibraryRolloverAppService : IApplicationService
{
	Task<List<LibraryRolloverYearDto>> GetOptionsAsync();
	Task<LibraryRolloverPreviewDto> PreviewAsync(LibraryRolloverInput input);
}

public sealed record LibraryRolloverInput(Guid SourceAcademicYearId, Guid TargetAcademicYearId, DateOnly? SchoolDate);
public sealed record LibraryRolloverYearDto(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn);
public sealed record LibraryRolloverPolicyDto(Guid Id, string Name, int Version, int Role, LibraryDueDateMode DueDateMode,
	DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid OriginAcademicYearId, bool HasSelectors);
public sealed record LibraryRolloverCalendarDto(string? ConfirmedTimeZoneId, int? OpenWeekdays, bool CanPublishAutomatic, string? HoursError);
public sealed record LibraryRolloverLoansDto(string Scope, int? SourceYearOpenCount, int? UnassignedYearOpenCount);
public sealed record LibraryRolloverPreviewDto(LibraryRolloverYearDto SourceYear, LibraryRolloverYearDto TargetYear, DateOnly SchoolDate,
	string PolicyScope, LibraryRolloverCalendarDto Calendar, List<LibraryRolloverPolicyDto> EffectivePolicies,
	List<LibraryRolloverPolicyDto> DraftPolicies, LibraryRolloverLoansDto Loans);
