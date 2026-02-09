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
    private static readonly List<string> StringOperators = new()
    {
        nameof(RuleOperator.Equals),
        nameof(RuleOperator.NotEquals),
        nameof(RuleOperator.Contains),
        nameof(RuleOperator.StartsWith),
        nameof(RuleOperator.EndsWith)
    };

    // Operators suitable for enum / select fields
    private static readonly List<string> EnumOperators = new()
    {
        nameof(RuleOperator.Equals),
        nameof(RuleOperator.NotEquals),
        nameof(RuleOperator.In)
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
                Name = "Gender",
                Type = nameof(RuleFieldType.Enum),
                Operators = EnumOperators,
                Options = Enum.GetNames<GenderEnum>()
                    .Select(v => new RuleFieldOptionDto { Value = v, Label = v })
                    .ToList()
            },

            // Admission Status — enum with all StudentLifeCycleStatusEnum values
            new()
            {
                Field = nameof(StudentRuleField.AdmissionStatus),
                Name = "Admission Status",
                Type = nameof(RuleFieldType.Enum),
                Operators = EnumOperators,
                Options = Enum.GetNames<StudentLifeCycleStatusEnum>()
                    .Select(v => new RuleFieldOptionDto { Value = v, Label = v })
                    .ToList()
            },

            // Name — free-text string
            new()
            {
                Field = nameof(StudentRuleField.Name),
                Name = "Name",
                Type = nameof(RuleFieldType.String),
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
                Name = "Department",
                Type = nameof(RuleFieldType.String),
                Operators = StringOperators,
                Options = null
            },

            // Employment Type — enum with Teaching / NonTeaching
            new()
            {
                Field = nameof(EmployeeRuleField.EmploymentType),
                Name = "Employment Type",
                Type = nameof(RuleFieldType.Enum),
                Operators = EnumOperators,
                Options = Enum.GetNames<EmployeeGroupType>()
                    .Select(v => new RuleFieldOptionDto { Value = v, Label = v })
                    .ToList()
            },

            // Gender — enum with Male / Female / Other (from GenderType)
            new()
            {
                Field = nameof(EmployeeRuleField.Gender),
                Name = "Gender",
                Type = nameof(RuleFieldType.Enum),
                Operators = EnumOperators,
                Options = Enum.GetNames<GenderType>()
                    .Select(v => new RuleFieldOptionDto { Value = v, Label = v })
                    .ToList()
            },

            // Name — free-text string
            new()
            {
                Field = nameof(EmployeeRuleField.Name),
                Name = "Name",
                Type = nameof(RuleFieldType.String),
                Operators = StringOperators,
                Options = null
            }
        };

        return Task.FromResult(fields);
    }
}
