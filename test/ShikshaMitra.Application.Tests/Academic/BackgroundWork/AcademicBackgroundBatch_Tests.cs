using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.BackgroundWork;

public class AcademicBackgroundBatch_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task More_Than_One_Page_Should_Commit_Other_Items_And_Retry_The_Failed_Item_In_Its_Tenant()
	{
		var repository = GetRequiredService<IExamRepository>();
		var tenant = GetRequiredService<ICurrentTenant>();
		var runner = GetRequiredService<AcademicBackgroundBatchRunner>();
		var exams = Enumerable.Range(1, AcademicBackgroundBatchRunner.BatchSize + 3)
			.Select(index => CreateExam(Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
				index % 3 == 0 ? null : index % 3 == 1 ? TenantA : TenantB)).ToList();
		await SeedAsync(exams);
		var failedId = exams[0].Id;
		var visited = new List<Guid>();
		var changed = await runner.RunAsync(repository, exam => exam.Status == ExamStatus.Pending, exam =>
		{
			tenant.Id.ShouldBe(exam.TenantId);
			visited.Add(exam.Id);
			exam.ChangeStatus(ExamStatus.Completed);
			if (exam.Id == failedId) throw new InvalidOperationException("Simulated item failure after mutation.");
			return 1;
		});
		changed.ShouldBe(exams.Count - 1);
		visited.Distinct().Count().ShouldBe(exams.Count);
		tenant.Id.ShouldBeNull();
		using (tenant.Change(exams[0].TenantId))
			await WithUnitOfWorkAsync(async () => (await repository.GetAsync(failedId)).Status.ShouldBe(ExamStatus.Pending));

		var retried = await runner.RunAsync(repository, exam => exam.Status == ExamStatus.Pending, exam =>
		{
			exam.Id.ShouldBe(failedId);
			exam.ChangeStatus(ExamStatus.Completed);
			return 1;
		});
		retried.ShouldBe(1);
		(await runner.RunAsync(repository, exam => exam.Status == ExamStatus.Pending, _ => throw new Exception("Already completed")))
			.ShouldBe(0);
	}

	[Fact]
	public async Task Cancellation_Should_Keep_Committed_Items_And_Allow_The_Rest_To_Resume()
	{
		var repository = GetRequiredService<IExamRepository>();
		var runner = GetRequiredService<AcademicBackgroundBatchRunner>();
		var exams = Enumerable.Range(1, 3).Select(index => CreateExam(
			Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), TenantA)).ToList();
		await SeedAsync(exams);
		using var cancellation = new CancellationTokenSource();
		var visited = 0;
		await Should.ThrowAsync<OperationCanceledException>(() => runner.RunAsync(repository,
			exam => exam.Status == ExamStatus.Pending,
			exam =>
			{
				if (++visited == 2)
				{
					cancellation.Cancel();
					cancellation.Token.ThrowIfCancellationRequested();
				}
				exam.ChangeStatus(ExamStatus.Completed);
				return 1;
			}, cancellation.Token));
		(await runner.RunAsync(repository, exam => exam.Status == ExamStatus.Pending, exam =>
		{
			exam.ChangeStatus(ExamStatus.Completed);
			return 1;
		})).ShouldBe(2);
	}

	private static readonly Guid TenantA = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
	private static readonly Guid TenantB = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

	private async Task SeedAsync(IEnumerable<Exam> exams)
	{
		foreach (var group in exams.GroupBy(exam => exam.TenantId))
		{
			using (GetRequiredService<ICurrentTenant>().Change(group.Key))
				await WithUnitOfWorkAsync(() => GetRequiredService<IExamRepository>().InsertManyAsync(group));
		}
	}

	private static Exam CreateExam(Guid id, Guid? tenantId) =>
		new(id, Guid.NewGuid(), "Background batch", Guid.NewGuid(), tenantId);
}
