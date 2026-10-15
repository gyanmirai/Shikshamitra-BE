using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class ShiftTenantBackfill_Tests
{
	[Fact]
	public void Migration_should_preserve_legacy_rows_without_guessing_ownership()
	{
		var operations = new AddShiftTenantIsolation().UpOperations;
		operations.Count.ShouldBe(2);
		var column = operations.OfType<AddColumnOperation>().Single();
		column.Table.ShouldBe("academic_ShiftManagements");
		column.Name.ShouldBe("TenantId");
		column.IsNullable.ShouldBeTrue();
		column.DefaultValue.ShouldBeNull();
		operations.OfType<CreateIndexOperation>().Single().Columns.ShouldBe(["TenantId"]);
		operations.OfType<SqlOperation>().ShouldBeEmpty();
		operations.OfType<DropTableOperation>().ShouldBeEmpty();
	}
}
