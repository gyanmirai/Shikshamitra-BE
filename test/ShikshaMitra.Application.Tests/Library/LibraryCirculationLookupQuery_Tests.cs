using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShikshaMitra.Library.IssueReturn;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCoreWorkflow_Tests
{
	[Fact]
	public async Task Copy_History_Should_Read_Only_Required_Person_Data_Without_Family_Bank_Or_Document_Graphs()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan: true));
		using var activity = new Activity("Library copy history query regression").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId);

		var result = await GetRequiredService<ILibraryCopyHistoryAppService>().GetAsync(context.Catalog.BookId, context.Catalog.CopyId);

		result.Items.Single().MemberId.ShouldBe(context.StudentId);
		var studentCommands = commands.Commands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
		studentCommands.ShouldNotBeEmpty("The regression must observe the real scoped history person query.");
		foreach (var sql in studentCommands)
		{
			sql.ShouldNotContain("StudentFamilyDetails");
			sql.ShouldNotContain("StudentBankDetails");
			sql.ShouldNotContain("AppStudentDocuments");
		}
	}

	[Fact]
	public async Task Circulation_Lookup_Should_Only_Read_Required_Borrower_Data_Without_The_Student_Record_Graph()
	{
		var context = await GetLibraryContextAsync();
		using var activity = new Activity("Library borrower query regression").SetIdFormat(ActivityIdFormat.W3C).Start();
		using var commands = new LibraryLookupSqlCapture(activity.TraceId);

		var borrower = await GetRequiredService<ILibraryCirculationMemberAppService>().ResolveAsync(context.ActiveStudentId.ToString());

		borrower.Id.ShouldBe(context.ActiveStudentId);
		var studentCommands = commands.Commands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
		studentCommands.ShouldNotBeEmpty("The regression must observe the actual scoped borrower query.");
		foreach (var sql in studentCommands)
		{
			sql.ShouldNotContain("StudentFamilyDetails");
			sql.ShouldNotContain("StudentBankDetails");
			sql.ShouldNotContain("AppStudentDocuments");
		}
	}
}

internal sealed class LibraryLookupSqlCapture : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
	private readonly ActivityTraceId traceId;
	private readonly Action<string>? beforeExecution;
	private readonly ConcurrentBag<IDisposable> subscriptions = [];
	private readonly IDisposable allListeners;
	public ConcurrentQueue<string> Commands { get; } = new();

	public LibraryLookupSqlCapture(ActivityTraceId traceId, Action<string>? beforeExecution = null)
	{
		this.traceId = traceId;
		this.beforeExecution = beforeExecution;
		allListeners = DiagnosticListener.AllListeners.Subscribe(this);
	}

	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name == "Microsoft.EntityFrameworkCore") subscriptions.Add(listener.Subscribe(this));
	}

	public void OnNext(KeyValuePair<string, object?> item)
	{
		if (Activity.Current?.TraceId == traceId && item.Key.EndsWith(".CommandExecuting", StringComparison.Ordinal) &&
			item.Value is CommandEventData data)
		{
			Commands.Enqueue(data.Command.CommandText);
			beforeExecution?.Invoke(data.Command.CommandText);
		}
	}

	public void OnCompleted() { }
	public void OnError(Exception error) { }

	public void Dispose()
	{
		allListeners.Dispose();
		foreach (var subscription in subscriptions) subscription.Dispose();
	}
}
