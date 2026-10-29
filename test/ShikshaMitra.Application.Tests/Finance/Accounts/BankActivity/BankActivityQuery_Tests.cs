using System;
using System.Linq;
using ShikshaMitra.Finance.Accounts.BankActivity;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;
namespace ShikshaMitra.Finance.Accounts;
public class BankActivityQuery_Tests
{
	[Theory]
	[InlineData(0, 10)]
	[InlineData(1, 0)]
	[InlineData(1, 101)]
	public void Rejects_invalid_paging(int page, int size) => Should.Throw<BusinessException>(() => BankActivityQueryRules.Validate(new() { AcademicYearId = Guid.NewGuid(), Page = page, PageSize = size }));
	[Theory]
	[InlineData("unknown", null, null)]
	[InlineData(null, "drop table", null)]
	[InlineData(null, null, "sideways")]
	public void Rejects_unknown_source_sort_and_direction(string? source, string? sort, string? direction) => Should.Throw<BusinessException>(() => BankActivityQueryRules.Validate(new() { AcademicYearId = Guid.NewGuid(), Source = source, SortBy = sort, Direction = direction }));
	[Fact]
	public void Rejects_unbounded_search_and_filters()
	{
		Should.Throw<BusinessException>(() => BankActivityQueryRules.Validate(new() { AcademicYearId = Guid.NewGuid(), Search = new string('a', 101) }));
		Should.Throw<BusinessException>(() => BankActivityQueryRules.Validate(new() { AcademicYearId = Guid.NewGuid(), Filters = "[{\"id\":\"secret\",\"operator\":\"eq\",\"value\":\"x\"}]" }));
	}
	[Fact]
	public void Historical_year_can_include_later_credit_dates()
	{
		var request = new BankActivityQuery { AcademicYearId = Guid.NewGuid(), From = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), ToExclusive = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
		BankActivityQueryRules.Validate(request);
	}
	[Fact]
	public void Scope_prefers_original_admission_account_and_never_grants_manual_rows_to_partial_scope()
	{
		var student = Guid.NewGuid(); var account = Guid.NewGuid();
		var facts = new[] { new BankActivityFact { Key = "student", StudentId = student }, new BankActivityFact { Key = "admission", StudentId = student, FinanceFeeAccountId = account }, new BankActivityFact { Key = "manual" } }.AsQueryable();
		BankActivityQueryRules.Scope(facts, new[] { student }.AsQueryable(), Array.Empty<Guid>().AsQueryable(), false).Select(x => x.Key).ShouldBe(new[] { "student" });
		BankActivityQueryRules.Scope(facts, Array.Empty<Guid>().AsQueryable(), new[] { account }.AsQueryable(), false).Select(x => x.Key).ShouldBe(new[] { "admission" });
	}
	[Fact]
	public void Manual_rows_require_full_selected_year_access_not_full_tenant_access()
	{
		var year = Guid.NewGuid(); var otherYear = Guid.NewGuid();
		var fullYear = new UserScopeContext { AcademicYearIds = [year], WildcardBoardAcademicYearIds = [year] };
		var narrow = new UserScopeContext { AcademicYearIds = [year], StandardIds = [Guid.NewGuid()] };
		var facts = new[] { new BankActivityFact { Key = "manual" } }.AsQueryable(); var empty = Array.Empty<Guid>().AsQueryable();
		BankActivityQueryRules.Scope(facts, empty, empty, fullYear.HasAcademicYearWideScope(year)).Count().ShouldBe(1);
		BankActivityQueryRules.Scope(facts, empty, empty, fullYear.HasAcademicYearWideScope(otherYear)).Count().ShouldBe(0);
		BankActivityQueryRules.Scope(facts, empty, empty, narrow.HasAcademicYearWideScope(year)).Count().ShouldBe(0);
	}
	[Fact]
	public void Pending_collection_requires_owning_provisional_view_before_counts_summaries_or_export()
	{
		var facts = new[] { new BankActivityFact { Source = "collection", RecognitionStatus = 1, PendingAmount = 999m }, new BankActivityFact { Source = "income", Incoming = 1.01m } }.AsQueryable();
		var visible = BankActivityQueryRules.AuthorizedSlices(facts, true, false, false, false);
		visible.Count().ShouldBe(1); BankActivityQueryRules.Aggregates(visible).Single().PendingIncoming.ShouldBe(0m);
		BankActivityQueryRules.AuthorizedSlices(facts, true, false, false, true).Count().ShouldBe(2);
		BankActivityQueryRules.AuthorizedSlices(facts, false, false, false, true).Count().ShouldBe(0);
	}
	[Fact]
	public void Summary_keeps_transit_and_provisional_disjoint_and_retains_paise()
	{
		var facts = new[] { new BankActivityFact { Incoming = 8000.01m }, new BankActivityFact { Outgoing = 123.45m }, new BankActivityFact { PendingAmount = 200m }, new BankActivityFact { InTransitAmount = 300.03m } }.AsQueryable();
		var result = BankActivityQueryRules.Aggregates(facts).Single();
		result.Net.ShouldBe(7876.56m); result.PendingIncoming.ShouldBe(200m); result.InTransit.ShouldBe(300.03m);
	}
}
