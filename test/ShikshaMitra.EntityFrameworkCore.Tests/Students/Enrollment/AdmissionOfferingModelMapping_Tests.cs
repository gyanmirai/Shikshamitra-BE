using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionOfferingModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Offering_model_has_nullable_overrides_defaults_and_soft_delete_unique_indexes()
	{
		var dbContext = GetRequiredService<ShikshaMitraDbContext>();
		var entity = dbContext.Model.FindEntityType(typeof(AdmissionOffering));
		entity.ShouldNotBeNull();
		entity.GetTableName().ShouldBe("AppAdmissionOfferings");

		entity.FindProperty(nameof(AdmissionOffering.Name))!.IsNullable.ShouldBeFalse();
		entity.FindProperty(nameof(AdmissionOffering.Name))!.GetMaxLength()
			.ShouldBe(AdmissionOfferingConsts.MaxNameLength);
		entity.FindProperty(nameof(AdmissionOffering.OpenDate))!.IsNullable.ShouldBeTrue();
		entity.FindProperty(nameof(AdmissionOffering.CloseDate))!.IsNullable.ShouldBeTrue();
		entity.FindProperty(nameof(AdmissionOffering.IsActive))!.GetDefaultValue().ShouldBe(true);
		entity.FindProperty("IsDeleted")!.GetDefaultValue().ShouldBe(false);

		var indexNames = entity.GetIndexes().Select(index => index.GetDatabaseName()).ToList();
		indexNames.ShouldContain("UX_AdmissionOfferings_Identity_NoCampus");
		indexNames.ShouldContain("UX_AdmissionOfferings_Identity_Campus");
		indexNames.ShouldContain("IX_AdmissionOfferings_CycleActiveStandard");
		entity.GetForeignKeys().ShouldAllBe(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
	}
}
