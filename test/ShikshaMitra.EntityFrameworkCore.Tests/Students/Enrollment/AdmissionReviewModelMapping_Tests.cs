using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionReviewModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Review_work_and_immutable_decisions_have_tenant_concurrency_and_restrictive_unique_links()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var work = model.FindEntityType(typeof(AdmissionReviewWorkItem)).ShouldNotBeNull();
		var record = model.FindEntityType(typeof(AdmissionReviewRecord)).ShouldNotBeNull();
		var decision = model.FindEntityType(typeof(AdmissionReviewDecision)).ShouldNotBeNull();
		foreach (var entity in new[] { work, record, decision })
		{
			entity.FindProperty("TenantId").ShouldNotBeNull(); entity.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
			entity.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		}
		work.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Single().Name == "StageInstanceId");
		record.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "StageInstanceId", "SequenceNumber" }));
		decision.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Single().Name == "RecordId");
		record.FindProperty("InputFingerprint")!.GetMaxLength().ShouldBe(64);
		record.FindProperty("EvidenceSnapshotJson")!.GetColumnType().ShouldBe("text");
	}
}
