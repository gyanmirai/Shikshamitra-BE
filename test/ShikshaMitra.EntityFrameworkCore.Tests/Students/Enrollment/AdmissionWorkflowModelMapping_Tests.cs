using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Students.Enrollment;

public class AdmissionWorkflowModelMapping_Tests : ShikshaMitraEntityFrameworkCoreTestBase
{
	[Fact]
	public void Admission_soft_delete_properties_are_client_generated_with_false_defaults()
	{
		var workflow = GetRequiredService<ShikshaMitraDbContext>().Model
			.FindEntityType(typeof(AdmissionWorkflowDefinition));
		workflow.ShouldNotBeNull();
		var isDeleted = workflow.FindProperty("IsDeleted");
		isDeleted.ShouldNotBeNull();

		isDeleted.ValueGenerated.ShouldBe(ValueGenerated.Never);
		isDeleted.GetDefaultValue().ShouldBe(false);
	}

	[Fact]
	public void Definition_and_runtime_models_have_required_indexes_and_restrictive_history_links()
	{
		var model = GetRequiredService<ShikshaMitraDbContext>().Model;
		var workflow = model.FindEntityType(typeof(AdmissionWorkflowDefinition));
		var workflowTransition = model.FindEntityType(typeof(AdmissionWorkflowTransitionDefinition));
		var workflowInstance = model.FindEntityType(typeof(AdmissionApplicationWorkflowInstance));
		var stageInstance = model.FindEntityType(typeof(AdmissionApplicationStageInstance));
		var history = model.FindEntityType(typeof(AdmissionWorkflowTransitionHistory));
		workflow.ShouldNotBeNull();
		workflowTransition.ShouldNotBeNull();
		workflowInstance.ShouldNotBeNull();
		stageInstance.ShouldNotBeNull();
		history.ShouldNotBeNull();

		workflow.GetIndexes().ShouldContain(index =>
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "Code", "Version" }) &&
			index.IsUnique);
		var transitionIndexNames = workflowTransition.GetIndexes()
			.Select(index => index.GetDatabaseName())
			.ToArray();
		transitionIndexNames.ShouldContain("UX_AdmWfTransitions_Workflow_From_Default");
		transitionIndexNames.ShouldContain("IX_AdmWfTransitions_Workflow_From_Priority");
		transitionIndexNames.ShouldContain("UX_AdmWfTransitions_Workflow_From_To");
		workflowTransition.GetIndexes().ShouldAllBe(index => index.GetDatabaseName()!.Length <= 63);
		history.GetIndexes().ShouldContain(index =>
			index.Properties.Select(property => property.Name)
				.SequenceEqual(new[] { "TenantId", "WorkflowInstanceId", "IdempotencyKey" }) &&
			index.IsUnique);
		history.GetForeignKeys().ShouldContain(key =>
			key.Properties.Single().Name == nameof(AdmissionWorkflowTransitionHistory.ApplicationId) &&
			key.DeleteBehavior == DeleteBehavior.Restrict);
		workflowInstance.GetForeignKeys().ShouldContain(key =>
			key.Properties.Single().Name == nameof(AdmissionApplicationWorkflowInstance.WorkflowDefinitionId) &&
			key.DeleteBehavior == DeleteBehavior.Restrict);
		stageInstance.FindProperty(nameof(AdmissionApplicationStageInstance.TenantId)).ShouldNotBeNull();
		stageInstance.GetForeignKeys().ShouldContain(key =>
			key.Properties.Single().Name == nameof(AdmissionApplicationStageInstance.DefinitionStageId) &&
			key.DeleteBehavior == DeleteBehavior.Restrict);
	}
}
