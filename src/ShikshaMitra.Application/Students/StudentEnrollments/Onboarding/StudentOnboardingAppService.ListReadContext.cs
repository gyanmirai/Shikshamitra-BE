using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Data;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private sealed class OnboardingListStudentRead
	{
		public Guid Id { get; set; }
		public string? FirstName { get; set; }
		public string? MiddleName { get; set; }
		public string? LastName { get; set; }
		public string? GrNumber { get; set; }
		public int? GrNo { get; set; }
		public ExtraPropertyDictionary ExtraProperties { get; set; } = new();

		public bool IsIdentifierLocked(string type) =>
			ExtraProperties.TryGetValue($"IdentifierLocked:{type}", out var value) &&
			bool.TryParse(value?.ToString(), out var locked) && locked;
	}

	private sealed class OnboardingListDivisionRead
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public ExtraPropertyDictionary ExtraProperties { get; set; } = new();

		public bool IsIdentifierLocked(Guid yearId, string type) =>
			ExtraProperties.TryGetValue(IdentifierGroupLockKey(yearId, type), out var value) &&
			bool.TryParse(value?.ToString(), out var locked) && locked;
	}

	private sealed class OnboardingListReadContext
	{
		public Lazy<Task<OnboardingListCredentialReadContext>>? CredentialMetadata { get; init; }
		public Dictionary<Guid, OnboardingListStudentRead> Students { get; set; } = [];
		public Dictionary<Guid, AdmissionApplication> Applications { get; set; } = [];
		public Dictionary<Guid, AdmissionOffering> Offerings { get; set; } = [];
		public Dictionary<Guid, string> YearNames { get; set; } = [];
		public Dictionary<Guid, string> BoardNames { get; set; } = [];
		public Dictionary<Guid, string> MediumNames { get; set; } = [];
		public Dictionary<Guid, string> StandardNames { get; set; } = [];
		public Dictionary<Guid, OnboardingListDivisionRead> Divisions { get; set; } = [];
		public ILookup<Guid, StudentOnboardingStep> Steps { get; set; } =
			Enumerable.Empty<StudentOnboardingStep>().ToLookup(item => item.OnboardingCaseId);
		public ILookup<Guid, StudentOnboardingServiceHandoff> RequiredHandoffs { get; set; } =
			Enumerable.Empty<StudentOnboardingServiceHandoff>().ToLookup(item => item.OnboardingCaseId);
		public Dictionary<Guid, StudentIdentifierAllocation> ReservedIdentifiers { get; set; } = [];
		public Dictionary<Guid, string> ActorNames { get; set; } = [];
		public Dictionary<Guid, string> RollAssignmentModes { get; set; } = [];
	}

	private sealed record OnboardingListCredentialReadContext(
		ILookup<Guid, StudentCredentialEmailRequest> Requests,
		StudentSiblingParentService.ParentFlowReadContext ParentFlows);

	private async Task<OnboardingListCredentialReadContext> LoadListCredentialReadContextAsync(
		IReadOnlyCollection<StudentOnboardingCase> cases)
	{
		var caseIds = cases.Select(item => item.Id).Distinct().ToArray();
		var requests = await LazyServiceProvider.LazyGetRequiredService<Volo.Abp.Domain.Repositories.IRepository<StudentCredentialEmailRequest, Guid>>()
			.GetListAsync(item => Enumerable.Contains(caseIds, item.CaseId));
		var parentFlows = await SiblingParents.ReadParentFlowsAsync(cases);
		return new(requests.ToLookup(item => item.CaseId), parentFlows);
	}

	// Dependency IDs come exclusively from the already tenant/user-scoped, paged cases.
	private async Task<OnboardingListReadContext> LoadListReadContextAsync(
		List<StudentOnboardingCase> cases, IReadOnlyDictionary<Guid, OnboardingRuleConfiguration?> configurations)
	{
		// Only persisted input rows are shared. The current case's rules and markers
		// remain evaluated after capture and the readiness helper's original guards.
		var context = new OnboardingListReadContext
		{
			// Adoption saves the ambient UoW and can flush a caller's added intents.
			// Keep credential reads fresh for the whole page when any case can adopt.
			CredentialMetadata = cases.Any(AutomaticOnboardingReadinessRules.CanAdopt)
				? null : new(() => LoadListCredentialReadContextAsync(cases))
		};
		if (cases.Count == 0) return context;
		var caseIds = cases.Select(item => item.Id).Distinct().ToArray();
		var studentIds = cases.Select(item => item.StudentId).Distinct().ToArray();
		var applicationIds = cases.Select(item => item.ApplicationId).Distinct().ToArray();
		var offeringIds = cases.Select(item => item.OfferingId).Distinct().ToArray();
		var yearIds = cases.Select(item => item.AcademicYearId).Distinct().ToArray();
		var boardIds = cases.Select(item => item.IntendedBoardId).Distinct().ToArray();
		var mediumIds = cases.Select(item => item.IntendedMediumId).Distinct().ToArray();
		var standardIds = cases.Select(item => item.IntendedStandardId).Distinct().ToArray();
		var divisionIds = cases.Where(item => item.DivisionId.HasValue)
			.Select(item => item.DivisionId!.Value).Distinct().ToArray();

		context.Students = (await AsyncExecuter.ToListAsync((await studentRepository.GetQueryableWithoutAutoIncludesAsync())
			.Where(item => Enumerable.Contains(studentIds, item.Id))
			.Select(item => new OnboardingListStudentRead
			{
				Id = item.Id, FirstName = item.FirstName, MiddleName = item.MiddleName, LastName = item.LastName,
				GrNumber = item.GrNumber, GrNo = item.GrNo, ExtraProperties = item.ExtraProperties
			}))).ToDictionary(item => item.Id);
		context.Applications = (await applicationRepository.GetListAsync(item => Enumerable.Contains(applicationIds, item.Id)))
			.ToDictionary(item => item.Id);
		context.Offerings = (await offeringRepository.GetListAsync(item => Enumerable.Contains(offeringIds, item.Id)))
			.ToDictionary(item => item.Id);
		context.YearNames = (await AsyncExecuter.ToListAsync((await academicYearRepository.GetQueryableAsync())
			.Where(item => Enumerable.Contains(yearIds, item.Id)).Select(item => new { item.Id, item.Name })))
			.ToDictionary(item => item.Id, item => item.Name);
		var visibleYears = await academicYearRepository.GetQueryableAsync();
		var visibleBoards = await boardRepository.GetQueryableAsync();
		// Force only the former required parent chain, using its current repository
		// filters so an explicit ABP filter override has the same meaning as FindAsync.
		context.BoardNames = (await AsyncExecuter.ToListAsync(visibleBoards
			.Where(item => Enumerable.Contains(boardIds, item.Id) && visibleYears.Any(year => year.Id == item.AcademicYearId))
			.Select(item => new { item.Id, item.Type })))
			.ToDictionary(item => item.Id, item => item.Type.ToString());
		context.MediumNames = (await AsyncExecuter.ToListAsync((await mediumRepository.GetQueryableAsync())
			.Where(item => Enumerable.Contains(mediumIds, item.Id) && visibleYears.Any(year => year.Id == item.EducationBoard.AcademicYearId))
			.Select(item => new { item.Id, item.Name })))
			.ToDictionary(item => item.Id, item => item.Name);
		context.StandardNames = (await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
			.Where(item => Enumerable.Contains(standardIds, item.Id) && visibleBoards.Any(board =>
				board.Id == item.EducationMediumStage.EducationMedium.EducationBoardId))
			.Select(item => new { item.Id, item.Name })))
			.ToDictionary(item => item.Id, item => item.Name);
		if (divisionIds.Length > 0)
			context.Divisions = (await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
				.Where(item => Enumerable.Contains(divisionIds, item.Id))
				.Select(item => new OnboardingListDivisionRead
				{
					Id = item.Id, Name = item.Name, ExtraProperties = item.ExtraProperties
				}))).ToDictionary(item => item.Id);

		var steps = await stepRepository.GetListAsync(item => Enumerable.Contains(caseIds, item.OnboardingCaseId));
		context.Steps = steps.ToLookup(item => item.OnboardingCaseId);
		context.RequiredHandoffs = (await handoffRepository.GetListAsync(item =>
			Enumerable.Contains(caseIds, item.OnboardingCaseId) && item.Required)).ToLookup(item => item.OnboardingCaseId);
		var missingGrCaseIds = cases.Where(item =>
			!context.Students.TryGetValue(item.StudentId, out var student) ||
			string.IsNullOrWhiteSpace(student.GrNumber) && student.GrNo == null).Select(item => item.Id).ToArray();
		if (missingGrCaseIds.Length > 0)
			context.ReservedIdentifiers = (await allocationRepository.GetListAsync(item =>
				Enumerable.Contains(missingGrCaseIds, item.OnboardingCaseId) && item.Status == StudentIdentifierAllocationStatus.Reserved))
				.GroupBy(item => item.OnboardingCaseId).ToDictionary(group => group.Key, group => group.Single());
		context.ActorNames = await LazyServiceProvider.LazyGetRequiredService<StudentJourneyActorReader>()
			.ReadAsync(steps.Select(item => item.CompletedByUserId));
		foreach (var yearId in yearIds)
		{
			configurations.TryGetValue(yearId, out var configuration);
			context.RollAssignmentModes[yearId] = configuration == null ? "manual" :
				OnboardingRuleConfiguration.Text(configuration.Root.GetProperty("rollNumber"), "assignmentMode", "both");
		}
		return context;
	}
}
