using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.FeeCollection;

public class StudentFeeDetailsDto
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public double TotalDue { get; set; }
    public double TotalPaid { get; set; }
    public FeeCollectionStatus Status { get; set; }
    public List<FeeHeadPaymentSummaryDto> FeeHeads { get; set; } = [];
    public List<ChargePaymentSummaryDto> Charges { get; set; } = [];
}

public class FeeHeadPaymentSummaryDto
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double AmountDue { get; set; }
    public double AmountPaid { get; set; }
}

public class ChargePaymentSummaryDto
{
    public Guid ChargeId { get; set; }
    public string ChargeName { get; set; } = string.Empty;
    public double AmountDue { get; set; }
    public double AmountPaid { get; set; }
}
