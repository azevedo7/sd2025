import grpc
from concurrent import futures
import sensor_analysis_pb2
import sensor_analysis_pb2_grpc
import statistics
import math
import time
import logging

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class SensorDataAnalysisServicer(sensor_analysis_pb2_grpc.SensorDataAnalysisServiceServicer):
    """
    Python gRPC service for basic statistical analysis of ocean sensor data.
    Provides mean, min, max, standard deviation, count, and median calculations.
    """
    
    def AnalyzeSensorData(self, request, context):
        """
        Analyze sensor data and return basic statistical metrics
        
        Args:
            request: SensorDataRequest containing data_type and values
            context: gRPC context
            
        Returns:
            SensorDataAnalysisResponse with statistical analysis
        """
        try:
            logger.info(f"Analyzing {request.data_type} data with {len(request.values)} values")
            
            # Validate input
            if not request.values:
                context.set_code(grpc.StatusCode.INVALID_ARGUMENT)
                context.set_details("No values provided for analysis")
                return sensor_analysis_pb2.SensorDataAnalysisResponse()
            
            values = list(request.values)
            
            # Calculate basic statistics
            avg = statistics.mean(values)
            min_val = min(values)
            max_val = max(values)
            count = len(values)
            median = statistics.median(values)
            
            # Calculate standard deviation
            std_dev = 0.0
            if count > 1:
                std_dev = statistics.stdev(values)
            
            logger.info(f"Analysis complete for {request.data_type}: avg={avg:.2f}, min={min_val:.2f}, max={max_val:.2f}, std_dev={std_dev:.2f}")
            
            return sensor_analysis_pb2.SensorDataAnalysisResponse(
                average=avg,
                min=min_val,
                max=max_val,
                std_dev=std_dev,
                count=count,
                median=median
            )
            
        except Exception as e:
            logger.error(f"Error analyzing sensor data: {str(e)}")
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(f"Analysis failed: {str(e)}")
            return sensor_analysis_pb2.SensorDataAnalysisResponse()

def serve():
    """
    Start the gRPC server for sensor data analysis
    """
    # Create gRPC server
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    
    # Add the servicer to the server
    sensor_analysis_pb2_grpc.add_SensorDataAnalysisServiceServicer_to_server(
        SensorDataAnalysisServicer(), server
    )
    
    # Listen on port 50052
    listen_addr = '[::]:50052'
    server.add_insecure_port(listen_addr)
    
    # Start the server
    server.start()
    logger.info(f"Sensor Data Analysis Service started on {listen_addr}")
    
    try:
        while True:
            time.sleep(86400)  # Keep server running (24 hours)
    except KeyboardInterrupt:
        logger.info("Shutting down server...")
        server.stop(0)

if __name__ == '__main__':
    serve()
