using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerDataBySampleName
    {
        public static List<Dictionary<string, object>> FetchData(string oracleConnectionString, string sampleName, int year, DatabaseService databaseService)
        {
            var schemas = databaseService.GetSchemasByYear(year);

            List<Dictionary<string, object>> combinedData = new List<Dictionary<string, object>>();

            foreach (var schema in schemas)
            {
                combinedData.AddRange(FetchDataForSampleName(oracleConnectionString, sampleName, schema));
            }

            return combinedData;
        }

        private static List<Dictionary<string, object>> FetchDataForSampleName(string connectionString, string sampleName, string schema)
        {
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            var query = $@"
            SELECT 
	            v.ID AS vial,
	            v.SAMPLENAME,
	            r.ID AS RESULT_ID,
	            r.DATED AS RESULT_DATED,
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
	           {schema}.VIAL v
            JOIN
	            {schema}.RESULT r
	            ON r.VIAL_ID = v.ID
            JOIN
	            W_DAAPI_10_OCT_000.PEAK p
	            ON p.RES_ID = r.ID  
            WHERE 
	            v.SAMPLENAME = '{sampleName}'";

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
