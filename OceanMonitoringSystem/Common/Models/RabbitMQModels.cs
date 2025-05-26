using System;

namespace Models
{
    /**
     * @class RabbitMQMessage
     * @description Represents a message structure for RabbitMQ communication between Wavy devices and Aggregators.
     * This structure encapsulates all necessary information for proper message routing and processing.
     */
    public class RabbitMQMessage
    {
        /** @property Unique identifier for the message */
        public string MessageId { get; set; } = Guid.NewGuid().ToString();
        
        /** @property Identifier of the originating Wavy device */
        public string WavyId { get; set; }
        
        /** @property Type of message: DATA_SEND, MAINTENANCE_STATE_UP, MAINTENANCE_STATE_DOWN, DISC_REQ */
        public string MessageType { get; set; }
        
        /** @property Time when the message was created */
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /** @property Sensor data payload (for DATA_SEND messages) */
        public DataWavy[] SensorData { get; set; } = Array.Empty<DataWavy>();
        
        /** @property Data format (CSV or JSON) for sensor data */
        public string DataFormat { get; set; } = "JSON";
        
        /** @property Optional payload for other message types */
        public string Payload { get; set; } = string.Empty;
        
        /** @property Message priority for queue processing */
        public byte Priority { get; set; } = 0;
        
        /** @property Target aggregator queue name */
        public string TargetQueue { get; set; } = string.Empty;
    }

    /**
     * @class RabbitMQConfig
     * @description Configuration settings for RabbitMQ connections
     */
    public class RabbitMQConfig
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "oceanguest";
        public string Password { get; set; } = "oceanpass";
        public string VirtualHost { get; set; } = "/";
        public bool AutomaticRecoveryEnabled { get; set; } = true;
        public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(10);
        public ushort RequestedHeartbeat { get; set; } = 60;
    }
}
