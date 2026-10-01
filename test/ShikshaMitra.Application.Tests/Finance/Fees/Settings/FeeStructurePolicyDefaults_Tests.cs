using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Fact]
	public async Task Settings_Expose_Exactly_One_Current_Default_Per_Family_After_Replacement()
	{
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity(
			[new Claim(AbpClaimTypes.UserId, TestUserId.ToString())], "Test"))))
		using (_currentTenant.Change(TestTenantId))
		{
			var (year, standard, metadata, head) = await SeedStandardAsync();
			var original = await CreateCollectionPolicyAsync(year, head, "Original collection default");
			var replacement = await _collectionPolicies.CreateAsync(new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = year, PolicyName = "Replacement collection default", PolicyCode = "CP-REPLACE",
				Status = "Active", IsDefault = true, EnableLateFee = false
			});
			var concession = await CreateConcessionPolicyAsync(year, "Concession default");
			var refunds = GetRequiredService<IRefundPolicyAppService>();
			var refund = await refunds.CreateAsync(new CreateUpdateRefundPolicyDto
			{
				AcademicYearId = year, PolicyName = "Refund default", Status = "Active"
			});
			refund.IsDefault.ShouldBeTrue();
			(await Should.ThrowAsync<BusinessException>(() => refunds.ArchiveAsync(refund.Id)))
				.Code.ShouldBe(RefundPolicyErrorCodes.DefaultPolicyCannotBeRemoved);
			var prorations = GetRequiredService<IProrationPolicyAppService>();
			var draft = await prorations.CreateAsync(new CreateUpdateProrationPolicyDto
			{
				AcademicYearId = year, PolicyName = "Proration default", Status = "Draft",
				FeeHeadRules = [new() { FeeHeadId = head, RuleType = "Daily", MinimumChargeDays = 0 }]
			});
			draft.IsDefault.ShouldBeFalse();
			var proration = await prorations.ActivateAsync(draft.Id);
			proration.IsDefault.ShouldBeTrue();
			(await Should.ThrowAsync<BusinessException>(() => prorations.ArchiveAsync(proration.Id)))
				.Code.ShouldBe(ProrationPolicyErrorCodes.DefaultPolicyCannotBeRemoved);

			var settings = await _feeStructures.GetSettingsAsync(metadata, standard);
			foreach (var (family, id) in new[]
			{
				("collection", replacement.Id), ("concession", concession),
				("refund", refund.Id), ("proration", proration.Id)
			})
			{
				var defaults = settings.Policies.Where(x => x.Family == family && x.IsDefault).ToList();
				defaults.Count.ShouldBe(1);
				defaults[0].Id.ShouldBe(id);
				defaults[0].IsCurrent.ShouldBeTrue();
			}
			settings.Policies.Where(x => x.Id == original).ShouldAllBe(x => !x.IsDefault);
			settings.Policies.Count(x => x.Id == original).ShouldBeGreaterThan(1);
			settings.Policies.Count(x => x.Id == original && x.IsCurrent).ShouldBe(1);
		}
	}
}
