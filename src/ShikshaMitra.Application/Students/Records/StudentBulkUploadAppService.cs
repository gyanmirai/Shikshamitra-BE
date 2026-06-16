using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
[RemoteService(false)]
public class StudentBulkUploadAppService(
	IStudentGeneralInfoAppService studentGeneralInfoAppService,
	IRepository<EducationBoard, Guid> educationBoardRepository,
	IRepository<EducationMedium, Guid> educationMediumRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository)
	: ApplicationService, IStudentBulkUploadAppService, ITransientDependency
{
	private static readonly string[] SupportedDateFormats =
	[
		"dd-MM-yyyy",
		"yyyy-MM-dd",
		"dd/MM/yyyy",
		"MM/dd/yyyy"
	];

	public async Task<StudentBulkUploadResultDto> UploadAsync(StudentBulkUploadFileDto input)
	{
		if (!input.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException("SM:StudentBulkUploadCsvOnly")
				.WithData("FileName", input.FileName);
		}

		if (input.Content.Length == 0)
		{
			throw new BusinessException("SM:StudentBulkUploadEmptyFile");
		}

		var rows = ParseCsv(input.Content);
		var result = new StudentBulkUploadResultDto { TotalRows = rows.Count };
		if (rows.Count == 0)
		{
			return result;
		}

		var lookup = await LoadAcademicLookupAsync();
		foreach (var row in rows)
		{
			try
			{
				var dto = BuildRegistrationDto(row, lookup);
				var created = await studentGeneralInfoAppService.CreateDirectRegistrationAsync(dto);
				result.StudentIds.Add(created.StudentId);
				result.ImportedCount++;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Failed to import student bulk upload row {RowNumber}", row.RowNumber);
				result.Errors.Add(new StudentBulkUploadErrorDto
				{
					RowNumber = row.RowNumber,
					Message = GetSafeRowErrorMessage(ex)
				});
			}
		}

		return result;
	}

	private async Task<AcademicLookup> LoadAcademicLookupAsync()
	{
		var boards = await AsyncExecuter.ToListAsync(
			(await educationBoardRepository.GetQueryableAsync()).Select(x => new AcademicBoardRow(x.Id, x.Type.ToString())));
		var mediums = await AsyncExecuter.ToListAsync(
			(await educationMediumRepository.GetQueryableAsync()).Select(x => new AcademicMediumRow(x.Id, x.Name, x.EducationBoardId)));
		var stages = await AsyncExecuter.ToListAsync(
			(await educationMediumStageRepository.GetQueryableAsync()).Select(x => new AcademicStageRow(x.Id, x.Name, x.EducationMediumId)));
		var standards = await AsyncExecuter.ToListAsync(
			(await standardRepository.GetQueryableAsync()).Select(x => new AcademicStandardRow(x.Id, x.Name, x.EducationMediumStageId)));
		var divisions = await AsyncExecuter.ToListAsync(
			(await divisionRepository.GetQueryableAsync()).Select(x => new AcademicDivisionRow(x.Id, x.Name, x.StandardId)));

		return new AcademicLookup(boards, mediums, stages, standards, divisions);
	}

	private static CreateDirectRegistrationDto BuildRegistrationDto(StudentBulkUploadRow row, AcademicLookup lookup)
	{
		var standard = ResolveStandard(row, lookup);
		var stage = lookup.Stages.Single(x => x.Id == standard.EducationMediumStageId);
		var divisionId = ResolveDivisionId(row, lookup, standard.Id);

		return new CreateDirectRegistrationDto
		{
			AcademicMediumId = stage.EducationMediumId,
			StandardId = standard.Id,
			DivisionId = divisionId,
			FormNumber = Required(row, "FormNumber"),
			FirstName = Required(row, "FirstName"),
			MiddleName = Optional(row, "MiddleName"),
			LastName = Required(row, "LastName"),
			DateOfBirth = ParseDate(Required(row, "DateOfBirth"), row.RowNumber),
			Gender = ParseGender(Required(row, "Gender"), row.RowNumber),
			Email = Optional(row, "Email"),
			PrimaryContactNo = Required(row, "ParentContactNumber", "PrimaryContactNo"),
			SecondaryContactNo = Optional(row, "AlternativeContactNumber", "SecondaryContactNo"),
			Address1 = Optional(row, "AddressLine1"),
			Address2 = Optional(row, "AddressLine2"),
			Address3 = Optional(row, "AddressLine3"),
			Country = new Country(
				Optional(row, "CountryName") ?? "India",
				Optional(row, "CountryIsoCode") ?? "IN"),
			State = new State(
				Required(row, "StateName"),
				Optional(row, "StateIsoCode") ?? Required(row, "StateName")),
			City = Required(row, "City"),
			Pincode = Optional(row, "PinCode", "Pincode"),
			AadharNo = Optional(row, "AadhaarNo", "AadharNo"),
			FamilyCast = Optional(row, "FamilyCast") ?? "General",
			Nationality = Optional(row, "Nationality"),
			BirthPlace = Optional(row, "BirthPlace"),
			PreviousSchool = Optional(row, "PreviousSchoolName"),
			LastClassPassed = Optional(row, "LastClassPassed")
		};
	}

	private static AcademicStandardRow ResolveStandard(StudentBulkUploadRow row, AcademicLookup lookup)
	{
		var standardToken = Required(row, "StandardId", "Class");
		var candidates = Guid.TryParse(standardToken, out var standardId)
			? lookup.Standards.Where(x => x.Id == standardId)
			: lookup.Standards.Where(x => Same(x.Name, standardToken));

		var stageToken = Optional(row, "EducationMediumStageId", "Stage", "Organization");
		if (!string.IsNullOrWhiteSpace(stageToken))
		{
			var stageIds = ResolveIds(stageToken, lookup.Stages.Select(x => (x.Id, x.Name)));
			candidates = candidates.Where(x => stageIds.Contains(x.EducationMediumStageId));
		}

		var mediumToken = Optional(row, "EducationMediumId", "AcademicMediumId", "Medium");
		if (!string.IsNullOrWhiteSpace(mediumToken))
		{
			var mediumIds = ResolveIds(mediumToken, lookup.Mediums.Select(x => (x.Id, x.Name)));
			var stageIds = lookup.Stages
				.Where(x => mediumIds.Contains(x.EducationMediumId))
				.Select(x => x.Id)
				.ToHashSet();
			candidates = candidates.Where(x => stageIds.Contains(x.EducationMediumStageId));
		}

		var boardToken = Optional(row, "EducationBoardId", "Board");
		if (!string.IsNullOrWhiteSpace(boardToken))
		{
			var boardIds = ResolveIds(boardToken, lookup.Boards.Select(x => (x.Id, x.Name)));
			var mediumIds = lookup.Mediums
				.Where(x => boardIds.Contains(x.EducationBoardId))
				.Select(x => x.Id)
				.ToHashSet();
			var stageIds = lookup.Stages
				.Where(x => mediumIds.Contains(x.EducationMediumId))
				.Select(x => x.Id)
				.ToHashSet();
			candidates = candidates.Where(x => stageIds.Contains(x.EducationMediumStageId));
		}

		var matches = candidates.ToList();
		if (matches.Count == 1)
		{
			return matches[0];
		}

		throw new BusinessException("SM:StudentBulkUploadInvalidStandard")
			.WithData("RowNumber", row.RowNumber)
			.WithData("Standard", standardToken);
	}

	private static Guid? ResolveDivisionId(StudentBulkUploadRow row, AcademicLookup lookup, Guid standardId)
	{
		var divisionToken = Optional(row, "DivisionId", "Division");
		if (string.IsNullOrWhiteSpace(divisionToken))
		{
			return null;
		}

		var candidates = Guid.TryParse(divisionToken, out var divisionId)
			? lookup.Divisions.Where(x => x.Id == divisionId)
			: lookup.Divisions.Where(x => Same(x.Name, divisionToken));
		var matches = candidates.Where(x => x.StandardId == standardId).ToList();
		if (matches.Count == 1)
		{
			return matches[0].Id;
		}

		throw new BusinessException("SM:StudentBulkUploadInvalidDivision")
			.WithData("RowNumber", row.RowNumber)
			.WithData("Division", divisionToken);
	}

	private static HashSet<Guid> ResolveIds(
		string token,
		IEnumerable<(Guid Id, string Name)> values)
	{
		if (Guid.TryParse(token, out var id))
		{
			return [id];
		}

		return values
			.Where(x => Same(x.Name, token))
			.Select(x => x.Id)
			.ToHashSet();
	}

	private static List<StudentBulkUploadRow> ParseCsv(byte[] content)
	{
		var text = Encoding.UTF8.GetString(content);
		var lines = text
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace('\r', '\n')
			.Split('\n')
			.Where(line => !string.IsNullOrWhiteSpace(line))
			.ToList();
		if (lines.Count == 0)
		{
			return [];
		}

		var headers = ParseCsvLine(lines[0])
			.Select(header => header.Trim().TrimStart('\ufeff'))
			.ToList();
		return lines
			.Skip(1)
			.Select((line, index) => new StudentBulkUploadRow(
				index + 2,
				headers
					.Zip(ParseCsvLine(line), (header, value) => new { Header = header, Value = value })
					.ToDictionary(x => x.Header, x => x.Value, StringComparer.OrdinalIgnoreCase)))
			.ToList();
	}

	private static List<string> ParseCsvLine(string line)
	{
		var values = new List<string>();
		var current = new StringBuilder();
		var inQuotes = false;

		for (var index = 0; index < line.Length; index++)
		{
			var c = line[index];
			if (c == '"' && inQuotes && index + 1 < line.Length && line[index + 1] == '"')
			{
				current.Append('"');
				index++;
				continue;
			}

			if (c == '"')
			{
				inQuotes = !inQuotes;
				continue;
			}

			if (c == ',' && !inQuotes)
			{
				values.Add(current.ToString().Trim());
				current.Clear();
				continue;
			}

			current.Append(c);
		}

		values.Add(current.ToString().Trim());
		return values;
	}

	private static DateTimeOffset ParseDate(string value, int rowNumber)
	{
		if (DateTimeOffset.TryParseExact(
			    value,
			    SupportedDateFormats,
			    CultureInfo.InvariantCulture,
			    DateTimeStyles.AssumeLocal,
			    out var parsed))
		{
			return parsed;
		}

		throw new BusinessException("SM:StudentBulkUploadInvalidDate")
			.WithData("RowNumber", rowNumber)
			.WithData("Value", value);
	}

	private static GenderEnum ParseGender(string value, int rowNumber)
	{
		if (Same(value, "M") || Same(value, "Male"))
		{
			return GenderEnum.Male;
		}

		if (Same(value, "F") || Same(value, "Female"))
		{
			return GenderEnum.Female;
		}

		throw new BusinessException("SM:StudentBulkUploadInvalidGender")
			.WithData("RowNumber", rowNumber)
			.WithData("Value", value);
	}

	private static string Required(StudentBulkUploadRow row, params string[] names) =>
		Optional(row, names) ??
		throw new BusinessException("SM:StudentBulkUploadMissingRequiredField")
			.WithData("RowNumber", row.RowNumber)
			.WithData("Field", names[0]);

	private static string? Optional(StudentBulkUploadRow row, params string[] names)
	{
		foreach (var name in names)
		{
			if (row.Values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
			{
				return value.Trim();
			}
		}

		return null;
	}

	private static string GetSafeRowErrorMessage(Exception exception) =>
		exception is BusinessException or UserFriendlyException
			? exception.Message
			: "Unable to import this row. Please verify the row data and try again.";

	private static bool Same(string left, string right) =>
		string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

	private sealed record StudentBulkUploadRow(
		int RowNumber,
		IReadOnlyDictionary<string, string> Values);

	private sealed record AcademicLookup(
		List<AcademicBoardRow> Boards,
		List<AcademicMediumRow> Mediums,
		List<AcademicStageRow> Stages,
		List<AcademicStandardRow> Standards,
		List<AcademicDivisionRow> Divisions);

	private sealed record AcademicBoardRow(Guid Id, string Name);
	private sealed record AcademicMediumRow(Guid Id, string Name, Guid EducationBoardId);
	private sealed record AcademicStageRow(Guid Id, string Name, Guid EducationMediumId);
	private sealed record AcademicStandardRow(Guid Id, string Name, Guid EducationMediumStageId);
	private sealed record AcademicDivisionRow(Guid Id, string Name, Guid StandardId);
}
