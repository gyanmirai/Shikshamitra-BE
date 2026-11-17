using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.FeeRefund.Dtos;

public sealed class FeeRefundListResultDto : PagedResultDto<FeeRefundRequestDto>
{
    public Dictionary<int, int> StatusCounts { get; set; } = [];
}
