using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Migrations;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class FinanceFeeAccountModel_Tests
{
	private static ShikshaMitraDbContext CreateContext()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		return new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Database=collectfees_model_only").Options);
	}

	[Fact]
	public void Applicant_Payer_Is_Explicit_And_Enrolled_Student_Assignment_Remains_Required()
	{
		using var db = CreateContext();
		var model = db.GetService<IDesignTimeModel>().Model;
		foreach (var type in new[] { typeof(DueFee), typeof(FeeRecordMetadata), typeof(FeeCollectionRecord),
			typeof(PaymentMethodRecord), typeof(FeePaymentReceipt), typeof(FeeRefundRequest) })
		{
			var entity = model.FindEntityType(type)!;
			Assert.True(entity.FindProperty("StudentId")!.IsNullable);
			Assert.True(entity.FindProperty("FinanceFeeAccountId")!.IsNullable);
			Assert.Contains(entity.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(FinanceFeeAccount)
				&& key.DeleteBehavior == DeleteBehavior.Restrict);
			Assert.Contains(entity.GetCheckConstraints(), check => check.Sql.Contains("StudentId") && check.Sql.Contains("FinanceFeeAccountId"));
		}
		Assert.False(model.FindEntityType(typeof(StudentFeeStructure))!.FindProperty("StudentId")!.IsNullable);
	}

	[Fact]
	public void Migration_Is_Additive_And_Rollback_Protects_Existing_Financial_Evidence()
	{
		var migration = new AddAdmissionFeeAccounts();
		Assert.DoesNotContain(migration.UpOperations, operation => operation is DropTableOperation or DropColumnOperation);
		Assert.All(migration.UpOperations.OfType<CreateTableOperation>(), operation => Assert.Equal("Finance_FeeAccounts", operation.Name));
		Assert.All(migration.UpOperations.OfType<AlterColumnOperation>(), operation =>
		{
			Assert.StartsWith("Finance_", operation.Table);
			Assert.Equal("StudentId", operation.Name);
			Assert.True(operation.IsNullable);
		});
		var guard = Assert.IsType<SqlOperation>(migration.DownOperations.First());
		Assert.Contains("Finance_FeeAccounts", guard.Sql);
		Assert.Contains("RAISE EXCEPTION", guard.Sql);
		Assert.All(migration.DownOperations.OfType<DropTableOperation>(), operation => Assert.Equal("Finance_FeeAccounts", operation.Name));
		Assert.All(migration.DownOperations.OfType<AlterColumnOperation>(), operation => Assert.Null(operation.DefaultValue));
	}

	[Fact]
	public void Finance_Snapshot_Matches_Current_Model_Without_Deleting_Unmapped_Legacy_Student_History()
	{
		using var db = CreateContext();
		var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
		var previous = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
		var current = db.GetService<IDesignTimeModel>().Model;
		var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(previous.GetRelationalModel(), current.GetRelationalModel());
		var sql = db.GetService<IMigrationsSqlGenerator>().Generate(differences, current);
		Assert.DoesNotContain(sql, command => command.CommandText.Contains("Finance_", StringComparison.Ordinal));
	}

	[Fact]
	public void Database_Uniqueness_Protects_Account_Schedule_And_Actual_Handoff_Assignment()
	{
		using var db = CreateContext();
		var model = db.GetService<IDesignTimeModel>().Model;
		Assert.Contains(model.FindEntityType(typeof(FinanceFeeAccount))!.GetIndexes(), index => index.IsUnique &&
			index.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "ApplicationId", "AcademicYearId" }));
		Assert.Contains(model.FindEntityType(typeof(DueFee))!.GetIndexes(), index => index.IsUnique &&
			index.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "FinanceFeeAccountId", "ScheduleInstanceKey" }));
		Assert.Contains(model.FindEntityType(typeof(StudentFeeStructure))!.GetIndexes(), index => index.IsUnique &&
			index.Properties.Select(p => p.Name).SequenceEqual(new[] { "FinanceFeeAccountId" }));
	}
}
