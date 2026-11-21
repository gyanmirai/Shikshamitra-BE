using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface ILibraryCirculationMemberAppService : IApplicationService
{
	Task<LibraryCirculationMemberDto> ResolveAsync(string identifier);
	Task<PaginatedResponse<LibraryCirculationMemberDto>> SearchAsync(string? search = null, int page = 1, int pageSize = 20);
}

public class LibraryCirculationMemberDto
{
	public Guid Id { get; set; }
	public string? MemberIdentifier { get; set; }
	public string MemberName { get; set; } = string.Empty;
	public int Role { get; set; }
	public Guid? AcademicYearId { get; set; }
	public string? ClassDivision { get; set; }
	public bool IsEligibleForIssue { get; set; }
	public string? IneligibilityReason { get; set; }
	public LibraryMemberStatisticsDto Statistics { get; set; } = new();
}
