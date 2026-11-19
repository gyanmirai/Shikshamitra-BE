using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Settings;

// No WebApplicationFactory or data seeding: verify the actual MVC-to-service seam.
public class DirectFinanceTableFilterBinding_Tests
{
	[Theory]
	[InlineData(typeof(FeeHeadController))]
	[InlineData(typeof(ReceiptBookController))]
	public void Direct_setting_lists_bind_optional_filters_from_query(Type controller)
	{
		var parameter = controller.GetMethod("GetListAsync")!.GetParameters().Last();
		parameter.Name.ShouldBe("filters");
		parameter.ParameterType.ShouldBe(typeof(string));
		parameter.HasDefaultValue.ShouldBeTrue();
		parameter.DefaultValue.ShouldBeNull();
		parameter.GetCustomAttribute<FromQueryAttribute>().ShouldNotBeNull();
	}

	[Theory]
	[InlineData(typeof(FeeHeadController), typeof(IFeeHeadAppService))]
	[InlineData(typeof(ReceiptBookController), typeof(IReceiptBookAppService))]
	public async Task Direct_setting_controllers_forward_filters_with_existing_context(Type controllerType, Type serviceType)
	{
		var year = Guid.NewGuid();
		var category = Guid.NewGuid();
		const string filters = """[{"column":"name","type":"contains","value":"Target"}]""";
		var service = Substitute.For([serviceType], []);
		var controller = Activator.CreateInstance(controllerType, service)!;
		var method = controllerType.GetMethod("GetListAsync")!;
		var filterParameter = method.GetParameters().SingleOrDefault(p => p.Name == "filters");
		filterParameter.ShouldNotBeNull("The offered header criterion must be bound before it can be forwarded.");
		object? Argument(ParameterInfo parameter) => parameter.Name switch
		{
			"academicYearId" => year, "feeCategoryId" => category,
			"page" => 3, "pageSize" => 7, "search" => "existing search", "filters" => filters,
			_ => throw new InvalidOperationException("Unexpected list argument: " + parameter.Name)
		};
		await (Task)method.Invoke(controller, method.GetParameters().Select(Argument).ToArray())!;
		var call = service.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "GetListAsync");
		var forwarded = call.GetMethodInfo().GetParameters().Select((p, i) => (p.Name, Value: call.GetArguments()[i]))
			.ToDictionary(x => x.Name!, x => x.Value);
		forwarded["filters"].ShouldBe(filters);
		forwarded["academicYearId"].ShouldBe(year);
		forwarded["page"].ShouldBe(3); forwarded["pageSize"].ShouldBe(7);
		forwarded["search"].ShouldBe("existing search");
		if (controllerType == typeof(FeeHeadController)) forwarded["feeCategoryId"].ShouldBe(category);
	}

	[Fact]
	public void Adjustment_list_input_carries_optional_serialized_header_criteria()
	{
		var property = typeof(GetFeeAdjustmentApplicationsInput).GetProperty("Filters");
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
		var input = new GetFeeAdjustmentApplicationsInput();
		property.GetValue(input).ShouldBeNull();
		const string filters = """[{"column":"adjustmentAmount","type":"greaterThan","value":500}]""";
		property.SetValue(input, filters);
		property.GetValue(input).ShouldBe(filters);
	}
}
