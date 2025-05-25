# Ocean Monitoring System - Python gRPC Analysis Service

This Python service provides statistical analysis for the Ocean Monitoring System using gRPC. It connects to your C# server and performs basic statistical analysis on sensor data.

## Features

- **Basic Statistical Analysis**: Mean, min, max, standard deviation, median, count
- **gRPC Service**: High-performance communication
- **Real Data Integration**: Connects to your existing C# Ocean Monitoring Server
- **Multiple Data Types**: Supports temperature, humidity, waterLevel, windSpeed

## Files Structure

```
PythonAnalysisService/
├── sensor_analysis.proto          # gRPC service definition
├── sensor_analysis_pb2.py         # Generated Python protobuf code
├── sensor_analysis_pb2_grpc.py    # Generated gRPC code
├── analysis_server.py             # gRPC server implementation
├── analysis_client.py             # Demo client with simulated data
├── real_data_client.py            # Client that uses real C# server data
├── requirements.txt               # Python dependencies
└── README.md                      # This file
```

## Setup Instructions

### 1. Install Python Dependencies

```bash
cd PythonAnalysisService
pip install -r requirements.txt
```

### 2. Start the gRPC Analysis Server

```bash
python analysis_server.py
```

The server will start on port `50052` and display:
```
INFO:__main__:Sensor Data Analysis Service started on [::]:50052
```

### 3. Test with Demo Data

In a new terminal, run the demo client:

```bash
python analysis_client.py
```

This will show a demonstration with simulated sensor data.

### 4. Analyze Real Data from C# Server

To analyze real data from your C# Ocean Monitoring Server:

1. **Start your C# Server** (from the Server directory)
2. **Export data**: In the C# server menu, select "1. View all sensor data" to create the CSV export
3. **Run the real data client**:
   ```bash
   python real_data_client.py
   ```

## Usage Example

### gRPC Service Definition

```protobuf
service SensorDataAnalysisService {
  rpc AnalyzeSensorData (SensorDataRequest) returns (SensorDataAnalysisResponse);
}

message SensorDataRequest {
  string data_type = 1;           // "temperature", "humidity", etc.
  repeated double values = 2;     // The sensor values
}

message SensorDataAnalysisResponse {
  double average = 1;
  double min = 2;
  double max = 3;
  double std_dev = 4;
  int32 count = 5;
  double median = 6;
}
```

### Python Client Code Example

```python
import grpc
import sensor_analysis_pb2
import sensor_analysis_pb2_grpc

# Connect to the gRPC service
with grpc.insecure_channel('localhost:50052') as channel:
    stub = sensor_analysis_pb2_grpc.SensorDataAnalysisServiceStub(channel)
    
    # Create request
    request = sensor_analysis_pb2.SensorDataRequest(
        data_type="temperature",
        values=[20.5, 21.0, 19.8, 22.1, 20.9]
    )
    
    # Call the service
    response = stub.AnalyzeSensorData(request)
    
    print(f"Average: {response.average}")
    print(f"Min: {response.min}")
    print(f"Max: {response.max}")
    print(f"Std Dev: {response.std_dev}")
```

## Integration with C# Server

The `real_data_client.py` integrates with your existing C# Ocean Monitoring System:

1. **Connects** to the C# server using the existing protocol (`CONN_REQ`/`CONN_ACK`)
2. **Reads** the CSV export file that the C# server creates
3. **Parses** the sensor data by type
4. **Sends** the data to the gRPC analysis service
5. **Displays** comprehensive statistical analysis

## Sample Output

```
🌊 REAL Ocean Monitoring Data Analysis
================================================================================
Step 1: Connecting to C# Ocean Monitoring Server...
✅ Connected successfully

Step 2: Looking for sensor data export...
✅ Found data export file

Step 3: Parsing sensor data...
INFO:__main__:Loaded 580 temperature readings
INFO:__main__:Loaded 445 humidity readings
INFO:__main__:Loaded 442 waterLevel readings
INFO:__main__:Loaded 571 windSpeed readings

Step 4: Running statistical analysis...
================================================================================

📊 TEMPERATURE ANALYSIS
----------------------------------------
Sample Size: 580 readings
Average: 15.23
Median: 15.41
Minimum: -4.99
Maximum: 29.98
Standard Deviation: 8.67
Range: 34.97
Coefficient of Variation: 56.9%
Interpretation: High variability (readings vary significantly)

📊 HUMIDITY ANALYSIS
----------------------------------------
Sample Size: 445 readings
Average: 64.51
Median: 64.00
Minimum: 30.00
Maximum: 99.00
Standard Deviation: 19.84
Range: 69.00
Coefficient of Variation: 30.8%
Interpretation: Moderate variability
```

## Architecture

```
┌─────────────────┐    CSV Export    ┌─────────────────┐
│   C# Server     │ ──────────────→ │  Python Client  │
│ (Ocean Monitor) │                  │                 │
└─────────────────┘                  └─────────────────┘
                                              │
                                              │ gRPC Call
                                              ▼
                                     ┌─────────────────┐
                                     │ Python gRPC     │
                                     │ Analysis Server │
                                     │   (Port 50052)  │
                                     └─────────────────┘
```

## Extending the Service

To add more analysis features:

1. **Update the .proto file** with new methods
2. **Regenerate** the Python code:
   ```bash
   python -m grpc_tools.protoc --proto_path=. --python_out=. --grpc_python_out=. sensor_analysis.proto
   ```
3. **Implement** the new methods in `analysis_server.py`

## Troubleshooting

### Common Issues

1. **Port 50052 already in use**
   - Change the port in both server and client
   - Kill existing processes: `lsof -ti:50052 | xargs kill`

2. **No CSV export file found**
   - Run the C# server
   - Select "1. View all sensor data" to create the export
   - Check the path in `real_data_client.py`

3. **gRPC connection failed**
   - Ensure `analysis_server.py` is running
   - Check firewall settings
   - Verify the port number

### Logs

The services provide detailed logging. Check the console output for:
- Connection status
- Data parsing results
- Analysis statistics
- Error messages

## Performance

- **gRPC**: Efficient binary protocol
- **Concurrent**: Supports multiple simultaneous analysis requests
- **Memory efficient**: Streams large datasets
- **Fast**: Calculates statistics for thousands of data points in milliseconds

## Next Steps

Consider extending this service with:
- Time series analysis
- Anomaly detection
- Data correlation analysis
- Predictive modeling
- Real-time streaming analysis
