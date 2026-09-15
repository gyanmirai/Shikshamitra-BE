using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class StudentFeeAssignmentMigration_Tests
{
	[Fact]
	public void Migration_Preserves_Amounts_And_Snapshot_Matches_New_Columns()
	{
		var migration = new AddCanonicalStudentFeeAssignments { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
		migration.UpOperations.OfType<AlterColumnOperation>().Select(x => x.Name).Order().ShouldBe(new[] { "ConcessionPolicyId", "ConcessionPolicyVersionId" });
		migration.UpOperations.OfType<DropColumnOperation>().ShouldBeEmpty();
		migration.UpOperations.OfType<SqlOperation>().ShouldBeEmpty();
		var fk = migration.UpOperations.OfType<AddForeignKeyOperation>().Single();
		fk.Name.ShouldBe("FK_DueFees_StudentFeeAssignment");
		fk.PrincipalTable.ShouldBe("Finance_StudentFeeStructures");
		var snapshotType = typeof(AddCanonicalStudentFeeAssignments).Assembly.GetTypes().Single(t => t.Name == "ShikshaMitraDbContextModelSnapshot");
		var model = ((Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!).Model;
		var assignment = model.FindEntityType(typeof(StudentFeeStructure).FullName!)!;
		assignment.FindProperty(nameof(StudentFeeStructure.ConcessionPolicyId))!.IsNullable.ShouldBeTrue();
		assignment.FindProperty(nameof(StudentFeeStructure.AssignmentSnapshotJson))!.GetColumnType().ShouldBe("text");
		var due = model.FindEntityType(typeof(DueFee).FullName!)!;
		due.FindProperty(nameof(DueFee.StudentFeeStructureId))!.IsNullable.ShouldBeTrue();
		due.FindProperty(nameof(DueFee.AllocationPercentage))!.GetPrecision().ShouldBe(18);
		due.GetForeignKeys().Single(x => x.Properties.Any(p => p.Name == nameof(DueFee.StudentFeeStructureId))).GetConstraintName().ShouldBe(fk.Name);
	}
}
