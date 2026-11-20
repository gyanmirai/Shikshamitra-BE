namespace ShikshaMitra.Migrations;

public partial class AddFinanceExternalSourceFingerprintFunctions
{
	// Freeze the existing .NET 9 producer protocol. Generic PostgreSQL JSON
	// formatting does not reproduce its escaping or fixed envelope byte order.
	private const string InstallFingerprintFunctions = """
		CREATE FUNCTION public."AppFinanceJsonQuoteV1"(value text)
		RETURNS text LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE SET search_path = pg_catalog AS $quote_v1$
		DECLARE
		    result text := '"';
		    piece text;
		    code integer;
		    index integer;
		BEGIN
		    IF value IS NULL THEN RETURN 'null'; END IF;
		    FOR index IN 1..char_length(value) LOOP
		        piece := substr(value, index, 1);
		        code := ascii(piece);
		        IF code BETWEEN 32 AND 126 AND code NOT IN (34,38,39,43,60,62,92,96) THEN
		            result := result || piece;
		        ELSIF code = 92 THEN
		            result := result || chr(92) || chr(92);
		        ELSIF code IN (8,9,10,12,13) THEN
		            result := result || chr(92) || CASE code WHEN 8 THEN 'b' WHEN 9 THEN 't'
		                WHEN 10 THEN 'n' WHEN 12 THEN 'f' ELSE 'r' END;
		        ELSIF code > 65535 THEN
		            result := result || chr(92) || 'u' || upper(lpad(to_hex(55296 + (code-65536)/1024), 4, '0'))
		                || chr(92) || 'u' || upper(lpad(to_hex(56320 + (code-65536)%1024), 4, '0'));
		        ELSE
		            result := result || chr(92) || 'u' || upper(lpad(to_hex(code), 4, '0'));
		        END IF;
		    END LOOP;
		    RETURN result || '"';
		END;
		$quote_v1$;

		CREATE FUNCTION public."AppFinanceTrySourceFingerprintV1"(document text)
		RETURNS text LANGUAGE plpgsql IMMUTABLE PARALLEL SAFE SET search_path = pg_catalog AS $fingerprint_v1$
		DECLARE
		    s_tenant text := public."AppFinanceTryJsonScalar"(document, 'TenantId', 'string');
		    s_kind text := public."AppFinanceTryJsonScalar"(document, 'Source.Kind', 'number');
		    s_source text := public."AppFinanceTryJsonScalar"(document, 'Source.SourceId', 'string');
		    s_version text := public."AppFinanceTryJsonScalar"(document, 'Source.SourceVersion', 'string');
		    s_year text := public."AppFinanceTryJsonScalar"(document, 'AcademicYearId', 'string');
		    s_year_confirmed text := public."AppFinanceTryJsonScalar"(document, 'AcademicYearConfirmed', 'boolean');
		    s_year_evidence text := public."AppFinanceTryJsonScalar"(document, 'AcademicYearEvidenceJson', 'string');
		    s_role text := public."AppFinanceTryJsonScalar"(document, 'Payer.Kind', 'number');
		    s_student text := public."AppFinanceTryJsonScalar"(document, 'Payer.StudentId', 'string');
		    s_employee text := public."AppFinanceTryJsonScalar"(document, 'Payer.EmployeeId', 'string');
		    s_amount numeric := public."AppFinanceTryJsonMoney"(document, 'Amount');
		    s_currency text := public."AppFinanceTryJsonScalar"(document, 'Currency', 'string');
		    s_assessment text := public."AppFinanceTryJsonScalar"(document, 'AssessmentDate', 'string');
		    s_due text := public."AppFinanceTryJsonScalar"(document, 'DueDate', 'string');
		    s_dates_confirmed text := public."AppFinanceTryJsonScalar"(document, 'SchoolDatesConfirmed', 'boolean');
		    s_reason text := public."AppFinanceTryJsonScalar"(document, 'Reason', 'string');
		    s_evidence text := public."AppFinanceTryJsonScalar"(document, 'EvidenceReference', 'string');
		    identity_text text;
		    amount_text text;
		    v1_json text;
		BEGIN
		    IF s_tenant IS NULL OR s_kind IS DISTINCT FROM '1' OR s_source IS NULL OR s_version IS NULL
		        OR s_year IS NULL OR s_year_confirmed IS NULL OR s_year_confirmed NOT IN ('true','false')
		        OR s_year_evidence IS NULL OR s_role IS NULL OR s_role NOT IN ('1','2') OR s_amount IS NULL
		        OR s_currency IS NULL OR s_assessment IS NULL OR s_due IS NULL OR s_dates_confirmed IS NULL
		        OR s_dates_confirmed NOT IN ('true','false') OR s_reason IS NULL OR s_evidence IS NULL THEN
		        RETURN NULL;
		    END IF;
		    IF s_student IS NULL AND public."AppFinanceTryJsonScalar"(document, 'Payer.StudentId', 'null') IS DISTINCT FROM 'null'
		        OR s_employee IS NULL AND public."AppFinanceTryJsonScalar"(document, 'Payer.EmployeeId', 'null') IS DISTINCT FROM 'null' THEN
		        RETURN NULL;
		    END IF;
		    FOREACH identity_text IN ARRAY ARRAY[s_tenant,s_source,s_year,s_student,s_employee] LOOP
		        IF identity_text IS NOT NULL AND (identity_text COLLATE "C") !~
		            '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' THEN
		            RETURN NULL;
		        END IF;
		    END LOOP;
		    IF (s_assessment COLLATE "C") !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$'
		        OR (s_due COLLATE "C") !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN RETURN NULL; END IF;
		    PERFORM make_date(substr(s_assessment,1,4)::integer, substr(s_assessment,6,2)::integer, substr(s_assessment,9,2)::integer);
		    PERFORM make_date(substr(s_due,1,4)::integer, substr(s_due,6,2)::integer, substr(s_due,9,2)::integer);
		    amount_text := s_amount::text;
		    IF strpos(amount_text, '.') > 0 THEN amount_text := rtrim(rtrim(amount_text, '0'), '.'); END IF;
		    s_reason := btrim(s_reason, U&'\0009\000A\000B\000C\000D\0020\0085\00A0\1680\2000\2001\2002\2003\2004\2005\2006\2007\2008\2009\200A\2028\2029\202F\205F\3000');
		    v1_json := '{"Schema":1,"TenantId":' || public."AppFinanceJsonQuoteV1"(lower(s_tenant))
		        || ',"Kind":' || s_kind || ',"SourceId":' || public."AppFinanceJsonQuoteV1"(lower(s_source))
		        || ',"SourceVersion":' || public."AppFinanceJsonQuoteV1"(s_version)
		        || ',"AcademicYearId":' || public."AppFinanceJsonQuoteV1"(lower(s_year))
		        || ',"AcademicYearConfirmed":' || s_year_confirmed
		        || ',"AcademicYearEvidenceJson":' || public."AppFinanceJsonQuoteV1"(s_year_evidence)
		        || ',"PayerKind":' || s_role || ',"StudentId":' || public."AppFinanceJsonQuoteV1"(lower(s_student))
		        || ',"EmployeeId":' || public."AppFinanceJsonQuoteV1"(lower(s_employee))
		        || ',"Amount":' || public."AppFinanceJsonQuoteV1"(amount_text)
		        || ',"Currency":' || public."AppFinanceJsonQuoteV1"(s_currency)
		        || ',"AssessmentDate":' || public."AppFinanceJsonQuoteV1"(s_assessment)
		        || ',"DueDate":' || public."AppFinanceJsonQuoteV1"(s_due)
		        || ',"SchoolDatesConfirmed":' || s_dates_confirmed
		        || ',"Reason":' || public."AppFinanceJsonQuoteV1"(s_reason)
		        || ',"EvidenceReference":' || public."AppFinanceJsonQuoteV1"(s_evidence) || '}';
		    RETURN encode(sha256(convert_to(v1_json, 'UTF8')), 'hex');
		EXCEPTION WHEN data_exception THEN
		    RETURN NULL;
		END;
		$fingerprint_v1$;
		""";
}
