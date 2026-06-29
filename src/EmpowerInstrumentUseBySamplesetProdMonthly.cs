using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerInstrumentUseBySamplesetProdMonthly
    {
        public static List<Dictionary<string, object>> FetchInstrumentsUseBySampleset(string connectionString, DatabaseService databaseService, int year, int month)
        {
            var schemas = databaseService.GetSchemasByYear(year);
            var combinedData = new List<Dictionary<string, object>>();

            foreach (var schema in schemas)
            {
                if (SchemaHasTables(connectionString, schema))
                {
                    combinedData.AddRange(FetchDataForSchema(connectionString, schema, year, month));
                }
            }

            return combinedData;
        }

        private static bool SchemaHasTables(string connectionString, string schema)
        {
            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM all_tables WHERE owner = '{schema}'";
                var tableCount = Convert.ToInt32(command.ExecuteScalar());
                return tableCount > 0;
            }
        }

        private static List<Dictionary<string, object>> FetchDataForSchema(string connectionString, string schema, int year, int month)
        {
            var data = new List<Dictionary<string, object>>();
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            var query = $@"
        SELECT
            CC.NAME,
            CC.NODE,
            CC.COMMENTS,
            CC.""LOCATION$"" as LOCATION,
            SS.CONFIG_ID AS INSTRUMENT,
            SS.ID, 
            SS.SSMETH_ID, 
            SS.SSMETH_NAME, 
            SS.""ACQUIRED_BY$"" as ACQUIRED_BY, 
            TO_CHAR(SS.DATED, 'YYYY-MM-DD""T""HH24:MI:SS') AS START_DATE,
            TO_CHAR(SS.FINISHED, 'YYYY-MM-DD""T""HH24:MI:SS') AS END_DATE, 
            CC.ID as CHROM_ID
        FROM
            {schema}.SAMPLESET SS,
            {schema}.CHROMSYSCONFIG CC
        WHERE 
            SS.CONFIG_ID = CC.ID
            AND EXTRACT(YEAR FROM SS.DATED) = :year
            AND EXTRACT(MONTH FROM SS.DATED) = :month
        ORDER BY 
            CC.NAME
    ";
            using (var cmd = new OracleCommand(query, conn))
            {
                cmd.Parameters.Add(new OracleParameter("year", year));
                cmd.Parameters.Add(new OracleParameter("month", month));

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dict = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var key = reader.GetName(i);
                            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            if (!dict.ContainsKey(key))  // Check if the key already exists
                            {
                                dict.Add(key, value);
                            }
                            else
                            {
                                dict[key] = value; // Update the value if key already exists
                            }
                        }
                        data.Add(dict);
                    }
                }
            }

            return data;
        }
    }
}
