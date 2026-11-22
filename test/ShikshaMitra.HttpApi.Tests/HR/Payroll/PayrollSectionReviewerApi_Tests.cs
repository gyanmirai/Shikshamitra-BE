using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name), TestModule(TestTraits.Modules.HR)]
public class PayrollSectionReviewerApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Root = "/api/payroll/run/approval-settings";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Reviewer_discovery_requires_current_role_membership_and_exact_section_scope()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var actorId = Guid.Parse(TestAuthHandler.TestUserId);
		var stageId = Factory.SeededMediumStageId;
		var roleId = Guid.NewGuid();
		var eligibleId = Guid.NewGuid();
		var otherSectionId = Guid.NewGuid();
		var otherRoleId = Guid.NewGuid();
		await WithSchoolAsync(async services =>
		{
			var users = services.GetRequiredService<IIdentityUserRepository>();
			if (await users.FindAsync(actorId) == null)
				await users.InsertAsync(NewAccount(actorId));
			await services.GetRequiredService<IIdentityRoleRepository>()
				.InsertAsync(new IdentityRole(roleId, $"Payroll reviewer {roleId:N}", TenantId));

			var otherStage = await services.GetRequiredService<EducationMediumStageManager>()
				.CreateAsync(Factory.SeededMediumId, $"Payroll test {roleId:N}");
			await services.GetRequiredService<IEducationMediumStageRepository>().InsertAsync(otherStage);

			var scopes = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await AddMemberAsync(eligibleId, stageId, member: true);
			await AddMemberAsync(otherSectionId, otherStage.Id, member: true);
			await AddMemberAsync(otherRoleId, stageId, member: false);

			async Task AddMemberAsync(Guid userId, Guid scopedStageId, bool member)
			{
				var account = NewAccount(userId);
				if (member) account.AddRole(roleId);
				await users.InsertAsync(account);
				await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: userId,
					educationMediumStageId: scopedStageId));
			}
		});

		var choices = await GetAndReadAsync<PayrollSectionOptionsDto>($"{Root}/section-options");
		choices.Stages.ShouldContain(option => option.Id == stageId);
		choices.Roles.ShouldContain(option => option.Id == roleId);
		var reviewerUrl = $"{Root}/section-reviewers?stageId={stageId}&roleId={roleId}";
		var initial = await GetAndReadAsync<List<PayrollApproverDto>>(reviewerUrl);
		initial.Select(item => item.Id).ShouldBe([eligibleId]);

		await WithSchoolAsync(async services =>
		{
			var users = services.GetRequiredService<IIdentityUserRepository>();
			var account = await users.GetAsync(eligibleId, includeDetails: true);
			account.RemoveRole(roleId);
			await users.UpdateAsync(account);
		});

		(await GetAndReadAsync<List<PayrollApproverDto>>(reviewerUrl)).ShouldBeEmpty();
	}

	private static IdentityUser NewAccount(Guid id) =>
		new(id, $"payroll-review-{id:N}", $"payroll-review-{id:N}@example.invalid", TenantId);

	private async Task WithSchoolAsync(Func<IServiceProvider, Task> run)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
		await run(scope.ServiceProvider);
		await unit.CompleteAsync();
	}
}
