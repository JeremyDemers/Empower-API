using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerDataByTimeRange
    {
        public static List<Dictionary<string, object>> FetchData(string oracleConnectionString, int interval, string period, DatabaseService databaseService)
        {
            List<Dictionary<string, object>> combinedData = new List<Dictionary<string, object>>();
            
            var schemas = databaseService.GetSchemasWithHasDataTrue(); // Retrieve the list of schemas from the SQLite database where HasData is 1

            foreach (var schema in schemas)
            {
                combinedData.AddRange(FetchDataForSchema(oracleConnectionString, interval, period, schema));
            }

            return combinedData;
        }

        private static List<Dictionary<string, object>> FetchDataForSchema(string connectionString, int interval, string period, string schema)
        {
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            string oraclePeriod = period.ToLower() switch
            {
                "second" => "SECOND",
                "minute" => "MINUTE",
                "day" => "DAY",
                "month" => "MONTH",
                _ => throw new ArgumentException("Invalid period parameter")
            };

            string intervalExpr = $"INTERVAL '{interval}' {oraclePeriod}";

            var query = $@"
            SELECT
                '{schema}' AS Schema_Name,
                rs.ID, 
                rs.SS_ID, 
                rs.SSMETH_NAME, 
                rs.DATED,  
                u.FULLNAME, 
                u.LDAPNAME
            FROM 
                {schema}.RESULTSET rs
            JOIN 
                {schema}.SAMPLESET s ON rs.SS_ID = s.ID
            JOIN 
                MILLENNIUM.USERINFO u ON s.ACQUIRED_BY$ = u.NAME
            WHERE 
                TO_TIMESTAMP_TZ(rs.DATED, 'DD-MON-RR HH.MI.SS AM TZR') BETWEEN (SYSTIMESTAMP - {intervalExpr}) AND SYSTIMESTAMP
            ORDER BY 
                TO_TIMESTAMP_TZ(rs.DATED, 'DD-MON-RR HH.MI.SS AM TZR') DESC
            ";

            using var cmd = new OracleCommand(query, conn);
            var data = new List<Dictionary<string, object>>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var dict = new Dictionary<string, object>();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var columnName = reader.GetName(i);
                        var columnValue = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        dict.Add(columnName, columnValue);
                    }
                    data.Add(dict);
                }
            }

            return data;
        }
    }
}
