using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance.Fees.Charges;

internal static class ChargeAudienceMapper
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static ChargeAudience ToDomain(ChargeAudienceDto dto)
	{
		return new ChargeAudience(
			new AcademicScope(
				dto.Scope.BoardId,
				dto.Scope.EducationMediumId,
				dto.Scope.ClassIds,
				dto.Scope.DivisionIds),
			new StudentSelection(dto.Selection.Mode, dto.Selection.StudentIds));
	}

	public static ChargeAudienceDto ToDto(ChargeAudience audience)
	{
		return new ChargeAudienceDto
		{
			Scope = new AcademicScopeDto
			{
				BoardId = audience.Scope.BoardId,
				EducationMediumId = audience.Scope.EducationMediumId,
				ClassIds = audience.Scope.ClassIds.ToList(),
				DivisionIds = audience.Scope.DivisionIds?.ToList()
			},
			Selection = new StudentSelectionDto
			{
				Mode = audience.Selection.Mode,
				StudentIds = audience.Selection.StudentIds?.ToList()
			}
		};
	}

	public static string SerializeSnapshot(ChargeAudienceDto dto)
		=> JsonSerializer.Serialize(dto, JsonOptions);

	public static ChargeAudienceDto DeserializeSnapshot(string json)
		=> JsonSerializer.Deserialize<ChargeAudienceDto>(json, JsonOptions)
			?? new ChargeAudienceDto();

	public static ChargeAudienceDto BuildLegacyAudience(Student student, Guid studentId)
	{
		return new ChargeAudienceDto
		{
			Scope = new AcademicScopeDto
			{
				BoardId = student.AcademicBoardId ?? Guid.Empty,
				EducationMediumId = student.AcademicMediumId,
				ClassIds = student.StandardId.HasValue ? [student.StandardId.Value] : [],
				DivisionIds = student.DivisionId.HasValue ? [student.DivisionId.Value] : null
			},
			Selection = new StudentSelectionDto
			{
				Mode = StudentSelectionMode.Explicit,
				StudentIds = [studentId]
			}
		};
	}
}
