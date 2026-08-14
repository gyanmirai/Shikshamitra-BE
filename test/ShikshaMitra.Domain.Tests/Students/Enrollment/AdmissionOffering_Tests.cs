using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionOffering_Tests
{
	[Fact]
	public void Offering_normalizes_code_and_defaults_active()
	{
		var offering = new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " grade-5 ",
			new DateTime(2026, 10, 1), new DateTime(2027, 3, 31));

		offering.Code.ShouldBe("GRADE-5");
		offering.IsActive.ShouldBeTrue();
	}

	[Fact]
	public void Offering_rejects_reverse_date_range()
	{
		Should.Throw<BusinessException>(() => new AdmissionOffering(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "GRADE-5",
			new DateTime(2027, 3, 31), new DateTime(2026, 10, 1)))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingDateRangeInvalid);
	}
}
