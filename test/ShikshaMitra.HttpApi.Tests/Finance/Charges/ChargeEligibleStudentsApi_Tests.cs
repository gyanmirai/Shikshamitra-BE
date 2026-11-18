using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Charges;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ChargeEligibleStudentsApi_Tests(ShikshaMitraApiTestFactory factory) : ChargeApiTestsBase(factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Lookup_pages_eligible_students_without_losing_the_full_audience()
	{
		var (first, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var (second, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var (unassigned, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory, assignFeeStructure: false);
		var query = $"{BaseUrl}/eligible-students?academicYearId={Factory.SeededAcademicYearId}" +
			$"&boardId={Factory.SeededBoardId}&educationMediumId={Factory.SeededMediumId}" +
			$"&classIds={Factory.SeededStandardId}&page=1&pageSize=1";
		var response = await Client.GetAsync(query);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var data = await response.Content.ReadFromJsonAsync<LookupResponse>(JsonOptions);
		data.ShouldNotBeNull();
		data.Items.Count.ShouldBe(1);
		data.CandidateStudentIds.ShouldContain(first);
		data.CandidateStudentIds.ShouldContain(second);
		data.CandidateStudentIds.ShouldNotContain(unassigned);
		data.MatchingStudentIds.Count.ShouldBe(data.TotalCount);
		data.MatchingStudentNames.Keys.ShouldContain(first);
		data.MatchingStudentNames[first].ShouldNotBeNullOrWhiteSpace();
		data.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
		var secondPage = await Client.GetFromJsonAsync<LookupResponse>(
			query.Replace("page=1", "page=2"), JsonOptions);
		secondPage.ShouldNotBeNull();
		secondPage.CandidateStudentIds.ShouldBe(data.CandidateStudentIds);
		secondPage.Items.Single().StudentId.ShouldNotBe(data.Items.Single().StudentId);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Lookup_search_filters_rows_without_shrinking_the_confirmed_audience()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client, Factory);
		var url = $"{BaseUrl}/eligible-students?academicYearId={Factory.SeededAcademicYearId}" +
			$"&boardId={Factory.SeededBoardId}&educationMediumId={Factory.SeededMediumId}" +
			$"&classIds={Factory.SeededStandardId}&search=NO_SUCH_STUDENT_{Guid.NewGuid():N}";
		var result = await Client.GetFromJsonAsync<LookupResponse>(url, JsonOptions);
		result.ShouldNotBeNull();
		result.CandidateStudentIds.ShouldContain(studentId);
		result.MatchingStudentIds.ShouldBeEmpty();
		result.MatchingStudentNames.ShouldBeEmpty();
		result.Items.ShouldBeEmpty();
		result.TotalCount.ShouldBe(0);
	}

	public sealed class LookupResponse
	{
		public List<LookupItem> Items { get; set; } = [];
		public List<Guid> CandidateStudentIds { get; set; } = [];
		public List<Guid> MatchingStudentIds { get; set; } = [];
		public Dictionary<Guid, string> MatchingStudentNames { get; set; } = [];
		public int TotalCount { get; set; }
	}

	public sealed class LookupItem
	{
		public Guid StudentId { get; set; }
	}
}
