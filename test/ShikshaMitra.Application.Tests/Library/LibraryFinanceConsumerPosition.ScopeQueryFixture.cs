using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<string[]> RestrictConsumerToDashboardAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		var groups = await GetRequiredService<IPermissionDefinitionManager>().GetGroupsAsync();
		var unrelated = groups.SelectMany(group => group.GetPermissionsWithChildren()).Select(permission => permission.Name)
			.Where(name => (name.StartsWith("Finance.", StringComparison.Ordinal) || name.StartsWith("Library.", StringComparison.Ordinal)) &&
				name != ShikshaMitraSubmodulePermissions.Library.Dashboard.View).Distinct().ToArray();
		var manager = GetRequiredService<IPermissionManager>();
		var role = $"external-finance-{owner.ActorId:N}";
		var existing = await manager.GetAsync(unrelated, "R", role);
		foreach (var grant in existing.Result.Where(grant => grant.IsGranted))
			await manager.SetAsync(grant.Name, "R", role, false);
		await manager.SetAsync(ShikshaMitraSubmodulePermissions.Library.Dashboard.View, "R", role, true);
		return unrelated;
	});

	private Task AssertConsumerDashboardAuthorityAsync(MovedTypedCashFixture moved, string[] deniedActions, bool currentPlacement) =>
		WithUnitOfWorkAsync(async () =>
		{
			var owner = moved.Settlement.Owner;
			var checker = new FinanceServices(ServiceProvider, owner, useStoredSource: true).GetRequiredService<IPermissionChecker>();
			(await checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Dashboard.View)).ShouldBeTrue();
			(await checker.IsGrantedAsync(deniedActions)).Result.Values.ShouldNotContain(PermissionGrantResult.Granted);
			(await GetRequiredService<IUserScopeService>().IsAdminUserAsync(owner.ActorId)).ShouldBeFalse();
			var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
			scope.HasTenantWideScope().ShouldBeFalse();
			(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(moved.Source.LoanId, scope)).ShouldBe(!currentPlacement);
			var employees = await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync(scope);
			(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(employees.Where(row => row.Id == owner.PersonId)))
				.ShouldBe(currentPlacement);
		});

	private Task<ConsumerQueryMeasurement> MeasureConsumerQueryAsync(Fixture owner) => WithUnitOfWorkAsync(async () =>
	{
		using var activity = new Activity("Canonical Library consumer count and pages").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var capture = new LibraryLookupSqlCapture(activity.TraceId);
		var positions = await ConsumerPositionService(owner).GetQueryAsync(new(
			LibraryChargePositionReadPurpose.ChargeHistory, owner.YearId));
		capture.Commands.Where(IsConsumerSourceOrFinanceSql).ShouldBeEmpty(
			"Constructing the query must not materialize assessment IDs or read each source's monetary evidence.");
		var executor = GetRequiredService<IAsyncQueryableExecuter>();
		var total = await executor.CountAsync(positions);
		var collectible = positions.Where(row => row.Evidence == FinanceExternalPositionEvidence.Verified && row.Collectible > 0m);
		var actionable = await executor.CountAsync(collectible);
		var sourceIds = collectible.Select(row => row.SourceId);
		var assessments = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetQueryableAsync();
		var candidates = assessments.Where(row => row.TenantId == owner.TenantId && row.AcademicYearId == owner.YearId);
		var joined = from assessment in candidates join sourceId in sourceIds on assessment.Id equals sourceId select assessment.Id;
		(await executor.CountAsync(joined)).ShouldBe(actionable);
		var page = await executor.ToListAsync(joined.OrderBy(id => id).Take(1));
		(await executor.ToListAsync(joined.OrderBy(id => id).Skip(1).Take(1))).ShouldBeEmpty();

		var sourceSql = capture.Commands.Where(sql => sql.Contains("\"AppLibraryCharges\"", StringComparison.Ordinal)).ToArray();
		var moneySql = capture.Commands.Where(sql => sql.Contains("\"Finance_", StringComparison.Ordinal)).ToArray();
		sourceSql.Length.ShouldBe(5, "Only the five explicit terminal operations may read assessment candidates.");
		// Count only the outer operation: the valid Finance projection may itself
		// contain grouped COUNTs or bounded scalar subqueries.
		sourceSql.Count(sql => sql.TrimStart().StartsWith("SELECT COUNT(", StringComparison.OrdinalIgnoreCase)).ShouldBe(3);
		sourceSql.Count(sql => !sql.TrimStart().StartsWith("SELECT COUNT(", StringComparison.OrdinalIgnoreCase) &&
			sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)).ShouldBe(2);
		moneySql.ShouldNotBeEmpty("The test must execute the actual canonical Finance evidence query.");
		moneySql.Length.ShouldBeLessThanOrEqualTo(5);
		moneySql.ShouldContain(sql => sql.Contains("\"Finance_ExternalReceivableSourceLinks\"", StringComparison.Ordinal));
		moneySql.ShouldContain(sql => sql.Contains("\"Finance_ChargeCollectionRecords\"", StringComparison.Ordinal));
		moneySql.ShouldContain(sql => sql.Contains("\"Finance_FeeRecordMetadatas\"", StringComparison.Ordinal));
		return new ConsumerQueryMeasurement(total, actionable, page.ToArray(), sourceSql.Length, moneySql.Length);
	});

	private static bool IsConsumerSourceOrFinanceSql(string sql) =>
		sql.Contains("\"AppLibraryCharges\"", StringComparison.Ordinal) || sql.Contains("\"Finance_", StringComparison.Ordinal);

	// Every input field is carried, using actual facts read in the foreign tenant.
	// This remains a real provider IQueryable over a persisted source row; no
	// Enumerable.AsQueryable, detached fake source or shortened evidence shape.
	private static IQueryable<FinanceExternalPositionSource> ProjectForeignConsumerCandidate(
		IQueryable<LibraryCharge> assessments, FinanceExternalPositionSource facts) =>
		assessments.Where(row => row.Id == facts.SourceId).Select(row => new FinanceExternalPositionSource
		{
			TenantId = row.TenantId!.Value, Kind = facts.Kind, SourceId = row.Id, SourceVersion = facts.SourceVersion,
			ExpectedSourceLinkId = row.FinanceSourceLinkId, AcademicYearId = row.AcademicYearId,
			AcademicYearConfirmed = facts.AcademicYearConfirmed, AcademicYearEvidenceJson = row.AcademicYearEvidenceJson,
			PayerKind = facts.PayerKind, StudentId = facts.StudentId, EmployeeId = facts.EmployeeId,
			AssessmentOriginal = row.Amount, RecordedCurrency = row.Currency, SchoolAssessmentDate = row.SchoolChargeDate,
			SchoolDueDate = row.SchoolDueDate, SchoolDatesConfirmed = facts.SchoolDatesConfirmed,
			SourceDateEvidenceJson = row.SourceDateEvidenceJson, RawSourceCategoryId = row.ChargeCategoryId,
			RawSourceRemarks = row.Remarks, CurrentLegacyResolvedReason = facts.CurrentLegacyResolvedReason,
			RecordedSettlementDeclaration = facts.RecordedSettlementDeclaration
		});

	private sealed record ConsumerQueryMeasurement(int Total, int Actionable, Guid[] Page, int SourceStatements, int FinanceStatements);
}
