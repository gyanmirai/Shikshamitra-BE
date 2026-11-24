using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20261124073000_ReleaseDeletedFinanceSettingKeys")]
public class ReleaseDeletedFinanceSettingKeys : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder) =>
		RebuildIndexes(migrationBuilder, "\"IsDeleted\" = false");

	protected override void Down(MigrationBuilder migrationBuilder) =>
		RebuildIndexes(migrationBuilder, filter: null);

	private static void RebuildIndexes(MigrationBuilder migrationBuilder, string? filter)
	{
		const string categories = "Finance_TransactionCategories";
		const string categoryIndex = "IX_Finance_TransactionCategories_TenantId_AcademicYearId_Cate~1";
		const string paymentTerms = "Finance_PaymentTerms";
		const string paymentTermIndex = "IX_Finance_PaymentTerms_TenantId_AcademicYearId_Code";

		migrationBuilder.DropIndex(categoryIndex, categories, ShikshaMitraConsts.DbSchema);
		migrationBuilder.CreateIndex(
			name: categoryIndex,
			table: categories,
			columns: ["TenantId", "AcademicYearId", "CategoryType", "NormalizedName"],
			schema: ShikshaMitraConsts.DbSchema,
			unique: true,
			filter: filter);

		migrationBuilder.DropIndex(paymentTermIndex, paymentTerms, ShikshaMitraConsts.DbSchema);
		migrationBuilder.CreateIndex(
			name: paymentTermIndex,
			table: paymentTerms,
			columns: ["TenantId", "AcademicYearId", "Code"],
			schema: ShikshaMitraConsts.DbSchema,
			unique: true,
			filter: filter);
	}
}
