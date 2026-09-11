using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.EntityFrameworkCore;

#nullable disable

namespace ShikshaMitra.Migrations;

[DbContext(typeof(ShikshaMitraDbContext))]
[Migration("20260819120000_ExpandAdmissionFormDefinition")]
public partial class ExpandAdmissionFormDefinition : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("ALTER TABLE \"AppAdmissionFormDefinitions\" ADD COLUMN IF NOT EXISTS \"Description\" character varying(1024) NULL;");
		migrationBuilder.Sql("ALTER TABLE \"AppAdmissionFormFields\" ADD COLUMN IF NOT EXISTS \"OptionsJson\" character varying(8000) NULL;");
		migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_AppAdmissionFormDefinitions_TenantId_Code_Version\";");
		migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_AppAdmissionFormDefinitions_TenantId_Code_Version\" ON \"AppAdmissionFormDefinitions\" (\"TenantId\", \"Code\", \"Version\") WHERE \"IsDeleted\" = false;");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.Sql("ALTER TABLE \"AppAdmissionFormFields\" DROP COLUMN IF EXISTS \"OptionsJson\";");
		migrationBuilder.Sql("ALTER TABLE \"AppAdmissionFormDefinitions\" DROP COLUMN IF EXISTS \"Description\";");
	}
}
