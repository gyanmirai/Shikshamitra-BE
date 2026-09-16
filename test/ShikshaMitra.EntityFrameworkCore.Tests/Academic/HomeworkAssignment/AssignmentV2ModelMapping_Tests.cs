using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Academic.HomeworkAssignment;

public class AssignmentV2ModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Assignment_Evaluation_Fields_Should_Be_Present_And_Nullable_In_The_Runtime_Model()
	{
		var dbContext = GetRequiredService<ShikshaMitraDbContext>();
		var entityType = dbContext.Model.FindEntityType(typeof(AssignmentEntity));
		entityType.ShouldNotBeNull();
		entityType.GetTableName().ShouldBe("academic_Assignments");
		var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

		var evaluationRequired = entityType.FindProperty(nameof(AssignmentEntity.EvaluationRequired));
		evaluationRequired.ShouldNotBeNull();
		evaluationRequired.IsNullable.ShouldBeTrue();
		evaluationRequired.GetColumnName(table).ShouldBe("EvaluationRequired");

		var evaluationMethod = entityType.FindProperty(nameof(AssignmentEntity.EvaluationMethod));
		evaluationMethod.ShouldNotBeNull();
		evaluationMethod.IsNullable.ShouldBeTrue();
		evaluationMethod.GetColumnName(table).ShouldBe("EvaluationMethod");
	}
}
