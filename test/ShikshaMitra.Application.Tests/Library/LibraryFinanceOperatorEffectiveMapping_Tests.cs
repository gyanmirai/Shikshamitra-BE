using System;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Operator_Posting_Options_Should_Disable_A_Mapped_Income_Outside_Its_Actual_Finance_Effective_Window(bool future)
	{
		var context = await SeedOperatorChoicesAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		using var actor = SignIn(context.Owner);
		var categoryId = context.VisibleCategories[0];
		var search = await WithUnitOfWorkAsync(async () =>
		{
			var now = new DateTimeOffset(GetRequiredService<IClock>().Now);
			var income = new TransactionCategory(Guid.NewGuid(), context.Owner.TenantId, context.Owner.YearId,
				TransactionType.Income, $"Window {Guid.NewGuid():N}"[..30], null,
				effectiveFrom: future ? now.AddDays(7) : now.AddDays(-14),
				effectiveTo: future ? now.AddDays(14) : now.AddDays(-7));
			await GetRequiredService<IRepository<TransactionCategory, Guid>>().InsertAsync(income, true);
			var categories = GetRequiredService<IRepository<ChargeCategory, Guid>>();
			var category = await categories.GetAsync(categoryId);
			category.SetAccountingMapping(income.Id);
			await categories.UpdateAsync(category, true);
			return category.Name;
		});
		var input = Post(context.Owner);
		input.FinanceChargeCategoryId = categoryId;
		var canonicalError = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(context.Owner, useStoredSource: true).PostAsync(input)));
		canonicalError.Code.ShouldBe(FinanceRoutingErrorCodes.InvalidCategory);

		var choices = await WithUnitOfWorkAsync(() => OperatorService(context.Owner).GetPostingOptionsAsync(context.Source,
			FinanceExternalOperatorOptionKind.ChargeCategory, search: search));

		choices.TotalCount.ShouldBe(1);
		choices.Items.Count.ShouldBe(1);
		choices.Items[0].Id.ShouldBe(categoryId);
		choices.Items[0].IsSelectable.ShouldBeFalse();
		choices.Items[0].BlockedReason.ShouldNotBeNullOrWhiteSpace();
		await AssertOperatorChoicesAreReadOnlyAsync(context);
	}
}
