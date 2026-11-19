using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionJoiningDate_Tests
{
	[Fact]
	public void Joining_date_is_application_local_and_preserves_the_calendar_day()
	{
		var first = CreateApplication();
		var second = CreateApplication();
		first.GetJoiningDate().ShouldBeNull();
		first.ConfirmJoiningDate(new DateTime(2026, 6, 2, 18, 45, 0, DateTimeKind.Local), false);
		first.GetJoiningDate().ShouldBe(new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
		second.GetJoiningDate().ShouldBeNull();
	}

	[Fact]
	public void Prepared_date_cannot_change_but_identical_confirmation_preserves_it()
	{
		var application = CreateApplication();
		var date = new DateTime(2026, 6, 2);
		application.ConfirmJoiningDate(date, false);
		application.ConfirmJoiningDate(date, true);
		Should.Throw<BusinessException>(() => application.ConfirmJoiningDate(date.AddDays(1), true)).Code.ShouldBe(AdmissionFeeSelectionErrorCodes.Locked);
		application.GetJoiningDate()!.Value.Date.ShouldBe(date);
	}

	private static AdmissionApplication CreateApplication() => new(Guid.NewGuid(), Guid.NewGuid(), "APP-TEST", Guid.NewGuid(), null,
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
