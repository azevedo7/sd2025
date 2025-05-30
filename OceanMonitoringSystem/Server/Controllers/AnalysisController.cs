using Microsoft.AspNetCore.Mvc;
using LiteDB;
using Models;
using Grpc.Net.Client;
using Google.Protobuf.Collections;

namespace OceanMonitoringSystem.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private static readonly string DbPath = "oceandata.db";
        private static readonly object DbLock = new object();
        private static readonly string GrpcServerUrl = "http://python-analysis:50052";

        /// <summary>
        /// Get all stored analysis results
        /// </summary>
        [HttpGet]
        public IActionResult GetAnalysisResults([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<AnalysisResult>("analysisResults");
                    
                    var skip = (page - 1) * pageSize;
                    var results = collection.FindAll()
                                          .OrderByDescending(x => x.AnalyzedAt)
                                          .Skip(skip)
                                          .Take(pageSize)
                                          .ToList();
                    
                    var totalCount = collection.Count();
                    
                    return Ok(new
                    {
                        data = results,
                        pagination = new
                        {
                            page = page,
                            pageSize = pageSize,
                            totalCount = totalCount,
                            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Run analysis on current sensor data using gRPC
        /// </summary>
        [HttpPost("run")]
        public async Task<IActionResult> RunAnalysis([FromBody] AnalysisRequest request)
        {
            try
            {
                var results = new List<AnalysisResult>();
                
                // Get data types to analyze
                var dataTypesToAnalyze = request.DataTypes?.Any() == true 
                    ? request.DataTypes 
                    : new[] { "temperature", "humidity", "waterLevel", "windSpeed" };

                foreach (var dataType in dataTypesToAnalyze)
                {
                    // Get sensor data from database
                    var sensorData = GetSensorDataForAnalysis(dataType, request.WavyId, request.HoursBack);
                    
                    if (sensorData.Count == 0)
                    {
                        continue; // Skip if no data for this type
                    }

                    // Call Python gRPC service for analysis
                    var analysisResult = await CallGrpcAnalysisService(dataType, sensorData);
                    
                    if (analysisResult != null)
                    {
                        // Create analysis result record
                        var result = new AnalysisResult
                        {
                            DataType = dataType,
                            Count = analysisResult.Count,
                            Average = analysisResult.Average,
                            Min = analysisResult.Min,
                            Max = analysisResult.Max,
                            StandardDeviation = analysisResult.StdDev,
                            Median = analysisResult.Median,
                            AnalyzedAt = DateTime.UtcNow,
                            DataRangeStart = sensorData.Min(x => x.Timestamp),
                            DataRangeEnd = sensorData.Max(x => x.Timestamp),
                            Filter = request.WavyId
                        };

                        // Save to database
                        lock (DbLock)
                        {
                            using var db = new LiteDatabase(DbPath);
                            var collection = db.GetCollection<AnalysisResult>("analysisResults");
                            collection.Insert(result);
                        }

                        results.Add(result);
                    }
                }

                return Ok(new { 
                    message = $"Analysis completed for {results.Count} data types",
                    results = results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete an analysis result
        /// </summary>
        [HttpDelete("{id}")]
        public IActionResult DeleteAnalysisResult(string id)
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<AnalysisResult>("analysisResults");
                    
                    try
                    {
                        var objectId = new ObjectId(id);
                        var deleted = collection.Delete(objectId);
                        if (deleted)
                        {
                            return Ok(new { message = "Analysis result deleted successfully" });
                        }
                        else
                        {
                            return NotFound(new { error = "Analysis result not found" });
                        }
                    }
                    catch (ArgumentException)
                    {
                        return BadRequest(new { error = "Invalid ID format" });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private List<SensorData> GetSensorDataForAnalysis(string dataType, string? wavyId = null, int hoursBack = 24)
        {
            lock (DbLock)
            {
                using var db = new LiteDatabase(DbPath);
                var collection = db.GetCollection<SensorData>("sensorData");
                
                var cutoffTime = DateTime.UtcNow.AddHours(-hoursBack);
                
                IEnumerable<SensorData> query = collection.Find(x => 
                    x.DataType == dataType && 
                    x.Timestamp >= cutoffTime);
                
                if (!string.IsNullOrEmpty(wavyId))
                {
                    query = query.Where(x => x.WavyId == wavyId);
                }
                
                return query.OrderBy(x => x.Timestamp).ToList();
            }
        }

        private async Task<OceanAnalysis.SensorDataAnalysisResponse?> CallGrpcAnalysisService(string dataType, List<SensorData> sensorData)
        {
            try
            {
                using var channel = GrpcChannel.ForAddress(GrpcServerUrl);
                var client = new OceanAnalysis.SensorDataAnalysisService.SensorDataAnalysisServiceClient(channel);

                var request = new OceanAnalysis.SensorDataRequest
                {
                    DataType = dataType
                };

                // Add values to the request
                foreach (var data in sensorData)
                {
                    if (double.TryParse(data.RawValue, out double value))
                    {
                        request.Values.Add(value);
                    }
                }

                // Call the gRPC service
                var response = await client.AnalyzeSensorDataAsync(request);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"gRPC call failed for {dataType}: {ex.Message}");
                return null;
            }
        }
    }

    public class AnalysisRequest
    {
        public string[]? DataTypes { get; set; }
        public string? WavyId { get; set; }
        public int HoursBack { get; set; } = 24;
    }
}
