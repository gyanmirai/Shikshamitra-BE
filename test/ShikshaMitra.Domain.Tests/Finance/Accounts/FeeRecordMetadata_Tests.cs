using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// Tests for FeeRecordMetadata domain state machine.
/// Verifies that MarkPaid / MarkPartial / MarkReversed enforce valid transitions.
/// </summary>
public class FeeRecordMetadata_Tests
{
    private static FeeRecordMetadata Create(FeeCollectionStatus status = FeeCollectionStatus.Pending)
    {
        var m = new FeeRecordMetadata(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            1000d, "REC-2026-000001", status);
        return m;
    }

    [Fact]
    public void MarkPaid_From_Pending_Sets_Status_Paid()
    {
        var m = Create(FeeCollectionStatus.Pending);
        m.MarkPaid();
        Assert.Equal(FeeCollectionStatus.Paid, m.Status);
    }

    [Fact]
    public void MarkPartial_From_Pending_Sets_Status_Partial()
    {
        var m = Create(FeeCollectionStatus.Pending);
        m.MarkPartial();
        Assert.Equal(FeeCollectionStatus.Partial, m.Status);
    }

    [Fact]
    public void MarkReversed_From_Pending_Sets_Status_Reversed()
    {
        var m = Create(FeeCollectionStatus.InReconciliation);
        m.MarkReversed("test");
        Assert.Equal(FeeCollectionStatus.Reversed, m.Status);
    }

    [Fact]
    public void MarkReversed_Already_Reversed_Is_Idempotent()
    {
        var m = Create(FeeCollectionStatus.InReconciliation);
        m.MarkReversed("first");
        m.MarkReversed("second");
        Assert.Equal(FeeCollectionStatus.Reversed, m.Status);
    }

    [Fact]
    public void Constructor_Sets_All_Fields()
    {
        var id = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var m = new FeeRecordMetadata(id, null, studentId, yearId, 500d, "REC-2026-000002", FeeCollectionStatus.Paid);

        Assert.Equal(id, m.Id);
        Assert.Equal(studentId, m.StudentId);
        Assert.Equal(yearId, m.AcademicYearId);
        Assert.Equal(500d, m.TotalPaidFee);
        Assert.Equal("REC-2026-000002", m.ReceiptNumber);
        Assert.Equal(FeeCollectionStatus.Paid, m.Status);
    }
}
