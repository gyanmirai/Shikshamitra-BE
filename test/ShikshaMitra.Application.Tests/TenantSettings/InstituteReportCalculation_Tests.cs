using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteReportCalculation_Tests
{
	[Theory]
	[InlineData(7, 0, 12, 0, "5")]
	[InlineData(7, 15, 12, 0, "4.75")]
	[InlineData(7, 0, 7, 30, "0.5")]
	[InlineData(22, 0, 6, 0, "8")]
	[InlineData(23, 30, 6, 0, "6.5")]
	[InlineData(7, 0, 7, 0, "0")]
	[InlineData(0, 0, 0, 1, "0.02")]
	[InlineData(0, 0, 23, 59, "23.98")]
	public void Shift_hours_use_decimal_elapsed_time_and_next_day_for_an_earlier_end(int startHour, int startMinute, int endHour, int endMinute, string expected) =>
		InstituteReportCalculations.ShiftTotalHours(new(startHour, startMinute), new(endHour, endMinute)).ShouldBe(expected);
	[Fact]
	public void Calculated_hours_can_be_selected_filtered_and_sorted_as_numbers()
	{
		var request = new InstituteReportRequest { ReportType = "shift-register", Columns = ["shift", "start", "end", "totalHours"], SortField = "totalHours",
			Conditions = [new() { Field = "totalHours", Operator = "gt", Value = "1" }] };
		InstituteReportCatalog.Validate(request);
		var rows = new[] { Row("12"), Row("2"), Row("0.5"), Row("5") };
		InstituteReportProjection.Apply(rows, request).Select(x => x["totalHours"]).ShouldBe(["2", "5", "12"]);
		request.Conditions = [new() { Field = "totalHours", Operator = "eq", Value = "5.00" }];
		InstituteReportProjection.Apply(rows, request).Single()["totalHours"].ShouldBe("5");
		request.ReportType = "rooms-inventory";
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(request));
		request.ReportType = "shift-register"; request.Columns = ["end - start"];
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(request));
	}
	private static Dictionary<string, string?> Row(string hours) => new() { ["totalHours"] = hours };
}
