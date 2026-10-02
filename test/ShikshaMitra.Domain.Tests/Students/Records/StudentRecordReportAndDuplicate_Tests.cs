using System;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentRecordReportAndDuplicate_Tests
{
	[Fact]
	public void Cancelled_generation_cannot_publish_a_late_result()
	{
		var job = new StudentRecordReportJob(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "request", "Directory", "{}", "hash", DateTimeOffset.UtcNow.AddDays(1));
		var generation = Guid.NewGuid(); job.Start(generation, DateTimeOffset.UtcNow); job.Cancel();
		Should.Throw<BusinessException>(() => job.Complete(generation, "blob", 1)); job.Status.ShouldBe("Cancelled"); job.BlobName.ShouldBeNull();
	}
	[Fact]
	public void Lease_recovery_prevents_the_original_generation_from_publishing()
	{
		var now = DateTimeOffset.UtcNow;
		var job = new StudentRecordReportJob(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "request", "Directory", "{}", "hash", now.AddDays(1));
		var first = Guid.NewGuid(); var replacement = Guid.NewGuid(); job.Start(first, now);
		Should.Throw<BusinessException>(() => job.Start(replacement, now.AddMinutes(1)));
		job.Start(replacement, now.AddMinutes(21));
		Should.Throw<BusinessException>(() => job.Complete(first, "stale-blob", 1));
		job.Complete(replacement, "current-blob", 2); job.BlobName.ShouldBe("current-blob"); job.RowCount.ShouldBe(2);
	}
	[Fact]
	public void Duplicate_flag_normalizes_pair_and_requires_a_separate_reviewer_without_merging()
	{
		var first = Guid.NewGuid(); var second = Guid.NewGuid(); var actor = Guid.NewGuid();
		var left = new StudentRecordDuplicateReview(Guid.NewGuid(), Guid.NewGuid(), first, second, actor, "Both entries may identify the same child");
		var right = new StudentRecordDuplicateReview(Guid.NewGuid(), Guid.NewGuid(), second, first, actor, "Both entries may identify the same child");
		left.StudentId.ShouldBe(right.StudentId); left.OtherStudentId.ShouldBe(right.OtherStudentId);
		Should.Throw<BusinessException>(() => left.Resolve(actor, true, "Verified against admission records"));
		left.Resolve(Guid.NewGuid(), true, "Verified against admission records"); left.Status.ShouldBe("ConfirmedDuplicate");
		Should.Throw<BusinessException>(() => left.Resolve(Guid.NewGuid(), false, "Attempted overwrite of review"));
		Should.Throw<BusinessException>(() => new StudentRecordDuplicateReview(Guid.NewGuid(), Guid.NewGuid(), first, first, actor, "Same record"));
	}
}
