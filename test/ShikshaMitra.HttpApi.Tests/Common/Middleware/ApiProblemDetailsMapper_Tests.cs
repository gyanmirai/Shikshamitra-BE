using Microsoft.AspNetCore.Http;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.HR;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Security.Roles;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Authorization;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsMapper_Tests
{
	[Theory]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource)]
	[InlineData(AbpAuthorizationErrorCodes.GivenRequirementHasNotGrantedForGivenResource)]
	[InlineData(AbpAuthorizationErrorCodes.GivenRequirementsHasNotGrantedForGivenResource)]
	public void Framework_authorization_codes_should_preserve_actual_authentication_and_authorization_status(string errorCode)
	{
		ApiProblemDetailsMapper.NormalizeStatusCode(401, errorCode).ShouldBe(401);
		ApiProblemDetailsMapper.NormalizeStatusCode(403, errorCode).ShouldBe(403);
	}

	[Theory]
	[InlineData(RoleErrorCodes.AssignedToUsers)]
	[InlineData(RoleErrorCodes.ReferencedByEmployees)]
	[InlineData(TenantSettingsDomainErrorCodes.DuplicateStandardName)]
	[InlineData(TenantSettingsDomainErrorCodes.DuplicateDivisionName)]
	[InlineData(FeeCollectionErrorCodes.IdempotencyConflict)]
	[InlineData(AccountSettingErrorCodes.DuplicateName)]
	[InlineData(AccountSettingErrorCodes.DuplicateCode)]
	[InlineData(AccountSettingErrorCodes.ActiveRecordCannotBeDeleted)]
	[InlineData(AccountSettingErrorCodes.CategoryInUse)]
	[InlineData(AccountSettingErrorCodes.BankAccountInUse)]
	[InlineData(FinanceFeeCategoryErrorCodes.CannotDeleteInUse)]
	[InlineData(FinanceFeeHeadErrorCodes.CannotDeleteInUse)]
	[InlineData(PaymentTermErrorCodes.DeleteWithReferences)]
	[InlineData(PaymentTermErrorCodes.DeleteRequiresDraft)]
	[InlineData(PaymentTermErrorCodes.DeleteBoardDefault)]
	[InlineData(PaymentTermErrorCodes.DuplicateCode)]
	[InlineData(PaymentTermErrorCodes.DuplicateName)]
	[InlineData(ConcessionTypeErrorCodes.CannotDeleteInUse)]
	[InlineData(ConcessionTypeErrorCodes.ActiveCannotBeDeleted)]
	[InlineData(ReceiptBookErrorCodes.CannotDeleteInUse)]
	[InlineData(FeeStructureErrorCodes.CannotDeleteInUse)]
	[InlineData(CollectionPolicyErrorCodes.DefaultPolicyCannotBeRemoved)]
	[InlineData(CollectionPolicyErrorCodes.PolicyInUse)]
	[InlineData(CollectionPolicyErrorCodes.ExceptionWindowAlreadyActive)]
	[InlineData(ConcessionPolicyErrorCodes.DefaultPolicyCannotBeRemoved)]
	[InlineData(ConcessionPolicyErrorCodes.PolicyInUse)]
	[InlineData(ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved)]
	[InlineData(ProrationPolicyErrorCodes.PolicyInUse)]
	[InlineData(RefundPolicyErrorCodes.DefaultPolicyCannotBeRemoved)]
	[InlineData(RefundPolicyErrorCodes.PolicyInUse)]
	[InlineData("HR:Payroll:SalaryTemplateSaveConflict")]
	[InlineData(HRErrorCodes.CompensationPreviewStale)]
	public void Conflict_errors_should_resolve_and_normalize_to_409(string errorCode)
	{
		ApiProblemDetailsMapper.ResolveBusinessStatusCode(errorCode)
			.ShouldBe(StatusCodes.Status409Conflict);
		ApiProblemDetailsMapper.NormalizeStatusCode(StatusCodes.Status400BadRequest, errorCode)
			.ShouldBe(StatusCodes.Status409Conflict);
	}

	[Theory]
	[InlineData(PaymentTermErrorCodes.NotFound)]
	[InlineData(CollectionPolicyErrorCodes.PolicyNotFound)]
	[InlineData(ConcessionPolicyErrorCodes.PolicyNotFound)]
	[InlineData(ProrationPolicyErrorCodes.PolicyNotFound)]
	public void Finance_settings_missing_records_should_resolve_to_404(string errorCode)
	{
		ApiProblemDetailsMapper.NormalizeStatusCode(StatusCodes.Status400BadRequest, errorCode)
			.ShouldBe(StatusCodes.Status404NotFound);
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
