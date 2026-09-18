using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionCycleModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Cycle_governance_and_history_are_tenant_scoped_and_concurrency_safe()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var cycle = model.FindEntityType(typeof(AdmissionCycle)).ShouldNotBeNull();
		var history = model.FindEntityType(typeof(AdmissionCycleHistory)).ShouldNotBeNull();

		cycle.FindProperty("TenantId").ShouldNotBeNull();
		cycle.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
		cycle.FindProperty(nameof(AdmissionCycle.Description))!.GetMaxLength().ShouldBe(1000);
		cycle.FindProperty(nameof(AdmissionCycle.ScheduledPublishAt)).ShouldNotBeNull();
		cycle.FindProperty(nameof(AdmissionCycle.ScheduledCloseAt)).ShouldNotBeNull();

		history.GetTableName().ShouldBe("AppAdmissionCycleHistories");
		history.FindProperty("TenantId").ShouldNotBeNull();
		history.FindProperty(nameof(AdmissionCycleHistory.Reason))!.GetMaxLength().ShouldBe(500);
		history.GetIndexes().ShouldContain(index => index.Properties.Select(property => property.Name)
			.SequenceEqual(new[] { "TenantId", "AdmissionCycleId", "OccurredAt" }));
	}
}
