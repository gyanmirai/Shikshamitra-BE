using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ChargeCategories;
using ShikshaMitra.Finance.Fees.Settings.ConcessionType;
using ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;
using ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Settings;

public class SettingsTableFilterBinding_Tests
{
	[Theory]
	[InlineData(typeof(IncomeCategoryController))]
	[InlineData(typeof(ExpenseCategoryController))]
	[InlineData(typeof(BankAccountController))]
	[InlineData(typeof(ChargeCategoryController))]
	[InlineData(typeof(ConcessionTypeController))]
	[InlineData(typeof(FeeStructureMetadataController))]
	public void Every_list_binds_optional_filters_from_query(Type controller)
	{
		var parameter = controller.GetMethod("GetListAsync")!.GetParameters().Last();
		parameter.Name.ShouldBe("filters");
		parameter.ParameterType.ShouldBe(typeof(string));
		parameter.HasDefaultValue.ShouldBeTrue();
		parameter.DefaultValue.ShouldBeNull();
		parameter.GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
	}

	[Fact]
	public async Task Controllers_forward_the_unchanged_filter_and_existing_context()
	{
		var year = Guid.NewGuid();
		const string filters = """[{"column":"name","type":"contains","value":"Target"}]""";
		var categories = Substitute.For<ITransactionCategoryAppService>();
		await new IncomeCategoryController(categories).GetListAsync(year, 2, 5, "search", false, filters);
		await categories.Received(1).GetListAsync(year, TransactionType.Income, 2, 5, "search", false, filters);
		await new ExpenseCategoryController(categories).GetListAsync(year, 2, 5, "search", false, filters);
		await categories.Received(1).GetListAsync(year, TransactionType.Expense, 2, 5, "search", false, filters);
		var banks = Substitute.For<IBankAccountAppService>();
		await new BankAccountController(banks).GetListAsync(year, 2, 5, "search", false, filters);
		await banks.Received(1).GetListAsync(year, 2, 5, "search", false, filters);
		var charges = Substitute.For<IChargeCategoryAppService>();
		await new ChargeCategoryController(charges).GetListAsync(year, 2, 5, "search", false, filters);
		await charges.Received(1).GetListAsync(year, 2, 5, "search", false, filters);
		var concessions = Substitute.For<IConcessionTypeAppService>();
		await new ConcessionTypeController(concessions).GetListAsync(year, 5, 5, "search", true, false, filters);
		await concessions.Received(1).GetListAsync(year, 5, 5, "search", true, false, filters);
		var structures = Substitute.For<IFeeStructureMetadataAppService>();
		await new FeeStructureMetadataController(structures).GetListAsync(year, 2, 5, "search", filters);
		await structures.Received(1).GetListAsync(year, 2, 5, "search", filters);
	}
}
