using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Groups.Dtos;

public class GetGroupsInputDto : PagedAndSortedResultRequestDto
{
    public string? Search { get; set; }
}
