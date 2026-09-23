using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public class ChargeCategoryApiContract_Tests
{
	[Fact]
	public void Status_route_supports_put_and_patch_with_change_status_permission()
	{
		var method = typeof(ChargeCategoryController)
			.GetMethod(nameof(ChargeCategoryController.UpdateStatusAsync))!;

		method.GetCustomAttributes<HttpPutAttribute>()
			.Single().Template.ShouldBe("{chargeCategoryId}/status");
		method.GetCustomAttributes<HttpPatchAttribute>()
			.Single().Template.ShouldBe("{chargeCategoryId}/status");
		method.GetCustomAttributes<AuthorizeAttribute>()
			.Single().Policy.ShouldBe(
				ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ChargeCategory.ChangeStatus);
	}

	[Fact]
	public void List_route_exposes_the_optional_active_filter()
	{
		var parameter = typeof(ChargeCategoryController)
			.GetMethod(nameof(ChargeCategoryController.GetListAsync))!
			.GetParameters()
			.Single(item => item.Name == "isActive");

		parameter.ParameterType.ShouldBe(typeof(bool?));
		parameter.HasDefaultValue.ShouldBeTrue();
		parameter.DefaultValue.ShouldBeNull();
	}

	[Fact]
	public void Status_reason_accepts_any_non_empty_length()
	{
		var reasonProperty = typeof(UpdateChargeCategoryStatusRequest)
			.GetProperty(nameof(UpdateChargeCategoryStatusRequest.Reason))!;

		reasonProperty.GetCustomAttribute<MinLengthAttribute>().ShouldBeNull();
		reasonProperty.GetCustomAttribute<MaxLengthAttribute>()!.Length.ShouldBe(500);
	}
}
