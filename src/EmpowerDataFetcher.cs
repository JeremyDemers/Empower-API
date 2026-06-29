using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api
{
    public static class EmpowerDataFetcher
    {
        public static List<Dictionary<string, object>> FetchTableData(string connectionString, List<(string ssId, string rsetId)> idList, DatabaseService databaseService, int year)
        {
            var schemas = databaseService.GetSchemasByYear(year);
            var combinedData = new List<Dictionary<string, object>>();

            foreach (var schema in schemas)
            {
                combinedData.AddRange(FetchDataForSchema(connectionString, idList, schema));
            }

            return combinedData;
        }

        private static List<Dictionary<string, object>> FetchDataForSchema(string connectionString, List<(string ssId, string rsetId)> idList, string schema)
        {
            using var conn = new OracleConnection(connectionString);
            conn.Open();

            var ssIdInClause = new List<string>();
            var rsetIdInClause = new List<string>();

            for (int i = 0; i < idList.Count; i++)
            {
                ssIdInClause.Add($":ssId{i}");
                rsetIdInClause.Add($":rsetId{i}");
            }

            var ssIdInClauseString = string.Join(", ", ssIdInClause);
            var rsetIdInClauseString = string.Join(", ", rsetIdInClause);

            var query = $@"
                SELECT
                    SS.ID SAMPLESET_ID,
                    SS.ACQUIRED_BY$ USERID,
                    -- Attempt to convert SS.DATED and return NULL if the format is incorrect
                     CASE 
                         WHEN SS.DATED IS NOT NULL THEN TO_CHAR(SS.DATED, 'YYYY-MM-DD HH24:MI:SS TZD')
                         ELSE NULL 
                     END AS SAMPLESET_START,
                    INJ.ID INJECTION_ID,
                    INJ.RUNTIME,
                    -- Attempt to convert INJ.DATED and return NULL if the format is incorrect
                     CASE 
                         WHEN INJ.DATED IS NOT NULL THEN TO_CHAR(INJ.DATED, 'YYYY-MM-DD HH24:MI:SS TZD')
                         ELSE NULL
                     END AS INJECTION_START,
                    RSET.ID RESULTSET_ID,
                    -- Attempt to convert RSET.DATED and return NULL if the format is incorrect
                    CASE 
                        WHEN RSET.DATED IS NOT NULL THEN TO_CHAR(RSET.DATED, 'YYYY-MM-DD HH24:MI:SS TZD') 
                        ELSE NULL 
                    END AS RESULTSET_DATE,
                    RES.ID RESULT_ID,
                    RES.TOTAL_AREA,
                    VIAL.EXPERIMENTID_,
                    VIAL.VIAL HPLC_POSITION,
                    VIAL.PROJECT_COMPOUND_ID_,
                    VIAL.SAMPLENAME,
                    PEK.POS_INDEX PEAK_POS_INDEX,
                    PEK.NAME PEAK_NAME,
                    PEK.RET_TIME RETENTION_TIME,
                    PEK.AREA PEAK_AREA,
                    PEK.PC_AREA,
                    PEK.INT_TYPE,
	                PEK.ASSAY1_,
	                PEK.IMPURITY_PFE_,
                    CONF.NAME INSTRUMENT,
                    CASE 
                        WHEN REGEXP_LIKE(SUBSTR(VIAL.SAMPLENAME, 12, 2), '^[0-9]*$') THEN 
                            TO_NUMBER(SUBSTR(VIAL.SAMPLENAME, 12, 2)) 
                        ELSE 
                            NULL 
                    END AS TEMP,
                    CASE 
                        WHEN REGEXP_LIKE(SUBSTR(VIAL.SAMPLENAME, 14, 2), '^[0-9]*$') THEN 
                            TO_NUMBER(SUBSTR(VIAL.SAMPLENAME, 14, 2)) 
                        ELSE 
                            NULL 
                    END AS RH,
                    CASE 
                        WHEN REGEXP_LIKE(SUBSTR(VIAL.SAMPLENAME, 17, 1), '^[0-9]*$') THEN 
                            TO_NUMBER(SUBSTR(VIAL.SAMPLENAME, 17, 1)) 
                        ELSE 
                            NULL 
                    END AS REP,
                    CASE 
                        WHEN REGEXP_LIKE(SUBSTR(VIAL.SAMPLENAME, 8, 3), '^[0-9]*$') THEN 
                            TO_NUMBER(SUBSTR(VIAL.SAMPLENAME, 8, 3)) 
                        ELSE 
                            NULL 
                    END AS TIMEINDAYS,
	                SUBSTR (VIAL.SAMPLENAME,1,1)|| SUBSTR (VIAL.SAMPLENAME,4,3) FORMULATION,
	                SUBSTR (VIAL.SAMPLENAME,12,2)|| 'C' || SUBSTR (VIAL.SAMPLENAME,14,2)|| 'RH' STRESS
                FROM
                    {schema}.SAMPLESET SS,
                    {schema}.CHROMSYSCONFIG CONF,
                    {schema}.INJECTION INJ,
                    {schema}.VIAL,
                    {schema}.RESULTSET RSET,
                    {schema}.RESULT RES,
                    {schema}.PEAK PEK
                WHERE
                    SS.ID = INJ.SS_ID
                    AND VIAL.SAMPLENAME NOT LIKE '%Blank%'
                    AND VIAL.SAMPLENAME NOT LIKE '%Filt.Blank%'
                    AND VIAL.SAMPLENAME NOT LIKE '%Filt. Blank%'
                    AND VIAL.SAMPLENAME NOT IN ('LOQ', 'STD1', 'STD2')
	                AND PEK.NAME NOT LIKE 'Dil%'
	                AND PEK.NAME NOT LIKE '%Dead%'
	                AND PEK.NAME NOT LIKE 'Solven%'
                    AND SS.CONFIG_ID = CONF.ID
                    AND SS.ID = RSET.SS_ID
                    AND SS.ID = RES.SS_ID
                    AND RSET.ID = RES.RS_ID
                    AND INJ.VIAL_ID = VIAL.ID
                    AND INJ.ID = RES.INJ_ID
                    AND RES.ID = PEK.RES_ID
                    AND SS.ID IN ({ssIdInClauseString})
                    AND RSET.ID IN ({rsetIdInClauseString})
                ORDER BY
                    INJ.ID
                ";


            using var cmd = new OracleCommand(query, conn);

            for (int i = 0; i < idList.Count; i++)
            {
                cmd.Parameters.Add(new OracleParameter(ssIdInClause[i], idList[i].ssId));
                cmd.Parameters.Add(new OracleParameter(rsetIdInClause[i], idList[i].rsetId));
            }

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
                    // Add the schema name to each row
                    dict.Add("SCHEMA", schema);
                    data.Add(dict);
                }
            }

            return data;
        }
    }
}
