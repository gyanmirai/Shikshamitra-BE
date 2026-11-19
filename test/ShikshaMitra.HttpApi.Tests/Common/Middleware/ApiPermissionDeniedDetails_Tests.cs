using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.HR;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiPermissionDeniedDetails_Tests
{
	private const string Recovery =
		"Ask your administrator to review your permissions and assigned data access, then try again.";
	private const string GenericDetail = "You don't have permission to access this resource.";

	[Theory]
	[InlineData("PayrollPolicies", "GetListAsync", "Payroll Policies: View")]
	[InlineData("StudentAdmission", "CreateAsync", "Student Admission: Add")]
	[InlineData("FinanceAccounts", "DeleteAsync", "Finance Accounts: Delete")]
	[InlineData("HRPolicies", "PreviewImpactAsync", "HR Policies: Preview Impact")]
	public async Task Authorization_exception_should_describe_static_action_without_request_data(
		string controller, string action, string expected)
	{
		var context = CreateContext(controller, action);
		var middleware = CreateMiddleware(_ => throw new AbpAuthorizationException());

		await middleware.InvokeAsync(context);

		using var result = await ReadResultAsync(context);
		context.Response.StatusCode.ShouldBe(403);
		var detail = result.RootElement.GetProperty("detail").GetString()!;
		detail.ShouldBe($"Access denied for {expected}. {Recovery}");
		detail.ShouldNotContain("private-record");
		detail.ShouldNotContain("private-query");
		detail.ShouldNotContain("/api/");
		result.RootElement.GetProperty("traceId").GetString().ShouldBe(context.TraceIdentifier);
	}

	[Fact]
	public async Task Bare_403_should_use_endpoint_metadata_and_keep_status()
	{
		var context = CreateContext("LeavePolicies", "UpdateAsync");
		var middleware = CreateMiddleware(httpContext =>
		{
			httpContext.Response.StatusCode = 403;
			return Task.CompletedTask;
		});

		await middleware.InvokeAsync(context);

		using var result = await ReadResultAsync(context);
		result.RootElement.GetProperty("detail").GetString()
			.ShouldBe($"Access denied for Leave Policies: Update. {Recovery}");
		context.Response.StatusCode.ShouldBe(403);
	}

	[Fact]
	public async Task Technical_authorization_message_should_not_expose_permission_identifiers()
	{
		var context = CreateContext("StaffDirectory", "CreateAsync");
		var middleware = CreateMiddleware(_ => throw new AbpAuthorizationException(
			"Authorization failed! Given policy has not granted: Internal.Permission.Identifier",
			AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName, null));

		await middleware.InvokeAsync(context);

		using var result = await ReadResultAsync(context);
		result.RootElement.GetProperty("detail").GetString()
			.ShouldBe($"Access denied for Staff Directory: Add. {Recovery}");
		result.RootElement.GetProperty("errorCode").GetString()
			.ShouldBe(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName);
	}

	[Theory]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted, false)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted, true)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName, false)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName, true)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource, false)]
	[InlineData(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource, true)]
	[InlineData(AbpAuthorizationErrorCodes.GivenRequirementHasNotGrantedForGivenResource, false)]
	[InlineData(AbpAuthorizationErrorCodes.GivenRequirementHasNotGrantedForGivenResource, true)]
	[InlineData(AbpAuthorizationErrorCodes.GivenRequirementsHasNotGrantedForGivenResource, false)]
	[InlineData(AbpAuthorizationErrorCodes.GivenRequirementsHasNotGrantedForGivenResource, true)]
	public async Task Framework_authorization_codes_should_keep_403_and_code_while_explaining_the_action(string code, bool useProblemDetails)
	{
		var detail = code switch
		{
			AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted => "Authorization failed! Given policy has not granted.",
			AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName => "Authorization failed! Given policy has not granted: Internal.Permission.Identifier",
			AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource => "Authorization failed! Given policy has not granted for given resource: private-record",
			AbpAuthorizationErrorCodes.GivenRequirementHasNotGrantedForGivenResource => "Authorization failed! Given requirement has not granted for given resource: private-record",
			_ => "Authorization failed! Given requirements has not granted for given resource: private-record"
		};
		var problem = new ProblemDetails { Status = 403, Detail = detail };
		problem.Extensions["errorCode"] = code;
		object payload = useProblemDetails ? problem : new { error = new { code, message = detail } };

		var result = await FilterAsync(new ObjectResult(payload) { StatusCode = 403 }, "PayrollPolicies", "GetListAsync");

		result.Status.ShouldBe(403);
		result.Extensions["errorCode"].ShouldBe(code);
		result.Detail.ShouldBe($"Access denied for Payroll Policies: View. {Recovery}");
	}

	[Theory]
	[InlineData("You don\u2019t have permission to access this resource")]
	[InlineData("You don't have permission to access this resource")]
	[InlineData("  YOU DO NOT HAVE PERMISSION TO ACCESS THIS RESOURCE!  ")]
	public async Task Generic_punctuation_variants_should_receive_context(string detail)
	{
		var result = await FilterAsync(new ObjectResult(new { message = detail }) { StatusCode = 403 });

		result.Detail.ShouldBe($"Access denied for Employees: View. {Recovery}");
	}

	[Fact]
	public async Task Framework_code_should_not_replace_a_specific_scope_explanation()
	{
		const string detail = "Access denied: student is outside your user scope.";
		var problem = new ProblemDetails { Status = 403, Detail = detail };
		problem.Extensions["code"] = AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource;

		var result = await FilterAsync(new ObjectResult(problem) { StatusCode = 403 });

		result.Status.ShouldBe(403);
		result.Detail.ShouldBe(detail);
		result.Extensions["code"].ShouldBe(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedForGivenResource);
	}

	[Theory]
	[InlineData("forbid")]
	[InlineData("abp-envelope")]
	[InlineData("problem-details")]
	[InlineData("plain-text")]
	public async Task Result_filter_should_normalize_each_generic_403_shape(string shape)
	{
		IActionResult source = shape switch
		{
			"forbid" => new ForbidResult(),
			"abp-envelope" => new ObjectResult(new { error = new { code = AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName, message = GenericDetail } }) { StatusCode = 403 },
			"problem-details" => new ObjectResult(new ProblemDetails { Status = 403, Detail = GenericDetail }) { StatusCode = 403 },
			_ => new ContentResult { StatusCode = 403, Content = "  Forbidden  " }
		};

		var result = await FilterAsync(source, "LibraryBooks", "GetListAsync");

		result.Status.ShouldBe(403);
		result.Detail.ShouldBe($"Access denied for Library Books: View. {Recovery}");
		result.Extensions.ShouldContainKey("traceId");
		result.Extensions.ShouldNotContainKey("missingPermissions");
		if (shape == "abp-envelope")
		{
			result.Extensions["errorCode"].ShouldBe(AbpAuthorizationErrorCodes.GivenPolicyHasNotGrantedWithPolicyName);
		}
	}

	[Theory]
	[InlineData("Access denied: student is outside your user scope.")]
	[InlineData("You are not allowed to view this tenant.")]
	[InlineData("You can only review lesson plans of employees in your reporting hierarchy.")]
	public async Task Specific_scope_and_tenant_messages_should_survive_both_pipelines(string detail)
	{
		var context = CreateContext("LessonPlans", "GetAsync");
		var middleware = CreateMiddleware(_ => throw new AbpAuthorizationException(detail));

		await middleware.InvokeAsync(context);

		using var output = await ReadResultAsync(context);
		output.RootElement.GetProperty("detail").GetString().ShouldBe(detail);
		var result = await FilterAsync(new ObjectResult(new { message = detail }) { StatusCode = 403 });
		result.Detail.ShouldBe(detail);
	}

	[Fact]
	public async Task Business_403_code_data_and_detail_should_remain_unchanged()
	{
		const string detail = "Policy assignment access is required before adding this staff member.";
		var context = CreateContext("EmployeePersonalInfo", "CreateAsync");
		var middleware = CreateMiddleware(_ => throw new BusinessException(HRErrorCodes.EmployeePolicyAccessRequired, detail)
			.WithData("missingPermissions", new[] { "HR.Policies.Assign" }));

		await middleware.InvokeAsync(context);

		using var output = await ReadResultAsync(context);
		output.RootElement.GetProperty("detail").GetString().ShouldBe(detail);
		output.RootElement.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.EmployeePolicyAccessRequired);
		output.RootElement.GetProperty("data").GetProperty("missingPermissions")[0].GetString().ShouldBe("HR.Policies.Assign");
		var problem = new ProblemDetails { Status = 403, Detail = GenericDetail };
		problem.Extensions["errorCode"] = HRErrorCodes.EmployeePolicyAccessRequired;
		var result = await FilterAsync(new ObjectResult(problem) { StatusCode = 403 });
		result.Detail.ShouldBe(GenericDetail);
	}

	[Theory]
	[InlineData(null, null)]
	[InlineData("/api/private-record", "GetAsync")]
	public async Task Missing_or_unsafe_metadata_should_use_guidance_without_deriving_context_from_url(string? controller, string? action)
	{
		var context = CreateContext(controller, action);
		await CreateMiddleware(_ => throw new AbpAuthorizationException(GenericDetail)).InvokeAsync(context);

		using var output = await ReadResultAsync(context);
		output.RootElement.GetProperty("detail").GetString().ShouldBe($"Access denied for this action. {Recovery}");
	}

	[Theory]
	[InlineData(401, "Authentication is required to access this resource.")]
	[InlineData(404, "The requested resource was not found.")]
	public async Task Other_status_messages_should_remain_unchanged(int status, string detail)
	{
		var result = await FilterAsync(new ObjectResult(new ProblemDetails { Status = status, Detail = detail }) { StatusCode = status });

		result.Status.ShouldBe(status);
		result.Detail.ShouldBe(detail);
	}

	private static ApiProblemDetailsMiddleware CreateMiddleware(RequestDelegate next) =>
		new(next, NullLogger<ApiProblemDetailsMiddleware>.Instance, new PassThroughErrorDetailResolver());

	private static DefaultHttpContext CreateContext(string? controller = null, string? action = null)
	{
		var context = new DefaultHttpContext();
		context.Request.Path = "/api/private-record";
		context.Request.QueryString = new QueryString("?name=private-query");
		context.Response.Body = new MemoryStream();
		if (controller != null && action != null)
		{
			context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
				new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller, ActionName = action }),
				"Static test endpoint"));
		}
		return context;
	}

	private static async Task<JsonDocument> ReadResultAsync(HttpContext context)
	{
		context.Response.Body.Position = 0;
		return await JsonDocument.ParseAsync(context.Response.Body);
	}

	private static async Task<ProblemDetails> FilterAsync(IActionResult source, string controller = "Employees", string action = "GetAsync")
	{
		var context = new ResultExecutingContext(
			new ActionContext(CreateContext(), new RouteData(), new ControllerActionDescriptor { ControllerName = controller, ActionName = action }),
			new List<IFilterMetadata>(), source, new object());
		await new ApiProblemDetailsResultFilter(new PassThroughErrorDetailResolver()).OnResultExecutionAsync(context,
			() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
		return ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
	}

	private sealed class PassThroughErrorDetailResolver : IApiProblemDetailsErrorDetailResolver
	{
		public string ResolveDetail(string? detail, string? errorCode, string defaultDetail,
			IReadOnlyDictionary<string, object?>? errorData = null) => detail ?? defaultDetail;
	}
}
