using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests.Dtos;

public class ConcessionRequestDto : FullAuditedEntityDto<Guid>
{
    public Guid StudentId { get; set; }
    public Guid ConcessionTypeId { get; set; }
    public ConcessionRequestStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public string? Remarks { get; set; }
    public List<ConcessionFeeHeadMappingDto> ConcessionFeeHeadMappings { get; set; } = new();
}
