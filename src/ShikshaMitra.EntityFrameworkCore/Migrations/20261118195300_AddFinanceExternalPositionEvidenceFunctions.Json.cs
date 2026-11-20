namespace ShikshaMitra.Migrations;

public partial class AddFinanceExternalPositionEvidenceFunctions
{
	// Versioned migration SQL: later changes must use a new migration.
	private const string InstallEvidenceFunctions = """
		CREATE FUNCTION public."AppFinanceTryJsonScalar"(document text, property_path text, expected_kind text)
		RETURNS text LANGUAGE plpgsql IMMUTABLE STRICT AS $function$
		DECLARE
		    current_value json;
		    path_keys text[];
		    path_key text;
		    key_count integer;
		BEGIN
		    path_keys := string_to_array(property_path, '.');
		    IF property_path = '' OR cardinality(path_keys) > 8 THEN RETURN NULL; END IF;
		    current_value := document::json;
		    FOREACH path_key IN ARRAY path_keys LOOP
		        IF json_typeof(current_value) IS DISTINCT FROM 'object' THEN RETURN NULL; END IF;
		        SELECT count(*) INTO key_count FROM json_each(current_value) AS pair
		            WHERE pair.key COLLATE "C" = path_key COLLATE "C";
		        IF key_count <> 1 THEN RETURN NULL; END IF;
		        current_value := current_value -> path_key;
		    END LOOP;
		    IF json_typeof(current_value) IS DISTINCT FROM expected_kind THEN RETURN NULL; END IF;
		    IF expected_kind = 'null' THEN RETURN 'null'; END IF;
		    IF expected_kind NOT IN ('string', 'number', 'boolean') THEN RETURN NULL; END IF;
		    RETURN current_value #>> '{}';
		EXCEPTION WHEN data_exception THEN RETURN NULL;
		END;
		$function$;

		CREATE FUNCTION public."AppFinanceTryJsonMoney"(document text, property_path text)
		RETURNS numeric LANGUAGE plpgsql IMMUTABLE STRICT AS $function$
		DECLARE
		    value_text text;
		    amount numeric;
		BEGIN
		    value_text := public."AppFinanceTryJsonScalar"(document, property_path, 'number');
		    IF value_text IS NULL OR value_text !~ '^(0|[1-9][0-9]{0,15})(\.[0-9]{1,28})?$' THEN RETURN NULL; END IF;
		    amount := value_text::numeric;
		    IF amount > 9999999999999999.99 OR amount <> trunc(amount, 2) THEN RETURN NULL; END IF;
		    RETURN amount;
		EXCEPTION WHEN data_exception THEN RETURN NULL;
		END;
		$function$;
		""";
}
