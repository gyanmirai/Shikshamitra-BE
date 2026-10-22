using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{


	[Fact]
	public void Intake_Setup_Validator_Should_Require_Fallback_Manager()
	{
		var input = ValidSetupInput("Missing fallback");
		input.CounsellorAssignmentRules.FallbackManagerUserId = Guid.Empty;
		new CreateAdmissionIntakeSetupDtoValidator().Validate(input).Errors
			.ShouldContain(error => error.PropertyName.EndsWith(nameof(AdmissionCounsellorAssignmentRulesDto.FallbackManagerUserId)));
	}


	private async Task EnsureSelectableAdmissionUserAsync()
	{
		await EnsureTestIdentityUserAsync(TestUserId, $"intake-admin-{TestUserId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var scopeRepository = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			if (!await scopeRepository.AnyAsync(item => item.UserId == TestUserId))
				await scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
					userId: TestUserId, description: "Admission intake setup test"), autoSave: true);
		});
	}

	private static CreateAdmissionIntakeSetupDto ValidSetupInput(string name) => new()
	{
		Name = name,
		InquiryRules = new AdmissionInquiryIntakeRulesDto(),
		CounsellorAssignmentRules = new AdmissionCounsellorAssignmentRulesDto
		{
			CounsellorUserIds = [TestUserId],
			FallbackManagerUserId = TestUserId,
			MaxActiveCasesPerCounsellor = 100
		}
	};

	private static UpdateAdmissionIntakeSetupDto ValidUpdateInput(AdmissionIntakeSetupDto setup, string name) => new()
	{
		Name = name,
		InquiryRules = setup.InquiryRules,
		CounsellorAssignmentRules = setup.CounsellorAssignmentRules,
		ConcurrencyStamp = setup.ConcurrencyStamp
	};
}
