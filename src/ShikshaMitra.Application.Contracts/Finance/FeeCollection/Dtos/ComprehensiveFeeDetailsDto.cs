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
    public List<ComprehensiveFeeHeadDto> FeeHeads { get; set; } = [];
    public List<ComprehensiveChargeDto> Charges { get; set; } = [];
    public List<CollectFeeOutput> PaymentHistory { get; set; } = [];
}

public class ComprehensiveFeeHeadDto
{
    public Guid FeeHeadId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public double AmountDue { get; set; }
    public double AmountPaid { get; set; }
}

public class ComprehensiveChargeDto
{
    public Guid ChargeId { get; set; }
    public string ChargeName { get; set; } = string.Empty;
    public double AmountDue { get; set; }
    public double AmountPaid { get; set; }
}
