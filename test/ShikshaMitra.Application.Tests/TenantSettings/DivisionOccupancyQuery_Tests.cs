using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class DivisionOccupancyQuery_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task Occupancy_is_aggregated_in_SQL_and_preserves_gender_status_tenant_and_empty_division_counts()
	{
		var tenantId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "Test")));
		var standardId = await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IIdentityUserRepository>().InsertAsync(new IdentityUser(userId, "occupancy", "occupancy@example.test", tenantId), true);
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId), true);
			var year = await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Occupancy year", new DateTime(2026, 4, 1), new DateTime(2027, 3, 31)), true);
			var board = await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, EducationBoardType.CBSE, year.Id), true);
			var medium = await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "English", board.Id), true);
			var stage = await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Primary", null, medium.Id), true);
			var standard = await GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(
				new(Guid.NewGuid(), tenantId, "Class 1", 6, stage.Id, null), true);
			var division = await GetRequiredService<IDivisionRepository>().InsertAsync(new(Guid.NewGuid(), tenantId, "A", standard.Id, 3, ""), true);
			await GetRequiredService<IDivisionRepository>().InsertAsync(new(Guid.NewGuid(), tenantId, "Empty", standard.Id, 5, ""), true);
			for (var index = 0; index < 7; index++)
			{
				var student = new Student(Guid.NewGuid(), index == 6 ? Guid.NewGuid() : tenantId, "Student " + index,
					null, null, year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id,
					status: index == 4 ? StudentLifeCycleStatusEnum.Registration : StudentLifeCycleStatusEnum.Enrolled);
				student.IsDeleted = index == 5;
				await GetRequiredService<IStudentRepository>().InsertAsync(student, true);
				if (index != 3)
					await GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>().InsertAsync(new StudentAdditionalDetail(
						student.Id, student.TenantId, null, index == 1 ? GenderEnum.Female : index == 2 ? null : GenderEnum.Male,
						null, "", null, null), true);
			}
			return standard.Id;
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var commands = new OccupancyCommands(db.Database.GetDbConnection());
			var detail = await GetRequiredService<IStandardAppService>().GetAsync(standardId);
			var occupied = detail.Divisions.Single(x => x.Name == "A");
			occupied.CurrentStudentCount.ShouldBe(4);
			occupied.HeadCount.ShouldBe(4);
			occupied.MaleCount.ShouldBe(1);
			occupied.FemaleCount.ShouldBe(1);
			occupied.AvailableSeats.ShouldBe(0);
			var empty = detail.Divisions.Single(x => x.Name == "Empty");
			empty.CurrentStudentCount.ShouldBe(0);
			empty.AvailableSeats.ShouldBe(5);
			commands.Sql.ShouldContain(sql => sql.Contains("AppStudents", StringComparison.Ordinal)
				&& sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
		});
	}

	private sealed class OccupancyCommands : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
	{
		private readonly DbConnection _connection;
		private readonly IDisposable _listener;
		private readonly ConcurrentBag<IDisposable> _subscriptions = [];
		public ConcurrentBag<string> Sql { get; } = [];
		public OccupancyCommands(DbConnection connection)
		{
			_connection = connection;
			_listener = DiagnosticListener.AllListeners.Subscribe(this);
		}
		public void OnNext(DiagnosticListener listener)
		{
			if (listener.Name == "Microsoft.EntityFrameworkCore")
				_subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
		}
		public void OnNext(KeyValuePair<string, object?> value)
		{
			if (value.Value is CommandExecutedEventData data && data.Context != null
				&& ReferenceEquals(data.Context.Database.GetDbConnection(), _connection)) Sql.Add(data.Command.CommandText);
		}
		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void Dispose()
		{
			_listener.Dispose();
			foreach (var subscription in _subscriptions) subscription.Dispose();
		}
	}
}
