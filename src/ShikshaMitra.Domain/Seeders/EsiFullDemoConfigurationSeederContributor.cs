using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public sealed class EsiFullDemoConfigurationSeederContributor : BaseSeederContributor
{
	internal const string StepName = "EsiFullDemoConfigurationSeeder";
	internal const string StepVersion = "EsiFullDemoConfigurationSeeder:v1";
	internal const int StepOrder = 163;

	private readonly IEmployeeRepository _employeeRepository;
	private readonly IRepository<EarningComponent, Guid> _earningComponentRepository;
	private readonly IRepository<StatutoryRuleVersion, Guid> _ruleRepository;
	private readonly IRepository<StatutoryEstablishment, Guid> _establishmentRepository;
	private readonly IRepository<PayrollComponentStatutoryTreatment, Guid> _treatmentRepository;
	private readonly IRepository<EmployeeStatutoryEnrollment, Guid> _enrollmentRepository;
	private readonly IRepository<EmployeeCompensationRevision, Guid> _revisionRepository;
	private readonly IRepository<EmployeeCompensationRevisionLine, Guid> _revisionLineRepository;
	private readonly HRSeederContributor _hrSeederContributor;
	private readonly IUnitOfWorkManager _unitOfWorkManager;
	private readonly IClock _clock;
	private readonly ILogger<EsiFullDemoConfigurationSeederContributor> _logger;

	public EsiFullDemoConfigurationSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		IEmployeeRepository employeeRepository,
		IRepository<EarningComponent, Guid> earningComponentRepository,
		IRepository<StatutoryRuleVersion, Guid> ruleRepository,
		IRepository<StatutoryEstablishment, Guid> establishmentRepository,
		IRepository<PayrollComponentStatutoryTreatment, Guid> treatmentRepository,
		IRepository<EmployeeStatutoryEnrollment, Guid> enrollmentRepository,
		IRepository<EmployeeCompensationRevision, Guid> revisionRepository,
		IRepository<EmployeeCompensationRevisionLine, Guid> revisionLineRepository,
		HRSeederContributor hrSeederContributor,
		IUnitOfWorkManager unitOfWorkManager,
		IClock clock,
		ILogger<EsiFullDemoConfigurationSeederContributor> logger)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_employeeRepository = employeeRepository;
		_earningComponentRepository = earningComponentRepository;
		_ruleRepository = ruleRepository;
		_establishmentRepository = establishmentRepository;
		_treatmentRepository = treatmentRepository;
		_enrollmentRepository = enrollmentRepository;
		_revisionRepository = revisionRepository;
		_revisionLineRepository = revisionLineRepository;
		_hrSeederContributor = hrSeederContributor;
		_unitOfWorkManager = unitOfWorkManager;
		_clock = clock;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (CurrentTenant.Id is null)
		{
			return;
		}

		var existingEstablishment = await _establishmentRepository.FindAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
		if (existingEstablishment is not null && !EsiFullDemoSeedCatalog.IsSyntheticEstablishment(
				existingEstablishment.RegistrationNumber,
				existingEstablishment.EvidenceReference))
		{
			_logger.LogWarning(
				"[EsiFullDemoSeeder] Existing non-synthetic ESI establishment was preserved for TenantId={TenantId}.",
				CurrentTenant.Id);
			return;
		}

		var effectiveOn = _clock.Now.Date;
		var rule = await ResolveRuleAsync(effectiveOn);
		var employees = await GetSeededEmployeesAsync();
		if (employees.Count == 0)
		{
			throw new InvalidOperationException("FullDemo ESI configuration requires the seeded DDVS employees.");
		}

		var activeComponents = (await _earningComponentRepository.GetListAsync(x => x.IsActive))
			.OrderBy(x => x.DisplayOrder)
			.ThenBy(x => x.Name)
			.ToList();
		var unknownComponents = activeComponents
			.Where(x => !EsiFullDemoSeedCatalog.ComponentTreatments.ContainsKey(x.Name))
			.Select(x => x.Name)
			.ToList();
		if (unknownComponents.Count > 0)
		{
			throw new InvalidOperationException(
				$"FullDemo ESI wage treatment is missing for: {string.Join(", ", unknownComponents)}.");
		}

		await EnsureContractDescriptionsAsync(activeComponents);
		var treatmentMap = await EnsureComponentTreatmentsAsync(activeComponents, effectiveOn);
		if (_unitOfWorkManager.Current is not null)
		{
			await _unitOfWorkManager.Current.SaveChangesAsync();
		}
		if (!await HasApprovedRevisionForEveryActiveEmployeeAsync(employees, effectiveOn))
		{
			await _hrSeederContributor.EnsureFullDemoEmployeeStatusAndCompensationAsync();
			if (_unitOfWorkManager.Current is not null)
			{
				await _unitOfWorkManager.Current.SaveChangesAsync();
			}
			employees = await GetSeededEmployeesAsync();
		}
		await _hrSeederContributor.EnsureFullDemoInactiveEmployeeExitDatesAsync();
		if (_unitOfWorkManager.Current is not null)
		{
			await _unitOfWorkManager.Current.SaveChangesAsync();
		}
		employees = await GetSeededEmployeesAsync();
		var establishment = existingEstablishment ?? await CreateEstablishmentAsync(employees);
		await EnsureEmployeeEnrollmentsAsync(employees, rule, establishment, treatmentMap, effectiveOn);
		await CurrentUnitOfWork.SaveChangesAsync();

		_logger.LogInformation(
			"[EsiFullDemoSeeder] ESI configuration ready for TenantId={TenantId}; Employees={EmployeeCount}, Components={ComponentCount}, Status={Status}.",
			CurrentTenant.Id,
			employees.Count(x => x.EmployeeStatusType == EmployeeStatusType.Active),
			activeComponents.Count,
			establishment.Status);
	}

	private async Task EnsureContractDescriptionsAsync(IReadOnlyCollection<EarningComponent> components)
	{
		foreach (var component in components)
		{
			if (!EsiFullDemoSeedCatalog.ContractDescriptions.TryGetValue(component.Name, out var description) ||
				string.Equals(component.Description, description, StringComparison.Ordinal))
			{
				continue;
			}

			component.Update(
				component.Name,
				description,
				component.ComponentType,
				component.CalculationType,
				component.Amount,
				component.Percentage,
				component.SelectedEarningComponentId,
				component.DependOn,
				component.VisibleInPayslip,
				component.DisplayOrder);
			await _earningComponentRepository.UpdateAsync(component, autoSave: false);
		}
	}

	private async Task<StatutoryRuleVersion> ResolveRuleAsync(DateTime effectiveOn)
	{
		var rules = await _ruleRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance &&
			x.JurisdictionCode == EsiFullDemoSeedCatalog.JurisdictionCode &&
			x.IsActive &&
			x.EffectiveFrom <= effectiveOn &&
			(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveOn));
		return rules
			.OrderByDescending(x => x.EffectiveFrom)
			.ThenByDescending(x => x.Version)
			.FirstOrDefault()
			?? throw new InvalidOperationException("FullDemo ESI configuration requires an effective IN-GJ ESI rule.");
	}

	private async Task<List<Employee>> GetSeededEmployeesAsync()
	{
		var seededEmails = HRSeederContributor.BuildSuratSeedEmployeeProfiles().Keys
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		return (await _employeeRepository.GetListAsync())
			.Where(x => !string.IsNullOrWhiteSpace(x.Email) && seededEmails.Contains(x.Email))
			.OrderBy(x => x.EmployeeCode)
			.ToList();
	}

	private async Task<bool> HasApprovedRevisionForEveryActiveEmployeeAsync(
		IReadOnlyCollection<Employee> employees,
		DateTime effectiveOn)
	{
		var activeEmployeeIds = employees
			.Where(x => x.EmployeeStatusType == EmployeeStatusType.Active)
			.Select(x => x.Id)
			.ToHashSet();
		if (activeEmployeeIds.Count == 0)
		{
			return false;
		}

		var currentMonth = DateTime.SpecifyKind(
			new DateTime(effectiveOn.Year, effectiveOn.Month, 1),
			DateTimeKind.Utc);
		var approvedEmployeeIds = (await _revisionRepository.GetListAsync(x =>
				activeEmployeeIds.Contains(x.EmployeeId) &&
				x.Status == CompensationRevisionStatus.Approved &&
				x.EffectiveMonth <= currentMonth))
			.Select(x => x.EmployeeId)
			.ToHashSet();
		return activeEmployeeIds.SetEquals(approvedEmployeeIds);
	}

	private async Task<Dictionary<Guid, StatutoryWageTreatment>> EnsureComponentTreatmentsAsync(
		IReadOnlyCollection<EarningComponent> components,
		DateTime effectiveOn)
	{
		var existingTreatments = await _treatmentRepository.GetListAsync(x =>
			x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance);
		var result = new Dictionary<Guid, StatutoryWageTreatment>();
		foreach (var component in components)
		{
			var effectiveTreatment = existingTreatments
				.Where(x => x.ComponentId == component.Id &&
					x.EffectiveFrom <= effectiveOn &&
					(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveOn))
				.OrderByDescending(x => x.EffectiveFrom)
				.FirstOrDefault();
			if (effectiveTreatment is not null)
			{
				result[component.Id] = effectiveTreatment.Treatment;
				continue;
			}

			var treatment = EsiFullDemoSeedCatalog.ComponentTreatments[component.Name];
			await _treatmentRepository.InsertAsync(
				new PayrollComponentStatutoryTreatment(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					component.Id,
					StatutorySchemeCodes.EmployeeStateInsurance,
					treatment,
					EsiFullDemoSeedCatalog.RuleEffectiveFrom),
				autoSave: false);
			result[component.Id] = treatment;
		}

		return result;
	}

	private async Task<StatutoryEstablishment> CreateEstablishmentAsync(IReadOnlyCollection<Employee> employees)
	{
		var establishment = new StatutoryEstablishment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			StatutorySchemeCodes.EmployeeStateInsurance,
			EsiFullDemoSeedCatalog.JurisdictionCode);
		establishment.Configure(
			EsiFullDemoSeedCatalog.JurisdictionCode,
			EsiFullDemoSeedCatalog.SyntheticRegistrationNumber,
			employees.Count(x => x.EmployeeStatusType == EmployeeStatusType.Active),
			EsiFullDemoSeedCatalog.RuleEffectiveFrom,
			EsiFullDemoSeedCatalog.RuleEffectiveFrom,
			EsiFullDemoSeedCatalog.SyntheticEvidenceReference);
		await _establishmentRepository.InsertAsync(establishment, autoSave: false);
		return establishment;
	}

	private async Task EnsureEmployeeEnrollmentsAsync(
		IReadOnlyCollection<Employee> employees,
		StatutoryRuleVersion rule,
		StatutoryEstablishment establishment,
		IReadOnlyDictionary<Guid, StatutoryWageTreatment> treatmentMap,
		DateTime effectiveOn)
	{
		var employeeIds = employees.Select(x => x.Id).ToHashSet();
		var existingEnrollments = (await _enrollmentRepository.GetListAsync(x =>
				x.SchemeCode == StatutorySchemeCodes.EmployeeStateInsurance && employeeIds.Contains(x.EmployeeId)))
			.ToDictionary(x => x.EmployeeId);
		var currentMonth = DateTime.SpecifyKind(new DateTime(effectiveOn.Year, effectiveOn.Month, 1), DateTimeKind.Utc);
		var approvedRevisions = await _revisionRepository.GetListAsync(x =>
			employeeIds.Contains(x.EmployeeId) &&
			x.Status == CompensationRevisionStatus.Approved &&
			x.EffectiveMonth <= currentMonth);
		var revisionsByEmployee = approvedRevisions
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => x.OrderByDescending(item => item.EffectiveMonth)
					.ThenByDescending(item => item.RevisionNumber)
					.First());
		var missingRevisionEmployees = employees
			.Where(x => x.EmployeeStatusType == EmployeeStatusType.Active && !revisionsByEmployee.ContainsKey(x.Id))
			.Select(x => x.Email ?? x.EmployeeCode)
			.ToList();
		if (missingRevisionEmployees.Count > 0)
		{
			throw new InvalidOperationException(
				$"FullDemo ESI configuration requires effective salary setups for: {string.Join(", ", missingRevisionEmployees)}.");
		}

		var revisionIds = revisionsByEmployee.Values.Select(x => x.Id).ToHashSet();
		var revisionLines = revisionIds.Count == 0
			? []
			: await _revisionLineRepository.GetListAsync(x => revisionIds.Contains(x.RevisionId));
		var linesByRevision = revisionLines.ToLookup(x => x.RevisionId);

		foreach (var employee in employees)
		{
			if (existingEnrollments.ContainsKey(employee.Id))
			{
				continue;
			}

			var statutoryWages = 0m;
			if (employee.EmployeeStatusType == EmployeeStatusType.Active)
			{
				var revision = revisionsByEmployee[employee.Id];
				var wageLines = BuildWageLines(employee, linesByRevision[revision.Id], treatmentMap);
				var calculation = EsiStatutoryCalculator.Calculate(
					effectiveOn,
					rule,
					establishment,
					null,
					wageLines,
					assumeEstablishmentActive: true);
				statutoryWages = calculation.StatutoryWages;
			}

			var scenario = EsiFullDemoSeedCatalog.ResolveEnrollmentScenario(
				employee.Email,
				employee.EmployeeStatusType,
				statutoryWages,
				rule.StandardWageCeiling,
				effectiveOn);
			var enrollment = new EmployeeStatutoryEnrollment(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				employee.Id,
				StatutorySchemeCodes.EmployeeStateInsurance);
			enrollment.Update(
				scenario.Status,
				StatutoryWageCeilingCategory.Standard,
				scenario.InsuranceNumber,
				scenario.CoveredFrom,
				null,
				scenario.ContributionPeriodEnd,
				scenario.StatusReason);
			await _enrollmentRepository.InsertAsync(enrollment, autoSave: false);

			if (employee.EmployeeStatusType == EmployeeStatusType.Active)
			{
				var revision = revisionsByEmployee[employee.Id];
				var validation = EsiStatutoryCalculator.Calculate(
					effectiveOn,
					rule,
					establishment,
					enrollment,
					BuildWageLines(employee, linesByRevision[revision.Id], treatmentMap),
					assumeEstablishmentActive: true);
				if (validation.IssueCodes.Count > 0)
				{
					throw new InvalidOperationException(
						$"FullDemo ESI enrollment for '{employee.Email ?? employee.EmployeeCode}' is not preview-ready: {string.Join(", ", validation.IssueCodes)}.");
				}
			}
		}
	}

	private static List<StatutoryWageLine> BuildWageLines(
		Employee employee,
		IEnumerable<EmployeeCompensationRevisionLine> revisionLines,
		IReadOnlyDictionary<Guid, StatutoryWageTreatment> treatmentMap)
	{
		var wageLines = new List<StatutoryWageLine>();
		foreach (var line in revisionLines.Where(x => x.IsEarning))
		{
			if (!line.ComponentId.HasValue || !treatmentMap.TryGetValue(line.ComponentId.Value, out var treatment))
			{
				throw new InvalidOperationException(
					$"FullDemo ESI wage treatment is missing for compensation line '{line.Name}' on employee '{employee.Email ?? employee.EmployeeCode}'.");
			}

			wageLines.Add(new StatutoryWageLine(line.ComponentId.Value, line.Name, line.Amount, treatment));
		}

		if (wageLines.Count == 0)
		{
			throw new InvalidOperationException(
				$"FullDemo ESI configuration requires earning lines for employee '{employee.Email ?? employee.EmployeeCode}'.");
		}

		return wageLines;
	}
}
