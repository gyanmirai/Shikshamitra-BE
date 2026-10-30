using System;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class NoneCategory_Tests
{
	[Theory]
	[InlineData(TransactionType.Income)]
	[InlineData(TransactionType.Expense)]
	public void None_cannot_be_renamed_or_deactivated(TransactionType type)
	{
		var category = new TransactionCategory(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, "None", null);
		Should.Throw<BusinessException>(() => category.Update("CODE", "Changed", null, null, null, null, []))
			.Code.ShouldBe("Finance:Accounts:Settings:SystemCategoryReadOnly");
		Should.Throw<BusinessException>(() => category.Deactivate("Not needed"))
			.Code.ShouldBe("Finance:Accounts:Settings:SystemCategoryReadOnly");
		category.Name.ShouldBe("None");
		category.IsActive.ShouldBeTrue();
	}
}
