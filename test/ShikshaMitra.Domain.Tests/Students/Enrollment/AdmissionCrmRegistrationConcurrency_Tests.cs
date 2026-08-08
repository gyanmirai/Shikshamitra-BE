using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmRegistrationConcurrency_Tests
{
	[Fact]
	public void Matching_response_version_allows_a_save()
	{
		var application = CreateApplication();
		var loadedVersion = application.ConcurrencyStamp;

		application.EnsureResponseVersion(loadedVersion);
		application.Save("{}", 10, DateTime.UtcNow);

		application.CompletionPercentage.ShouldBe(10);
	}

	[Fact]
	public void Opening_a_registration_does_not_replace_the_ef_owned_response_version()
	{
		var application = CreateApplication();
		var issuedVersion = application.ConcurrencyStamp;
		var now = DateTime.UtcNow;

		application.MarkOpened(now);
		application.MarkOpened(now.AddMinutes(1));

		application.ConcurrencyStamp.ShouldBe(issuedVersion);
		application.OpenedAt.ShouldBe(now);
	}

	[Fact]
	public void Mismatched_response_version_is_rejected()
	{
		var application = CreateApplication();

		var exception = Should.Throw<BusinessException>(() =>
			application.EnsureResponseVersion(Guid.NewGuid().ToString("N")));

		exception.Code.ShouldBe("AdmissionCrm:RegistrationConcurrencyConflict");
	}

	[Fact]
	public void Missing_response_version_is_rejected()
	{
		var application = CreateApplication();

		var exception = Should.Throw<BusinessException>(() =>
			application.EnsureResponseVersion(string.Empty));

		exception.Code.ShouldBe("AdmissionCrm:RegistrationConcurrencyStampRequired");
	}

	private static AdmissionCrmRegistrationApplication CreateApplication()
	{
		var now = DateTime.UtcNow;
		return new AdmissionCrmRegistrationApplication(
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			Guid.NewGuid(),
			1,
			null,
			"REG-0001",
			"{}",
			"{}",
			now,
			now.AddDays(14),
			0);
	}
}
