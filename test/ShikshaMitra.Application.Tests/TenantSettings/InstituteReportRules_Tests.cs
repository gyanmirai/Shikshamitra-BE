using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteReportRules_Tests
{
	[Fact]
	public void Academic_reports_require_year_but_school_catalogues_do_not()
	{
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new()));
		InstituteReportCatalog.Validate(new() { ReportType = "rooms-inventory" });
		InstituteReportCatalog.Validate(new() { ReportType = "shift-register" });
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new() { ReportType = "shift-register", Filters = new() { AcademicYearId = Guid.NewGuid() } }));
	}
	[Theory]
	[InlineData("students", "shift", "contains")]
	[InlineData("shift-register", "salary", "eq")]
	[InlineData("shift-register", "shift", "sql")]
	[InlineData("shift-register", "shift", "gt")]
	public void Unknown_sources_fields_and_operators_cannot_reach_query_execution(string type, string field, string op) =>
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new() { ReportType = type, Conditions = [new() { Field = field, Operator = op, Value = "1" }] }));
	[Fact]
	public void Numeric_conditions_sort_the_complete_population_before_pagination()
	{
		var rows = new[] { Row("9"), Row("100"), Row("30"), Row(null) };
		var result = InstituteReportProjection.Apply(rows, new() { SortField = "capacity", Conditions = [new() { Field = "capacity", Operator = "gt", Value = "10" }] }).ToList();
		result.Select(x => x["capacity"]).ShouldBe(["30", "100"]);
	}
	[Fact]
	public void Approval_cannot_be_reused_after_user_tenant_format_filter_or_column_change()
	{
		var approvals = new InstituteReportApproval(new EphemeralDataProtectionProvider());
		var user = Guid.NewGuid(); var tenant = Guid.NewGuid(); var request = new InstituteReportRequest { ReportType = "shift-register" };
		var token = approvals.Create(user, tenant, request);
		approvals.Validate(token, user, tenant, request);
		Should.Throw<BusinessException>(() => approvals.Validate(token, Guid.NewGuid(), tenant, request));
		Should.Throw<BusinessException>(() => approvals.Validate(token, user, Guid.NewGuid(), request));
		request.Format = "excel";
		Should.Throw<BusinessException>(() => approvals.Validate(token, user, tenant, request));
		request.Format = "pdf"; request.Filters.ShiftId = Guid.NewGuid();
		Should.Throw<BusinessException>(() => approvals.Validate(token, user, tenant, request));
		request.Filters = new(); request.Columns = ["shift"];
		Should.Throw<BusinessException>(() => approvals.Validate(token, user, tenant, request));
	}
	[Fact]
	public void Scoped_projection_keeps_only_the_assigned_division_without_widening_to_its_parent()
	{
		var year = Guid.NewGuid(); var standard = Guid.NewGuid(); var own = Guid.NewGuid(); var other = Guid.NewGuid();
		var rows = new[] { new InstituteHierarchyRow { AcademicYearId = year, StandardId = standard, DivisionId = own }, new InstituteHierarchyRow { AcademicYearId = year, StandardId = standard, DivisionId = other } }.AsQueryable();
		rows.ApplyUserScope(new UserScopeContext { AcademicYearIds = [year], DivisionIds = [own] }).Single().DivisionId.ShouldBe(own);
		rows.ApplyUserScope(new UserScopeContext()).ShouldBeEmpty();
	}
	[Fact]
	public void Download_scope_stamp_is_stable_but_changes_when_access_is_revoked()
	{
		var id = Guid.NewGuid(); var scope = new UserScopeContext { DivisionIds = [id] };
		var before = InstituteReportFiles.ScopeHash(scope);
		scope.CalculatedAt = DateTime.UtcNow.AddMinutes(1);
		InstituteReportFiles.ScopeHash(scope).ShouldBe(before);
		scope.DivisionIds.Clear();
		InstituteReportFiles.ScopeHash(scope).ShouldNotBe(before);
	}
	private static Dictionary<string, string?> Row(string? capacity) => new() { ["capacity"] = capacity };
}
