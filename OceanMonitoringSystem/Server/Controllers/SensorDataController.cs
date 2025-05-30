using Microsoft.AspNetCore.Mvc;
using LiteDB;
using Models;

namespace OceanMonitoringSystem.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SensorDataController : ControllerBase
    {
        private static readonly string DbPath = "oceandata.db";
        private static readonly object DbLock = new object();

        /// <summary>
        /// Get all sensor data with optional pagination
        /// </summary>
        [HttpGet]
        public IActionResult GetAllSensorData([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<SensorData>("sensorData");
                    
                    var skip = (page - 1) * pageSize;
                    var data = collection.FindAll()
                                        .OrderByDescending(x => x.ReceivedAt)
                                        .Skip(skip)
                                        .Take(pageSize)
                                        .ToList();
                    
                    var totalCount = collection.Count();
                    
                    return Ok(new
                    {
                        data = data,
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
        /// Get sensor data by Wavy ID
        /// </summary>
        [HttpGet("wavy/{wavyId}")]
        public IActionResult GetDataByWavyId(string wavyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<SensorData>("sensorData");
                    
                    var skip = (page - 1) * pageSize;
                    var data = collection.Find(x => x.WavyId == wavyId)
                                        .OrderByDescending(x => x.ReceivedAt)
                                        .Skip(skip)
                                        .Take(pageSize)
                                        .ToList();
                    
                    var totalCount = collection.Count(x => x.WavyId == wavyId);
                    
                    return Ok(new
                    {
                        wavyId = wavyId,
                        data = data,
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
        /// Get sensor data by data type
        /// </summary>
        [HttpGet("type/{dataType}")]
        public IActionResult GetDataByType(string dataType, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<SensorData>("sensorData");
                    
                    var skip = (page - 1) * pageSize;
                    var data = collection.Find(x => x.DataType == dataType)
                                        .OrderByDescending(x => x.ReceivedAt)
                                        .Skip(skip)
                                        .Take(pageSize)
                                        .ToList();
                    
                    var totalCount = collection.Count(x => x.DataType == dataType);
                    
                    return Ok(new
                    {
                        dataType = dataType,
                        data = data,
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
        /// Get aggregator information
        /// </summary>
        [HttpGet("aggregators")]
        public IActionResult GetAggregators()
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<Aggregator>("aggregators");
                    var aggregators = collection.FindAll().OrderBy(a => a.RegisteredAt).ToList();
                    
                    return Ok(aggregators);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get database statistics
        /// </summary>
        [HttpGet("stats")]
        public IActionResult GetStatistics()
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var sensorCollection = db.GetCollection<SensorData>("sensorData");
                    var aggregatorCollection = db.GetCollection<Aggregator>("aggregators");
                    
                    var totalRecords = sensorCollection.Count();
                    var aggregatorCount = aggregatorCollection.Count();
                    
                    // Get data type distribution
                    var dataTypeStats = sensorCollection.FindAll()
                        .GroupBy(x => x.DataType)
                        .Select(g => new { DataType = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    
                    // Get Wavy device statistics
                    var wavyStats = sensorCollection.FindAll()
                        .GroupBy(x => x.WavyId)
                        .Select(g => new { WavyId = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                    
                    // Get latest record timestamp
                    var latestRecord = sensorCollection.FindAll()
                        .OrderByDescending(x => x.ReceivedAt)
                        .FirstOrDefault();
                    
                    return Ok(new
                    {
                        totalRecords = totalRecords,
                        aggregatorCount = aggregatorCount,
                        dataTypeDistribution = dataTypeStats,
                        wavyDeviceStats = wavyStats,
                        latestRecordTime = latestRecord?.ReceivedAt
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Export sensor data to CSV
        /// </summary>
        [HttpGet("export/csv")]
        public IActionResult ExportToCsv([FromQuery] string? wavyId = null, [FromQuery] string? dataType = null)
        {
            try
            {
                lock (DbLock)
                {
                    using var db = new LiteDatabase(DbPath);
                    var collection = db.GetCollection<SensorData>("sensorData");
                    
                    IEnumerable<SensorData> data = collection.FindAll();
                    
                    // Apply filters if provided
                    if (!string.IsNullOrEmpty(wavyId))
                        data = data.Where(x => x.WavyId == wavyId);
                    
                    if (!string.IsNullOrEmpty(dataType))
                        data = data.Where(x => x.DataType == dataType);
                    
                    var orderedData = data.OrderByDescending(x => x.ReceivedAt).ToList();
                    
                    var csv = new System.Text.StringBuilder();
                    csv.AppendLine("WavyId,AggregatorId,DataType,Timestamp,RawValue,ReceivedAt");
                    
                    foreach (var item in orderedData)
                    {
                        csv.AppendLine($"{item.WavyId},{item.AggregatorId},{item.DataType},{item.Timestamp:yyyy-MM-dd HH:mm:ss},{item.RawValue},{item.ReceivedAt:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    var fileName = "sensor_data_export.csv";
                    if (!string.IsNullOrEmpty(wavyId))
                        fileName = $"sensor_data_{wavyId}_export.csv";
                    else if (!string.IsNullOrEmpty(dataType))
                        fileName = $"sensor_data_{dataType}_export.csv";
                    
                    return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
