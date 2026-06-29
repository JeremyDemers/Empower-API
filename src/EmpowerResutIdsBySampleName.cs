using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerResutIdsBySampleName
    {
        public static List<Dictionary<string, object>> FetchData(string oracleConnectionString, string sampleName, int year, DatabaseService databaseService)
        {
            var schemas = databaseService.GetSchemasByYear(year);

            List<Dictionary<string, object>> combinedData = new List<Dictionary<string, object>>();

            foreach (var schema in schemas)
            {
                combinedData.AddRange(FetchResutIdsForSampleName(oracleConnectionString, sampleName, schema));
            }

            return combinedData;
        }

        private static List<Dictionary<string, object>> FetchResutIdsForSampleName(string connectionString, string sampleName, string schema)
        {
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            var query = $@"
            SELECT 
	            v.ID AS VIAL_ID,
	            v.SAMPLENAME,
	            r.ID AS RESULT_ID
            FROM 
	            W_DAAPI_10_OCT_000.VIAL v,
	            W_DAAPI_10_OCT_000.""RESULT"" r 
            WHERE 
	            v.SAMPLENAME = '{sampleName}'
                AND r.VIAL_ID = v.ID";

            using var cmd = new OracleCommand(query, conn);
            var data = new List<Dictionary<string, object>>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var dict = new Dictionary<string, object>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        dict.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                    }
                    data.Add(dict);
                }
            }

            return data;
        }
    }
}
