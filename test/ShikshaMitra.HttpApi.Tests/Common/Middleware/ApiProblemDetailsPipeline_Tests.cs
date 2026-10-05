using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsPipeline_Tests
{
	private const string GenericInternalDetail = "An internal error occurred during your request!";

	[Theory]
	[InlineData(AdmissionErrorCodes.PublicInquiryOfferingRequired, "activate an offering")]
	[InlineData(AdmissionErrorCodes.PublicInquiryCounsellorUnavailable, "counsellor pool")]
	[InlineData(AdmissionErrorCodes.PublicInquiryFallbackUnavailable, "fallback manager")]
	[InlineData(AdmissionErrorCodes.PublicInquiryExpiryInvalid, "Choose a future link expiry")]
	public async Task Public_inquiry_rejection_should_include_localized_reason_and_next_action(string code, string nextAction)
	{
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		var resourceName = assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json"));
		using var resource = assembly.GetManifestResourceStream(resourceName)!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = template });
		var filter = new ApiProblemDetailsResultFilter(resolver);
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/student/enrollment/admissions/public-inquiry-links";
		var source = new ProblemDetails { Status = 400, Detail = "One or more business rules were violated." };
		source.Extensions["errorCode"] = code;
		source.Extensions["data"] = new Dictionary<string, object?>
		{
			["PeriodName"] = "RTE", ["SetupName"] = "General Admission"
		};
		var context = new ResultExecutingContext(
			new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
			new List<IFilterMetadata>(), new ObjectResult(source) { StatusCode = 400 }, new object());
		await filter.OnResultExecutionAsync(context, () => Task.FromResult(new ResultExecutedContext(
			context, new List<IFilterMetadata>(), context.Result, new object())));
		var output = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
		output.Status.ShouldBe(400);
		output.Extensions["errorCode"].ShouldBe(code);
		if (code != AdmissionErrorCodes.PublicInquiryExpiryInvalid)
			output.Detail.ShouldContain("RTE");
		output.Detail.ShouldContain(nextAction);
		output.Detail.ShouldNotContain("{PeriodName}");
		output.Detail.ShouldNotContain("{SetupName}");
	}

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
	public async Task Middleware_Should_Explain_When_The_Only_Default_Attendance_Policy_Is_Removed()
	{
		const string expected =
			"This attendance policy is the only active default for the selected academic year. " +
			"Set another active attendance policy as default before clearing this policy's default status or archiving it.";
		var resolver = CreateResolver(new Dictionary<string, string>());
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(HRErrorCodes.DefaultAttendancePolicyRequired),
			NullLogger<ApiProblemDetailsMiddleware>.Instance,
			resolver);
		var context = new DefaultHttpContext();
		context.Request.Path = "/api/hr/attendance-policies/policy-id";
		context.Response.Body = new MemoryStream();

		await middleware.InvokeAsync(context);

		context.Response.Body.Position = 0;
		using var document = await JsonDocument.ParseAsync(context.Response.Body);
		document.RootElement.GetProperty("errorCode").GetString()
			.ShouldBe(HRErrorCodes.DefaultAttendancePolicyRequired);
		document.RootElement.GetProperty("detail").GetString().ShouldBe(expected);
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

	[Fact]
	public async Task Middleware_Should_Return_Readable_Academic_Detail_When_Abp_Uses_Generic_Business_Message()
	{
		const string academicCode = "ShikshaMitra.Exam:InvalidPassingMarks";
		var resolver = CreateResolver(new Dictionary<string, string>());
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(academicCode),
			NullLogger<ApiProblemDetailsMiddleware>.Instance,
			resolver);

		var context = new DefaultHttpContext();
		context.Request.Path = "/api/academic/exams";
		context.Response.Body = new MemoryStream();

		await middleware.InvokeAsync(context);

		context.Response.Body.Position = 0;
		using var document = await JsonDocument.ParseAsync(context.Response.Body);
		document.RootElement.GetProperty("detail").GetString().ShouldBe("Invalid passing marks.");
	}

	[Fact]
	public async Task Middleware_Should_Use_Known_Hr_Code_Fallback_When_Localization_Is_Missing()
	{
		var resolver = CreateResolver(new Dictionary<string, string>());
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(
				HRErrorCodes.PayrollAlreadyLocked,
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
		document.RootElement.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.PayrollAlreadyLocked);
		var detail = document.RootElement.GetProperty("detail").GetString();
		detail.ShouldNotBeNullOrWhiteSpace();
		detail.ShouldContain("Payroll");
		detail.ShouldContain("Locked");
		detail.ShouldNotBe(ApiProblemDetailsMapper.GetDefaultDetail(StatusCodes.Status400BadRequest));
	}

	[Fact]
	public async Task Middleware_Should_Fallback_To_Specific_Detail_For_LeaveInvalidMonth_When_Localization_Missing()
	{
		var resolver = CreateResolver(new Dictionary<string, string>());
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(
					HRErrorCodes.LeaveInvalidMonthForAcademicYear,
					GenericInternalDetail)
				.WithData("requestedMonth", "2026-05")
				.WithData("academicYearName", "2026-2027")
				.WithData("academicYearStart", "2026-06-01")
				.WithData("academicYearEnd", "2027-05-31"),
			NullLogger<ApiProblemDetailsMiddleware>.Instance,
			resolver);

		var context = new DefaultHttpContext();
		context.Request.Path = "/api/test";
		context.Response.Body = new MemoryStream();

		await middleware.InvokeAsync(context);

		context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
		context.Response.Body.Position = 0;
		using var document = await JsonDocument.ParseAsync(context.Response.Body);
		document.RootElement.GetProperty("errorCode").GetString().ShouldBe(HRErrorCodes.LeaveInvalidMonthForAcademicYear);
		var detail = document.RootElement.GetProperty("detail").GetString();
		detail.ShouldNotBeNullOrWhiteSpace();
		detail.ShouldContain("2026-05");
		detail.ShouldContain("2026-06-01");
		detail.ShouldContain("2027-05-31");
		detail.ShouldNotBe(ApiProblemDetailsMapper.GetDefaultDetail(StatusCodes.Status400BadRequest));
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
