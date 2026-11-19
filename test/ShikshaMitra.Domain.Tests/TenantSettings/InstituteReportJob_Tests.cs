using System;
using ShikshaMitra.TenantSettings.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteReportJob_Tests
{
	[Fact]
	public void Export_lifecycle_prevents_duplicate_start_and_retains_requester_and_snapshot()
	{
		var user = Guid.NewGuid(); var tenant = Guid.NewGuid();
		var job = new InstituteReportJob(Guid.NewGuid(), tenant, user, "{\"ReportType\":\"shift-register\"}", false);
		job.TenantId.ShouldBe(tenant); job.RequestedBy.ShouldBe(user); job.Status.ShouldBe("Queued");
		job.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow.AddDays(6));
		Should.Throw<BusinessException>(() => job.Complete("blob"));
		job.Start(); Should.Throw<BusinessException>(job.Start);
		job.Complete("blob"); job.Status.ShouldBe("Completed"); job.BlobName.ShouldBe("blob");
		job.RemoveExpiredFile(); job.BlobName.ShouldBeNull(); job.RequestJson.ShouldContain("shift-register");
	}
}
