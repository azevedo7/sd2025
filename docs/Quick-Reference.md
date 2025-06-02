# Ocean Monitoring System - Quick Reference Guide

## Table of Contents
1. [System Overview](#system-overview)
2. [Topic Patterns](#topic-patterns)
3. [Room Types](#room-types)
4. [Environment Variables](#environment-variables)
5. [Common Operations](#common-operations)
6. [Troubleshooting](#troubleshooting)

## System Overview

### Architecture
```
Wavy Sensors → RabbitMQ Exchange → Topic-Based Queues → Aggregators
```

### Key Components
- **RabbitMQ**: Message broker with topic exchange
- **Wavy**: Publishers (sensors)
- **Aggregators**: Subscribers (data processors)
- **Topics**: Routing keys for message delivery

## Topic Patterns

### Standard Topic Structure
```
sensor.{sensor_type}.{wavy_id}.{message_type}
```

### Examples
| Topic | Description |
|-------|-------------|
| `sensor.temperature.wavy001.data` | Temperature data from Wavy001 |
| `sensor.humidity.wavy002.data` | Humidity data from Wavy002 |
| `sensor.pressure.wavy001.maintenance` | Maintenance from Wavy001 |
| `sensor.*.wavy001.*` | All messages from Wavy001 |
| `sensor.temperature.*.*` | All temperature data |

### Routing Patterns
| Pattern | Matches |
|---------|---------|
| `#` | Everything |
| `*` | Exactly one word |
| `sensor.temperature.#` | All temperature messages |
| `sensor.*.*.data` | All data messages |
| `sensor.temperature.*.*` | All temperature messages |

## Room Types

### Sensor Type Rooms
| Room | Topic Pattern | Purpose |
|------|---------------|---------|
| Temperature Room | `sensor.temperature.*.*` | Temperature monitoring |
| Humidity Room | `sensor.humidity.*.*` | Humidity monitoring |
| Pressure Room | `sensor.pressure.*.*` | Pressure monitoring |
| pH Room | `sensor.ph.*.*` | pH level monitoring |
| Oxygen Room | `sensor.oxygen.*.*` | Oxygen level monitoring |

### Device-Specific Rooms
| Room | Topic Pattern | Purpose |
|------|---------------|---------|
| Wavy001 Room | `sensor.*.wavy001.*` | Monitor specific device |
| Critical Devices | `sensor.*.wavy001.*,sensor.*.wavy003.*` | Monitor critical devices |

### Message Type Rooms
| Room | Topic Pattern | Purpose |
|------|---------------|---------|
| Data Room | `sensor.*.*.data` | Only sensor data |
| Maintenance Room | `sensor.*.*.maintenance` | Only maintenance messages |
| Discovery Room | `sensor.*.*.discovery` | Only discovery requests |

## Environment Variables

### RabbitMQ Connection
```bash
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=oceanguest
RABBITMQ_PASSWORD=oceanpass
```

### Wavy Configuration
```bash
WAVY_ID=wavy001
SENSOR_TYPE=temperature
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=oceanguest
RABBITMQ_PASSWORD=oceanpass
```

### Aggregator Configuration
```bash
AGGREGATOR_ID=temp_aggregator_001
AGGREGATOR_TYPE=temperature
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672
RABBITMQ_USERNAME=oceanguest
RABBITMQ_PASSWORD=oceanpass
```

### Aggregator Types
| Type | Subscribed Rooms | Description |
|------|------------------|-------------|
| `temperature` | Temperature Room | Only temperature data |
| `environmental` | Temperature + Humidity + Pressure | Environmental monitoring |
| `maintenance` | Maintenance Room | Only maintenance messages |
| `critical_monitor` | Critical Devices | Monitor specific devices |
| `general` | All Data Room | All sensor data |

### Aggregator Topic Selection
| Variable | Description | Example |
|----------|-------------|---------|
| `SENSOR_TYPES` | **Comma-separated list of sensor types to subscribe to** | `temperature,humidity` |
| `AGGREGATOR_TYPE` | **Predefined aggregator behavior type** | `environmental` |

**Topic Subscription Examples:**
```yaml
# Temperature and Humidity only
- SENSOR_TYPES=temperature,humidity

# All environmental sensors  
- SENSOR_TYPES=temperature,humidity,windSpeed,waterLevel
- AGGREGATOR_TYPE=environmental

# Temperature only with specialized processing
- SENSOR_TYPES=temperature
- AGGREGATOR_TYPE=temperature
```

## Common Operations

### Publishing a Message (Wavy)
```csharp
// Generate topic
var topic = RabbitMQService.GenerateTopic("temperature", "wavy001", "data");

// Create message
var message = new RabbitMQMessage
{
    WavyId = "wavy001",
    MessageType = "DATA_SEND",
    SensorData = sensorData,
    Priority = 5
};

// Publish
await rabbitMQService.PublishToTopic(topic, message);
```

### Subscribing to a Room (Aggregator)
```csharp
// Define room configuration
var room = new RoomConfig
{
    RoomName = "temperature_room",
    TopicPatterns = new[] { "sensor.temperature.*.*" },
    Description = "All temperature data"
};

// Subscribe
rabbitMQService.SubscribeToRoom(
    room.RoomName, 
    room.TopicPatterns, 
    ProcessMessage
);
```

### Message Processing
```csharp
private static void ProcessMessage(RabbitMQMessage message)
{
    switch (message.MessageType)
    {
        case "DATA_SEND":
            ProcessSensorData(message);
            break;
        case "MAINTENANCE_STATE_UP":
        case "MAINTENANCE_STATE_DOWN":
            ProcessMaintenanceMessage(message);
            break;
    }
}
```

## Troubleshooting

### Check RabbitMQ Status
```bash
# Check if RabbitMQ is running
docker exec rabbitmq rabbitmq-diagnostics status

# Check logs
docker logs rabbitmq

# Check queues
docker exec rabbitmq rabbitmqctl list_queues name messages

# Check exchanges
docker exec rabbitmq rabbitmqctl list_exchanges

# Check bindings
docker exec rabbitmq rabbitmqctl list_bindings
```

### Common Issues

#### 1. Messages Not Being Delivered
**Symptoms**: Aggregator not receiving messages
**Solutions**:
- Check topic patterns match exactly
- Verify queue bindings in management UI
- Ensure exchange exists and is type "topic"
- Check message routing key format

#### 2. Connection Failures
**Symptoms**: Connection refused errors
**Solutions**:
- Verify RabbitMQ container is running
- Check environment variables
- Ensure network connectivity
- Verify credentials

#### 3. High Memory Usage
**Symptoms**: RabbitMQ consuming lots of memory
**Solutions**:
- Check for unprocessed messages in queues
- Implement message TTL
- Optimize consumer processing speed
- Monitor queue lengths

### Debug Commands

```bash
# Purge a specific queue
docker exec rabbitmq rabbitmqctl purge_queue temperature_room_queue

# Delete a queue
docker exec rabbitmq rabbitmqctl delete_queue humidity_room_queue

# Check specific queue info
docker exec rabbitmq rabbitmqctl list_queue_bindings temperature_room_queue

# Monitor real-time activity
docker exec rabbitmq rabbitmqctl list_consumers
```

### Management UI
- **URL**: `http://localhost:15672`
- **Username**: `oceanguest`
- **Password**: `oceanpass`

#### Key Sections:
- **Queues**: View all queues and message counts
- **Exchanges**: View routing configuration
- **Connections**: Monitor active connections
- **Channels**: View communication channels

### Log Monitoring

```bash
# Wavy logs
docker logs wavy1

# Aggregator logs
docker logs aggregator_temperature

# RabbitMQ logs
docker logs rabbitmq

# Follow logs in real-time
docker logs -f wavy1
```

### Performance Monitoring

#### Queue Metrics
- **Messages**: Number of unprocessed messages
- **Message Rate**: Messages per second
- **Consumer Count**: Number of active consumers

#### Key Indicators
- Queue depth should remain low
- Message rate should be steady
- No connection drops
- Consumer acknowledgment rate should match message rate

### Example Docker Commands

```bash
# Start system
docker-compose up -d

# Scale aggregators
docker-compose up -d --scale aggregator_temperature=3

# View running services
docker-compose ps

# Stop system
docker-compose down

# View system logs
docker-compose logs -f
```

This quick reference provides essential information for developers working with the Ocean Monitoring System's pub/sub architecture.
