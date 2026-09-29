using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentUserLink_Tests
{
	[Fact]
	public void A_student_can_be_linked_to_one_identity_user()
	{
		var student = new Student(Guid.NewGuid(), Guid.NewGuid(), "Aanya", null, "Patel",
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var userId = Guid.NewGuid();

		student.LinkUser(userId);
		student.LinkUser(userId);

		student.UserId.ShouldBe(userId);
		Should.Throw<InvalidOperationException>(() => student.LinkUser(Guid.NewGuid()));
		student.UserId.ShouldBe(userId);
		Should.Throw<ArgumentException>(() => student.LinkUser(Guid.Empty));
	}
}
