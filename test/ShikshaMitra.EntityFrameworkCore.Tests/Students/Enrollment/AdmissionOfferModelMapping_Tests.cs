using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionOfferModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Offer_history_preserves_nullable_audit_fields_and_scoped_idempotency()
	{
		var entity = GetRequiredService<ShikshaMitraDbContext>().Model
			.FindEntityType(typeof(AdmissionApplicationDecisionHistory)).ShouldNotBeNull();
		entity.FindProperty(nameof(AdmissionApplicationDecisionHistory.Action))!.GetMaxLength().ShouldBe(32);
		entity.FindProperty(nameof(AdmissionApplicationDecisionHistory.IdempotencyKey))!.GetMaxLength().ShouldBe(80);
		entity.FindProperty(nameof(AdmissionApplicationDecisionHistory.StageInstanceId))!.IsNullable.ShouldBeTrue();
		entity.FindProperty(nameof(AdmissionApplicationDecisionHistory.MetadataJson))!.GetColumnType().ShouldBe("text");

		var index = entity.GetIndexes().Single(item => item.Properties.Select(property => property.Name)
			.SequenceEqual(new[] { "TenantId", "ApplicationId", "StageInstanceId", "IdempotencyKey" }));
		index.IsUnique.ShouldBeTrue();
		index.GetDatabaseName().ShouldBe("UX_AdmissionDecisionHistory_Stage_Idempotency");
		index.GetFilter().ShouldBe("\"IsDeleted\" = false");
	}
}
