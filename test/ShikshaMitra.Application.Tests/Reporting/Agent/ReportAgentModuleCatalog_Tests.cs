using System.Linq;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Reporting.Agent;

public class ReportAgentModuleCatalog_Tests
{
	[Fact]
	public void Catalog_Should_Map_Every_Report_Module_To_Explicit_Permissions()
	{
		ReportAgentModuleCatalog.All.Count.ShouldBeGreaterThanOrEqualTo(11);
		ReportAgentModuleCatalog.All.Select(module => module.Id).Distinct().Count()
			.ShouldBe(ReportAgentModuleCatalog.All.Count);
		ReportAgentModuleCatalog.All.ShouldAllBe(module =>
			!string.IsNullOrWhiteSpace(module.ViewPermission) &&
			!string.IsNullOrWhiteSpace(module.BuildPermission) &&
			!string.IsNullOrWhiteSpace(module.ExportPermission));
	}

	[Fact]
	public void Implemented_Modules_Should_Be_Advertised_As_Available()
	{
		var available = ReportAgentModuleCatalog.All
			.Where(module => module.Availability == ReportAgentModuleAvailability.Available)
			.Select(module => module.Id)
			.ToList();

		available.ShouldBe([
			"academic.curriculum", "academic.timetable", "academic.homework-assignment",
			"academic.examination", "students.records", "students.attendance", "hr.reports",
			"finance.reports", "library.reports", "transport.reports"
		]);
	}
}
