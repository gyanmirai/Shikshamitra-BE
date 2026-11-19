using System;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.Reports;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class InstituteReportModuleRules_Tests
{
	[Theory]
	[InlineData("school-document-register")]
	[InlineData("school-document-counts")]
	[InlineData("school-identity-register")]
	[InlineData("school-identity-counts")]
	[InlineData("school-identity-signatures")]
	public void New_reports_have_source_grants_and_do_not_require_an_academic_year(string report)
	{
		InstituteReportCatalog.Validate(new() { ReportType = report });
		InstituteReportCatalog.Get(report).RequiredPermissions.ShouldNotBeEmpty();
	}
	[Fact]
	public void Metadata_reports_reject_unrelated_filters_and_protected_fields()
	{
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new() { ReportType = "school-document-counts", Filters = new() { AcademicYearId = Guid.NewGuid() } }));
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new() { ReportType = "school-document-counts", Filters = new() { DocumentCategory = "Invented" } }));
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new() { ReportType = "school-identity-register", Columns = ["principalName"] }));
		Should.Throw<BusinessException>(() => InstituteReportCatalog.Validate(new() { ReportType = "school-identity-register", Filters = new() { StandardId = Guid.NewGuid() } }));
		InstituteReportCatalog.RequiresScopeStamp("school-identity-counts").ShouldBeTrue();
	}
	[Fact]
	public void Identity_read_scope_uses_exact_context_membership_and_keeps_management_stricter()
	{
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid(); var organization = Guid.NewGuid();
		var scope = new UserScopeContext { BoardIds = [board], MediumIds = [medium], MediumStageIds = [organization] };
		SchoolIdentityScopeAccess.Allows(scope, year, board, medium, organization).ShouldBeTrue();
		SchoolIdentityScopeAccess.Allows(scope, year, board, medium, Guid.NewGuid()).ShouldBeFalse();
		SchoolIdentityScopeAccess.Allows(scope, year, board, medium, organization, manage: true).ShouldBeFalse();
		SchoolIdentityScopeAccess.Allows(new(), null, null, null).ShouldBeTrue();
		SchoolIdentityScopeAccess.Allows(new(), year, board, medium).ShouldBeFalse();
	}
}
