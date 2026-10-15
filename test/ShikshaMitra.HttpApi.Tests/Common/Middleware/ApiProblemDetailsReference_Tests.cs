using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common.Middleware;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Common.Tests.Middleware;

public class ApiProblemDetailsReference_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Unexpected_failures_should_return_and_log_the_same_reference(bool useResultFilter)
	{
		var http = new DefaultHttpContext { TraceIdentifier = "role-delete-error-reference" };
		http.Request.Path = "/api/identity/roles/test";
		if (useResultFilter)
		{
			var logger = new CapturingLogger<ApiProblemDetailsResultFilter>();
			var context = new ResultExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()),
				new List<IFilterMetadata>(), new ObjectResult(null) { StatusCode = 500 }, new object());
			await new ApiProblemDetailsResultFilter(new DefaultResolver(), logger).OnResultExecutionAsync(context,
				() => Task.FromResult(new ResultExecutedContext(context, new List<IFilterMetadata>(), context.Result, new object())));
			var problem = ((ObjectResult)context.Result).Value.ShouldBeOfType<ProblemDetails>();
			problem.Status.ShouldBe(500);
			problem.Extensions["traceId"].ShouldBe(http.TraceIdentifier);
			logger.Messages.ShouldContain(message => message.Contains(http.TraceIdentifier));
		}
		else
		{
			http.Response.Body = new MemoryStream();
			var logger = new CapturingLogger<ApiProblemDetailsMiddleware>();
			await new ApiProblemDetailsMiddleware(_ => throw new Exception("Sensitive database failure"), logger, new DefaultResolver()).InvokeAsync(http);
			http.Response.StatusCode.ShouldBe(500);
			http.Response.Body.Position = 0;
			using var problem = await JsonDocument.ParseAsync(http.Response.Body);
			problem.RootElement.GetProperty("traceId").GetString().ShouldBe(http.TraceIdentifier);
			problem.RootElement.GetProperty("detail").GetString()!.ShouldNotContain("Sensitive");
			logger.Messages.ShouldContain(message => message.Contains(http.TraceIdentifier));
		}
	}

	private sealed class DefaultResolver : IApiProblemDetailsErrorDetailResolver
	{
		public string ResolveDetail(string? detail, string? code, string fallback, IReadOnlyDictionary<string, object?>? data = null) => fallback;
	}
	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public List<string> Messages { get; } = [];
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => true;
		public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Messages.Add(formatter(state, exception));
	}
}
