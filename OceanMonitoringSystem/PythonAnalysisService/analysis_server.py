import grpc
from concurrent import futures
import sensor_analysis_pb2
import sensor_analysis_pb2_grpc
import statistics
import math
import time
import logging
import numpy as np

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)

class SensorDataAnalysisServicer(sensor_analysis_pb2_grpc.SensorDataAnalysisServiceServicer):
    """
    Enhanced Python gRPC service for comprehensive statistical analysis of ocean sensor data.
    Provides mean, min, max, standard deviation, count, median, and additional insights.
    """
    
    def AnalyzeSensorData(self, request, context):
        """
        Analyze sensor data and return comprehensive statistical metrics
        
        Args:
            request: SensorDataRequest containing data_type and values
            context: gRPC context
            
        Returns:
            SensorDataAnalysisResponse with statistical analysis
        """
        try:
            logger.info(f"🔍 Analyzing {request.data_type} data with {len(request.values)} values")
            
            # Validate input
            if not request.values:
                logger.warning(f"No values provided for {request.data_type} analysis")
                context.set_code(grpc.StatusCode.INVALID_ARGUMENT)
                context.set_details("No values provided for analysis")
                return sensor_analysis_pb2.SensorDataAnalysisResponse()
            
            values = list(request.values)
            
            # Remove any invalid values (NaN, infinite)
            clean_values = [v for v in values if not (math.isnan(v) or math.isinf(v))]
            
            if not clean_values:
                logger.warning(f"No valid values found for {request.data_type} after cleaning")
                context.set_code(grpc.StatusCode.INVALID_ARGUMENT)
                context.set_details("No valid values found after cleaning data")
                return sensor_analysis_pb2.SensorDataAnalysisResponse()
            
            # Calculate basic statistics
            avg = statistics.mean(clean_values)
            min_val = min(clean_values)
            max_val = max(clean_values)
            count = len(clean_values)
            median = statistics.median(clean_values)
            
            # Calculate standard deviation
            std_dev = 0.0
            if count > 1:
                std_dev = statistics.stdev(clean_values)
            
            # Additional analysis for better insights
            data_range = max_val - min_val
            coefficient_of_variation = (std_dev / avg * 100) if avg != 0 else 0
            
            # Log detailed results
            logger.info(f"✅ Analysis complete for {request.data_type}:")
            logger.info(f"   📊 Count: {count} | Average: {avg:.2f} | Range: {min_val:.2f} - {max_val:.2f}")
            logger.info(f"   📈 Median: {median:.2f} | Std Dev: {std_dev:.2f} | CV: {coefficient_of_variation:.1f}%")
            
            # Determine data quality and provide insights
            quality_score = self._calculate_data_quality(clean_values, len(values))
            logger.info(f"   ⭐ Data Quality Score: {quality_score:.1f}/10")
            
            return sensor_analysis_pb2.SensorDataAnalysisResponse(
                average=avg,
                min=min_val,
                max=max_val,
                std_dev=std_dev,
                count=count,
                median=median
            )
            
        except Exception as e:
            logger.error(f"❌ Error analyzing {request.data_type} sensor data: {str(e)}")
            context.set_code(grpc.StatusCode.INTERNAL)
            context.set_details(f"Analysis failed: {str(e)}")
            return sensor_analysis_pb2.SensorDataAnalysisResponse()
    
    def _calculate_data_quality(self, values, original_count):
        """
        Calculate a simple data quality score based on various factors
        
        Args:
            values: List of clean numeric values
            original_count: Original number of values before cleaning
            
        Returns:
            Quality score from 0-10
        """
        score = 10.0
        
        # Penalize for missing/invalid data
        if original_count > 0:
            data_completeness = len(values) / original_count
            score *= data_completeness
        
        # Penalize for insufficient data points
        if len(values) < 10:
            score *= 0.7
        elif len(values) < 5:
            score *= 0.4
        
        # Bonus for having good data volume
        if len(values) >= 100:
            score = min(10.0, score * 1.1)
        
        return max(0.0, min(10.0, score))

def serve():
    """
    Start the enhanced gRPC server for sensor data analysis
    """
    # Create gRPC server with more workers for better performance
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=20))
    
    # Add the servicer to the server
    sensor_analysis_pb2_grpc.add_SensorDataAnalysisServiceServicer_to_server(
        SensorDataAnalysisServicer(), server
    )
    
    # Listen on port 50052
    listen_addr = '[::]:50052'
    server.add_insecure_port(listen_addr)
    
    # Start the server
    server.start()
    logger.info(f"🚀 Enhanced Sensor Data Analysis Service started on {listen_addr}")
    logger.info(f"📡 Ready to process ocean monitoring data analysis requests")
    
    try:
        while True:
            time.sleep(86400)  # Keep server running (24 hours)
    except KeyboardInterrupt:
        logger.info("🛑 Shutting down server...")
        server.stop(0)

if __name__ == '__main__':
    serve()
