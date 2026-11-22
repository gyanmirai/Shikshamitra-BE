using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Academic.Notifications;

public class AcademicNotificationRecipientReader_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IStudentRepository _students = Substitute.For<IStudentRepository>();
	private readonly IRepository<StudentFamilyDetail, Guid> _families = Substitute.For<IRepository<StudentFamilyDetail, Guid>>();
	private readonly IRepository<StudentPortalAccountLink, Guid> _links = Substitute.For<IRepository<StudentPortalAccountLink, Guid>>();
	private readonly IRepository<StudentGuardianAccessReview, Guid> _reviews = Substitute.For<IRepository<StudentGuardianAccessReview, Guid>>();
	private readonly IRepository<IdentityUser, Guid> _users = Substitute.For<IRepository<IdentityUser, Guid>>();
	private readonly IRepository<Employee, Guid> _employees = Substitute.For<IRepository<Employee, Guid>>();
	private readonly List<Student> _studentRows = [];
	private readonly List<StudentFamilyDetail> _familyRows = [];
	private readonly List<StudentPortalAccountLink> _linkRows = [];
	private readonly List<StudentGuardianAccessReview> _reviewRows = [];
	private readonly List<IdentityUser> _userRows = [];
	private readonly AcademicNotificationRecipientReader _reader;

	public AcademicNotificationRecipientReader_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_students.GetQueryableWithoutAutoIncludesAsync().Returns(_studentRows.AsQueryable());
		_families.GetQueryableAsync().Returns(_familyRows.AsQueryable());
		_links.GetQueryableAsync().Returns(_linkRows.AsQueryable());
		_reviews.GetQueryableAsync().Returns(_reviewRows.AsQueryable());
		_users.GetQueryableAsync().Returns(_userRows.AsQueryable());
		_employees.GetQueryableAsync().Returns(Array.Empty<Employee>().AsQueryable());
		_reader = new(_tenant, _students, _families, _links, _reviews, _users, _employees, new AsyncQueryableExecuter([]));
	}

	[Fact]
	public async Task Batches_children_links_users_and_reviews_without_contact_string_association()
	{
		for (var i = 0; i < 100; i++) AddStudent(_tenantId);
		var first = _studentRows[0];
		var linked = new IdentityUser(Guid.NewGuid(), "linked", "shared@example.invalid", _tenantId);
		_userRows.Add(linked);
		_userRows.Add(new(Guid.NewGuid(), "unrelated", "shared@example.invalid", _tenantId));
		_linkRows.Add(new(Guid.NewGuid(), _tenantId, first.Id, linked.Id, "guardian", "mother", Guid.NewGuid()));
		_reviewRows.Add(new(Guid.NewGuid(), _tenantId, first.Id, Guid.NewGuid(), "[]", "SuspendUntilReview"));
		var result = await ReadAsync();
		result.Students.Count.ShouldBe(100);
		result.Students.Single(x => x.Student.Id == first.Id).Accounts.Single().User.Id.ShouldBe(linked.Id);
		result.Students.Single(x => x.Student.Id == first.Id).GuardianReviewPending.ShouldBeTrue();
		result.Students.Count(x => x.Accounts.Count != 0).ShouldBe(1);
		await _students.Received(1).GetQueryableWithoutAutoIncludesAsync();
		await _families.Received(1).GetQueryableAsync();
		await _links.Received(1).GetQueryableAsync();
		await _reviews.Received(1).GetQueryableAsync();
		await _users.Received(1).GetQueryableAsync();
	}

	[Fact]
	public async Task Explicit_tenant_predicates_and_link_ownership_reject_foreign_data()
	{
		AddStudent(_tenantId);
		AddStudent(Guid.NewGuid());
		var user = new IdentityUser(Guid.NewGuid(), "foreign", "foreign@example.invalid", Guid.NewGuid());
		_userRows.Add(user);
		_linkRows.Add(new(Guid.NewGuid(), _tenantId, _studentRows[0].Id, user.Id, "student", "self", Guid.NewGuid()));
		var result = await ReadAsync();
		result.Students.Count.ShouldBe(1);
		result.Students.Single().Accounts.ShouldBeEmpty();
	}

	[Fact]
	public async Task Staff_identity_is_projected_once_when_multiple_roles_target_the_same_user()
	{
		var target = new IdentityUser(Guid.NewGuid(), "assigned", "assigned@example.invalid", _tenantId) { Name = "Class teacher" };
		_userRows.Add(target);
		var result = await _reader.LoadAsync(_tenantId, [
			new("AssignedTeacher", UserId: target.Id), new("ClassTeacher", UserId: target.Id)
		], default);
		var person = result.Staff.ShouldHaveSingleItem();
		person.UserId.ShouldBe(target.Id);
		person.DisplayName.ShouldBe("Class teacher");
		person.IsActive.ShouldBeTrue();
	}

	[Fact]
	public async Task Host_and_wrong_tenant_calls_are_denied_without_reading_repositories()
	{
		_tenant.Id.Returns((Guid?)null);
		await Should.ThrowAsync<AbpAuthorizationException>(() => ReadAsync());
		_tenant.Id.Returns(Guid.NewGuid());
		await Should.ThrowAsync<AbpAuthorizationException>(() => ReadAsync());
		await _students.DidNotReceive().GetQueryableWithoutAutoIncludesAsync();
	}

	private Task<AcademicNotificationRecipientData> ReadAsync() => _reader.LoadAsync(_tenantId,
		_studentRows.Select(x => new NotificationRecipientRefDto("Student", x.Id)).ToArray(), default);

	private void AddStudent(Guid tenantId)
	{
		var student = new Student(Guid.NewGuid(), tenantId, "Child", null, "Family", Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status: StudentLifeCycleStatusEnum.Enrolled);
		_studentRows.Add(student);
		_familyRows.Add(new(student.Id, tenantId));
	}
}
