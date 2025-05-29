# Deployment and Configuration Guide

## Overview
This guide provides step-by-step instructions for deploying and configuring the Ocean Monitoring System with RabbitMQ pub/sub architecture.

## Prerequisites

### System Requirements
- Docker Engine 20.10+
- Docker Compose 2.0+
- Minimum 4GB RAM
- Minimum 10GB disk space

### Network Requirements
- Port 5672 (RabbitMQ AMQP)
- Port 15672 (RabbitMQ Management UI)
- Port 8080 (Server)

## Deployment Steps

### Step 1: Clone and Prepare

```bash
# Clone the repository
git clone <repository-url>
cd sd2025

# Verify Docker installation
docker --version
docker-compose --version
```

### Step 2: Configuration Files

#### Docker Compose Configuration
The `docker-compose.yml` includes all necessary services:

```yaml
# Key services configured:
# - rabbitmq: Message broker
# - server: Central server
# - wavy1, wavy2, wavy3: Sensor devices  
# - aggregator_temperature: Temperature room aggregator
# - aggregator_environmental: Multi-sensor aggregator
# - aggregator_maintenance: Maintenance monitoring
```

#### Environment Variables
Create `.env` file in project root:

```bash
# RabbitMQ Configuration
RABBITMQ_DEFAULT_USER=oceanguest
RABBITMQ_DEFAULT_PASS=oceanpass
RABBITMQ_HOST=rabbitmq
RABBITMQ_PORT=5672

# Aggregator Configuration
AGGREGATOR_TEMPERATURE_ID=temp_agg_001
AGGREGATOR_ENVIRONMENTAL_ID=env_agg_001
AGGREGATOR_MAINTENANCE_ID=maint_agg_001

# Wavy Configuration
WAVY1_ID=wavy001
WAVY1_SENSOR_TYPE=temperature
WAVY2_ID=wavy002
WAVY2_SENSOR_TYPE=humidity
WAVY3_ID=wavy003
WAVY3_SENSOR_TYPE=pressure

# Server Configuration
SERVER_PORT=8080
```

### Step 3: Build and Deploy

```bash
# Build all services
docker-compose build

# Start the system
docker-compose up -d

# Verify all services are running
docker-compose ps
```

Expected output:
```
NAME                  COMMAND                  SERVICE               STATUS
aggregator_environ…   "dotnet Aggregator.dll"  aggregator_environ…   running
aggregator_mainten…   "dotnet Aggregator.dll"  aggregator_mainten…   running
aggregator_temper…    "dotnet Aggregator.dll"  aggregator_temper…    running
rabbitmq              "docker-entrypoint.s…"   rabbitmq              running
server                "dotnet Server.dll"      server                running
wavy1                 "dotnet Wavy.dll"        wavy1                 running
wavy2                 "dotnet Wavy.dll"        wavy2                 running
wavy3                 "dotnet Wavy.dll"        wavy3                 running
```

### Step 4: Verify Deployment

#### Check RabbitMQ Management UI
1. Open browser to `http://localhost:15672`
2. Login with `oceanguest` / `oceanpass`
3. Verify exchanges and queues are created:
   - Exchange: `ocean_monitoring_exchange`
   - Queues: `temperature_room_queue`, `environmental_room_queue`, etc.

#### Check Service Logs
```bash
# Check RabbitMQ logs
docker logs rabbitmq

# Check Wavy logs
docker logs wavy1
docker logs wavy2
docker logs wavy3

# Check Aggregator logs
docker logs aggregator_temperature
docker logs aggregator_environmental
docker logs aggregator_maintenance
```

#### Verify Message Flow
Look for log messages indicating:
- Wavy sensors publishing to topics
- Aggregators receiving messages from subscribed rooms
- Successful message processing

## Configuration Options

### Room Configuration

#### Predefined Room Types
You can configure aggregators for different room types by setting `AGGREGATOR_TYPE`:

| Type | Rooms Subscribed | Use Case |
|------|------------------|----------|
| `temperature` | Temperature Room | Temperature-only monitoring |
| `humidity` | Humidity Room | Humidity-only monitoring |
| `pressure` | Pressure Room | Pressure-only monitoring |
| `environmental` | Temperature + Humidity + Pressure | Multi-sensor environmental monitoring |
| `maintenance` | Maintenance Room | Device maintenance monitoring |
| `critical_monitor` | Specific critical devices | Monitor critical Wavy devices |
| `general` | All data | Monitor all sensor data |

#### Custom Room Configuration
You can create custom aggregator configurations by modifying the aggregator code:

```csharp
// Example: Custom room for specific sensors
var customRoom = new RoomConfig
{
    RoomName = "custom_monitoring_room",
    TopicPatterns = new[] { 
        "sensor.temperature.wavy001.*",
        "sensor.pressure.wavy003.*"
    },
    Description = "Custom monitoring for specific sensors"
};
```

### Scaling Configuration

#### Horizontal Scaling
Scale specific services based on load:

```bash
# Scale temperature aggregators
docker-compose up -d --scale aggregator_temperature=3

# Scale Wavy sensors
docker-compose up -d --scale wavy1=2

# Scale all aggregators
docker-compose up -d --scale aggregator_environmental=2 --scale aggregator_maintenance=2
```

#### Resource Limits
Add resource limits to `docker-compose.yml`:

```yaml
services:
  rabbitmq:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G

  aggregator_temperature:
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
```

### Performance Tuning

#### RabbitMQ Configuration
Optimize RabbitMQ for your use case:

```yaml
rabbitmq:
  environment:
    RABBITMQ_DEFAULT_USER: oceanguest
    RABBITMQ_DEFAULT_PASS: oceanpass
    RABBITMQ_VM_MEMORY_HIGH_WATERMARK: 0.8
    RABBITMQ_DISK_FREE_LIMIT: 2GB
    RABBITMQ_HEARTBEAT: 60
```

#### Queue Settings
Configure queue properties for optimal performance:

```csharp
// In RabbitMQService.cs
_channel.QueueDeclare(
    queue: queueName,
    durable: true,           // Survive broker restarts
    exclusive: false,        // Allow multiple consumers
    autoDelete: false,       // Don't auto-delete when empty
    arguments: new Dictionary<string, object>
    {
        ["x-message-ttl"] = 86400000,     // 24 hours TTL
        ["x-max-length"] = 10000,         // Max queue length
        ["x-overflow"] = "drop-head"      // Drop oldest when full
    }
);
```

#### Consumer Settings
Optimize consumer performance:

```csharp
// Set prefetch count for better load distribution
_channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

// Use manual acknowledgments for reliability
_channel.BasicConsume(
    queue: queueName,
    autoAck: false,  // Manual acknowledgment
    consumer: consumer
);
```

## Monitoring and Maintenance

### Health Checks

#### Service Health
All services include health checks in the Docker Compose configuration:

```yaml
healthcheck:
  test: rabbitmq-diagnostics -q ping
  interval: 30s
  timeout: 30s
  retries: 3
```

#### Custom Health Monitoring Script
Create a monitoring script:

```bash
#!/bin/bash
# health_check.sh

echo "=== Ocean Monitoring System Health Check ==="

# Check RabbitMQ
if docker exec rabbitmq rabbitmq-diagnostics -q ping > /dev/null 2>&1; then
    echo "✅ RabbitMQ: Healthy"
else
    echo "❌ RabbitMQ: Unhealthy"
fi

# Check queue depths
echo "\n📊 Queue Status:"
docker exec rabbitmq rabbitmqctl list_queues name messages

# Check service status
echo "\n🐳 Service Status:"
docker-compose ps --services --filter "status=running" | wc -l
echo "Running services"

# Check recent logs for errors
echo "\n📋 Recent Errors:"
docker-compose logs --tail=50 | grep -i error || echo "No recent errors"
```

### Log Management

#### Log Rotation
Configure log rotation to prevent disk space issues:

```yaml
services:
  wavy1:
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

#### Centralized Logging
For production environments, consider using a centralized logging solution:

```yaml
# Add logging service
logging:
  image: grafana/loki:latest
  ports:
    - "3100:3100"
```

### Backup and Recovery

#### Data Backup
Important data to backup:
- RabbitMQ data: `/var/lib/rabbitmq`
- Server data: `/app/data`
- Configuration files

```bash
# Backup script
#!/bin/bash
BACKUP_DIR="/backup/$(date +%Y%m%d_%H%M%S)"
mkdir -p $BACKUP_DIR

# Backup RabbitMQ data
docker run --rm -v rabbitmq_data:/data -v $BACKUP_DIR:/backup alpine tar czf /backup/rabbitmq_data.tar.gz -C /data .

# Backup server data
docker run --rm -v server_data:/data -v $BACKUP_DIR:/backup alpine tar czf /backup/server_data.tar.gz -C /data .

# Backup configuration
cp docker-compose.yml $BACKUP_DIR/
cp .env $BACKUP_DIR/
```

#### Recovery Procedure
1. Stop all services: `docker-compose down`
2. Restore data volumes from backup
3. Restart services: `docker-compose up -d`
4. Verify system health

## Production Deployment

### Security Considerations

#### Change Default Credentials
```bash
# Generate secure passwords
RABBITMQ_PASSWORD=$(openssl rand -base64 32)
echo "RABBITMQ_DEFAULT_PASS=$RABBITMQ_PASSWORD" >> .env
```

#### Network Security
```yaml
# Use custom network with restricted access
networks:
  ocean_network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16
```

#### SSL/TLS Configuration
For production, enable SSL for RabbitMQ:

```yaml
rabbitmq:
  environment:
    RABBITMQ_SSL_CERTFILE: /etc/rabbitmq/ssl/cert.pem
    RABBITMQ_SSL_KEYFILE: /etc/rabbitmq/ssl/key.pem
    RABBITMQ_SSL_CACERTFILE: /etc/rabbitmq/ssl/ca.pem
  volumes:
    - ./ssl:/etc/rabbitmq/ssl:ro
```

### High Availability

#### RabbitMQ Clustering
For high availability, deploy RabbitMQ in cluster mode:

```yaml
rabbitmq1:
  image: rabbitmq:3-management-alpine
  environment:
    RABBITMQ_ERLANG_COOKIE: cluster_cookie
    RABBITMQ_NODENAME: rabbit@rabbitmq1

rabbitmq2:
  image: rabbitmq:3-management-alpine
  environment:
    RABBITMQ_ERLANG_COOKIE: cluster_cookie
    RABBITMQ_NODENAME: rabbit@rabbitmq2
```

#### Load Balancing
Use a load balancer for multiple aggregator instances:

```yaml
nginx:
  image: nginx:alpine
  ports:
    - "80:80"
  volumes:
    - ./nginx.conf:/etc/nginx/nginx.conf
  depends_on:
    - aggregator_temperature
```

### Troubleshooting Common Deployment Issues

#### Issue 1: Port Conflicts
**Symptom**: Port already in use errors
**Solution**: 
```bash
# Check what's using the port
netstat -tulpn | grep :5672
# Kill the process or change port in docker-compose.yml
```

#### Issue 2: Memory Issues
**Symptom**: Services crashing due to OOM
**Solution**:
```bash
# Check memory usage
docker stats
# Increase Docker memory limits or add swap
```

#### Issue 3: Network Issues
**Symptom**: Services can't communicate
**Solution**:
```bash
# Check network configuration
docker network ls
docker network inspect sd2025_ocean_network
# Recreate network if needed
docker-compose down && docker-compose up -d
```

#### Issue 4: Data Persistence
**Symptom**: Data lost after restart
**Solution**:
```bash
# Verify volumes are created
docker volume ls
# Check volume mounts in docker-compose.yml
```

This deployment guide provides comprehensive instructions for setting up, configuring, and maintaining the Ocean Monitoring System in various environments.
