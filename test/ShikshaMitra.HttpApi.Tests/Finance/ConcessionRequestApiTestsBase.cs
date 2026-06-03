using System;
using System.Collections.Generic;
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

	protected async Task<Guid> CreatePendingRequestWithFeeHeadAsync()
	{
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);

		return await ConcessionRequestTestHelper.CreateConcessionRequestAsync(
			Client, studentId, typeId, new[] { feeHeadId });
	}
}
