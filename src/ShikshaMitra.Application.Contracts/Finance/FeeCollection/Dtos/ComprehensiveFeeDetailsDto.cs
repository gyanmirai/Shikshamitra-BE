using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.FeeCollection;

public class ComprehensiveFeeDetailsDto
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public double TotalDue { get; set; }
    public double TotalPaid { get; set; }
    public double TotalConcession { get; set; }
    public FeeCollectionStatus Status { get; set; }
    public List<FeeHeadPaymentSummaryDto> FeeHeads { get; set; } = [];
    public List<ChargePaymentSummaryDto> Charges { get; set; } = [];
    public List<CollectFeeOutput> PaymentHistory { get; set; } = [];
}
