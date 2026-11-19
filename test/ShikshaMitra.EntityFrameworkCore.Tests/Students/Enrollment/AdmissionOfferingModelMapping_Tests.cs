using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Migrations;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionOfferingModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Application_counsellor_is_nullable_and_indexed_for_tracking()
	{
		var context = GetRequiredService<ShikshaMitraDbContext>();
		var entity = context.Model.FindEntityType(typeof(AdmissionApplication));
		entity.ShouldNotBeNull();
		entity.FindProperty(nameof(AdmissionApplication.AssignedCounsellorId))!.IsNullable.ShouldBeTrue();
		entity.GetIndexes().ShouldContain(index =>
			index.GetDatabaseName() == "IX_AdmissionApplications_AssignedCounsellor");
	}

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
		entity.FindProperty(nameof(AdmissionOffering.Track))!.GetDefaultValue().ShouldBe(AdmissionTrack.General);
		entity.FindProperty(nameof(AdmissionOffering.EntryMode))!.GetDefaultValue().ShouldBe(AdmissionEntryMode.Inquiry);
		entity.FindProperty("IsDeleted")!.GetDefaultValue().ShouldBe(false);

		var indexNames = entity.GetIndexes().Select(index => index.GetDatabaseName()).ToList();
		indexNames.ShouldContain("UX_AdmissionOfferings_Identity_NoCampus");
		indexNames.ShouldContain("UX_AdmissionOfferings_Identity_Campus");
		indexNames.ShouldContain("IX_AdmissionOfferings_CycleActiveStandard");
		entity.GetIndexes().Where(index => index.GetDatabaseName()!.StartsWith("UX_AdmissionOfferings_Identity"))
			.ShouldAllBe(index => index.Properties.Any(property => property.Name == nameof(AdmissionOffering.Track)));
		entity.GetForeignKeys().ShouldAllBe(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
	}

	[Fact]
	public void Class_room_allocations_are_shared_by_track_and_cannot_reuse_a_room_in_one_cycle()
	{
		var dbContext = GetRequiredService<ShikshaMitraDbContext>();
		var entity = dbContext.Model.FindEntityType(typeof(AdmissionClassRoomAllocation));
		entity.ShouldNotBeNull();
		entity.GetTableName().ShouldBe("AppAdmissionClassRoomAllocations");
		entity.FindProperty(nameof(AdmissionClassRoomAllocation.RoomId))!.IsNullable.ShouldBeFalse();
		entity.FindProperty(nameof(AdmissionOffering.Track)).ShouldBeNull();
		entity.GetIndexes().ShouldContain(index => index.GetDatabaseName() == "UX_AdmissionClassRooms_ClassRoom" &&
			index.IsUnique && index.Properties.Any(property => property.Name == nameof(AdmissionClassRoomAllocation.StandardId)));
		entity.GetIndexes().ShouldContain(index => index.GetDatabaseName() == "UX_AdmissionClassRooms_CycleRoom" &&
			index.IsUnique && index.Properties.Any(property => property.Name == nameof(AdmissionClassRoomAllocation.RoomId)));
		entity.GetForeignKeys().ShouldAllBe(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
	}

	[Fact]
	public void Class_room_allocation_migration_targets_current_academic_table_names()
	{
		var migration = new AddAdmissionClassRoomAllocations();
		var table = migration.UpOperations.OfType<CreateTableOperation>().Single();
		table.ForeignKeys.Select(key => key.PrincipalTable).ToHashSet()
			.SetEquals(new[] { "academic_Years", "academic_Standards", "academic_Rooms" })
			.ShouldBeTrue();
	}

	[Fact]
	public void Form_purpose_migration_uses_active_offering_references_and_rejects_ambiguous_shared_forms()
	{
		var migration = new AddAdmissionFormPurpose { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		var sql = migration.UpOperations.OfType<SqlOperation>().Select(operation => operation.Sql).ToList();
		sql.Count.ShouldBe(2);
		sql[1].ShouldContain("RAISE EXCEPTION 'An existing Admission Form is used as both Inquiry and Registration");
		sql[1].ShouldContain("registration_offering.\"ApplicationFormDefinitionId\" = inquiry_offering.\"InquiryFormDefinitionId\"");
		sql[1].ShouldContain("offering.\"InquiryFormDefinitionId\" = form_definition.\"Id\"");
		sql[1].ShouldContain("offering.\"ApplicationFormDefinitionId\" = form_definition.\"Id\"");
		sql[1].ShouldContain("offering.\"IsDeleted\" = false");
		sql[1].IndexOf("RAISE EXCEPTION", System.StringComparison.Ordinal)
			.ShouldBeLessThan(sql[1].IndexOf("UPDATE \"AppAdmissionFormDefinitions\" form_definition", System.StringComparison.Ordinal));
	}

	[Fact]
	public void New_admission_migrations_generate_postgresql_up_and_down_sql_without_a_database()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only").Options);
		var generator = context.GetService<IMigrationsSqlGenerator>();
		Migration[] migrations =
		[
			new AddAdmissionFormPurpose(),
			new AddAdmissionOfferingTracks(),
			new AddAdmissionClassRoomAllocations(),
			new AddAdmissionApplicationCounsellor()
		];
		foreach (var migration in migrations)
		{
			migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
			generator.Generate(migration.UpOperations, model: null).ShouldNotBeEmpty();
			generator.Generate(migration.DownOperations, model: null).ShouldNotBeEmpty();
		}
	}
}
