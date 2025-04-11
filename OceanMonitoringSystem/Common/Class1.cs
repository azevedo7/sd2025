namespace OceanMonitoringSystem.Common
{
    public static class Protocol
    {
        // Message Types
        public const string CONN_REQ = "CONN_REQ";
        public const string CONN_ACK = "CONN_ACK";
        public const string DATA_TYPES_REQ = "DATA_TYPES_REQ";
        public const string DATA_TYPES_RESP = "DATA_TYPES_RESP";
        public const string DATA_SEND = "DATA_SEND";
        public const string DATA_ACK = "DATA_ACK";
        public const string AGG_DATA_SEND = "AGG_DATA_SEND";
        public const string AGG_DATA_ACK = "AGG_DATA_ACK";
        public const string STATUS_UPD = "STATUS_UPD";
        public const string STATUS_ACK = "STATUS_ACK";
        public const string DISC_REQ = "DISC_REQ";
        public const string DISC_ACK = "DISC_ACK";
        public const string MAINTENANCE_STATE = "MAINTENANCE_STATE";

        // Message delimiter
        public const string END = "END";

        // Helper method to create protocol messages
        public static string CreateMessage(string messageType, string payload = "")
        {
            return $"{messageType}|{payload}|{END}";
        }

        // Helper method to parse protocol messages
        public static (string messageType, string payload) ParseMessage(string message)
        {
            string[] parts = message.Split('|');
            if (parts.Length < 3 || parts[2] != END)
                throw new FormatException("Invalid message format");

            return (parts[0], parts[1]);
        }
        // Helper method to check if the message follows the protocol
        public static bool IsValidMessage(string message)
        {
            string[] parts = message.Split('|');
            return parts.Length >= 3 && parts[2] == END;
        }
    }

    public static class WavyStatus
    {
        public const string ACTIVE = "associada";
        public const string INACTIVE = "desativada";
        public const string MAINTENANCE = "manutenção";
        public const string OPERATION = "operação";
    }
}
