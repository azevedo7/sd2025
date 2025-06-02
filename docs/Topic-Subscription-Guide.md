# Topic-Based Aggregator Subscription Guide

## Overview

The Ocean Monitoring System now supports **topic-based subscription selection** for aggregators, allowing you to configure which sensor types and message types each aggregator subscribes to. This provides flexible, scalable sensor data routing.

## How It Works

### Topic Structure
The system uses a hierarchical topic structure:
```
sensor.{sensor_type}.{wavy_id}.{message_type}
```

**Examples:**
- `sensor.temperature.wavy1.sensor_data` - Temperature data from Wavy1
- `sensor.humidity.wavy2.maintenance_up` - Maintenance message from Wavy2
- `sensor.windSpeed.wavy3.sensor_data` - Wind speed data from Wavy3

### Subscription Patterns
Aggregators subscribe to topics using RabbitMQ routing patterns:
- `sensor.temperature.*.*` - All temperature messages from any Wavy device
- `sensor.*.wavy1.*` - All messages from Wavy1
- `sensor.humidity.*.sensor_data` - Only humidity data (no maintenance)

## Configuration Methods

### 1. Using SENSOR_TYPES Environment Variable

The primary way to configure aggregator subscriptions is through the `SENSOR_TYPES` environment variable:

```yaml
environment:
  - SENSOR_TYPES=temperature,humidity  # Subscribe to temperature and humidity topics
```

**Available Sensor Types:**
- `temperature` - Temperature sensor data
- `humidity` - Humidity sensor data  
- `windSpeed` - Wind speed sensor data
- `waterLevel` - Water level sensor data

### 2. Using AGGREGATOR_TYPE for Specialized Behavior

You can also set an `AGGREGATOR_TYPE` for predefined subscription patterns:

```yaml
environment:
  - AGGREGATOR_TYPE=environmental  # Subscribe to all environmental sensors
```

**Available Aggregator Types:**
- `temperature` - Temperature-only monitoring
- `environmental` - All environmental sensors (temperature, humidity, windSpeed, waterLevel)
- `maintenance` - Focus on maintenance messages from all sensors
- `general` - Subscribe to all sensor data

## Current Docker Compose Configuration

The system is configured with several specialized aggregators:

### aggregator1 (Humidity + Water Level)
```yaml
aggregator1:
  environment:
    - AGGREGATOR_ID=humidity_agg
    - SENSOR_TYPES=humidity,waterLevel
```
**Subscribes to:**
- `sensor.humidity.*.sensor_data`
- `sensor.humidity.*.maintenance_up`
- `sensor.humidity.*.maintenance_down`
- `sensor.waterLevel.*.sensor_data`
- `sensor.waterLevel.*.maintenance_up`
- `sensor.waterLevel.*.maintenance_down`

### aggregator2 (Temperature + Wind Speed)
```yaml
aggregator2:
  environment:
    - AGGREGATOR_ID=temperature_agg
    - SENSOR_TYPES=temperature,windSpeed
```
**Subscribes to:**
- `sensor.temperature.*.sensor_data`
- `sensor.temperature.*.maintenance_up`
- `sensor.temperature.*.maintenance_down`
- `sensor.windSpeed.*.sensor_data`
- `sensor.windSpeed.*.maintenance_up`
- `sensor.windSpeed.*.maintenance_down`

### aggregator_environmental (All Environmental Sensors)
```yaml
aggregator_environmental:
  environment:
    - AGGREGATOR_ID=environmental_agg
    - SENSOR_TYPES=temperature,humidity,windSpeed,waterLevel
    - AGGREGATOR_TYPE=environmental
```
**Subscribes to:** All sensor types and all message types

### aggregator_temp_only (Temperature Only)
```yaml
aggregator_temp_only:
  environment:
    - AGGREGATOR_ID=temp_only_agg
    - SENSOR_TYPES=temperature
    - AGGREGATOR_TYPE=temperature
```
**Subscribes to:** Only temperature sensor messages

### aggregator_maintenance (Maintenance Focus)
```yaml
aggregator_maintenance:
  environment:
    - AGGREGATOR_ID=maintenance_agg
    - SENSOR_TYPES=temperature,humidity,windSpeed,waterLevel
    - AGGREGATOR_TYPE=maintenance
```
**Subscribes to:** Maintenance messages from all sensor types

## Message Types

Each subscription includes these message types:
- `sensor_data` - Regular sensor readings
- `maintenance_up` - Device entering maintenance mode
- `maintenance_down` - Device exiting maintenance mode

## How to Add New Aggregators

### Example 1: Specific Sensor Type Aggregator
```yaml
my_pressure_aggregator:
  build:
    context: ./OceanMonitoringSystem
    dockerfile: Aggregator/Dockerfile
  environment:
    - AGGREGATOR_ID=pressure_agg
    - SENSOR_TYPES=pressure  # Only pressure sensors
    - AGGREGATOR_PORT=9005
    - SERVER_IP=server
    - SERVER_PORT=8080
    - RABBITMQ_HOST=rabbitmq
    - RABBITMQ_PORT=5672
    - RABBITMQ_USER=oceanguest
    - RABBITMQ_PASSWORD=oceanpass
  ports:
    - "9005:9005"
  # ... rest of configuration
```

### Example 2: Multi-Sensor Aggregator
```yaml
my_combined_aggregator:
  environment:
    - AGGREGATOR_ID=combined_agg
    - SENSOR_TYPES=temperature,pressure,pH  # Multiple sensor types
    - AGGREGATOR_TYPE=environmental
```

### Example 3: Device-Specific Aggregator
For monitoring specific Wavy devices, you would need to modify the code to support device-specific patterns like:
```
sensor.*.wavy1.*  # All messages from Wavy1
sensor.*.wavy2.*  # All messages from Wavy2
```

## Benefits of Topic-Based Subscription

1. **Scalability** - Easy to add new aggregators without changing existing ones
2. **Flexibility** - Each aggregator can subscribe to exactly what it needs
3. **Performance** - Aggregators only process relevant messages
4. **Specialization** - Different aggregators can have specialized processing logic
5. **Load Distribution** - Distribute processing load across multiple aggregators

## Testing the Configuration

1. **Start the system:**
   ```bash
   docker-compose up -d
   ```

2. **Check RabbitMQ Management UI:**
   - Visit `http://localhost:15672`
   - Login: `oceanguest` / `oceanpass`
   - View queues and routing to see topic subscriptions

3. **Monitor aggregator logs:**
   ```bash
   docker logs aggregator1
   docker logs aggregator2
   docker logs aggregator_environmental
   ```

4. **Check message routing:**
   Each aggregator will only show messages for its subscribed sensor types.

## Advanced Customization

For more advanced routing patterns, you can modify the aggregator code to support:
- Custom topic patterns
- Complex filtering logic
- Priority-based message handling
- Room-based subscriptions (as documented in other guides)

The topic-based subscription system provides a foundation for sophisticated sensor data routing in your Ocean Monitoring System.
