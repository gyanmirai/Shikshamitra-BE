using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureMetadataAppService
{
	private static readonly IReadOnlyDictionary<string, LambdaExpression> MetadataDisplayFields = new Dictionary<string, LambdaExpression>
		{
			["name"] = (Expression<Func<FeeStructureDetailsResponse, string>>)(x => x.Name),
			["boardMediumValue"] = (Expression<Func<FeeStructureDetailsResponse, string>>)(x => (x.BoardName ?? "") + "/" + (x.MediumName ?? "")),
			["totalClassValue"] = (Expression<Func<FeeStructureDetailsResponse, string>>)(x => x.ConfiguredClasses + "/" + x.TotalClasses)
		};
	private static readonly SettingsTableFilter<FeeStructureDetailsResponse> ListFilter = new(MetadataDisplayFields);

	private static readonly IReadOnlyDictionary<string, LambdaExpression> MetadataSqlFields = new Dictionary<string, LambdaExpression>
	{
		["name"] = (Expression<Func<FeeStructureMetadata, string?>>)(x => x.Name)
	};
}
