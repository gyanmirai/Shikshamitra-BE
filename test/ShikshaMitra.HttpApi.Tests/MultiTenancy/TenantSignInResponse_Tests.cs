using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.MultiTenancy;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.MultiTenancy;
using Xunit;

namespace ShikshaMitra.MultiTenancy;

public class TenantSignInResponse_Tests
{
	[Theory]
	[InlineData("Volo.AbpIo.MultiTenancy:010001", "TENANT_NOT_FOUND")]
	[InlineData("Volo.AbpIo.MultiTenancy:010002", "TENANT_INACTIVE")]
	public async Task Token_Requests_Return_Specific_Safe_Tenant_Error(string code, string expectedError)
	{
		using var services = new ServiceCollection().AddOptions().BuildServiceProvider();
		var context = new DefaultHttpContext();
		context.RequestServices = services;
		context.Request.Path = "/connect/token";
		context.Response.Body = new MemoryStream();
		var options = new AbpAspNetCoreMultiTenancyOptions();
		InactiveTenantResponse.Configure(options);

		(await options.MultiTenancyMiddlewareErrorPageBuilder(context,
			new BusinessException(code, details: "private tenant details"))).ShouldBeTrue();

		context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
		context.Response.Body.Position = 0;
		using var payload = await JsonDocument.ParseAsync(context.Response.Body);
		payload.RootElement.GetProperty("errorCode").GetString().ShouldBe(expectedError);
		payload.RootElement.GetProperty("error").GetString().ShouldBe("invalid_grant");
		payload.RootElement.ToString().ShouldNotContain("private tenant details");
	}

	[Theory]
	[InlineData("/api/students", "Volo.AbpIo.MultiTenancy:010001")]
	[InlineData("/connect/token", "unrelated-error")]
	public async Task Unrelated_Errors_Keep_Existing_Fallback(string path, string code)
	{
		var called = false;
		var options = new AbpAspNetCoreMultiTenancyOptions
		{
			MultiTenancyMiddlewareErrorPageBuilder = (_, _) =>
			{
				called = true;
				return Task.FromResult(true);
			}
		};
		InactiveTenantResponse.Configure(options);
		var context = new DefaultHttpContext();
		context.Request.Path = path;
		(await options.MultiTenancyMiddlewareErrorPageBuilder(context, new BusinessException(code))).ShouldBeTrue();
		called.ShouldBeTrue();
	}
}
