using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionEligibilityModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Eligibility_evaluations_and_append_only_exception_decisions_are_tenant_scoped_and_restricted()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var evaluation = model.FindEntityType(typeof(AdmissionEligibilityEvaluation)).ShouldNotBeNull();
		var exception = model.FindEntityType(typeof(AdmissionEligibilityException)).ShouldNotBeNull();
		foreach (var entity in new[] { evaluation, exception })
		{
			entity.FindProperty("TenantId").ShouldNotBeNull();
			entity.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
			entity.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		}
		evaluation.FindProperty("InputFingerprint")!.GetMaxLength().ShouldBe(64);
		evaluation.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(new[] { "StageInstanceId", "SequenceNumber" }));
		exception.GetIndexes().ShouldContain(index => index.IsUnique && index.Properties.Single().Name == "RequestId");
		model.FindEntityType(typeof(AdmissionInquiry))!.FindProperty("FormResponseDefinitionId").ShouldBeNull();
	}
}
