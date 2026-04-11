using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Groups;

/// <summary>
/// Provides metadata about rule fields — which properties can be filtered,
/// which operators apply to each, and what selectable values exist.
/// Pure in-memory, no database calls.
/// </summary>
[RemoteService(false)]
public class RuleFieldsAppService : ApplicationService, IRuleFieldsAppService
{
    // Operators suitable for free-text string fields
    private static readonly List<RuleFieldOperatorDto> StringOperators = new()
    {
        new() { Name = nameof(RuleOperator.Equals), Value = (int)RuleOperator.Equals },
        new() { Name = nameof(RuleOperator.NotEquals), Value = (int)RuleOperator.NotEquals },
        new() { Name = nameof(RuleOperator.Contains), Value = (int)RuleOperator.Contains },
        new() { Name = nameof(RuleOperator.StartsWith), Value = (int)RuleOperator.StartsWith },
        new() { Name = nameof(RuleOperator.EndsWith), Value = (int)RuleOperator.EndsWith }
    };

    // Operators suitable for enum / select fields
    private static readonly List<RuleFieldOperatorDto> EnumOperators = new()
    {
        new() { Name = nameof(RuleOperator.Equals), Value = (int)RuleOperator.Equals },
        new() { Name = nameof(RuleOperator.NotEquals), Value = (int)RuleOperator.NotEquals },
        new() { Name = nameof(RuleOperator.In), Value = (int)RuleOperator.In }
    };

    /// <inheritdoc />
    public Task<List<RuleFieldDto>> GetStudentRuleFieldsAsync()
    {
        var fields = new List<RuleFieldDto>
        {
            // Gender — enum with Male / Female (from GenderEnum)
            new()
            {
                Field = nameof(StudentRuleField.Gender),
                FieldValue = (int)StudentRuleField.Gender,
                Name = "Gender",
                Type = nameof(RuleFieldType.Enum),
                TypeValue = (int)RuleFieldType.Enum,
                Operators = EnumOperators,
                Options = Enum.GetValues<GenderEnum>()
                    .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString(), IntValue = (int)v })
                    .ToList()
            },

            // Admission Status — enum with all StudentLifeCycleStatusEnum values
            new()
            {
                Field = nameof(StudentRuleField.AdmissionStatus),
                FieldValue = (int)StudentRuleField.AdmissionStatus,
                Name = "Admission Status",
                Type = nameof(RuleFieldType.Enum),
                TypeValue = (int)RuleFieldType.Enum,
                Operators = EnumOperators,
                Options = Enum.GetValues<StudentLifeCycleStatusEnum>()
                    .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString(), IntValue = (int)v })
                    .ToList()
            },

            // Name — free-text string
            new()
            {
                Field = nameof(StudentRuleField.Name),
                FieldValue = (int)StudentRuleField.Name,
                Name = "Name",
                Type = nameof(RuleFieldType.String),
                TypeValue = (int)RuleFieldType.String,
                Operators = StringOperators,
                Options = null
            }
        };

        return Task.FromResult(fields);
    }

    /// <inheritdoc />
    public Task<List<RuleFieldDto>> GetEmployeeRuleFieldsAsync()
    {
        var fields = new List<RuleFieldDto>
        {
            // Department — free-text string on Employee entity
            new()
            {
                Field = nameof(EmployeeRuleField.Department),
                FieldValue = (int)EmployeeRuleField.Department,
                Name = "Department",
                Type = nameof(RuleFieldType.String),
                TypeValue = (int)RuleFieldType.String,
                Operators = StringOperators,
                Options = null
            },

            // Employment Type — enum with Teaching / NonTeaching
            new()
            {
                Field = nameof(EmployeeRuleField.EmploymentType),
                FieldValue = (int)EmployeeRuleField.EmploymentType,
                Name = "Employment Type",
                Type = nameof(RuleFieldType.Enum),
                TypeValue = (int)RuleFieldType.Enum,
                Operators = EnumOperators,
                Options = Enum.GetValues<EmployeeGroupType>()
                    .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString(), IntValue = (int)v })
                    .ToList()
            },

            // Gender — enum with Male / Female / Other (from GenderType)
            new()
            {
                Field = nameof(EmployeeRuleField.Gender),
                FieldValue = (int)EmployeeRuleField.Gender,
                Name = "Gender",
                Type = nameof(RuleFieldType.Enum),
                TypeValue = (int)RuleFieldType.Enum,
                Operators = EnumOperators,
                Options = Enum.GetValues<GenderType>()
                    .Select(v => new RuleFieldOptionDto { Value = v.ToString(), Label = v.ToString(), IntValue = (int)v })
                    .ToList()
            },

            // Name — free-text string
            new()
            {
                Field = nameof(EmployeeRuleField.Name),
                FieldValue = (int)EmployeeRuleField.Name,
                Name = "Name",
                Type = nameof(RuleFieldType.String),
                TypeValue = (int)RuleFieldType.String,
                Operators = StringOperators,
                Options = null
            }
        };

        return Task.FromResult(fields);
    }
}
