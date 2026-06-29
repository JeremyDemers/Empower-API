using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace smart_lab_empower_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpowerDataController : ControllerBase
    {
        private readonly string _connectionString;
        private readonly DatabaseService _databaseService;

        public EmpowerDataController(IConfiguration configuration, DatabaseService databaseService)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection");
            _databaseService = databaseService; // Initialize DatabaseService
        }

        /// <summary>
        /// Retrieves Empower 3 project details based on given sampleset ids, resultset ids, and year.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/empower-data?pairs=ssId1,rsetId1;ssId2,rsetId2;ssId3,rsetId380&amp;year=2023
        ///     
        /// Some verified pairs are 1924,2175;2295,2507. Do not forget the semicolon between pairs and include the year!
        ///
        /// Each sampleset ID (ssId) must be paired with a corresponding resultset ID (rsetId).
        /// The year parameter influences schema selection for the query.
        /// 
        /// Example response:
        ///<code>
        ///[
        /// {
        ///   "SAMPLESET_ID": 1924,
        ///   "USERID": "WangMJ",
        ///   "SAMPLESET_START": "2023-01-20 14:42:29 EST",
        ///   "INJECTION_ID": 1532,
        ///   "RUNTIME": 32,
        ///   "INJECTION_START": "2023-01-20 14:43:23 EST",
        ///   "RESULTSET_ID": 2175,
        ///   "RESULTSET_DATE": "2023-01-22 14:32:14 EST",
        ///   "RESULT_ID": 2186,
        ///   "TOTAL_AREA": 106501.9859483975,
        ///   "EXPERIMENTID_": "PF-06651622",
        ///   "HPLC_POSITION": "1:A,1",
        ///   "PROJECT_COMPOUND_ID_": "PF-06651622-11",
        ///   "SAMPLENAME": "Diluent",
        ///   "PEAK_POS_INDEX": 0,
        ///   "PEAK_NAME": "PF-06715222",
        ///   "RETENTION_TIME": 2.174,
        ///   "PEAK_AREA": null,
        ///   "PC_AREA": null,
        ///   "INT_TYPE": "Missing",
        ///   "INSTRUMENT": "GRO349",
        ///   "TEMP": null,
        ///   "RH": null,
        ///   "REP": null,
        ///   "TIMEINDAYS": null,
        ///   "FORMULATION": "Kendall",
        ///   "STRESS": "CRH"
        ///  }
        ///] 
        /// </code>
        /// </remarks>
        /// <param name="pairs">A string of pairs of sampleset and resultset IDs separated by semicolons.</param>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <response code="200">Returns the project details as a list of dictionaries.</response>
        /// <response code="400">If the input format is incorrect, a required ID pair is missing, or the year is not provided.</response>
        /// <response code="404">If no data is found for the provided ID pairs and year.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("empower-data")]
        public IActionResult GetEmpowerData([FromQuery] string pairs, [FromQuery] int year)
        {
            if (string.IsNullOrEmpty(pairs))
            {
                return BadRequest("The pairs parameter is required and cannot be empty.");
            }

            if (year <= 0)
            {
                return BadRequest("A valid year is required.");
            }

            var idPairs = pairs.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var idList = new List<(string ssId, string rsetId)>();

            foreach (var pair in idPairs)
            {
                var ids = pair.Split(',');
                if (ids.Length != 2)
                {
                    return BadRequest("Each SampleSet ID must be paired with a ResultSet ID. Format: ssId1,rsetId1;ssId2,rsetId2;...");
                }
                idList.Add((ssId: ids[0].Trim(), rsetId: ids[1].Trim()));
            }

            try
            {
                var data = EmpowerDataFetcher.FetchTableData(_connectionString, idList, _databaseService, year);
                if (data.Count == 0)
                {
                    return NotFound("No data found for the provided ID pairs and year.");
                }
                return Ok(data);
            }
            catch (OracleException ex)
            {
                // Log the exception (add logging as necessary)
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Retrieves data based on a time range specified by the interval and period.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/EmpowerData/data-by-time-range?interval=80&amp;period=day
        ///     
        /// </remarks>
        /// <param name="interval">The time interval number.</param>
        /// <param name="period">The time period (second, minute, day, month).</param>
        /// <response code="200">Returns the resultset id, sampleset id, sampleset method id, resultset date, fullname of the user, and the users email.</response>
        /// <response code="400">If the input parameters are incorrect or missing.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("data-by-time-range")]
        public IActionResult GetDataByTimeRange(int interval, string period)
        {
            if (interval <= 0)
            {
                return BadRequest("Interval must be a positive number.");
            }

            var allowedPeriods = new HashSet<string> { "second", "minute", "day", "month" };
            if (!allowedPeriods.Contains(period.ToLower()))
            {
                return BadRequest("Invalid period parameter. Allowed values are 'second', 'minute', 'day', 'MONTH'.");
            }

            try
            {
                // Use DatabaseService instance
                var data = EmpowerDataByTimeRange.FetchData(_connectionString, interval, period, _databaseService);
                if (data.Count == 0)
                {
                    return NotFound("No data found for the specified time range.");
                }
                return Ok(data);
            }
            catch (OracleException ex)
            {
                // Log the exception (add logging as necessary)
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        /// <summary>
        /// Retrieves result Ids for a given sample name across schemas based on the specified year.
        /// </summary>
        /// <remarks>
        /// Fetches JSON with result Ids matching the sample name in schemas relevant to the specified year:
        /// <code>
        ///    [
        ///      {
        ///        "VIAL_ID": 16311,
        ///        "SAMPLENAME": "702824-389-05",
        ///        "RESULT_ID": 17850
        ///      },
        ///      {
        ///        "VIAL_ID": 16311,
        ///        "SAMPLENAME": "702824-389-05",
        ///        "RESULT_ID": 17851
        ///      }
        ///    ]
        /// </code>
        /// Sample request:
        /// 
        ///     GET /api/EmpowerData/result-ids-from-sample-name?sampleName=702824-389-05&amp;year=2023
        ///     
        /// </remarks>
        /// <param name="sampleName">The sample name to search for.</param>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <returns>A JSON object of result Id entries matching the sample name across selected schemas.</returns>
        /// <response code="200">Successfully found and returns the data.</response>
        /// <response code="400">If the required parameters are missing or invalid.</response>
        /// <response code="404">If no data is found for the given parameters.</response>
        /// <response code="500">If an error occurs during the processing of the request.</response>
        [HttpGet("result-ids-from-sample-name")]
        public IActionResult GetResultIdsBySampleName([FromQuery] string sampleName, [FromQuery] int year)
        {
            if (string.IsNullOrEmpty(sampleName) || year <= 0)
            {
                return BadRequest("Both sample name (sampleName) and year (year) parameters are required.");
            }

            try
            {
                var data = EmpowerResutIdsBySampleName.FetchData(_connectionString, sampleName, year, _databaseService);
                if (data.Count == 0)
                {
                    return NotFound("No data found for the given sample name and year.");
                }
                return Ok(data);
            }
            catch (Exception ex)
            {
                // Log the exception as necessary
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }

        /// <summary>
        /// Retrieves data for a given sample name across schemas based on the specified year.
        /// </summary>
        /// <remarks>
        /// Fetches data matching the sample name in schemas relevant to the specified year.
        /// Sample request:
        /// 
        ///     GET /api/EmpowerData/data-by-sample-name?sampleName=702824-389-05&amp;year=2023
        ///     
        /// </remarks>
        /// <param name="sampleName">The sample name to search for.</param>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <returns>A JSON object of data entries matching the sample name across selected schemas.</returns>
        /// <response code="200">Successfully found and returns the data.</response>
        /// <response code="400">If the required parameters are missing or invalid.</response>
        /// <response code="404">If no data is found for the given parameters.</response>
        /// <response code="500">If an error occurs during the processing of the request.</response>
        [HttpGet("data-by-sample-name")]
        public IActionResult GetDataBySampleName([FromQuery] string sampleName, [FromQuery] int year)
        {
            if (string.IsNullOrEmpty(sampleName) || year <= 0)
            {
                return BadRequest("Both sample name (sampleName) and year (year) parameters are required.");
            }

            try
            {
                var data = EmpowerDataBySampleName.FetchData(_connectionString, sampleName, year, _databaseService);
                if (data.Count == 0)
                {
                    return NotFound("No data found for the given sample name and year.");
                }
                return Ok(data);
            }
            catch (Exception ex)
            {
                // Log the exception as necessary
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
        /// <summary>
        /// Retrieves peak data for a result id across schemas based on the specified year.
        /// </summary>
        /// <remarks>
        /// Fetches peak data matching the result id in schemas relevant to the specified year.
        /// Sample request:
        /// 
        ///     GET /api/EmpowerData/data-by-result-id?resultId=5813&amp;yr=2023
        ///     
        /// </remarks>
        /// <param name="resultId">The result id to search for.</param>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <returns>A list of peak data entries matching the result id across selected schemas.</returns>
        /// <response code="200">Successfully found and returns the data.</response>
        /// <response code="400">If the required parameters are missing or invalid.</response>
        /// <response code="404">If no data is found for the given parameters.</response>
        /// <response code="500">If an error occurs during the processing of the request.</response>
        [HttpGet("data-by-result-id")]
        public IActionResult GetDataByResultId([FromQuery] int resultId, [FromQuery] int year)
        {
            if (resultId <= 0 || year <= 0)
            {
                return BadRequest("Both result Id (resultId) and year (year) parameters are required.");
            }

            try
            {
                var data = EmpowerDataByResultId.FetchData(_connectionString, resultId, year, _databaseService);
                if (data.Count == 0)
                {
                    return NotFound("No peak data found for the given result id and year.");
                }
                return Ok(data);
            }
            catch (Exception ex)
            {
                // Log the exception as necessary
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
        /// <summary>
        /// Retrieves peak data for a result id or multiple result ids across schemas based on the specified year.
        /// </summary>
        /// <remarks>
        /// Fetches peak data matching the result id in schemas relevant to the specified year.
        /// Sample request:
        /// 
        ///     GET /api/EmpowerData/data-by-multiple-result-ids?resultId=17850,17851,17994&amp;year=2023
        ///     
        /// </remarks>
        /// <param name="resultId">The result id or multiple ids separated by commas to search peak data on.</param>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <returns>A json object of peak data entries matching the result(s) id across selected schemas.</returns>
        /// <response code="200">Successfully found and returns the data.</response>
        /// <response code="400">If the required parameters are missing or invalid.</response>
        /// <response code="404">If no data is found for the given parameters.</response>
        /// <response code="500">If an error occurs during the processing of the request.</response>
        [HttpGet("data-by-multiple-result-ids")]
        public IActionResult GetDataByMultipleResultIds([FromQuery] string resultId, [FromQuery] int year)
        {
            if (string.IsNullOrEmpty(resultId) || year <= 0)
            {
                return BadRequest("Both resultId and year parameters are required and must be valid.");
            }


            try
            {
                var data = EmpowerDataByMultipleResultIds.FetchData(_connectionString, resultId, year, _databaseService);
                if (data.Count == 0)
                {
                    return NotFound("No peak data found for the given result id and year.");
                }
                return Ok(data);
            }
            catch (Exception ex)
            {
                // Log the exception as necessary
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
        /// <summary>
        /// Retrieves lab instruments in use based on the specified year.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/EmpowerData/instruments-in-use?year=2024
        ///     
        /// <code>
        /// [
        ///   {
        ///     "USERID": "KRTFY",
        ///     "INSTRUMENT": "GRO468",
        ///     "SERVER": "Amrgrobemp2084",
        ///     "COMMENTS": "GRO / 220 / 342 / AGILENT / 1260",
        ///     "SAMPLE_SET_ID": 279939,
        ///     "INJ_DATE": "2024-07-31T00:00:00",
        ///     "SINCE_LAST": 8.64,
        ///     "NEXT_INJ": 1.27
        ///   },
        ///   {
        ///     "USERID": "VANHAGG",
        ///     "INSTRUMENT": "GRO073",
        ///     "SERVER": "Amrgrobemp2037",
        ///     "COMMENTS": "GRO / 220 / 246 / WATERS / UPLC / SM, BSM, PDA, CM, ELSD",
        ///     "SAMPLE_SET_ID": 56930,
        ///     "INJ_DATE": "2024-07-31T00:00:00",
        ///     "SINCE_LAST": 1.44,
        ///     "NEXT_INJ": 10.47
        ///   }
        /// ]
        /// 
        /// </code>
        ///     
        /// </remarks>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <response code="200">Returns the instrument usage data as a list of dictionaries.</response>
        /// <response code="400">If the input parameters are incorrect or missing.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("instruments-in-use")]
        public IActionResult GetInstrumentsInUse([FromQuery] int year)
        {
            if (year <= 0)
            {
                return BadRequest("A valid year is required.");
            }

            try
            {
                var data = EmpowerInstrumentsInUse.FetchInstrumentsInUse(_connectionString, _databaseService, year);
                if (data.Count == 0)
                {
                    return Ok(data);
                }
                return Ok(data);
            }
            catch (OracleException ex)
            {
                // Log the exception (add logging as necessary)
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        /// <summary>
        /// Retrieves all lab instruments based on the specified year.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/EmpowerData/all-instruments?year=2024
        ///     
        /// <code>
        ///    [
        ///     {
        ///       "INSTRUMENT": "GRO309",
        ///       "NODE": "Amrgrobemp2056",
        ///       "COMMENTS": "GRO / 220 / 342 / WATERS / UPLC / FTN, PDA, QSM"
        ///     },
        ///     {
        ///       "INSTRUMENT": "GRO514",
        ///       "NODE": "Amrgrobemp2090",
        ///       "COMMENTS": "GRO / 220 / 342 / AGILENT / 1290"
        ///     },
        ///     {
        ///       "INSTRUMENT": "GRO448",
        ///       "NODE": "Amrgrobemp2009",
        ///       "COMMENTS": "GRO / 220/ 457 / AGILENT / 1260"
        ///     },
        ///     {
        ///       "INSTRUMENT": "GRO407",
        ///       "NODE": "Amrgrobemp2019",
        ///       "COMMENTS": "GRO / 220 / 244B / WATERS / UPLC2 / SM, BSM, CCM, CM, PDA, ISM, QDA"
        ///     }
        ///    ]
        /// </code>
        ///     
        /// </remarks>
        /// <param name="year">The year which influences schema selection for the query in format YYYY.</param>
        /// <response code="200">Returns the list of all instruments as a list of dictionaries.</response>
        /// <response code="400">If the input parameters are incorrect or missing.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("all-instruments")]
        public IActionResult GetAllInstruments([FromQuery] int year)
        {
            if (year <= 0)
            {
                return BadRequest("A valid year is required.");
            }

            try
            {
                var data = EmpowerFetchAllInstruments.FetchAllInstruments(_connectionString, _databaseService, year);
                if (data.Count == 0)
                {
                    return NotFound("No instruments found for the provided year.");
                }
                return Ok(data);
            }
            catch (OracleException ex)
            {
                // Log the exception (add logging as necessary)
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        }
        /// <summary>
        /// Retrieves instrument use by samplesets based on the specified year.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///   
        ///     GET /api/EmpowerData/instrument_use-by-sampleset_year?year=2024
        /// 
        /// <code>
        /// [
        ///  {
        ///     "NAME": "GRO143",
        ///     "NODE": "Amrgrobemp2051",
        ///     "COMMENTS": "DRO",
        ///     "LOCATION": "GRO",
        ///     "INSTRUMENT": 65387,
        ///     "ID": 83532,
        ///     "SSMETH_ID": 83531,
        ///     "SSMETH_NAME": "MD_00710775_0554_GRO143_2",
        ///     "ACQUIRED_BY": "DEMBOM",
        ///     "START_DATE": "2024-09-05T11:10:00",
        ///     "END_DATE": "2024-09-05T13:12:05",
        ///     "CHROM_ID": 65387
        ///   },
        ///   {
        ///     "NAME": "GRO303_QDa",
        ///     "NODE": "Amrgrobemp2061",
        ///     "COMMENTS": "GRO",
        ///     "LOCATION": "GRO",
        ///     "INSTRUMENT": 10082,
        ///     "ID": 59121,
        ///     "SSMETH_ID": 59120,
        ///     "SSMETH_NAME": "!QuickSet",
        ///     "ACQUIRED_BY": "VANHAIJ",
        ///     "START_DATE": "2024-08-05T09:56:06",
        ///     "END_DATE": "2024-08-05T11:53:36",
        ///     "CHROM_ID": 10082
        ///   }
        /// ]
        /// </code>
        /// 
        /// </remarks>
        /// <param year="year">The year which influences schema selection for the query.</param>
        /// <returns>Returns a list of instruments usage details for given samplesets.</returns>
        /// <response code="200">Returns the list of all instruments as a list of dictionaries.</response> 
        /// <response code="400">If the input parameters are incorrect or missing.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("instrument_use-by-sampleset-year")]
        public IActionResult GetInstrumentsBySamplesetProd([FromQuery] int year)
        {
            if (year <= 0)
            {
                return BadRequest("A valid year is required.");
            }

            try
            {
                var data = EmpowerInstrumentUseBySamplesetProd.FetchInstrumentsUseBySampleset(_connectionString, _databaseService, year);
                if (data.Count == 0)
                {
                    return NotFound("No data found for the provided year.");
                }
                return Ok(data);
            }
            catch (OracleException ex)
            {
                // Log the exception as necessary
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
        /// <summary>
        /// Retrieves instrument use by samplesets based on the specified year and month.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///   
        ///     GET /api/EmpowerData/instrument_use-by-sampleset-year-and-month?year=2024&amp;month=5
        /// 
        /// <code>
        /// [
        ///  {
        ///     "NAME": "GRO143",
        ///     "NODE": "Amrgrobemp2051",
        ///     "COMMENTS": "DRO",
        ///     "LOCATION": "GRO",
        ///     "INSTRUMENT": 65387,
        ///     "ID": 83532,
        ///     "SSMETH_ID": 83531,
        ///     "SSMETH_NAME": "MD_00710775_0554_GRO143_2",
        ///     "ACQUIRED_BY": "DEMBOM",
        ///     "START_DATE": "2024-09-05T11:10:00",
        ///     "END_DATE": "2024-09-05T13:12:05",
        ///     "CHROM_ID": 65387
        ///   },
        ///   {
        ///     "NAME": "GRO303_QDa",
        ///     "NODE": "Amrgrobemp2061",
        ///     "COMMENTS": "GRO",
        ///     "LOCATION": "GRO",
        ///     "INSTRUMENT": 10082,
        ///     "ID": 59121,
        ///     "SSMETH_ID": 59120,
        ///     "SSMETH_NAME": "!QuickSet",
        ///     "ACQUIRED_BY": "VANHAIJ",
        ///     "START_DATE": "2024-08-05T09:56:06",
        ///     "END_DATE": "2024-08-05T11:53:36",
        ///     "CHROM_ID": 10082
        ///   }
        /// ]
        /// </code>
        /// 
        /// </remarks>
        /// <param year="year">The year which influences schema selection for the query.</param>
        /// <param month="month">The year which influences schema selection for the query.</param>
        /// <returns>Returns a list of instruments usage details for given samplesets.</returns>
        /// <response code="200">Returns the list of all instruments as a list of dictionaries.</response> 
        /// <response code="400">If the input parameters are incorrect or missing.</response>
        /// <response code="500">If an error occurs while processing the request.</response>
        [HttpGet("instrument_use-by-sampleset-year-and-month")]
        public IActionResult GetInstrumentsBySamplesetProdMonthly([FromQuery] int year, int month)
        {
            if (year <= 0)
            {
                return BadRequest("A valid year is required.");
            }

            try
            {
                var data = EmpowerInstrumentUseBySamplesetProdMonthly.FetchInstrumentsUseBySampleset(_connectionString, _databaseService, year, month);
                if (data.Count == 0)
                {
                    return NotFound("No data found for the provided year.");
                }
                return Ok(data);
            }
            catch (OracleException ex)
            {
                // Log the exception as necessary
                return Problem(detail: ex.Message, statusCode: 500);
            }
        }
    }
}