# Topic-Based Routing Implementation Guide

## Overview
This guide provides step-by-step instructions for implementing the topic-based "rooms" concept in the Ocean Monitoring System, where Wavy sensors publish to specific topics and Aggregators subscribe to chosen data types.

## Room Types and Topics

### 1. Sensor Type Rooms
Each sensor type has its own "room" (topic pattern):

| Room Name | Topic Pattern | Description |
|-----------|---------------|-------------|
| Temperature Room | `sensor.temperature.*.*` | All temperature sensor data |
| Humidity Room | `sensor.humidity.*.*` | All humidity sensor data |
| Pressure Room | `sensor.pressure.*.*` | All pressure sensor data |
| pH Room | `sensor.ph.*.*` | All pH sensor data |
| Oxygen Room | `sensor.oxygen.*.*` | All oxygen level data |

### 2. Device-Specific Rooms
Monitor specific Wavy devices:

| Room Name | Topic Pattern | Description |
|-----------|---------------|-------------|
| Wavy001 Room | `sensor.*.wavy001.*` | All data from Wavy001 |
| Wavy002 Room | `sensor.*.wavy002.*` | All data from Wavy002 |
| Critical Devices | `sensor.*.wavy001.*,sensor.*.wavy003.*` | Multiple critical devices |

### 3. Message Type Rooms
Filter by message type:

| Room Name | Topic Pattern | Description |
|-----------|---------------|-------------|
| Data Room | `sensor.*.*.data` | Only sensor data messages |
| Maintenance Room | `sensor.*.*.maintenance` | Only maintenance messages |
| Discovery Room | `sensor.*.*.discovery` | Only discovery requests |

## Implementation Steps

### Step 1: Update RabbitMQ Service for Topic Support

Add topic-based methods to `RabbitMQService.cs`:

```csharp
// Add to RabbitMQService class
public class RabbitMQService : IDisposable
{
    // ... existing code ...

    /**
     * @method PublishToTopic
     * @description Publishes a message to a specific topic
     */
    public async Task PublishToTopic(string topic, RabbitMQMessage message)
    {
        try
        {
            EnsureConnection();
            
            var messageJson = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageJson);

            var properties = _channel!.CreateBasicProperties();
            properties.Persistent = true;
            properties.Priority = message.Priority;
            properties.MessageId = message.MessageId;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: topic,
                basicProperties: properties,
                body: body
            );

            Console.WriteLine($"Published message to topic: {topic}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing to topic {topic}: {ex.Message}");
            throw;
        }
    }

    /**
     * @method SubscribeToRoom
     * @description Subscribes to a specific room (set of topics)
     */
    public void SubscribeToRoom(string roomName, string[] topicPatterns, Action<RabbitMQMessage> messageHandler)
    {
        try
        {
            EnsureConnection();

            // Declare room-specific queue
            var queueName = $"{roomName}_queue";
            _channel!.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Bind queue to all topic patterns for this room
            foreach (var pattern in topicPatterns)
            {
                _channel.QueueBind(
                    queue: queueName,
                    exchange: _exchangeName,
                    routingKey: pattern
                );
                Console.WriteLine($"Bound queue {queueName} to pattern: {pattern}");
            }

            // Set up consumer
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);
                    var message = JsonSerializer.Deserialize<RabbitMQMessage>(messageJson);
                    
                    if (message != null)
                    {
                        Console.WriteLine($"Received message in {roomName} from topic: {ea.RoutingKey}");
                        messageHandler(message);
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message in {roomName}: {ex.Message}");
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );

            Console.WriteLine($"Started consuming from {roomName} with patterns: {string.Join(", ", topicPatterns)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error subscribing to room {roomName}: {ex.Message}");
            throw;
        }
    }

    /**
     * @method GenerateTopic
     * @description Generates a topic string based on sensor type, device ID, and message type
     */
    public static string GenerateTopic(string sensorType, string wavyId, string messageType)
    {
        return $"sensor.{sensorType.ToLower()}.{wavyId.ToLower()}.{messageType.ToLower()}";
    }
}
```

### Step 2: Update Wavy to Use Topic-Based Publishing

Update the Wavy `Program.cs` to publish to appropriate topics:

```csharp
// In Wavy Program.cs, update the SendDataToAggregator method
private static async Task SendDataToAggregator(DataWavy[] dataToSend, RabbitMQService rabbitMQService, string wavyId)
{
    try
    {
        // Group data by sensor type for topic-based publishing
        var groupedData = dataToSend.GroupBy(d => d.SensorType.ToLower());

        foreach (var group in groupedData)
        {
            var sensorType = group.Key;
            var sensorData = group.ToArray();

            // Generate topic for this sensor type
            var topic = RabbitMQService.GenerateTopic(sensorType, wavyId, "data");

            var message = new RabbitMQMessage
            {
                WavyId = wavyId,
                MessageType = "DATA_SEND",
                SensorData = sensorData,
                DataFormat = "JSON",
                Priority = GetPriorityForSensorType(sensorType)
            };

            // Publish to topic
            await rabbitMQService.PublishToTopic(topic, message);
            Console.WriteLine($"Published {sensorData.Length} {sensorType} readings to topic: {topic}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending data to aggregator: {ex.Message}");
    }
}

// Add priority mapping for different sensor types
private static byte GetPriorityForSensorType(string sensorType)
{
    return sensorType.ToLower() switch
    {
        "temperature" => 5,
        "pressure" => 7,
        "ph" => 8,
        "oxygen" => 9,
        _ => 3
    };
}

// Update maintenance state methods to use topics
private static async Task SendMaintenanceState(string state, RabbitMQService rabbitMQService, string wavyId, string sensorType)
{
    try
    {
        var topic = RabbitMQService.GenerateTopic(sensorType, wavyId, "maintenance");
        
        var message = new RabbitMQMessage
        {
            WavyId = wavyId,
            MessageType = $"MAINTENANCE_STATE_{state}",
            Payload = $"Maintenance state: {state}",
            Priority = 10 // High priority for maintenance
        };

        await rabbitMQService.PublishToTopic(topic, message);
        Console.WriteLine($"Published maintenance state '{state}' to topic: {topic}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending maintenance state: {ex.Message}");
    }
}
```

### Step 3: Create Room Configuration System

Create a configuration class for managing room subscriptions:

```csharp
// Add to Common/Models/RoomConfig.cs
using System.Collections.Generic;

namespace Models
{
    public class RoomConfig
    {
        public string RoomName { get; set; } = string.Empty;
        public string[] TopicPatterns { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class AggregatorRoomSubscriptions
    {
        public string AggregatorId { get; set; } = string.Empty;
        public List<RoomConfig> SubscribedRooms { get; set; } = new List<RoomConfig>();

        // Predefined room configurations
        public static class PredefinedRooms
        {
            public static RoomConfig TemperatureRoom => new RoomConfig
            {
                RoomName = "temperature_room",
                TopicPatterns = new[] { "sensor.temperature.*.*" },
                Description = "All temperature sensor data"
            };

            public static RoomConfig HumidityRoom => new RoomConfig
            {
                RoomName = "humidity_room",
                TopicPatterns = new[] { "sensor.humidity.*.*" },
                Description = "All humidity sensor data"
            };

            public static RoomConfig PressureRoom => new RoomConfig
            {
                RoomName = "pressure_room",
                TopicPatterns = new[] { "sensor.pressure.*.*" },
                Description = "All pressure sensor data"
            };

            public static RoomConfig MaintenanceRoom => new RoomConfig
            {
                RoomName = "maintenance_room",
                TopicPatterns = new[] { "sensor.*.*.maintenance" },
                Description = "All maintenance messages"
            };

            public static RoomConfig CriticalDevicesRoom => new RoomConfig
            {
                RoomName = "critical_devices_room",
                TopicPatterns = new[] { "sensor.*.wavy001.*", "sensor.*.wavy003.*" },
                Description = "Critical device monitoring"
            };

            public static RoomConfig AllDataRoom => new RoomConfig
            {
                RoomName = "all_data_room",
                TopicPatterns = new[] { "sensor.*.*.data" },
                Description = "All sensor data messages"
            };
        }
    }
}
```

### Step 4: Update Aggregator for Room-Based Consumption

Update the Aggregator to use room-based subscriptions:

```csharp
// In Aggregator Program.cs
using Models;
using OceanMonitoringSystem.Common.Services;

class Program
{
    private static RabbitMQService? _rabbitMQService;
    private static string _aggregatorId = Environment.GetEnvironmentVariable("AGGREGATOR_ID") ?? "aggregator_default";

    static async Task Main(string[] args)
    {
        Console.WriteLine($"Starting Aggregator: {_aggregatorId}");

        // Initialize RabbitMQ service
        var config = new RabbitMQConfig
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq",
            Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672"),
            UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "oceanguest",
            Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "oceanpass"
        };

        _rabbitMQService = new RabbitMQService(config);

        // Configure room subscriptions based on environment or aggregator type
        ConfigureRoomSubscriptions();

        // Keep the application running
        Console.WriteLine("Aggregator is running. Press Ctrl+C to exit.");
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            Shutdown();
        };

        // Keep alive
        await Task.Delay(-1);
    }

    private static void ConfigureRoomSubscriptions()
    {
        var subscriptionConfig = GetSubscriptionConfiguration();

        foreach (var room in subscriptionConfig.SubscribedRooms)
        {
            if (room.IsActive)
            {
                Console.WriteLine($"Subscribing to {room.RoomName}: {room.Description}");
                _rabbitMQService!.SubscribeToRoom(room.RoomName, room.TopicPatterns, ProcessRoomMessage);
            }
        }
    }

    private static AggregatorRoomSubscriptions GetSubscriptionConfiguration()
    {
        // This could be loaded from configuration file or environment variables
        var aggregatorType = Environment.GetEnvironmentVariable("AGGREGATOR_TYPE") ?? "general";

        var subscriptions = new AggregatorRoomSubscriptions
        {
            AggregatorId = _aggregatorId
        };

        switch (aggregatorType.ToLower())
        {
            case "temperature":
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.TemperatureRoom);
                break;

            case "environmental":
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.TemperatureRoom);
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.HumidityRoom);
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.PressureRoom);
                break;

            case "maintenance":
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.MaintenanceRoom);
                break;

            case "critical_monitor":
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.CriticalDevicesRoom);
                break;

            case "general":
            default:
                subscriptions.SubscribedRooms.Add(AggregatorRoomSubscriptions.PredefinedRooms.AllDataRoom);
                break;
        }

        return subscriptions;
    }

    private static void ProcessRoomMessage(RabbitMQMessage message)
    {
        try
        {
            Console.WriteLine($"\n=== Message Received ===");
            Console.WriteLine($"Message ID: {message.MessageId}");
            Console.WriteLine($"Wavy ID: {message.WavyId}");
            Console.WriteLine($"Message Type: {message.MessageType}");
            Console.WriteLine($"Timestamp: {message.Timestamp}");
            Console.WriteLine($"Data Format: {message.DataFormat}");
            Console.WriteLine($"Priority: {message.Priority}");

            switch (message.MessageType)
            {
                case "DATA_SEND":
                    ProcessSensorData(message);
                    break;

                case "MAINTENANCE_STATE_UP":
                case "MAINTENANCE_STATE_DOWN":
                    ProcessMaintenanceMessage(message);
                    break;

                case "DISC_REQ":
                    ProcessDiscoveryRequest(message);
                    break;

                default:
                    Console.WriteLine($"Unknown message type: {message.MessageType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
    }

    private static void ProcessSensorData(RabbitMQMessage message)
    {
        Console.WriteLine($"Processing sensor data from {message.WavyId}:");
        
        foreach (var data in message.SensorData)
        {
            Console.WriteLine($"  - {data.SensorType}: {data.Value} {data.Unit} (Time: {data.Timestamp})");
            
            // Room-specific processing logic
            ProcessDataByType(data, message.WavyId);
        }
    }

    private static void ProcessDataByType(DataWavy data, string wavyId)
    {
        switch (data.SensorType.ToLower())
        {
            case "temperature":
                ProcessTemperatureData(data, wavyId);
                break;
            case "humidity":
                ProcessHumidityData(data, wavyId);
                break;
            case "pressure":
                ProcessPressureData(data, wavyId);
                break;
            default:
                ProcessGenericData(data, wavyId);
                break;
        }
    }

    private static void ProcessTemperatureData(DataWavy data, string wavyId)
    {
        // Temperature-specific logic
        if (data.Value > 30.0)
        {
            Console.WriteLine($"⚠️  HIGH TEMPERATURE ALERT from {wavyId}: {data.Value}°C");
        }
        else if (data.Value < 5.0)
        {
            Console.WriteLine($"❄️  LOW TEMPERATURE ALERT from {wavyId}: {data.Value}°C");
        }
        
        // Store in temperature-specific database/file
        StoreTemperatureReading(data, wavyId);
    }

    private static void ProcessHumidityData(DataWavy data, string wavyId)
    {
        // Humidity-specific logic
        if (data.Value > 90.0)
        {
            Console.WriteLine($"💧 HIGH HUMIDITY ALERT from {wavyId}: {data.Value}%");
        }
        
        StoreHumidityReading(data, wavyId);
    }

    private static void ProcessPressureData(DataWavy data, string wavyId)
    {
        // Pressure-specific logic
        if (data.Value < 1000.0)
        {
            Console.WriteLine($"📉 LOW PRESSURE ALERT from {wavyId}: {data.Value} hPa");
        }
        
        StorePressureReading(data, wavyId);
    }

    private static void ProcessGenericData(DataWavy data, string wavyId)
    {
        Console.WriteLine($"Processing generic sensor data: {data.SensorType} = {data.Value} {data.Unit}");
        StoreGenericReading(data, wavyId);
    }

    private static void ProcessMaintenanceMessage(RabbitMQMessage message)
    {
        Console.WriteLine($"🔧 Maintenance message from {message.WavyId}: {message.Payload}");
        // Handle maintenance state changes
        LogMaintenanceEvent(message);
    }

    private static void ProcessDiscoveryRequest(RabbitMQMessage message)
    {
        Console.WriteLine($"🔍 Discovery request from {message.WavyId}");
        // Handle device discovery
        RespondToDiscovery(message);
    }

    // Storage methods (placeholder implementations)
    private static void StoreTemperatureReading(DataWavy data, string wavyId) { /* Implementation */ }
    private static void StoreHumidityReading(DataWavy data, string wavyId) { /* Implementation */ }
    private static void StorePressureReading(DataWavy data, string wavyId) { /* Implementation */ }
    private static void StoreGenericReading(DataWavy data, string wavyId) { /* Implementation */ }
    private static void LogMaintenanceEvent(RabbitMQMessage message) { /* Implementation */ }
    private static void RespondToDiscovery(RabbitMQMessage message) { /* Implementation */ }

    private static void Shutdown()
    {
        Console.WriteLine("Shutting down aggregator...");
        _rabbitMQService?.Dispose();
        Environment.Exit(0);
    }
}
```

### Step 5: Docker Compose Configuration for Rooms

Update `docker-compose.yml` to support different aggregator types:

```yaml
# Temperature-specific aggregator
aggregator_temperature:
  build:
    context: ./OceanMonitoringSystem
    dockerfile: Aggregator/Dockerfile
  environment:
    - RABBITMQ_HOST=rabbitmq
    - RABBITMQ_PORT=5672
    - RABBITMQ_USERNAME=oceanguest
    - RABBITMQ_PASSWORD=oceanpass
    - AGGREGATOR_ID=temp_aggregator_001
    - AGGREGATOR_TYPE=temperature
  depends_on:
    rabbitmq:
      condition: service_healthy
  networks:
    - ocean_network

# Environmental aggregator (temperature + humidity + pressure)
aggregator_environmental:
  build:
    context: ./OceanMonitoringSystem
    dockerfile: Aggregator/Dockerfile
  environment:
    - RABBITMQ_HOST=rabbitmq
    - RABBITMQ_PORT=5672
    - RABBITMQ_USERNAME=oceanguest
    - RABBITMQ_PASSWORD=oceanpass
    - AGGREGATOR_ID=env_aggregator_001
    - AGGREGATOR_TYPE=environmental
  depends_on:
    rabbitmq:
      condition: service_healthy
  networks:
    - ocean_network

# Maintenance monitoring aggregator
aggregator_maintenance:
  build:
    context: ./OceanMonitoringSystem
    dockerfile: Aggregator/Dockerfile
  environment:
    - RABBITMQ_HOST=rabbitmq
    - RABBITMQ_PORT=5672
    - RABBITMQ_USERNAME=oceanguest
    - RABBITMQ_PASSWORD=oceanpass
    - AGGREGATOR_ID=maint_aggregator_001
    - AGGREGATOR_TYPE=maintenance
  depends_on:
    rabbitmq:
      condition: service_healthy
  networks:
    - ocean_network
```

## Testing the Implementation

### 1. Start the System
```bash
docker-compose up -d
```

### 2. Monitor RabbitMQ Management UI
Visit `http://localhost:15672` and check:
- Exchanges: `ocean_monitoring_exchange`
- Queues: `temperature_room_queue`, `humidity_room_queue`, etc.
- Bindings: Topic patterns bound to queues

### 3. Verify Topic Routing
Check that messages are being routed to the correct queues based on topics.

### 4. Test Different Room Subscriptions
- Start different aggregator types
- Verify they only receive messages for their subscribed rooms
- Check that multiple aggregators can consume from the same room

This implementation provides a complete topic-based routing system with the "rooms" concept, allowing for flexible and scalable message distribution in your Ocean Monitoring System.
