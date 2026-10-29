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
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsPipeline_Tests
{
	[Theory]
	[InlineData(false, "Finance:Concession:010033", "Approved")]
	[InlineData(true, "Finance:Concession:010033", "Approved")]
	[InlineData(false, "Finance:Concession:010034", "approved benefit")]
	[InlineData(true, "Finance:Concession:010034", "approved benefit")]
	[InlineData(false, "Finance:Concession:010010", "academic year")]
	[InlineData(true, "Finance:Concession:010010", "academic year")]
	[InlineData(false, "Finance:Concession:020006", "selected fees")]
	[InlineData(true, "Finance:Concession:020006", "selected fees")]
	[InlineData(false, "Finance:ConcessionPolicy:010012", "policy version")]
	[InlineData(true, "Finance:ConcessionPolicy:010012", "policy version")]
	[InlineData(false, "Finance:ConcessionPolicy:010010", "active")]
	[InlineData(true, "Finance:ConcessionPolicy:010010", "active")]
	[InlineData(false, "Finance:Concession:010017", "documents")]
	[InlineData(true, "Finance:Concession:010017", "documents")]
	[InlineData(false, "Finance:Concession:010023", "repair")]
	[InlineData(true, "Finance:Concession:010023", "repair")]
	[InlineData(false, "Finance:Concession:010025", "configuration")]
	[InlineData(true, "Finance:Concession:010025", "configuration")]
	[InlineData(false, "Finance:Concession:020005", "assignment")]
	[InlineData(true, "Finance:Concession:020005", "assignment")]
	public async Task Concession_rejections_explain_the_rule_and_preserve_support_reference(bool useResultFilter, string code, string expected)
	{
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		using var resource = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json")))!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = template });
		var http = new DefaultHttpContext { TraceIdentifier = "concession-update-reference" };
		http.Request.Path = "/api/concession-request/request-id";
		var data = new Dictionary<string, object?> { ["Status"] = "Approved" };
		if (useResultFilter)
		{
			var payload = new { Error = new { Code = code, Message = "One or more business rules were violated.", Data = data } };
			var context = new ResultExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
				new List<IFilterMetadata>(), new ObjectResult(payload) { StatusCode = 400 }, new object());
			await new ApiProblemDetailsResultFilter(resolver).OnResultExecutionAsync(context,
				() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
			var result = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
			result.Detail!.ShouldContain(expected);
			result.Detail!.ShouldNotContain("{Status}");
			result.Detail!.ShouldNotContain("One or more business rules were violated");
			result.Extensions["errorCode"].ShouldBe(code);
			result.Extensions["traceId"].ShouldBe(http.TraceIdentifier);
		}
		else
		{
			http.Response.Body = new MemoryStream();
			await new ApiProblemDetailsMiddleware(_ => throw new BusinessException(code).WithData("Status", "Approved"),
				NullLogger<ApiProblemDetailsMiddleware>.Instance, resolver).InvokeAsync(http);
			http.Response.Body.Position = 0;
			using var result = await JsonDocument.ParseAsync(http.Response.Body);
			result.RootElement.GetProperty("detail").GetString()!.ShouldContain(expected);
			result.RootElement.GetProperty("errorCode").GetString().ShouldBe(code);
			result.RootElement.GetProperty("traceId").GetString().ShouldBe(http.TraceIdentifier);
		}
	}

	[Fact]
	public void Framework_business_message_is_replaced_with_specific_rule_even_when_status_default_differs()
	{
		const string code = "Finance:Concession:030003";
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = "This concession request was changed elsewhere. Refresh the request and try again." });
		resolver.ResolveDetail("One or more business rules were violated.", code, ApiProblemDetailsMapper.GetDefaultDetail(409))
			.ShouldBe("This concession request was changed elsewhere. Refresh the request and try again.");
	}

	[Theory]
	[InlineData(false, "010005", "different name")]
	[InlineData(true, "010005", "different name")]
	[InlineData(false, "010014", "Review")]
	[InlineData(true, "010014", "Review")]
	[InlineData(false, "010026", "another Active")]
	[InlineData(true, "010026", "another Active")]
	[InlineData(false, "010027", "reactivate")]
	[InlineData(true, "010027", "reactivate")]
	public async Task Proration_clone_errors_preserve_specific_recovery_in_problem_json(bool useResultFilter, string rule, string recovery)
	{
		var code = "Finance:ProrationPolicy:" + rule;
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		using var resource = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json")))!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = template });
		var data = new Dictionary<string, object?>
		{
			["Name"] = "Admission Copy", ["Location"] = "Archived", ["FeeHeadNames"] = "Transport Fee"
		};
		var http = new DefaultHttpContext();
		http.Request.Path = "/api/finance/proration-policies/source/clone";
		void AssertRecovery(string? detail)
		{
			detail.ShouldContain(recovery);
			if (rule == "010005") detail.ShouldContain("Admission Copy");
			if (rule == "010027") detail.ShouldContain("Transport Fee");
			detail.ShouldNotContain("{FeeHeadNames}");
			detail.ShouldNotContain("{Name}");
		}
		if (useResultFilter)
		{
			var payload = new { Error = new { Code = code, Message = ApiProblemDetailsMapper.GetDefaultDetail(400), Data = data } };
			var context = new ResultExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
				new List<IFilterMetadata>(), new ObjectResult(payload) { StatusCode = 400 }, new object());
			await new ApiProblemDetailsResultFilter(resolver).OnResultExecutionAsync(context,
				() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
			var result = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
			AssertRecovery(result.Detail);
			result.Extensions["errorCode"].ShouldBe(code);
		}
		else
		{
			http.Response.Body = new MemoryStream();
			var exception = new BusinessException(code);
			foreach (var pair in data) exception.WithData(pair.Key, pair.Value);
			await new ApiProblemDetailsMiddleware(_ => throw exception,
				NullLogger<ApiProblemDetailsMiddleware>.Instance, resolver).InvokeAsync(http);
			http.Response.StatusCode.ShouldBe(400);
			http.Response.ContentType.ShouldBe("application/problem+json");
			http.Response.Body.Position = 0;
			using var result = await JsonDocument.ParseAsync(http.Response.Body);
			AssertRecovery(result.RootElement.GetProperty("detail").GetString());
			result.RootElement.GetProperty("errorCode").GetString().ShouldBe(code);
		}
	}

	[Theory]
	[InlineData("UserScope:UsersNotFound", "linked account")]
	[InlineData("UserScope:InvalidReferences", "Reload the academic hierarchy")]
	[InlineData("UserScope:BulkAssignFailed", "reference-123")]
	[InlineData("UserScope:InvalidInput", "Select an academic year")]
	public async Task UserScope_errors_should_include_the_rule_or_recovery(string code, string expectedDetail)
	{
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		using var resource = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json")))!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = template });
		var detail = resolver.ResolveDetail(null, code, ApiProblemDetailsMapper.GetDefaultDetail(400),
			new Dictionary<string, object?>
			{
				["Reason"] = "Select an academic year before saving academic permissions.",
				["CorrelationId"] = "reference-123"
			});
		detail.ShouldContain(expectedDetail);
		detail.ShouldNotContain("One or more business rules were violated");
		detail.ShouldNotContain("{Reason}");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Curriculum_teacher_rule_should_preserve_named_detail_and_field(bool useResultFilter)
	{
		const string code = "Academic:Curriculum:020005";
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		using var resource = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json")))!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = template });
		var data = new Dictionary<string, object?>
		{
			["TeacherName"] = "Dharmik Jinjala", ["ClassName"] = "Nursery",
			["SubjectId"] = "subject-id", ["Field"] = "teacherIds"
		};
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/academics/curriculum/subject-assignments";
		if (useResultFilter)
		{
			var source = new ProblemDetails { Status = 400, Detail = ApiProblemDetailsMapper.GetDefaultDetail(400) };
			source.Extensions["errorCode"] = code;
			source.Extensions["data"] = data;
			var context = new ResultExecutingContext(
				new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
				new List<IFilterMetadata>(), new ObjectResult(source) { StatusCode = 400 }, new object());
			await new ApiProblemDetailsResultFilter(resolver).OnResultExecutionAsync(context,
				() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
			var output = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
			output.Detail.ShouldStartWith("Dharmik Jinjala is not assigned to Nursery in Academic Permission.");
			output.Extensions["errorCode"].ShouldBe(code);
			output.Extensions["data"].ShouldBe(data);
		}
		else
		{
			httpContext.Response.Body = new MemoryStream();
			var exception = new BusinessException(code);
			foreach (var pair in data) exception.WithData(pair.Key, pair.Value);
			await new ApiProblemDetailsMiddleware(_ => throw exception,
				NullLogger<ApiProblemDetailsMiddleware>.Instance, resolver).InvokeAsync(httpContext);
			httpContext.Response.StatusCode.ShouldBe(400);
			httpContext.Response.Body.Position = 0;
			using var output = await JsonDocument.ParseAsync(httpContext.Response.Body);
			output.RootElement.GetProperty("errorCode").GetString().ShouldBe(code);
			output.RootElement.GetProperty("detail").GetString().ShouldStartWith("Dharmik Jinjala is not assigned to Nursery in Academic Permission.");
			output.RootElement.GetProperty("data").GetProperty("Field").GetString().ShouldBe("teacherIds");
		}
	}

	private const string GenericInternalDetail = "An internal error occurred during your request!";

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Assessment_scheduling_rejection_should_preserve_reason_after_error_normalization(bool useResultFilter)
	{
		const string reason = "Room is reserved for 'Entrance exam' (Exam) from 14:15 to 15:05.";
		var exception = AdmissionAssessmentSchedulingService.Invalid(reason);
		exception.Code.ShouldBe(AdmissionAssessmentErrorCodes.InvalidState);
		exception.Data["Reason"].ShouldBe(reason);
		var resolver = CreateResolver(new Dictionary<string, string>
		{
			[exception.Code!] = "This assessment action is not available in the current state. Refresh the record and check its requirements."
		});
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/student/enrollment/admissions/assessments/sessions/preview";
		if (useResultFilter)
		{
			// ABP localizes the message by code; the scheduling reason must survive in data.
			var source = new { Error = new { Code = exception.Code, Message = "One or more business rules were violated.", Data = exception.Data } };
			var context = new ResultExecutingContext(
				new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
				new List<IFilterMetadata>(), new ObjectResult(source) { StatusCode = 400 }, new object());
			await new ApiProblemDetailsResultFilter(resolver).OnResultExecutionAsync(context,
				() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
			var output = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
			output.Status.ShouldBe(400);
			output.Extensions["errorCode"].ShouldBe(exception.Code);
			var data = output.Extensions["data"].ShouldBeOfType<Dictionary<string, object?>>();
			data["Reason"].ShouldBe(reason);
		}
		else
		{
			httpContext.Response.Body = new MemoryStream();
			await new ApiProblemDetailsMiddleware(_ => throw exception,
				NullLogger<ApiProblemDetailsMiddleware>.Instance, resolver).InvokeAsync(httpContext);
			httpContext.Response.StatusCode.ShouldBe(400);
			httpContext.Response.Body.Position = 0;
			using var output = await JsonDocument.ParseAsync(httpContext.Response.Body);
			output.RootElement.GetProperty("errorCode").GetString().ShouldBe(exception.Code);
			output.RootElement.GetProperty("data").GetProperty("Reason").GetString().ShouldBe(reason);
		}
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Roll_preview_duplicate_should_include_localized_reason_and_recovery(bool useResultFilter)
	{
		var code = StudentOnboardingErrorCodes.RollNumberDuplicate;
		var assembly = typeof(ShikshaMitraDomainSharedModule).Assembly;
		var resourceName = assembly.GetManifestResourceNames()
			.Single(name => name.EndsWith("Localization.ShikshaMitra.en.json"));
		using var resource = assembly.GetManifestResourceStream(resourceName)!;
		using var messages = await JsonDocument.ParseAsync(resource);
		var template = messages.RootElement.GetProperty("Texts").GetProperty(code).GetString()!;
		var resolver = CreateResolver(new Dictionary<string, string> { [code] = template });
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Path = "/api/student/onboarding/test-case/roll-preview";
		if (useResultFilter)
		{
			var source = new ProblemDetails { Status = 400, Detail = ApiProblemDetailsMapper.GetDefaultDetail(400) };
			source.Extensions["errorCode"] = code;
			var context = new ResultExecutingContext(
				new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
				new List<IFilterMetadata>(), new ObjectResult(source) { StatusCode = 400 }, new object());
			await new ApiProblemDetailsResultFilter(resolver).OnResultExecutionAsync(context,
				() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
			var output = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
			output.Status.ShouldBe(400);
			output.Extensions["errorCode"].ShouldBe(code);
			output.Detail.ShouldBe(template);
		}
		else
		{
			httpContext.Response.Body = new MemoryStream();
			var middleware = new ApiProblemDetailsMiddleware(_ => throw new BusinessException(code),
				NullLogger<ApiProblemDetailsMiddleware>.Instance, resolver);
			await middleware.InvokeAsync(httpContext);
			httpContext.Response.StatusCode.ShouldBe(400);
			httpContext.Response.Body.Position = 0;
			using var output = await JsonDocument.ParseAsync(httpContext.Response.Body);
			output.RootElement.GetProperty("errorCode").GetString().ShouldBe(code);
			output.RootElement.GetProperty("detail").GetString().ShouldBe(template);
		}
		template.ShouldContain("already assigned or reserved");
		template.ShouldContain("Enter an unused number");
		template.ShouldContain("Automatic if available");
	}

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

	[Theory]
	[InlineData(GenericInternalDetail)]
	[InlineData("You don't have permission to access this resource.")]
	public async Task Middleware_Should_Resolve_Localized_Detail_From_ErrorCode_When_Detail_Is_Generic(string detail)
	{
		var resolver = CreateResolver(new Dictionary<string, string>
		{
			[HRErrorCodes.LeaveEmployeeNotFound] = "Current user is not linked to an employee profile, so this leave action is not allowed."
		});
		var middleware = new ApiProblemDetailsMiddleware(
			_ => throw new BusinessException(
				HRErrorCodes.LeaveEmployeeNotFound,
				detail),
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
		document.RootElement.GetProperty("detail").GetString().ShouldBe("Invalid Passing Marks.");
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
