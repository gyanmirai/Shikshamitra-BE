using System;
using System.Data.Common;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Receipts;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

public sealed class FeeAssignmentPostgresFactAttribute : FactAttribute
{
	public FeeAssignmentPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(FeeAssignmentPostgresFactory.ConnectionVariable)))
			Skip = "Requires a new empty private high-port loopback assignment_correction_test_* PostgreSQL database.";
	}
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FeeAssignmentPostgresCollection : ICollectionFixture<FeeAssignmentPostgresFactory>
{
	public const string Name = "PostgreSQL fee assignment correction";
}

/// <summary>Real HTTP/app-service transactions; only database fault/barrier interception is test-owned.</summary>
[Collection(FeeAssignmentPostgresCollection.Name)]
[Trait("Module", "Finance")]
[Trait("Tier", "Critical")]
public sealed class StudentFeeAssignmentPostgres_Correction_Tests(FeeAssignmentPostgresFactory factory)
{
	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private const string Route = "/api/student-fee-structure";

	private async Task<(AssignStudentFeeStructureDto Input, AssignmentCommitResultDto Assigned)> PrepareAsync()
	{
		factory.Probe.Reset();
		var scenario = new StudentFeeAssignmentApi_Tests(factory);
		var fixture = await InvokeAsync<(AssignStudentFeeStructureDto Input, Guid[] Heads)>(scenario, "CreateFixtureAsync", true, true, false, false);
		using (var services = factory.Services.CreateScope())
		{
			var sequenceRepository = services.ServiceProvider.GetRequiredService<IReceiptNumberSequenceRepository>();
			ProxyUtil.GetUnproxiedType(sequenceRepository).ShouldBe(typeof(EfCoreReceiptNumberSequenceRepository),
				$"PostgreSQL collection must use the production persistent receipt sequence repository; resolved {sequenceRepository.GetType().FullName}.");
		}
		fixture.Input.SelectedOptionalFeeHeadIds = [fixture.Heads[1], fixture.Heads[2]];
		var assigned = await InvokeAsync<AssignmentCommitResultDto>(scenario, "PreviewAndAssignAsync", fixture.Input);
		await InvokeAsync<AssignStudentFeeStructureDto>(scenario, "BeginCorrectionAsync", fixture.Input, "PostgreSQL correction proof");
		fixture.Input.SelectedOptionalFeeHeadIds = [];
		using var client = factory.CreateClient();
		var previewResponse = await client.PostAsJsonAsync(Route + "/preview", fixture.Input, Json);
		previewResponse.EnsureSuccessStatusCode();
		fixture.Input.PreviewToken = (await previewResponse.Content.ReadFromJsonAsync<StudentFeeAssignmentPreviewDto>(Json))!.PreviewToken;
		return (fixture.Input, assigned);
	}

	[FeeAssignmentPostgresFact]
	public async Task Correction_Insert_Failure_Rolls_Back_Archived_Dues_Assignment_And_History()
	{
		var (input, assigned) = await PrepareAsync();
		var before = await EvidenceAsync(input, assigned.AssignmentId);
		factory.Probe.Arm(AssignmentProbeMode.FailReplacementInsert, assigned.Assignment.Lines[0].DueFeeId!.Value);
		using var client = factory.CreateClient();
		var response = await client.PostAsJsonAsync(Route + "/assign", input, Json);
		response.IsSuccessStatusCode.ShouldBeFalse();
		factory.Probe.ArchiveExecuted.ShouldBeTrue("Failure must happen after the old-dues flush, not at input validation.");
		factory.Probe.FailureInjected.ShouldBeTrue();
		var after = await EvidenceAsync(input, assigned.AssignmentId);
		after.ShouldBe(before, "PostgreSQL must roll back the prior schedule, canonical snapshot, fingerprint and history together.");
	}

	[FeeAssignmentPostgresFact]
	public async Task Collection_Committed_After_Correction_Read_Prevents_Stale_Archival()
	{
		var (input, assigned) = await PrepareAsync();
		using var before = JsonDocument.Parse(await EvidenceAsync(input, assigned.AssignmentId));
		factory.Probe.Arm(AssignmentProbeMode.PauseArchival, assigned.Assignment.Lines[0].DueFeeId!.Value);
		using var correctionClient = factory.CreateClient();
		correctionClient.Timeout = TimeSpan.FromMinutes(3);
		var correction = correctionClient.PostAsJsonAsync(Route + "/assign", input, Json);
		try
		{
			await factory.Probe.Paused.Task.WaitAsync(TimeSpan.FromSeconds(90));
			using var collectionClient = factory.CreateClient();
			var payment = BuildPayment(input, assigned);
			var collected = await collectionClient.PostAsJsonAsync("/api/fee-collections", payment, Json);
			collected.IsSuccessStatusCode.ShouldBeTrue(await collected.Content.ReadAsStringAsync());
		}
		finally { factory.Probe.Release.TrySetResult(); }
		(await correction).IsSuccessStatusCode.ShouldBeFalse();
		using var evidence = JsonDocument.Parse(await EvidenceAsync(input, assigned.AssignmentId));
		evidence.RootElement.GetProperty("paid").GetDecimal().ShouldBe(1000m);
		evidence.RootElement.GetProperty("activeTotal").GetDecimal().ShouldBe(20000m);
		evidence.RootElement.GetProperty("archived").GetInt32().ShouldBe(0);
		evidence.RootElement.GetProperty("history").GetString().ShouldBeNull();
		evidence.RootElement.GetProperty("receiptSequences").EnumerateArray().Sum(s => s.GetProperty("nextValue").GetInt64())
			.ShouldBeGreaterThan(before.RootElement.GetProperty("receiptSequences").EnumerateArray().Sum(s => s.GetProperty("nextValue").GetInt64()));
	}

	[FeeAssignmentPostgresFact]
	public async Task Correction_Committed_After_Collection_Read_Invalidates_Old_Due_Payment()
	{
		var (input, assigned) = await PrepareAsync();
		using var before = JsonDocument.Parse(await EvidenceAsync(input, assigned.AssignmentId));
		factory.Probe.Arm(AssignmentProbeMode.PauseCollection, assigned.Assignment.Lines[0].DueFeeId!.Value);
		using var collectionClient = factory.CreateClient();
		collectionClient.Timeout = TimeSpan.FromMinutes(3);
		var collection = collectionClient.PostAsJsonAsync("/api/fee-collections", BuildPayment(input, assigned), Json);
		try
		{
			await factory.Probe.Paused.Task.WaitAsync(TimeSpan.FromSeconds(90));
			using var correctionClient = factory.CreateClient();
			var corrected = await correctionClient.PostAsJsonAsync(Route + "/assign", input, Json);
			corrected.IsSuccessStatusCode.ShouldBeTrue(await corrected.Content.ReadAsStringAsync());
		}
		finally { factory.Probe.Release.TrySetResult(); }
		(await collection).IsSuccessStatusCode.ShouldBeFalse();
		using var evidence = JsonDocument.Parse(await EvidenceAsync(input, assigned.AssignmentId));
		evidence.RootElement.GetProperty("paid").GetDecimal().ShouldBe(0m);
		evidence.RootElement.GetProperty("activeTotal").GetDecimal().ShouldBe(12000m);
		evidence.RootElement.GetProperty("collectionCount").GetInt32().ShouldBe(0);
		evidence.RootElement.GetProperty("archived").GetInt32().ShouldBe(assigned.Assignment.Lines.Count);
		evidence.RootElement.GetProperty("receiptSequences").GetRawText().ShouldBe(before.RootElement.GetProperty("receiptSequences").GetRawText(),
			"The failed collection must roll back its persistent receipt sequence increment.");
	}

	private static ShikshaMitra.Finance.Fees.Collections.Dtos.CollectFeeInput BuildPayment(AssignStudentFeeStructureDto input, AssignmentCommitResultDto assigned)
	{
		var due = assigned.Assignment.Lines.First(l => l.ProratedAmount >= 1000m);
		return FeeCollectionTestHelper.BuildCollectFeeInput(input.StudentId, input.AcademicYearId, due.FeeHeadId, due.DueFeeId!.Value, 1000d);
	}

	private async Task<string> EvidenceAsync(AssignStudentFeeStructureDto input, Guid assignmentId)
	{
		using var services = factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
		using var uow = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
		var assignment = await services.ServiceProvider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().GetAsync(assignmentId);
		using var deleted = services.ServiceProvider.GetRequiredService<IDataFilter>().Disable<ISoftDelete>();
		var dues = await services.ServiceProvider.GetRequiredService<IRepository<DueFee, Guid>>().GetListAsync(d => d.StudentFeeStructureId == assignmentId);
		var collections = await services.ServiceProvider.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>()
			.GetListAsync(p => p.StudentId == input.StudentId && p.AcademicYearId == input.AcademicYearId);
		var sequences = await services.ServiceProvider.GetRequiredService<IRepository<ReceiptNumberSequence, Guid>>()
			.GetListAsync(s => s.StoredTenantId == Guid.Parse(TestAuthHandler.TestTenantId));
		return JsonSerializer.Serialize(new { assignment.FeeStructureStandardId, assignment.AssignmentSnapshotJson,
			assignment.AssignmentRequestFingerprint, history = assignment.ExtraProperties.TryGetValue("AssignmentCorrectionHistory", out var history) ? history?.ToString() : null,
			activeIds = dues.Where(d => !d.IsDeleted).Select(d => d.Id).Order(), activeTotal = dues.Where(d => !d.IsDeleted).Sum(d => d.OriginalAmount),
			paid = dues.Sum(d => d.PaidAmount), archived = dues.Count(d => d.IsDeleted), collectionCount = collections.Count,
			receiptSequences = sequences.OrderBy(s => s.Year).ThenBy(s => s.StoredKind).Select(s => new { s.Year, s.StoredKind, s.NextValue }) }, Json);
	}

	private static Task<T> InvokeAsync<T>(StudentFeeAssignmentApi_Tests scenario, string method, params object?[] args) =>
		(Task<T>)typeof(StudentFeeAssignmentApi_Tests).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(scenario, args)!;
}

public sealed class FeeAssignmentPostgresFactory : FinanceReceiptApiTestFactory
{
	public const string ConnectionVariable = "FEE_ASSIGNMENT_TEST_POSTGRES";
	protected override bool SeedTransactionally => true;
	public AssignmentTransactionProbe Probe { get; } = new();
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		// Replace the SQLite-only fake after the base test-services registration.
		builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Transient<
			IReceiptNumberSequenceRepository, EfCoreReceiptNumberSequenceRepository>()));
	}
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (settings.Host != "127.0.0.1" || settings.Port < 49152 || settings.Username != "fee_assignment_test" ||
			settings.Database == null || !Regex.IsMatch(settings.Database, "^assignment_correction_test_[a-z0-9_]+$"))
			throw new InvalidOperationException("Only a fresh private high-port loopback assignment_correction_test_* database is allowed.");
		using (var connection = new NpgsqlConnection(settings.ConnectionString))
		{
			connection.Open();
			using var empty = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')", connection);
			if ((long)empty.ExecuteScalar()! != 0) throw new InvalidOperationException("Refusing to reuse or reset a populated database.");
		}
		services.Configure<AbpDbContextOptions>(options => options.Configure(context => context.DbContextOptions
			.UseNpgsql(settings.ConnectionString).AddInterceptors(Probe)));
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(settings.ConnectionString).Options);
		db.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
}

public enum AssignmentProbeMode { None, FailReplacementInsert, PauseArchival, PauseCollection }

public sealed class AssignmentTransactionProbe : DbCommandInterceptor
{
	private int _paused;
	private Guid _due;
	private AssignmentProbeMode _mode;
	public bool ArchiveExecuted { get; private set; }
	public bool FailureInjected { get; private set; }
	public TaskCompletionSource Paused { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public TaskCompletionSource Release { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public void Reset() { _mode = AssignmentProbeMode.None; _paused = 0; ArchiveExecuted = false; FailureInjected = false;
		Paused = new(TaskCreationOptions.RunContinuationsAsynchronously); Release = new(TaskCreationOptions.RunContinuationsAsynchronously); }
	public void Arm(AssignmentProbeMode mode, Guid due) { Reset(); _mode = mode; _due = due; }
	public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
	{
		if (_mode == AssignmentProbeMode.FailReplacementInsert && ArchiveExecuted && Regex.IsMatch(command.CommandText,
			"INSERT INTO\\s+(?:\"[^\"]+\"\\.)?\"Finance_DueFees\""))
		{
			FailureInjected = true;
			throw new InvalidOperationException("Synthetic failure after unused dues were archived, before replacement insert.");
		}
		var deletion = DueDeletionValue(command);
		var paidValue = DueUpdateValue(command, "PaidAmount");
		var collectsPrincipal = paidValue is decimal paid && paid > 0m;
		if (((_mode == AssignmentProbeMode.PauseArchival && deletion == true) || (_mode == AssignmentProbeMode.PauseCollection && deletion != true && collectsPrincipal)) &&
			command.Parameters.Cast<DbParameter>().Any(p => p.Value is Guid id && id == _due) && Interlocked.CompareExchange(ref _paused, 1, 0) == 0)
		{
			Paused.TrySetResult();
			await Release.Task.WaitAsync(cancellationToken);
		}
		return result;
	}
	public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
		DbDataReader result, CancellationToken cancellationToken = default)
	{
		if (DueDeletionValue(command) == true) ArchiveExecuted = true;
		return ValueTask.FromResult(result);
	}
	private static bool? DueDeletionValue(DbCommand command)
	{
		return DueUpdateValue(command, "IsDeleted") is bool value ? value : null;
	}
	private static object? DueUpdateValue(DbCommand command, string column)
	{
		var update = Regex.Match(command.CommandText, "UPDATE\\s+(?:\"[^\"]+\"\\.)?\"Finance_DueFees\"[\\s\\S]*?(?:;|$)");
		if (!update.Success) return null;
		foreach (Match match in Regex.Matches(update.Value, "\"" + Regex.Escape(column) + "\"\\s*=\\s*(@p[0-9]+)"))
		{
			var name = match.Groups[1].Value.TrimStart('@');
			var parameter = command.Parameters.Cast<DbParameter>().FirstOrDefault(p => p.ParameterName.TrimStart('@') == name);
			if (parameter != null) return parameter.Value;
		}
		return null;
	}
}
