using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationSourcePartitioner_Tests
{
	private readonly NotificationSourcePartitioner _partitioner = new();
	private readonly NotificationEventDefinition _definition = new AcademicNotificationCatalogue([]).Get("Academic.Homework.Published");
	private readonly NotificationIntentContext _context = new(Guid.NewGuid(), Guid.NewGuid(), "Academic.Homework.Published", "Academic.Homework",
		Guid.NewGuid(), "revision-1", DateTimeOffset.UtcNow, []);
	private readonly NotificationSourceScopeDto _first = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	[Fact]
	public void A_school_event_is_chunked_without_dropping_recipients_or_weakening_scope()
	{
		var recipients = Enumerable.Range(0, 10001).Select(_ => Student()).ToArray();
		var second = _first with { ClassId = Guid.NewGuid() };
		var source = Source(recipients, [new(_first, recipients[..5000]), new(second, recipients[5000..])]);
		var partitions = _partitioner.Partition(_context, _definition, source);
		partitions.Count.ShouldBe(101); partitions.Sum(x => x.Recipients.Count).ShouldBe(10001);
		partitions.SelectMany(x => x.Recipients).Distinct().Count().ShouldBe(10001);
		partitions.Select(x => x.Scope.ClassId).Distinct().Count().ShouldBe(2);
		foreach (var partition in partitions)
		{
			partition.Recipients.Count.ShouldBeInRange(1, 100);
			JsonSerializer.Serialize(partition.Recipients).Length.ShouldBeLessThan(65536);
		}
	}
	[Fact]
	public void Bounded_event_references_do_not_expand_to_new_source_recipients()
	{
		var affected = Student(); var late = Student(); var source = Source([affected, late], [new(_first, [affected, late])]);
		var partitions = _partitioner.Partition(_context with { AffectedRecipients = [affected] }, _definition, source);
		partitions.SelectMany(x => x.Recipients).ShouldBe([affected]);
	}
	[Theory]
	[InlineData("missing")]
	[InlineData("extra")]
	[InlineData("uncovered")]
	[InlineData("scope")]
	[InlineData("revision")]
	[InlineData("input")]
	[InlineData("kind")]
	public void Inconsistent_source_scope_or_roster_fails_closed(string variant)
	{
		var recipient = variant == "kind" ? new NotificationRecipientRefDto("AllAdministrators", UserId: Guid.NewGuid()) : Student();
		var source = Source([recipient], [new(_first, [recipient])]); var context = _context;
		if (variant == "missing") source = source with { AudienceSlices = null };
		if (variant == "extra") source = source with { AudienceSlices = [new(_first, [recipient, Student()])] };
		if (variant == "uncovered") source = source with { AudienceSlices = [] };
		if (variant == "scope") source = source with { AudienceSlices = [new(new(ClassId: Guid.NewGuid()), [recipient])] };
		if (variant == "revision") source = source with { SourceRevision = "another-revision" };
		if (variant == "input") context = context with { AffectedRecipients = [Student()] };
		Should.Throw<BusinessException>(() => _partitioner.Partition(context, _definition, source));
	}
	[Fact]
	public void One_student_cannot_be_assigned_conflicting_current_scopes()
	{
		var recipient = Student(); var source = Source([recipient], [new(_first, [recipient]), new(_first with { ClassId = Guid.NewGuid() }, [recipient])]);
		Should.Throw<BusinessException>(() => _partitioner.Partition(_context, _definition, source));
	}
	[Fact]
	public void The_same_teacher_can_receive_distinct_class_contexts_without_role_duplicates()
	{
		var teacher = new NotificationRecipientRefDto("AssignedTeacher", UserId: Guid.NewGuid());
		var source = Source([teacher], [new(_first, [teacher, teacher]), new(_first with { ClassId = Guid.NewGuid() }, [teacher])]);
		var partitions = _partitioner.Partition(_context, _definition, source);
		partitions.Count.ShouldBe(2); partitions.ShouldAllBe(x => x.Recipients.Count == 1);
	}
	[Fact]
	public void Overlapping_roles_in_one_scope_stay_together_when_the_roster_is_chunked()
	{
		var context = _context with { EventKey = "Academic.Progress.BehindSchedule", SourceType = "Academic.Progress" };
		var definition = new AcademicNotificationCatalogue([]).Get(context.EventKey);
		var userId = Guid.NewGuid();
		var roles = new[] {
			new NotificationRecipientRefDto("AssignedTeacher", UserId: userId),
			new NotificationRecipientRefDto("ClassTeacher", UserId: userId)
		};
		var roster = Enumerable.Range(0, 101).Select(_ => new NotificationRecipientRefDto("AssignedTeacher", UserId: Guid.NewGuid()))
			.Concat(roles).ToArray();
		var partitions = _partitioner.Partition(context, definition, Source(roster, [new(_first, roster)]));
		partitions.Single(x => x.Recipients.Any(r => r.UserId == userId)).Recipients.Count(r => r.UserId == userId).ShouldBe(2);
		partitions.ShouldAllBe(x => x.Recipients.Count <= 100);
	}
	[Fact]
	public void Ineligible_sources_produce_no_work_and_unrelated_content_overrides_are_rejected()
	{
		var recipient = Student();
		_partitioner.Partition(_context, _definition, Source([], []) with { IsEligible = false }).ShouldBeEmpty();
		var source = Source([recipient], [new(_first, [recipient])]) with { RecipientContent = [new(Student(), new Dictionary<string, NotificationVariableValueDto>(), "/academic/tasks")] };
		Should.Throw<BusinessException>(() => _partitioner.Partition(_context, _definition, source));
	}
	private NotificationSourceSnapshot Source(IReadOnlyList<NotificationRecipientRefDto> recipients,
		IReadOnlyList<NotificationSourceAudienceSliceDto> slices) => new(true, null, _context.SourceRevision, "/academic/tasks",
		new Dictionary<string, NotificationVariableValueDto>(), recipients, AudienceSlices: slices);
	private static NotificationRecipientRefDto Student() => new("Student", StudentId: Guid.NewGuid());
}
