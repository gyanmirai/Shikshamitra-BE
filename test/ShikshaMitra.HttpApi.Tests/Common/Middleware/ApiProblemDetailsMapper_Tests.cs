using Microsoft.AspNetCore.Http;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.HR;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsMapper_Tests
{
	[Fact]
	public void ResolveBusinessStatusCode_Should_Return_403_For_Known_Authz_Business_Codes()
	{
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(HRErrorCodes.LeaveEmployeeNotFound)
			.ShouldBe(StatusCodes.Status403Forbidden);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
			.ShouldBe(StatusCodes.Status403Forbidden);
	}

	[Fact]
	public void ResolveBusinessStatusCode_Should_Return_400_For_Validation_Business_Codes()
	{
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(HRErrorCodes.LeaveInvalidMonthForAcademicYear)
			.ShouldBe(StatusCodes.Status400BadRequest);
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(HRErrorCodes.LeaveTargetEmployeeRequired)
			.ShouldBe(StatusCodes.Status400BadRequest);
	}

	[Fact]
	public void NormalizeStatusCode_Should_Use_Canonical_Business_Code_Mapping()
	{
		ApiProblemDetailsMapper.NormalizeStatusCode(
			StatusCodes.Status403Forbidden,
			HRErrorCodes.LeaveInvalidMonthForAcademicYear)
			.ShouldBe(StatusCodes.Status400BadRequest);

		ApiProblemDetailsMapper.NormalizeStatusCode(
			StatusCodes.Status403Forbidden,
			HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
			.ShouldBe(StatusCodes.Status403Forbidden);
	}
}
