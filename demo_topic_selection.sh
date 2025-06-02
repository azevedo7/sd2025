#!/bin/bash

# Topic-Based Subscription Demonstration Script
# This script demonstrates how different aggregators subscribe to different sensor topics

echo "==============================================="
echo "Ocean Monitoring System - Topic Subscription Demo"
echo "==============================================="

echo -e "\n🚀 Starting the Ocean Monitoring System with topic-based aggregators..."
docker-compose up -d

echo -e "\n⏳ Waiting for services to start (30 seconds)..."
sleep 30

echo -e "\n📊 Current Running Services:"
docker-compose ps

echo -e "\n🔍 Checking Aggregator Configurations:"

echo -e "\n1️⃣  AGGREGATOR 1 (Humidity + Water Level):"
echo "   - Subscribes to: humidity, waterLevel topics"
echo "   - Topic patterns: sensor.humidity.*.*, sensor.waterLevel.*.*"
docker logs aggregator1 2>/dev/null | grep -E "(Configuration|Subscribing|sensor\.humidity|sensor\.waterLevel)" | tail -5

echo -e "\n2️⃣  AGGREGATOR 2 (Temperature + Wind Speed):"
echo "   - Subscribes to: temperature, windSpeed topics"  
echo "   - Topic patterns: sensor.temperature.*.*, sensor.windSpeed.*.*"
docker logs aggregator2 2>/dev/null | grep -E "(Configuration|Subscribing|sensor\.temperature|sensor\.windSpeed)" | tail -5

echo -e "\n3️⃣  ENVIRONMENTAL AGGREGATOR (All Sensors):"
echo "   - Subscribes to: temperature, humidity, windSpeed, waterLevel topics"
echo "   - Topic patterns: All sensor topic patterns"
docker logs aggregator_environmental 2>/dev/null | grep -E "(Configuration|Subscribing)" | tail -5

echo -e "\n4️⃣  TEMPERATURE-ONLY AGGREGATOR:"
echo "   - Subscribes to: temperature topics only"
echo "   - Topic patterns: sensor.temperature.*.*"
docker logs aggregator_temp_only 2>/dev/null | grep -E "(Configuration|Subscribing|sensor\.temperature)" | tail -5

echo -e "\n5️⃣  MAINTENANCE AGGREGATOR:"
echo "   - Subscribes to: maintenance messages from all sensors"
echo "   - Focus on: maintenance_up, maintenance_down messages"
docker logs aggregator_maintenance 2>/dev/null | grep -E "(Configuration|Subscribing|maintenance)" | tail -5

echo -e "\n🐰 RabbitMQ Management Interface:"
echo "   URL: http://localhost:15672"
echo "   Username: oceanguest"
echo "   Password: oceanpass"
echo "   💡 Check the 'Queues' tab to see topic-based queues"

echo -e "\n📡 Monitoring Message Flow:"
echo "   Watch aggregator logs to see topic-based message routing:"

echo -e "\n   For Aggregator 1 (humidity + waterLevel):"
echo "   docker logs -f aggregator1"

echo -e "\n   For Aggregator 2 (temperature + windSpeed):"
echo "   docker logs -f aggregator2"

echo -e "\n   For Environmental Aggregator (all sensors):"
echo "   docker logs -f aggregator_environmental"

echo -e "\n🔧 Topic Selection Commands:"

echo -e "\n   To add a new pressure-only aggregator:"
echo "   # Add to docker-compose.yml:"
echo "   environment:"
echo "     - AGGREGATOR_ID=pressure_agg"
echo "     - SENSOR_TYPES=pressure"
echo "     - AGGREGATOR_TYPE=pressure"

echo -e "\n   To create a custom multi-sensor aggregator:"
echo "   environment:"
echo "     - AGGREGATOR_ID=custom_agg"
echo "     - SENSOR_TYPES=temperature,humidity,pressure"
echo "     - AGGREGATOR_TYPE=environmental"

echo -e "\n🧪 Testing Topic Routing:"

echo -e "\n   1. Check which messages each aggregator receives:"
for aggregator in aggregator1 aggregator2 aggregator_environmental aggregator_temp_only; do
    echo -e "\n   📦 $aggregator recent activity:"
    docker logs $aggregator 2>/dev/null | grep -E "(Processing message|Saved.*from)" | tail -3
done

echo -e "\n✅ Topic-based subscription system is active!"
echo "   Each aggregator will only process messages for its subscribed sensor types."
echo "   Check the logs above to see the selective message processing in action."

echo -e "\n📚 For more information, see:"
echo "   - docs/Topic-Subscription-Guide.md"
echo "   - docs/Quick-Reference.md"
echo "   - docs/PubSub-Architecture.md"

echo -e "\n🛑 To stop the demo:"
echo "   docker-compose down"

echo -e "\n==============================================="
echo "Demo completed! Monitor the logs to see topic-based routing in action."
echo "==============================================="
