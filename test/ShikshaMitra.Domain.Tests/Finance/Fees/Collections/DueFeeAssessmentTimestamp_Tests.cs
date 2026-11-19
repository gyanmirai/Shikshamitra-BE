using System;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public class DueFeeAssessmentTimestamp_Tests
{
	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Reloaded_DateTime_Assessment_Is_Read_As_Utc(DateTimeKind kind)
	{
		var due = Create();
		due.ExtraProperties["LateFeeAssessedAt"] = new DateTime(2026, 11, 4, 6, 30, 0, kind);
		Assert.Equal(new DateTimeOffset(2026, 11, 4, 6, 30, 0, TimeSpan.Zero), due.GetLateFeeAssessedAt());
	}

	[Fact]
	public void Reloaded_Local_Timestamp_Preserves_The_Recorded_Instant()
	{
		var due = Create();
		due.ExtraProperties["LateFeeAssessedAt"] = new DateTime(2026, 11, 4, 6, 30, 0, DateTimeKind.Utc).ToLocalTime();
		Assert.Equal(new DateTimeOffset(2026, 11, 4, 6, 30, 0, TimeSpan.Zero), due.GetLateFeeAssessedAt());
	}

	[Fact]
	public void New_Assessment_Preserves_Its_Offset_And_Missing_Assessment_Is_Null()
	{
		var due = Create();
		Assert.Null(due.GetLateFeeAssessedAt());
		var timestamp = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.FromMinutes(330));
		due.PreserveLateFeeAssessment(200m, timestamp, Guid.NewGuid(), "Assessment");
		Assert.Equal(timestamp, due.GetLateFeeAssessedAt());
		Assert.Equal(timestamp.Offset, due.GetLateFeeAssessedAt()!.Value.Offset);
	}

	private static DueFee Create() => new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
		MonthEnum.November, 2026, false, 5000m, new DateTime(2026, 11, 1));
}
