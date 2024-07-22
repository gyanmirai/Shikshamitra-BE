using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Authors;

public class GetAuthorListDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}