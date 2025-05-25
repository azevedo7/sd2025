import socket
import json
import grpc
import sensor_analysis_pb2
import sensor_analysis_pb2_grpc
import logging
import time
from typing import List, Dict, Any, Optional

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class RealOceanDataClient:
    """
    Client that connects to the real C# Ocean Monitoring Server
    and integrates with the Python analysis service
    """
    
    def __init__(self, server_host='127.0.0.1', server_port=8080, analysis_port=50052):
        self.server_host = server_host
        self.server_port = server_port
        self.analysis_port = analysis_port
        self.socket = None
        self.client_id = "PythonAnalysisClient"
    
    def connect_to_server(self) -> bool:
        """
        Connect to the C# Ocean Monitoring Server using the existing protocol
        """
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.settimeout(10)  # 10 second timeout
            self.socket.connect((self.server_host, self.server_port))
            
            # Send connection request using the protocol from your system
            conn_req = f"CONN_REQ|{self.client_id}|END"
            self.socket.send(conn_req.encode('ascii'))
            
            # Wait for acknowledgment
            response = self.socket.recv(1024).decode('ascii')
            logger.info(f"Server response: {response}")
            
            if "CONN_ACK" in response:
                logger.info("Successfully connected to Ocean Monitoring Server")
                return True
            else:
                logger.error("Failed to get connection acknowledgment")
                return False
                
        except Exception as e:
            logger.error(f"Failed to connect to server: {e}")
            return False
    
    def disconnect_from_server(self):
        """
        Properly disconnect from the server
        """
        try:
            if self.socket:
                disc_req = f"DISC_REQ||END"
                self.socket.send(disc_req.encode('ascii'))
                response = self.socket.recv(1024).decode('ascii')
                logger.info(f"Disconnect response: {response}")
                self.socket.close()
                logger.info("Disconnected from server")
        except Exception as e:
            logger.error(f"Error during disconnect: {e}")
    
    def request_data_export(self) -> Optional[str]:
        """
        Since the C# server exports data to CSV, we can work with that file
        This method would trigger an export (if such functionality existed)
        For now, we'll read the existing export file
        """
        # The C# server creates "SensorDataExport.csv" when viewing all data
        # We'll read this file if it exists
        import os
        csv_file = "/Users/joaoazevedo/Documents/Utad/3.2/sd/sd2025/OceanMonitoringSystem/Server/SensorDataExport.csv"
        
        if os.path.exists(csv_file):
            logger.info("Found existing sensor data export file")
            return csv_file
        else:
            logger.warning("No sensor data export file found. Run 'View all sensor data' from the C# server first.")
            return None
    
    def parse_csv_data(self, csv_file: str) -> Dict[str, List[float]]:
        """
        Parse the CSV file exported by the C# server
        """
        data_by_type = {
            'temperature': [],
            'humidity': [],
            'waterLevel': [],
            'windSpeed': []
        }
        
        try:
            with open(csv_file, 'r') as f:
                lines = f.readlines()
                
            # Skip header
            for line in lines[1:]:
                parts = line.strip().split(',')
                if len(parts) >= 5:
                    # CSV format: WavyId,AggregatorId,DataType,Timestamp,RawValue,ReceivedAt
                    data_type = parts[2]
                    raw_value = parts[4]
                    
                    try:
                        value = float(raw_value)
                        if data_type in data_by_type:
                            data_by_type[data_type].append(value)
                    except ValueError:
                        continue  # Skip non-numeric values
            
            # Log statistics
            for data_type, values in data_by_type.items():
                logger.info(f"Loaded {len(values)} {data_type} readings")
            
            return data_by_type
            
        except Exception as e:
            logger.error(f"Error parsing CSV file: {e}")
            return {}
    
    def analyze_with_grpc(self, data_type: str, values: List[float]) -> Optional[Dict[str, Any]]:
        """
        Send data to the gRPC analysis service
        """
        if not values:
            logger.warning(f"No values to analyze for {data_type}")
            return None
        
        try:
            with grpc.insecure_channel(f'localhost:{self.analysis_port}') as channel:
                stub = sensor_analysis_pb2_grpc.SensorDataAnalysisServiceStub(channel)
                
                request = sensor_analysis_pb2.SensorDataRequest(
                    data_type=data_type,
                    values=values
                )
                
                response = stub.AnalyzeSensorData(request)
                
                return {
                    'data_type': data_type,
                    'average': response.average,
                    'min': response.min,
                    'max': response.max,
                    'std_dev': response.std_dev,
                    'count': response.count,
                    'median': response.median
                }
                
        except Exception as e:
            logger.error(f"gRPC analysis failed for {data_type}: {e}")
            return None
    
    def run_real_analysis(self):
        """
        Run analysis on real data from the C# server
        """
        print("🌊 REAL Ocean Monitoring Data Analysis")
        print("=" * 80)
        
        # Step 1: Try to connect to the server
        print("Step 1: Connecting to C# Ocean Monitoring Server...")
        if self.connect_to_server():
            print("✅ Connected successfully")
            self.disconnect_from_server()
        else:
            print("❌ Connection failed, but continuing with existing data...")
        
        # Step 2: Get the data export file
        print("\nStep 2: Looking for sensor data export...")
        csv_file = self.request_data_export()
        
        if not csv_file:
            print("❌ No data file found. Please:")
            print("   1. Start your C# Server")
            print("   2. Select option '1. View all sensor data'")
            print("   3. Run this Python client again")
            return
        
        # Step 3: Parse the CSV data
        print("✅ Found data export file")
        print("\nStep 3: Parsing sensor data...")
        data_by_type = self.parse_csv_data(csv_file)
        
        if not any(values for values in data_by_type.values()):
            print("❌ No valid sensor data found")
            return
        
        # Step 4: Analyze each data type
        print("\nStep 4: Running statistical analysis...")
        print("=" * 80)
        
        for data_type, values in data_by_type.items():
            if not values:
                continue
                
            print(f"\n📊 {data_type.upper()} ANALYSIS")
            print("-" * 40)
            
            result = self.analyze_with_grpc(data_type, values)
            
            if result:
                print(f"Sample Size: {result['count']:,} readings")
                print(f"Average: {result['average']:.2f}")
                print(f"Median: {result['median']:.2f}")
                print(f"Minimum: {result['min']:.2f}")
                print(f"Maximum: {result['max']:.2f}")
                print(f"Standard Deviation: {result['std_dev']:.2f}")
                
                # Additional insights
                range_val = result['max'] - result['min']
                cv = (result['std_dev'] / result['average']) * 100 if result['average'] != 0 else 0
                
                print(f"Range: {range_val:.2f}")
                print(f"Coefficient of Variation: {cv:.1f}%")
                
                # Interpretation
                if cv < 10:
                    variability = "Low variability (consistent readings)"
                elif cv < 30:
                    variability = "Moderate variability"
                else:
                    variability = "High variability (readings vary significantly)"
                
                print(f"Interpretation: {variability}")
                
        print("\n" + "=" * 80)
        print("✅ Analysis Complete!")
        print("\nNote: This analysis used the gRPC service for statistical calculations.")

def main():
    """
    Main function
    """
    print("🔬 Ocean Data Analysis with gRPC Service")
    print("This tool connects to your C# server and analyzes real sensor data")
    print("Make sure the analysis_server.py is running on port 50052\n")
    
    client = RealOceanDataClient()
    client.run_real_analysis()

if __name__ == '__main__':
    main()
