import grpc
import sensor_analysis_pb2
import sensor_analysis_pb2_grpc
import socket
import json
import time
import logging
from typing import List, Dict, Any

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class OceanDataClient:
    """
    Client that connects to the C# Ocean Monitoring Server,
    fetches sensor data, and sends it to the Python analysis service
    """
    
    def __init__(self, server_host='127.0.0.1', server_port=8080, analysis_service_port=50052):
        self.server_host = server_host
        self.server_port = server_port
        self.analysis_service_port = analysis_service_port
        
    def connect_to_server(self):
        """
        Connect to the C# Ocean Monitoring Server using TCP
        """
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.connect((self.server_host, self.server_port))
            logger.info(f"Connected to Ocean Monitoring Server at {self.server_host}:{self.server_port}")
            return True
        except Exception as e:
            logger.error(f"Failed to connect to server: {e}")
            return False
    
    def send_message(self, message: str) -> str:
        """
        Send a message to the C# server and receive response
        """
        try:
            self.socket.send(message.encode('utf-8'))
            response = self.socket.recv(4096).decode('utf-8')
            return response
        except Exception as e:
            logger.error(f"Error communicating with server: {e}")
            return ""
    
    def fetch_sensor_data_by_type(self, data_type: str) -> List[float]:
        """
        This is a mock implementation since the C# server doesn't have a direct API
        for fetching data by type. In a real implementation, you would:
        1. Add a new protocol message to request data by type
        2. Implement the handler in the C# server
        3. Parse the returned data here
        
        For now, we'll simulate sensor data based on the data types from your system
        """
        # Simulate realistic sensor data based on your system's data types
        import random
        
        data_ranges = {
            'temperature': (0, 30),      # Celsius
            'humidity': (30, 100),       # Percentage
            'waterLevel': (0, 10),       # Meters
            'windSpeed': (0, 150)        # km/h
        }
        
        if data_type not in data_ranges:
            logger.warning(f"Unknown data type: {data_type}")
            return []
        
        min_val, max_val = data_ranges[data_type]
        
        # Generate 50-200 realistic data points
        num_points = random.randint(50, 200)
        values = []
        
        for _ in range(num_points):
            # Add some normal distribution around the middle of the range
            mid_point = (min_val + max_val) / 2
            range_span = max_val - min_val
            value = random.normalvariate(mid_point, range_span * 0.15)
            # Clamp to range
            value = max(min_val, min(max_val, value))
            values.append(value)
        
        logger.info(f"Generated {num_points} sample values for {data_type}")
        return values
    
    def analyze_data_with_grpc(self, data_type: str, values: List[float]) -> Dict[str, Any]:
        """
        Send data to the Python gRPC analysis service
        """
        try:
            # Create gRPC channel
            with grpc.insecure_channel(f'localhost:{self.analysis_service_port}') as channel:
                stub = sensor_analysis_pb2_grpc.SensorDataAnalysisServiceStub(channel)
                
                # Create request
                request = sensor_analysis_pb2.SensorDataRequest(
                    data_type=data_type,
                    values=values
                )
                
                # Make the gRPC call
                response = stub.AnalyzeSensorData(request)
                
                # Convert response to dictionary
                result = {
                    'data_type': data_type,
                    'average': response.average,
                    'min': response.min,
                    'max': response.max,
                    'std_dev': response.std_dev,
                    'count': response.count,
                    'median': response.median
                }
                
                logger.info(f"Analysis complete for {data_type}")
                return result
                
        except Exception as e:
            logger.error(f"Error calling analysis service: {e}")
            return {}
    
    def run_analysis_demo(self):
        """
        Run a demonstration of the analysis service with all data types
        """
        data_types = ['temperature', 'humidity', 'waterLevel', 'windSpeed']
        
        print("=" * 80)
        print("OCEAN SENSOR DATA ANALYSIS SERVICE DEMO")
        print("=" * 80)
        
        for data_type in data_types:
            print(f"\n📊 Analyzing {data_type.upper()} data...")
            
            # Fetch data (in this demo, we simulate it)
            values = self.fetch_sensor_data_by_type(data_type)
            
            if not values:
                print(f"❌ No data available for {data_type}")
                continue
            
            # Analyze with gRPC service
            result = self.analyze_data_with_grpc(data_type, values)
            
            if result:
                print(f"✅ Analysis Results for {data_type}:")
                print(f"   • Sample Count: {result['count']}")
                print(f"   • Average: {result['average']:.2f}")
                print(f"   • Minimum: {result['min']:.2f}")
                print(f"   • Maximum: {result['max']:.2f}")
                print(f"   • Median: {result['median']:.2f}")
                print(f"   • Standard Deviation: {result['std_dev']:.2f}")
                
                # Add some interpretation
                cv = (result['std_dev'] / result['average']) * 100 if result['average'] != 0 else 0
                print(f"   • Coefficient of Variation: {cv:.1f}%")
                
                if cv < 10:
                    variability = "Low"
                elif cv < 30:
                    variability = "Moderate"
                else:
                    variability = "High"
                print(f"   • Data Variability: {variability}")
            else:
                print(f"❌ Analysis failed for {data_type}")
        
        print("\n" + "=" * 80)
        print("Analysis complete! 🎉")

def main():
    """
    Main function to run the client demo
    """
    print("🌊 Ocean Monitoring System - Analysis Client")
    print("Starting analysis service client...")
    
    client = OceanDataClient()
    
    # For this demo, we'll skip the actual server connection
    # and just demonstrate the analysis service
    print("\nNote: Using simulated data for demonstration.")
    print("In production, this would connect to your C# server to fetch real data.\n")
    
    client.run_analysis_demo()

if __name__ == '__main__':
    main()
