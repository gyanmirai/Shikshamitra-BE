using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Common.Header;

public static class HeaderValidationExtractionHelper
{
    public static (Guid academicYearId, Guid academicBoardId, Guid academicMediumId) ValidateAndExtractHeaders(IHeaderDictionary headers)
		{
        if (!headers.ContainsKey("academicYearId") || StringValues.IsNullOrEmpty(headers["academicYearId"]))
        {
            throw new ApplicationException("The 'academicYearId' header is missing or empty.");
        }

        if (!headers.ContainsKey("academicBoardId") || StringValues.IsNullOrEmpty(headers["academicBoardId"]))
        {
            throw new ApplicationException("The 'academicBoardId' header is missing or empty.");
        }

        if (!headers.ContainsKey("academicMediumId") || StringValues.IsNullOrEmpty(headers["academicMediumId"]))
        {
            throw new ApplicationException("The 'academicMediumId' header is missing or empty.");
        }

        // Fix for CS8604: Ensure the value is not null or empty before parsing  
        string academicYearIdValue = headers["academicYearId"].ToString();
        string academicBoardIdValue = headers["academicBoardId"].ToString();
        string academicMediumIdValue = headers["academicMediumId"].ToString();

        if (string.IsNullOrWhiteSpace(academicYearIdValue) || string.IsNullOrWhiteSpace(academicBoardIdValue) || string.IsNullOrWhiteSpace(academicMediumIdValue))
        {
            throw new ApplicationException("One or more required headers are empty.");
        }

        Guid academicYearId = Guid.Parse(academicYearIdValue);
        Guid academicBoardId = Guid.Parse(academicBoardIdValue);
        Guid academicMediumId = Guid.Parse(academicMediumIdValue);

        return (academicYearId, academicBoardId, academicMediumId);
    }
}
