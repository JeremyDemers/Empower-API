using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerDataByResultId
    {
        public static List<Dictionary<string, object>> FetchData(string oracleConnectionString, int resultId, int year, DatabaseService databaseService)
        {
            var schemas = databaseService.GetSchemasByYear(year);

            List<Dictionary<string, object>> combinedData = new List<Dictionary<string, object>>();

            foreach (var schema in schemas)
            {
                combinedData.AddRange(FetchDataForResultId(oracleConnectionString, resultId, schema));
            }

            return combinedData;
        }

        private static List<Dictionary<string, object>> FetchDataForResultId(string connectionString, int resultId, string schema)
        {
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            var query = $@"
            SELECT 
	            p.RES_ID AS PEAK_RES_ID,
	            p.NAME AS PEAK_NAME,
	            p.POS_INDEX AS PEAK_POS_INDEX,
	            p.""TYPE"" AS PEAK_TYPE,
	            p.RET_TIME AS PEAK_RET_TIME,
	            p.MAX_TIME AS PEAK_MAX_TIME,
	            p.REL_RET_TIME AS PEAK_REL_RET_TIME,
	            p.START_TIME AS PEAK_START_TIME,
	            p.END_TIME  AS PEAK_END_TIME,
	            p.BSLN_START AS PEAK_BSLN_START,
	            p.BSLN_END AS PEAK_BSLN_END,
	            p.AREA AS PEAK_AREA,
	            p.HEIGHT AS PEAK_HEIGHT
            FROM 
                {schema}.PEAK p
            JOIN
                {schema}.RESULT r
            ON 
                p.RES_ID = r.ID
            WHERE
                 p.RES_ID = :resultId";

            using var cmd = new OracleCommand(query, conn);
            cmd.Parameters.Add(new OracleParameter("resultId", resultId));
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
