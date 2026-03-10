using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

public class GetConcessionRequestListRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
}
