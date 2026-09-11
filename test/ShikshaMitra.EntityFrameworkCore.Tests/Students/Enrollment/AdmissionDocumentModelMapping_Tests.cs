using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionDocumentModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Document_evidence_and_reviews_preserve_immutable_versions_and_restrict_deletion()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var evidence = model.FindEntityType(typeof(AdmissionDocumentEvidence)).ShouldNotBeNull();
		var review = model.FindEntityType(typeof(AdmissionDocumentReview)).ShouldNotBeNull();
		evidence.FindProperty("TenantId").ShouldNotBeNull();
		review.FindProperty("TenantId").ShouldNotBeNull();
		evidence.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
		evidence.FindProperty("PolicySnapshotJson")!.IsNullable.ShouldBeFalse();
		evidence.FindProperty("StudentId")!.IsNullable.ShouldBeTrue();
		evidence.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "StageInstanceId", "RequirementCode", "VersionNumber" }));
		review.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Single().Name == "EvidenceId");
		evidence.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		review.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
	}
}
