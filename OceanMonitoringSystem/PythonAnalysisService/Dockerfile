FROM python:3.12-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    gcc \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements and install Python dependencies
COPY PythonAnalysisService/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application files
COPY PythonAnalysisService/ .

# Expose the gRPC port
EXPOSE 50052

# Run the analysis server
CMD ["python", "analysis_server.py"]