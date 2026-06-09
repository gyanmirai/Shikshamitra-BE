using System;
using ShikshaMitra.Finance.Common;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.FeeRefund.Dtos;

public class GetFeeRefundsInput : PagedAndSortedResultRequestDto, IFinanceClassDivisionFilter
{
    public Guid? StudentId { get; set; }

    public Guid? AcademicMediumId { get; set; }

    public FeeRefundStatus? Status { get; set; }

    public string? Search { get; set; }

    public Guid? StandardId { get; set; }

    public Guid? DivisionId { get; set; }
}
