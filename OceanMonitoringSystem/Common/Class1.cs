namespace OceanMonitoringSystem.Common
{
    /**
     * @class Protocol
     * @description Defines the communication protocol used between Wavy devices, Aggregators,
     * and the central Server. Provides message types, formatting, and parsing functions to ensure
     * consistent communication across all tiers of the distributed system.
     */
    public static class Protocol
    {
        // Connection messages
        /** @const Request to establish a connection */
        public const string CONN_REQ = "CONN_REQ";
        /** @const Acknowledge a connection request */
        public const string CONN_ACK = "CONN_ACK";
        
        // Data information messages
        /** @const Request for supported data types */
        public const string DATA_TYPES_REQ = "DATA_TYPES_REQ";
        /** @const Response with supported data types */
        public const string DATA_TYPES_RESP = "DATA_TYPES_RESP";
        
        // Data transmission messages
        /** @const Send sensor data (from Wavy to Aggregator) */
        public const string DATA_SEND = "DATA_SEND";
        /** @const Acknowledge receipt of sensor data */
        public const string DATA_ACK = "DATA_ACK";
        /** @const Send aggregated data (from Aggregator to Server) */
        public const string AGG_DATA_SEND = "AGG_DATA_SEND";
        /** @const Acknowledge receipt of aggregated data */
        public const string AGG_DATA_ACK = "AGG_DATA_ACK";
        
        // Status messages
        /** @const Update status information */
        public const string STATUS_UPD = "STATUS_UPD";
        /** @const Acknowledge status update */
        public const string STATUS_ACK = "STATUS_ACK";
        
        // Disconnection messages
        /** @const Request to disconnect */
        public const string DISC_REQ = "DISC_REQ";
        /** @const Acknowledge disconnection request */
        public const string DISC_ACK = "DISC_ACK";
        
        // Maintenance mode messages
        /** @const Notification that device is entering maintenance mode */
        public const string MAINTENANCE_STATE_UP = "MAINTENANCE_STATE_UP";
        /** @const Notification that device is exiting maintenance mode */
        public const string MAINTENANCE_STATE_DOWN = "MAINTENANCE_STATE_DOWN";

        // Message protocol terminator
        /** @const Message terminator to indicate end of protocol message */
        public const string END = "END";

        /**
         * @method CreateMessage
         * @description Creates a formatted protocol message with the specified type and payload
         * @param messageType The type of message (one of the protocol constants)
         * @param payload Optional data payload for the message
         * @return Formatted protocol message string
         */
        public static string CreateMessage(string messageType, string payload = "")
        {
            return $"{messageType}|{payload}|{END}";
        }

        /**
         * @method ParseMessage
         * @description Parses a protocol message into its type and payload components
         * @param message The protocol message to parse
         * @return Tuple containing the message type and payload
         * @throws FormatException if the message doesn't follow the protocol format
         */
        public static (string messageType, string payload) ParseMessage(string message)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 3 || parts[2] != END)
                throw new FormatException("Invalid message format");

            return (parts[0], parts[1]);
        }
        
        /**
         * @method IsValidMessage
         * @description Checks if a message follows the required protocol format
         * @param message The message to validate
         * @return Boolean indicating if the message is valid according to the protocol
         */
        public static bool IsValidMessage(string message)
        {
            string[] parts = message.Split('|');
            return parts.Length >= 3 && parts[2] == END;
        }
    }

    /**
     * @class WavyStatus
     * @description Defines the possible status values for Wavy devices in the system.
     * Used for tracking device state in databases and status updates.
     */
    public static class WavyStatus
    {
        /** @const Device is connected and sending data */
        public const string ACTIVE = "associada";
        /** @const Device is disconnected or not responsive */
        public const string INACTIVE = "desativada";
        /** @const Device is in maintenance mode (temporarily not sending data) */
        public const string MAINTENANCE = "manutenção";
        /** @const Device is in normal operation */
        public const string OPERATION = "operação";
    }
}
