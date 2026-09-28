using System;
using System.Linq;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class StandardAcademicOrder_Tests
{
	[Fact]
	public void Sort_Should_Return_The_Academic_Sequence_Regardless_Of_Creation_Order()
	{
		var names = new[]
		{
			"LKG", "Class 12", "Class 1", "Class 10", "Class 6", "Class 9", "Class 7",
			"Class 4", "UKG", "Class 11", "Nursery", "Class 2", "Class 5", "Class 3", "Class 8"
		};
		var standards = names.Select(name =>
			new Standard(Guid.NewGuid(), Guid.NewGuid(), name, 6, Guid.NewGuid(), null)).ToList();

		StandardAcademicOrder.Sort(standards, standard => standard.Name, standard => standard.AgeLowerLimit, standard => standard.Id)
			.Select(standard => standard.Name).ShouldBe(new[]
		{
			"Nursery", "LKG", "UKG", "Class 1", "Class 2", "Class 3", "Class 4", "Class 5",
			"Class 6", "Class 7", "Class 8", "Class 9", "Class 10", "Class 11", "Class 12"
		});
		standards.Select(standard => standard.Name).ShouldBe(names);
	}

	[Fact]
	public void Sort_Should_Order_Custom_Names_Deterministically_After_Numbered_Classes()
	{
		var standards = new[]
		{
			new Standard(Guid.NewGuid(), Guid.NewGuid(), "Prep B", 5, Guid.NewGuid(), null),
			new Standard(Guid.NewGuid(), Guid.NewGuid(), "Class 2", 6, Guid.NewGuid(), null),
			new Standard(Guid.NewGuid(), Guid.NewGuid(), "Prep A", 5, Guid.NewGuid(), null)
		};

		StandardAcademicOrder.Sort(standards, standard => standard.Name, standard => standard.AgeLowerLimit, standard => standard.Id)
			.Select(standard => standard.Name)
			.ShouldBe(new[] { "Class 2", "Prep A", "Prep B" });
	}
}
