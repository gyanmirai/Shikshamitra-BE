using System;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionApplication_Tests
{
	[Fact]
	public void StartsAsDraftAndCanSubmitOnce()
	{
		var application = new AdmissionApplication(Guid.NewGuid(), null, "APP/2026/ABC", Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		application.Status.ShouldBe(AdmissionApplicationStatus.Draft);
		application.Submit();
		application.Status.ShouldBe(AdmissionApplicationStatus.Submitted);
		Should.Throw<Volo.Abp.BusinessException>(() => application.Submit());
	}
}
