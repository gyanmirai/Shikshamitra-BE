using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionRequestValidationApi_Tests : ConcessionRequestApiTestsBase
{
	public ConcessionRequestValidationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_400_When_Create_Missing_StudentId()
	{
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var input = new CreateConcessionRequestDto
		{
			StudentId = Guid.Empty,
			ConcessionTypeId = typeId,
		};

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}?academicYearId={ConcessionRequestTestHelper.DefaultAcademicYearId}",
			input,
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_400_When_Reject_Without_Message()
	{
		var pendingId = await CreatePendingRequestWithFeeHeadAsync();

		var response = await Client.PostAsJsonAsync(
			$"{BaseUrl}/{pendingId}/reject",
			new RejectConcessionRequestDto { Message = "" },
			JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
