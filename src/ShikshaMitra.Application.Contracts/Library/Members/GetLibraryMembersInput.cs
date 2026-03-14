using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.Members;

public class GetLibraryMembersInput : PagedAndSortedResultRequestDto
{
    public string? Search { get; set; }
    public string? Role { get; set; }
}