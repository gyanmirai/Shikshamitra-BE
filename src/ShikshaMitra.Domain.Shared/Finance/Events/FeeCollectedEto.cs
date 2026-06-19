using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Events;

public class FeeCollectedEto
{
    public Guid FeeCollectionTransactionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public double TotalPaidFee { get; set; }
    public FeeCollectionStatus Status { get; set; }
    public List<PaymentMethodEnum> PaymentMethods { get; set; } = [];
    public Guid? TenantId { get; set; }
    public DateTime CollectedAt { get; set; }
}
