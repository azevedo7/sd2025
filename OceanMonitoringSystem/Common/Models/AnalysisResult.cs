using LiteDB;

namespace Models
{
    /// <summary>
    /// Represents the result of a statistical analysis operation on sensor data
    /// </summary>
    public class AnalysisResult
    {
        [BsonId]
        public ObjectId Id { get; set; } = ObjectId.NewObjectId();
        
        /// <summary>
        /// Type of data analyzed (temperature, humidity, waterLevel, windSpeed)
        /// </summary>
        public string DataType { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of data points analyzed
        /// </summary>
        public int Count { get; set; }
        
        /// <summary>
        /// Average value
        /// </summary>
        public double Average { get; set; }
        
        /// <summary>
        /// Minimum value
        /// </summary>
        public double Min { get; set; }
        
        /// <summary>
        /// Maximum value
        /// </summary>
        public double Max { get; set; }
        
        /// <summary>
        /// Standard deviation
        /// </summary>
        public double StandardDeviation { get; set; }
        
        /// <summary>
        /// Median value
        /// </summary>
        public double Median { get; set; }
        
        /// <summary>
        /// When this analysis was performed
        /// </summary>
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Time range of the data analyzed (start)
        /// </summary>
        public DateTime DataRangeStart { get; set; }
        
        /// <summary>
        /// Time range of the data analyzed (end)
        /// </summary>
        public DateTime DataRangeEnd { get; set; }
        
        /// <summary>
        /// Optional filter applied (e.g., specific wavy device)
        /// </summary>
        public string? Filter { get; set; }
    }
}
