using System;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Record_transition_apply_serialization_rolls_back_and_requires_manual_retry(bool wrapped) => RunCertificationScenarioAsync(async () =>
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy);
		var id = await SeedApprovedSerializationTransitionAsync(fixture.Student.Id, fixture.Input, policy);
		using var database = await UseRealSerializationTransactionsAsync();
		var sourceEnrollment = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(x => x.StudentId == fixture.Student.Id));
		var repository = GetRequiredService<IRepository<StudentRecordTransition, Guid>>();
		var fault = RecordApplyFaultRepository<StudentRecordTransition>.Create(repository, GetRequiredService<IUnitOfWorkManager>(), SerializationFault(wrapped), ServiceProvider);
		var application = FaultedTransitionApplication(fault);
		await application.ApplyOrScheduleAsync(id);
		var stored = await WithUnitOfWorkAsync(() => repository.GetAsync(id));
		stored.Status.ShouldBe("Exception"); stored.LastError.ShouldBe("Students:Records:ConcurrencyConflict"); stored.AppliedAt.ShouldBeNull(); stored.AfterJson.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
		var unchanged = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(sourceEnrollment.Id));
		unchanged.IsActive.ShouldBeTrue(); unchanged.ConcurrencyStamp.ShouldBe(sourceEnrollment.ConcurrencyStamp);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == id && x.Action == "Applied"))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => repository.CountAsync(x => x.Id == id && (x.Status == "Approved" || x.Status == "Scheduled")))).ShouldBe(0);
		await application.ApplyOrScheduleAsync(id);
		RecordApplyFaultRepository<StudentRecordTransition>.State(fault).AppliedWrites.ShouldBe(1);
		(await WithUnitOfWorkAsync(() => repository.GetAsync(id))).Status.ShouldBe("Exception");
		await application.ApplyAsRecordedActorAsync(id);
		(await WithUnitOfWorkAsync(() => repository.GetAsync(id))).Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(sourceEnrollment.Id))).IsActive.ShouldBeFalse();
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Record_change_apply_serialization_rolls_back_and_stops_automatic_apply(bool wrapped) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var id = await SeedApprovedSerializationChangeAsync(fixture.Student.Id, fixture.Input);
		using var database = await UseRealSerializationTransactionsAsync();
		var repository = GetRequiredService<IRepository<StudentRecordChange, Guid>>();
		var fault = RecordApplyFaultRepository<StudentRecordChange>.Create(repository, GetRequiredService<IUnitOfWorkManager>(), SerializationFault(wrapped), ServiceProvider);
		var application = FaultedChangeApplication(fault);
		await application.ApplyOrScheduleAsync(id);
		var stored = await WithUnitOfWorkAsync(() => repository.GetAsync(id));
		stored.Status.ShouldBe("Conflict"); stored.ExceptionCode.ShouldBe("Students:Records:ConcurrencyConflict"); stored.AppliedAt.ShouldBeNull();
		(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Snapshot.ShouldBe(fixture.Input.ExpectedSnapshot);
		(await WithUnitOfWorkAsync(() => repository.CountAsync(x => x.Id == id && (x.Status == "Approved" || x.Status == "Scheduled")))).ShouldBe(0);
		await application.ApplyOrScheduleAsync(id);
		RecordApplyFaultRepository<StudentRecordChange>.State(fault).AppliedWrites.ShouldBe(1);
		(await WithUnitOfWorkAsync(() => repository.GetAsync(id))).Status.ShouldBe("Conflict");
		// A reviewed replacement request retains the failed command and uses the unchanged snapshot.
		fixture.Input.ClientKey = Guid.NewGuid().ToString("N");
		(await fixture.Service.CreateAsync(fixture.Input)).Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => repository.GetAsync(id))).Status.ShouldBe("Conflict");
	});

	[Theory]
	[InlineData("transition")]
	[InlineData("change")]
	public Task Record_apply_unrelated_failure_is_not_swallowed_or_classified_as_conflict(string kind) => RunCertificationScenarioAsync(async () =>
	{
		var unexpected = new InvalidOperationException("Unrelated application fault");
		if (kind == "transition")
		{
			var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct";
			var fixture = await TransitionFixtureAsync(policy);
			var id = await SeedApprovedSerializationTransitionAsync(fixture.Student.Id, fixture.Input, policy);
			using var database = await UseRealSerializationTransactionsAsync();
			var repository = GetRequiredService<IRepository<StudentRecordTransition, Guid>>();
			var fault = RecordApplyFaultRepository<StudentRecordTransition>.Create(repository, GetRequiredService<IUnitOfWorkManager>(), unexpected, ServiceProvider);
			(await Should.ThrowAsync<InvalidOperationException>(() => FaultedTransitionApplication(fault).ApplyOrScheduleAsync(id))).ShouldBeSameAs(unexpected);
			var stored = await WithUnitOfWorkAsync(() => repository.GetAsync(id)); stored.Status.ShouldBe("Approved"); stored.LastError.ShouldBeNull();
			(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.Student.Id))).Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
		}
		else
		{
			var fixture = await RecordContactFixtureAsync();
			var id = await SeedApprovedSerializationChangeAsync(fixture.Student.Id, fixture.Input);
			using var database = await UseRealSerializationTransactionsAsync();
			var repository = GetRequiredService<IRepository<StudentRecordChange, Guid>>();
			var fault = RecordApplyFaultRepository<StudentRecordChange>.Create(repository, GetRequiredService<IUnitOfWorkManager>(), unexpected, ServiceProvider);
			(await Should.ThrowAsync<InvalidOperationException>(() => FaultedChangeApplication(fault).ApplyOrScheduleAsync(id))).ShouldBeSameAs(unexpected);
			var stored = await WithUnitOfWorkAsync(() => repository.GetAsync(id)); stored.Status.ShouldBe("Approved"); stored.ExceptionCode.ShouldBeNull();
			(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Snapshot.ShouldBe(fixture.Input.ExpectedSnapshot);
		}
	});

	private async Task<Guid> SeedApprovedSerializationTransitionAsync(Guid studentId, StudentRecordTransitionBatchInput input, StudentRecordsPolicy policy) => await WithUnitOfWorkAsync(async () =>
	{
		var source = await GetRequiredService<StudentRecordTransitionReadinessService>().GetSourceAsync(studentId, input.AcademicYearId);
		var batch = new StudentRecordTransitionBatch(Guid.NewGuid(), TestTenantId, input.AcademicYearId, input.IdempotencyKey, JsonSerializer.Serialize(input), "[]");
		await GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().InsertAsync(batch, true);
		var command = input.Items.Single();
		var item = new StudentRecordTransition(Guid.NewGuid(), TestTenantId, batch.Id, studentId, input.AcademicYearId, source.Enrollment.Id,
			command.Outcome, command.Reason, command.EffectiveDate, JsonSerializer.Serialize(command), StudentRecordTransitionReadinessService.Capture(source.Student, source.Enrollment),
			null, policy.ToJson(), JsonSerializer.Serialize(policy.Changes["Status"]), TestUserId, 0);
		await GetRequiredService<IRepository<StudentRecordTransition, Guid>>().InsertAsync(item, true); return item.Id;
	});

	private async Task<Guid> SeedApprovedSerializationChangeAsync(Guid studentId, CreateStudentRecordChangeDto input) => await WithUnitOfWorkAsync(async () =>
	{
		var before = await GetRequiredService<StudentRecordPayloadService>().CaptureAsync(studentId, input.Section);
		var student = await _studentRepository.GetAsync(studentId);
		var item = new StudentRecordChange(Guid.NewGuid(), TestTenantId, studentId, student.AcademicYearId!.Value, TestUserId,
			input.Section, "Contact", input.ClientKey, input.Reason, before, input.Payload.GetRawText(), StudentRecordsPolicy.Starter().ToJson(), null, "[]", 0, DateTimeOffset.UtcNow.AddMinutes(-1));
		await GetRequiredService<IRepository<StudentRecordChange, Guid>>().InsertAsync(item, true); return item.Id;
	});

	private StudentRecordTransitionApplication FaultedTransitionApplication(IRepository<StudentRecordTransition, Guid> repository) => new(repository,
		GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>(), _studentRepository, _studentEnrollmentRepository,
		GetRequiredService<StudentRecordsAccessService>(), GetRequiredService<StudentRecordTransitionReadinessService>(), GetRequiredService<StudentRecordRenewalCoordinator>(),
		GetRequiredService<StudentRecordTransitionCorrectionService>(), GetRequiredService<IPermissionChecker>(), GetRequiredService<IdentityUserManager>(),
		_principalAccessor, GetRequiredService<StudentRecordTransitionAuthorityService>(), GetRequiredService<StudentRecordAdmissionReturnService>()) { LazyServiceProvider = TransactionalApplyServices() };

	private StudentRecordChangeApplication FaultedChangeApplication(IRepository<StudentRecordChange, Guid> repository) => new(repository,
		GetRequiredService<IRepository<StudentRecordChangeDecision, Guid>>(), GetRequiredService<StudentRecordPayloadService>(), GetRequiredService<StudentRecordsAccessService>(),
		GetRequiredService<IPermissionChecker>(), GetRequiredService<IdentityUserManager>(), _principalAccessor, GetRequiredService<StudentGuardianAccessReviewService>())
		{ LazyServiceProvider = TransactionalApplyServices() };

	private AbpLazyServiceProvider TransactionalApplyServices() => new(new RecordApplyServiceProvider(ServiceProvider,
		ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider)));

	private static Exception SerializationFault(bool wrapped) => wrapped ? new InvalidOperationException("Wrapped database commit failure", new RecordSerializationDbException()) : new RecordSerializationDbException();

	private async Task<RecordApplyRealSqliteScope> UseRealSerializationTransactionsAsync()
	{
		var source = await WithUnitOfWorkAsync(async () => (SqliteConnection)(await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).Database.GetDbConnection());
		var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
		source.BackupDatabase(connection);
		return new RecordApplyRealSqliteScope(GetRequiredService<IOptions<AbpDbContextOptions>>().Value, source, connection);
	}
}

/// <summary>Bypass the common test-only AlwaysDisableTransactions manager for the application under test.</summary>
public sealed class RecordApplyServiceProvider(IServiceProvider services, IUnitOfWorkManager manager) : IServiceProvider
{
	public object? GetService(Type serviceType) => serviceType == typeof(IUnitOfWorkManager) ? manager : services.GetService(serviceType);
}

/// <summary>Isolate a real connection for rollback tests; the common test module disables UOW transactions.</summary>
public sealed class RecordApplyRealSqliteScope : IDisposable
{
	private readonly AbpDbContextOptions _options;
	private readonly SqliteConnection _original;
	private readonly SqliteConnection _connection;
	public RecordApplyRealSqliteScope(AbpDbContextOptions options, SqliteConnection original, SqliteConnection connection)
	{
		_options = options; _original = original; _connection = connection; Configure(connection);
	}
	private void Configure(SqliteConnection connection) => _options.Configure(context => context.DbContextOptions.UseSqlite(connection)
		.ConfigureLoggingCacheTime(TimeSpan.Zero).ReplaceService<IMemberTranslatorProvider, SqliteApplicationTestMemberTranslatorProvider>());
	public void Dispose() { Configure(_original); _connection.Dispose(); }
}

/// <summary>Forward to real repositories; fail once after transactional apply writes reach the database.</summary>
public class RecordApplyFaultRepository<TEntity> : DispatchProxy where TEntity : class, IEntity<Guid>
{
	private IRepository<TEntity, Guid> _repository = null!;
	private IUnitOfWorkManager _manager = null!;
	private IServiceProvider _services = null!;
	private Exception? _fault;
	public int AppliedWrites { get; private set; }
	public static IRepository<TEntity, Guid> Create(IRepository<TEntity, Guid> repository, IUnitOfWorkManager manager, Exception fault, IServiceProvider services)
	{
		var proxy = DispatchProxy.Create<IRepository<TEntity, Guid>, RecordApplyFaultRepository<TEntity>>();
		var state = State(proxy); state._repository = repository; state._manager = manager; state._fault = fault; state._services = services; return proxy;
	}
	public static RecordApplyFaultRepository<TEntity> State(IRepository<TEntity, Guid> repository) => (RecordApplyFaultRepository<TEntity>)(object)repository;
	protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
	{
		object? result;
		try { result = targetMethod!.Invoke(_repository, args); }
		catch (TargetInvocationException exception) when (exception.InnerException != null) { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
		if (targetMethod!.Name == "UpdateAsync" && args?[0] is TEntity entity && typeof(TEntity).GetProperty("Status")?.GetValue(entity) is "Applied")
			return FailAppliedWriteAsync((Task<TEntity>)result!);
		return result;
	}
	private async Task<TEntity> FailAppliedWriteAsync(Task<TEntity> operation)
	{
		var result = await operation; AppliedWrites++;
		if (_fault is { } fault)
		{
			_fault = null;
			_manager.Current!.Options.IsTransactional.ShouldBeTrue();
			var database = await _services.GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			database.Database.CurrentTransaction.ShouldNotBeNull();
			database.Database.GetDbConnection().GetType().ShouldBe(typeof(SqliteConnection));
			await _manager.Current!.SaveChangesAsync();
			throw fault;
		}
		return result;
	}
}

public sealed class RecordSerializationDbException : DbException
{
	public override string SqlState => "40001";
}
