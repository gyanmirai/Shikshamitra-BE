using ShikshaMitra.Finance.ConcessionRequests;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions.Dtos;

public class ConcessionFeeHeadMappingDto : FullAuditedEntityDto<Guid>
{
    public Guid ConcessionRequestId { get; set; }
    public Guid FeeHeadId { get; set; }
    public decimal FeeHeadConcessionAmount { get; set; }
    public decimal PreConcessionFeeHeadAmount { get; set; }
    public decimal PostConcessionFeeHeadAmount { get; set; }
    public decimal UserSpecifiedConcessionAmountValue { get; set; }
    public AmountType UserSpecifiedConcessionAmountType { get; set; }
}
