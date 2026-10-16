using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Policies;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.HR.Employees;

public class EmployeePolicyAccess_Tests
{
	private const string DefaultMarker = "__employee_onboarding_default_policy_assignment__";

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(false, true)]
	public async Task Creation_Should_Reject_Missing_Policy_Access_Before_Any_Writes(bool assign, bool preview)
	{
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.Assign).Returns(assign);
		checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact).Returns(preview);
		var writer = new EmployeePersonalInfoPolicyOverrideWriter(null!, checker);
		var workflow = new EmployeePersonalInfoCreationWorkflow(
			null!, null!, null!, null!, null!, null!, writer, null!, null!, null!, null!, null!);

		var exception = await Should.ThrowAsync<BusinessException>(() => workflow.CreateAsync(
			new CreateEmployeePersonalInfoDto
			{
				PolicyOverride = new CreateEmployeePolicyOverrideDto { ConfirmationToken = DefaultMarker }
			}, null, null));

		exception.Code.ShouldBe(HRErrorCodes.EmployeePolicyAccessRequired);
		var missing = exception.Data["missingPermissions"].ShouldBeOfType<string[]>();
		missing.Contains(ShikshaMitraSubmodulePermissions.HR.Policies.Assign).ShouldBe(!assign);
		missing.Contains(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact).ShouldBe(!preview);
	}

	[Fact]
	public async Task No_Override_Should_Not_Require_Policy_Permissions()
	{
		var checker = Substitute.For<IPermissionChecker>();
		var writer = new EmployeePersonalInfoPolicyOverrideWriter(null!, checker);

		await writer.CheckAccessAsync(null);

		await checker.DidNotReceive().IsGrantedAsync(Arg.Any<string>());
	}

	[Fact]
	public async Task Explicit_Override_Should_Only_Require_Assignment_And_Recheck_Revocation()
	{
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.Assign).Returns(true, false);
		var writer = new EmployeePersonalInfoPolicyOverrideWriter(null!, checker);
		var input = new CreateEmployeePolicyOverrideDto { ConfirmationToken = "reviewed-token" };

		await writer.CheckAccessAsync(input);
		var exception = await Should.ThrowAsync<BusinessException>(() => writer.CheckAccessAsync(input));

		exception.Code.ShouldBe(HRErrorCodes.EmployeePolicyAccessRequired);
		await checker.DidNotReceive().IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact);
	}

	[Fact]
	public async Task Default_Override_Should_Accept_Assign_And_Preview_Without_Unrelated_Grants()
	{
		var checker = Substitute.For<IPermissionChecker>();
		checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.Assign).Returns(true);
		checker.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.PreviewImpact).Returns(true);
		var assignments = Substitute.For<IHrPolicyAssignmentAppService>();
		var writer = new EmployeePersonalInfoPolicyOverrideWriter(assignments, checker);

		await writer.CheckAccessAsync(new CreateEmployeePolicyOverrideDto { ConfirmationToken = DefaultMarker });

		await checker.DidNotReceive().IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Policies.View);
		await assignments.DidNotReceive().UpsertEmployeeOverrideAsync(Arg.Any<UpsertEmployeePolicyOverrideDto>());
	}

	[Fact]
	public async Task Direct_Role_Lookup_Should_Reject_Before_Reading_Identity_Records()
	{
		var repository = Substitute.For<IIdentityRoleRepository>();
		var service = new EmployeeAssignableRoleAppService(repository, Substitute.For<IPermissionChecker>());

		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetListAsync());

		repository.ReceivedCalls().ShouldBeEmpty();
	}
}
