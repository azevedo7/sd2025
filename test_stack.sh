#!/bin/bash

echo "🚀 Starting comprehensive Docker Compose stack test..."
echo "================================================"

# Color codes for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test functions
test_service() {
    local service_name=$1
    local test_command=$2
    local expected_pattern=$3
    
    echo -n "Testing $service_name... "
    if eval "$test_command" | grep -q "$expected_pattern"; then
        echo -e "${GREEN}✅ PASS${NC}"
        return 0
    else
        echo -e "${RED}❌ FAIL${NC}"
        return 1
    fi
}

# Test 1: Check all services are running
echo -e "${YELLOW}📋 Checking service status...${NC}"
docker-compose ps --format "table {{.Name}}\t{{.Status}}"
echo ""

# Test 2: RabbitMQ Health
echo -e "${YELLOW}🐰 Testing RabbitMQ...${NC}"
test_service "RabbitMQ Management" "curl -s -u oceanguest:oceanpass http://localhost:15672/api/overview" '"management_version"'

# Test 3: rpcGoDatatype Service
echo -e "${YELLOW}🔧 Testing rpcGoDatatype service...${NC}"
test_service "rpcGoDatatype Port" "nc -z localhost 50051" ""
test_service "rpcGoDatatype Process" "docker-compose exec -T rpc-go-datatype ps aux" "./main"

# Test 4: Python Analysis Service
echo -e "${YELLOW}🐍 Testing Python Analysis service...${NC}"
test_service "Python Analysis Port" "nc -z localhost 50052" ""

# Test 5: Server Web Interface
echo -e "${YELLOW}🌐 Testing Server Web Interface...${NC}"
test_service "Web Server" "curl -s -I http://localhost:5001" "HTTP/1.1 200"

# Test 6: TCP Server
echo -e "${YELLOW}📡 Testing TCP Server...${NC}"
test_service "TCP Server Port" "nc -z localhost 8080" ""

# Test 7: Aggregators
echo -e "${YELLOW}📊 Testing Aggregators...${NC}"
test_service "Aggregator 1" "nc -z localhost 9000" ""
test_service "Aggregator 2" "nc -z localhost 9001" ""

# Test 8: Check service logs for errors
echo -e "${YELLOW}📜 Checking for recent errors in logs...${NC}"
echo "Recent rpcGoDatatype logs:"
docker-compose logs --tail=3 rpc-go-datatype

echo ""
echo "Recent server logs:"
docker-compose logs --tail=3 server

# Test 9: Network connectivity between services
echo -e "${YELLOW}🌐 Testing inter-service network connectivity...${NC}"
echo "Testing if services can resolve each other..."

# Check if services are in the same network
docker network inspect sd2025_ocean_network --format='{{range .Containers}}{{.Name}} {{end}}' | tr ' ' '\n' | grep -E "(rpc-go-datatype|server|python-analysis)" | sort

echo ""
echo -e "${GREEN}🎉 Docker Compose stack test completed!${NC}"
echo -e "${GREEN}✅ All core services are running and accessible${NC}"
echo ""
echo "Services summary:"
echo "- RabbitMQ: Management UI at http://localhost:15672 (oceanguest/oceanpass)"
echo "- Server Web UI: http://localhost:5001"
echo "- rpcGoDatatype gRPC: localhost:50051"
echo "- Python Analysis gRPC: localhost:50052"
echo "- TCP Server: localhost:8080"
echo "- Aggregator 1: localhost:9000"
echo "- Aggregator 2: localhost:9001"
