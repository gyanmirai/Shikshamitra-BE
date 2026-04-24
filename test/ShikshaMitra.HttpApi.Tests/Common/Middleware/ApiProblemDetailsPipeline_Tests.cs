using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using ShikshaMitra.Common.Middleware;
using ShikshaMitra.HR;
using ShikshaMitra.Localization;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsPipeline_Tests
{
	private const string GenericInternalDetail = "An internal error occurred during your request!";

	[Fact]
	public async Task Middleware_Should_Resolve_Localized_Detail_From_ErrorCode_When_Detail_Is_Generic()
	{
		var resolver = CreateResolver(new Dictionary<string, string>
		{
			[HRErrorCodes.LeaveEmployeeNotFound] = "Current user is not linked to an employee profile, so this leave action is not allowed."
		});
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(
				HRErrorCodes.LeaveEmployeeNotFound,
				GenericInternalDetail),
			NullLogger<ApiProblemDetailsMiddleware>.Instance,
			resolver);

		var context = new DefaultHttpContext();
		context.Request.Path = "/api/test";
		context.Response.Body = new MemoryStream();

		await middleware.InvokeAsync(context);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
		context.Response.Body.Position = 0;
		using var document = await JsonDocument.ParseAsync(context.Response.Body);
		document.RootElement.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.LeaveEmployeeNotFound);
		document.RootElement.GetProperty("detail").GetString()
			.ShouldBe("Current user is not linked to an employee profile, so this leave action is not allowed.");
	}

	[Fact]
	public async Task ResultFilter_Should_Resolve_Localized_Detail_From_ErrorCode_When_Detail_Is_Generic()
	{
		var resolver = CreateResolver(new Dictionary<string, string>
		{
			[HRErrorCodes.LeaveEmployeeNotFound] = "Current user is not linked to an employee profile, so this leave action is not allowed."
		});
		var filter = new ApiProblemDetailsResultFilter(resolver);
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/test";
		var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
		var sourceProblem = new ProblemDetails
		{
			Status = StatusCodes.Status500InternalServerError,
			Detail = GenericInternalDetail
		};
		sourceProblem.Extensions["errorCode"] = HRErrorCodes.LeaveEmployeeNotFound;
		var executingContext = new ResultExecutingContext(
			actionContext,
			new List<IFilterMetadata>(),
			new ObjectResult(sourceProblem)
			{
				StatusCode = StatusCodes.Status500InternalServerError
			},
			controller: new object());

		await filter.OnResultExecutionAsync(
			executingContext,
			() => Task.FromResult(new ResultExecutedContext(
				actionContext,
				new List<IFilterMetadata>(),
				executingContext.Result,
				controller: new object())));

		var output = (executingContext.Result as ObjectResult)!.Value.ShouldBeOfType<ProblemDetails>();
		output.Status.ShouldBe(StatusCodes.Status403Forbidden);
		output.Extensions["errorCode"]!.ToString().ShouldBe(HRErrorCodes.LeaveEmployeeNotFound);
		output.Detail.ShouldBe("Current user is not linked to an employee profile, so this leave action is not allowed.");
	}

	[Fact]
	public async Task ResultFilter_Should_Resolve_Localized_Detail_When_Detail_Is_Status_Default()
	{
		var resolver = CreateResolver(new Dictionary<string, string>
		{
			[HRErrorCodes.LeaveInsufficientBalance] = "Insufficient leave balance for this request."
		});
		var filter = new ApiProblemDetailsResultFilter(resolver);
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/test";
		var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
		var sourceProblem = new ProblemDetails
		{
			Status = StatusCodes.Status400BadRequest,
			Detail = ApiProblemDetailsMapper.GetDefaultDetail(StatusCodes.Status400BadRequest)
		};
		sourceProblem.Extensions["errorCode"] = HRErrorCodes.LeaveInsufficientBalance;
		var executingContext = new ResultExecutingContext(
			actionContext,
			new List<IFilterMetadata>(),
			new ObjectResult(sourceProblem)
			{
				StatusCode = StatusCodes.Status400BadRequest
			},
			controller: new object());

		await filter.OnResultExecutionAsync(
			executingContext,
			() => Task.FromResult(new ResultExecutedContext(
				actionContext,
				new List<IFilterMetadata>(),
				executingContext.Result,
				controller: new object())));

		var output = (executingContext.Result as ObjectResult)!.Value.ShouldBeOfType<ProblemDetails>();
		output.Detail.ShouldBe("Insufficient leave balance for this request.");
		output.Extensions["errorCode"]!.ToString().ShouldBe(HRErrorCodes.LeaveInsufficientBalance);
	}

	[Fact]
	public async Task ResultFilter_Should_Resolve_Localized_Detail_When_Status_Default_Detail_Has_Case_Or_Whitespace_Drift()
	{
		var resolver = CreateResolver(new Dictionary<string, string>
		{
			[HRErrorCodes.LeaveInsufficientBalance] = "Insufficient leave balance for this request."
		});
		var filter = new ApiProblemDetailsResultFilter(resolver);
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/test";
		var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
		var sourceProblem = new ProblemDetails
		{
			Status = StatusCodes.Status400BadRequest,
			Detail = "  one or more business rules were violated.  "
		};
		sourceProblem.Extensions["errorCode"] = HRErrorCodes.LeaveInsufficientBalance;
		var executingContext = new ResultExecutingContext(
			actionContext,
			new List<IFilterMetadata>(),
			new ObjectResult(sourceProblem)
			{
				StatusCode = StatusCodes.Status400BadRequest
			},
			controller: new object());

		await filter.OnResultExecutionAsync(
			executingContext,
			() => Task.FromResult(new ResultExecutedContext(
				actionContext,
				new List<IFilterMetadata>(),
				executingContext.Result,
				controller: new object())));

		var output = (executingContext.Result as ObjectResult)!.Value.ShouldBeOfType<ProblemDetails>();
		output.Detail.ShouldBe("Insufficient leave balance for this request.");
		output.Extensions["errorCode"]!.ToString().ShouldBe(HRErrorCodes.LeaveInsufficientBalance);
	}

	[Fact]
	public async Task Middleware_Should_Fallback_To_Default_Detail_When_ErrorCode_Has_No_Localization()
	{
		const string unmappedCode = "HR:Leave:029999";
		var resolver = CreateResolver(new Dictionary<string, string>());
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(
				unmappedCode,
				GenericInternalDetail),
			NullLogger<ApiProblemDetailsMiddleware>.Instance,
			resolver);

		var context = new DefaultHttpContext();
		context.Request.Path = "/api/test";
		context.Response.Body = new MemoryStream();

		await middleware.InvokeAsync(context);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
		context.Response.Body.Position = 0;
		using var document = await JsonDocument.ParseAsync(context.Response.Body);
		document.RootElement.GetProperty("errorCode").GetString().ShouldBe(unmappedCode);
		document.RootElement.GetProperty("detail").GetString()
			.ShouldBe(ApiProblemDetailsMapper.GetDefaultDetail(StatusCodes.Status400BadRequest));
	}

	private static ApiProblemDetailsErrorDetailResolver CreateResolver(IDictionary<string, string> values)
	{
		return new ApiProblemDetailsErrorDetailResolver(new FakeStringLocalizer(values));
	}

	private sealed class FakeStringLocalizer : IStringLocalizer<ShikshaMitraResource>
	{
		private readonly IDictionary<string, string> _values;

		public FakeStringLocalizer(IDictionary<string, string> values)
		{
			_values = values;
		}

		public LocalizedString this[string name]
		{
			get
			{
				if (_values.TryGetValue(name, out var value))
				{
					return new LocalizedString(name, value, resourceNotFound: false);
				}

				return new LocalizedString(name, name, resourceNotFound: true);
			}
		}

		public LocalizedString this[string name, params object[] arguments] => this[name];

		public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
		{
			foreach (var pair in _values)
			{
				yield return new LocalizedString(pair.Key, pair.Value, resourceNotFound: false);
			}
		}

		public IStringLocalizer WithCulture(CultureInfo culture)
		{
			return this;
		}
	}
}
