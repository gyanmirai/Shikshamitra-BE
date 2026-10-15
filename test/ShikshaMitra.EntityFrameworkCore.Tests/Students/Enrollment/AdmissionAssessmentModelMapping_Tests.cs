using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionAssessmentModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Assessment_records_preserve_tenant_concurrency_and_unique_attempt_evidence()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var session = model.FindEntityType(typeof(AdmissionAssessmentSession)).ShouldNotBeNull();
		var attempt = model.FindEntityType(typeof(AdmissionAssessmentAttempt)).ShouldNotBeNull();
		session.FindProperty("TenantId").ShouldNotBeNull();
		attempt.FindProperty("TenantId").ShouldNotBeNull();
		session.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
		attempt.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
		attempt.FindProperty("PolicySnapshotJson")!.IsNullable.ShouldBeFalse();
		attempt.FindProperty("Marks")!.GetColumnType().ShouldBe("numeric");
		attempt.FindProperty("Marks")!.GetPrecision().ShouldBeNull();
		attempt.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name).SequenceEqual(new[] { "StageInstanceId", "AttemptNumber" }));
		attempt.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		session.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		var scope = model.FindEntityType(typeof(AdmissionAssessmentSessionScope)).ShouldNotBeNull();
		scope.FindProperty("TenantId").ShouldNotBeNull();
		scope.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SessionId", "AdmissionOfferingId", "WorkflowStageDefinitionId" }));
		attempt.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SessionId", "SlotStartsAt" }));
		attempt.GetIndexes().ShouldContain(index => !index.IsUnique && index.GetFilter() == null &&
			index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SessionId" }));
		model.FindEntityType(typeof(AdmissionAssessmentBookingBatch)).ShouldNotBeNull().FindProperty("Fingerprint").ShouldNotBeNull();
		session.FindProperty("RoomReservationStatus").ShouldBeNull();
	}
}
