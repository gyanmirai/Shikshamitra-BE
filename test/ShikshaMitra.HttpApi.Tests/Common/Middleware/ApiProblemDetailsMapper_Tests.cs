using Microsoft.AspNetCore.Http;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.HR;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Security.Roles;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsMapper_Tests
{
	[Theory]
	[InlineData(RoleErrorCodes.AssignedToUsers)]
	[InlineData(RoleErrorCodes.ReferencedByEmployees)]
	[InlineData(TenantSettingsDomainErrorCodes.DuplicateStandardName)]
	[InlineData(TenantSettingsDomainErrorCodes.DuplicateDivisionName)]
	[InlineData(FeeCollectionErrorCodes.IdempotencyConflict)]
	public void Conflict_errors_should_resolve_and_normalize_to_409(string errorCode)
	{
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(errorCode)
			.ShouldBe(StatusCodes.Status409Conflict);
		ApiProblemDetailsMapper.NormalizeStatusCode(StatusCodes.Status400BadRequest, errorCode)
			.ShouldBe(StatusCodes.Status409Conflict);
	}

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
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(HRErrorCodes.LeaveInsufficientBalance)
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

		ApiProblemDetailsMapper.NormalizeStatusCode(
			StatusCodes.Status403Forbidden,
			HRErrorCodes.LeaveInsufficientBalance)
			.ShouldBe(StatusCodes.Status400BadRequest);
	}
}
