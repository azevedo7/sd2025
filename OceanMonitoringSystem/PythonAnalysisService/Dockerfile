FROM python:3.11-slim

WORKDIR /app

# Install system dependencies needed for numpy and other packages
RUN apt-get update && apt-get install -y \
    gcc \
    g++ \
    gfortran \
    libopenblas-dev \
    liblapack-dev \
    pkg-config \
    && rm -rf /var/lib/apt/lists/*

# Upgrade pip and setuptools first
RUN pip install --upgrade pip setuptools wheel

# Copy requirements and install Python dependencies
COPY PythonAnalysisService/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application files
COPY PythonAnalysisService/ .

# Expose the gRPC port
EXPOSE 50052

# Run the analysis server
CMD ["python", "analysis_server.py"]