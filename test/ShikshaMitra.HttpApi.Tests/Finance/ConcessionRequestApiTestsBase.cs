using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

public abstract class ConcessionRequestApiTestsBase : ShikshaMitraApiTestBase
{
	protected const string BaseUrl = "/api/concession-request";
	protected const string ConcessionRequestsBaseUrl = "/api/concession-requests";

	protected ConcessionRequestApiTestsBase(ShikshaMitraApiTestFactory factory) : base(factory)
	{
		ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	protected async Task<Guid> CreateDraftWithFeeHeadsAndSubmitAsync()
	{
		var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, boardId,
			ConcessionRequestTestHelper.DefaultMediumId,
			ConcessionRequestTestHelper.DefaultStandardId,
			ConcessionRequestTestHelper.DefaultDivisionId,
			new[] { typeId });

		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		(await Client.PutAsJsonAsync($"{BaseUrl}/{requestId}/feeHeadsData",
			new SaveFeeHeadsDataDto
			{
				FeeHeadsData = new List<ConcessionTypeFeeHeadsDto>
				{
					new() { ConcessionTypeId = typeId, SelectedFeeHeadIds = new List<Guid> { feeHeadId } },
				},
			}, JsonOptions)).EnsureSuccessStatusCode();

		(await Client.PostAsJsonAsync($"{BaseUrl}/{requestId}/submit", (object?)null, JsonOptions))
			.EnsureSuccessStatusCode();

		return requestId;
	}
}
