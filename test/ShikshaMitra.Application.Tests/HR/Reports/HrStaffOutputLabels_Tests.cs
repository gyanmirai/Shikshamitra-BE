using System.Linq;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class HrStaffOutputLabels_Tests
{
    [Fact]
    public void Catalog_Should_Display_Staff_While_Preserving_Field_Keys_And_Default_Columns()
    {
        var source = HrReportSourceDefinitions.Build().Single(x => x.Id == HrReportDataSources.Employee);
        source.Category.ShouldBe("Staff");
        source.Label.ShouldBe("Staff Reports");
        source.Fields.Single(x => x.Id == "employeeCode").Label.ShouldBe("Staff Code");
        source.Fields.Single(x => x.Id == "employeeName").Label.ShouldBe("Staff Name");
        source.DefaultColumns.ShouldContain("employeeCode");
        source.DefaultColumns.ShouldContain("employeeName");
        source.DefaultSorts.ShouldBe(new[] { "employeeCode" });
    }
}
