using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class SensorData
    {
        public ObjectId Id { get; set; }
        public string WavyId { get; set; }
        public string AggregatorId { get; set; }
        public string DataType { get; set; }
        public DateTime Timestamp { get; set; }
        public string RawValue { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    public class AggregatorSensorData
    {
        public string WavyId { get; set; }
        public string AggregatorId { get; set; }
        public string DataType { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string RawValue { get; set; }
    }

    public class Aggregator
    {
        public string ClientId { get; set; }
        public string Status { get; set; }
        public DateTime RegisteredAt { get; set; } = DateTime.Now;
        public DateTime LastConnectedAt { get; set; } = DateTime.Now;
    }
    
}
