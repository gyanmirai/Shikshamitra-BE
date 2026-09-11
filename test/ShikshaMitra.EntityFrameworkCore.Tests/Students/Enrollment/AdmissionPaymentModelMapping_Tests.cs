using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Finance.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionPaymentModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Payment_transactions_use_decimal_concurrency_and_restrictive_idempotent_links()
	{
		var entity = GetRequiredService<ShikshaMitraDbContext>().Model
			.FindEntityType(typeof(AdmissionPaymentTransaction)).ShouldNotBeNull();
		entity.FindProperty("TenantId").ShouldNotBeNull();
		entity.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
		entity.FindProperty("Amount")!.GetPrecision().ShouldBe(18);
		entity.FindProperty("Amount")!.GetScale().ShouldBe(2);
		entity.FindProperty("PolicySnapshotJson")!.GetMaxLength().ShouldBe(8000);
		entity.GetForeignKeys().ShouldAllBe(key => key.DeleteBehavior == DeleteBehavior.Restrict);
		entity.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "StageInstanceId", "AttemptNumber" }));
		entity.GetIndexes().ShouldContain(index => index.IsUnique &&
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "StageInstanceId", "IdempotencyKey" }));
	}
}
