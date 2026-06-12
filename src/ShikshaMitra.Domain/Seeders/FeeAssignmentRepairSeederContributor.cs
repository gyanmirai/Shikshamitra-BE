using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Idempotent repair pass for fee assignment data gaps (missing FeeStructureDetails,
/// missing StudentFeeStructure rows, missing DueFees). Runs even when fee receipts already exist.
/// </summary>
public class FeeAssignmentRepairSeederContributor : BaseSeederContributor
{
	private readonly IRepository<FeeStructure, Guid> _feeStructureRepository;
	private readonly IRepository<FeeStructureDetail, Guid> _feeStructureDetailRepository;
	private readonly IRepository<FeeStructureMetadata, Guid> _feeStructureMetadataRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IRepository<StudentFeeStructure, Guid> _studentFeeStructureRepository;
	private readonly IDueFeeRepository _dueFeeRepository;
	private readonly DueFeeManager _dueFeeManager;
	private readonly ILogger<FeeAssignmentRepairSeederContributor> _logger;

	public FeeAssignmentRepairSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		IRepository<FeeStructure, Guid> feeStructureRepository,
		IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
		IRepository<FeeStructureMetadata, Guid> feeStructureMetadataRepository,
		IRepository<AcademicYear, Guid> academicYearRepository,
		IRepository<Student, Guid> studentRepository,
		IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
		IDueFeeRepository dueFeeRepository,
		DueFeeManager dueFeeManager,
		ILogger<FeeAssignmentRepairSeederContributor> logger)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_feeStructureRepository = feeStructureRepository;
		_feeStructureDetailRepository = feeStructureDetailRepository;
		_feeStructureMetadataRepository = feeStructureMetadataRepository;
		_academicYearRepository = academicYearRepository;
		_studentRepository = studentRepository;
		_studentFeeStructureRepository = studentFeeStructureRepository;
		_dueFeeRepository = dueFeeRepository;
		_dueFeeManager = dueFeeManager;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		_logger.LogInformation(
			"[FeeAssignmentRepairSeeder] Starting repair. TenantId={TenantId}",
			CurrentTenant.Id);

		await RepairFeeStructureDetailsFromDueFeesAsync();
		await RepairStudentAssignmentsAndDueFeesAsync();

		await CurrentUnitOfWork.SaveChangesAsync();

		_logger.LogInformation(
			"[FeeAssignmentRepairSeeder] Repair completed. TenantId={TenantId}",
			CurrentTenant.Id);
	}

	private async Task RepairFeeStructureDetailsFromDueFeesAsync()
	{
		var structures = await _feeStructureRepository.GetListAsync();
		foreach (var structure in structures)
		{
			if (await _feeStructureDetailRepository.CountAsync(d => d.FeeStructureId == structure.Id) > 0)
				continue;

			var assignmentStudentIds = (await _studentFeeStructureRepository.GetListAsync(
					sfs => sfs.FeeStructureStandardId == structure.Id))
				.Select(sfs => sfs.StudentId)
				.ToList();

			var studentsWithFeeStructureId = (await _studentRepository.GetListAsync(
					s => s.FeeStructureId == structure.Id))
				.Select(s => s.Id)
				.ToList();

			var studentIds = assignmentStudentIds
				.Union(studentsWithFeeStructureId)
				.Distinct()
				.ToList();

			if (studentIds.Count == 0)
				continue;

			var dueFees = await _dueFeeRepository.GetListAsync(d => studentIds.Contains(d.StudentId));
			if (dueFees.Count == 0)
				continue;

			foreach (var group in dueFees.GroupBy(d => d.FeeHeadId))
			{
				var lines = group.ToList();
				var isOneTime = lines.All(l => l.IsOneTime);
				var amount = lines.Sum(l => l.OriginalAmount);

				await _feeStructureDetailRepository.InsertAsync(new FeeStructureDetail(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					structure.Id,
					group.Key,
					amount,
					isOneTime,
					isOptional: false,
					payableMonth: isOneTime ? lines[0].Month : null,
					payableYear: isOneTime ? lines[0].Year : null), autoSave: false);

				_logger.LogInformation(
					"[FeeAssignmentRepairSeeder] Added FeeStructureDetail for structure {StructureId}, fee head {FeeHeadId}.",
					structure.Id,
					group.Key);
			}
		}
	}

	private async Task RepairStudentAssignmentsAndDueFeesAsync()
	{
		var students = await _studentRepository.GetListAsync(s => s.FeeStructureId != null);
		foreach (var student in students)
		{
			var feeStructureId = student.FeeStructureId!.Value;
			var assignment = await _studentFeeStructureRepository.FirstOrDefaultAsync(
				sfs => sfs.StudentId == student.Id);

			if (assignment == null)
			{
				await _studentFeeStructureRepository.InsertAsync(
					new StudentFeeStructure(
						GuidGenerator.Create(),
						CurrentTenant.Id,
						student.Id,
						feeStructureId),
					autoSave: false);
			}

			if (await _dueFeeRepository.ExistsForStudentAsync(student.Id))
				continue;

			var details = await _feeStructureDetailRepository.GetListAsync(
				d => d.FeeStructureId == feeStructureId);
			if (details.Count == 0)
				continue;

			var structure = await _feeStructureRepository.GetAsync(feeStructureId);
			foreach (var detail in details.Where(d => structure.FeeStructureDetails.All(x => x.Id != d.Id)))
				structure.AddDetail(detail);

			var metadata = await _feeStructureMetadataRepository.GetAsync(structure.FeeStructureMetadataId);
			if (metadata.AcademicYearId == null)
				continue;

			var academicYear = await _academicYearRepository.GetAsync(metadata.AcademicYearId.Value);
			var dueFees = await _dueFeeManager.GenerateForStudentAsync(
				student.Id,
				structure,
				academicYear,
				Array.Empty<Guid>());

			foreach (var dueFee in dueFees)
				await _dueFeeRepository.InsertAsync(dueFee, autoSave: false);

			_logger.LogInformation(
				"[FeeAssignmentRepairSeeder] Generated {Count} DueFees for student {StudentId}.",
				dueFees.Count,
				student.Id);
		}
	}
}
