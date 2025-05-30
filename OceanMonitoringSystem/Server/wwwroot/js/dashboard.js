// Ocean Monitoring Dashboard JavaScript

let currentView = 'dashboard';
let charts = {};
let currentFilters = {};

// API base URL
const API_BASE = '/api/sensordata';

// Initialize dashboard
document.addEventListener('DOMContentLoaded', function() {
    loadDashboard();
    
    // Auto-refresh every 30 seconds
    setInterval(refreshData, 30000);
});

// Navigation functions
function showDashboard() {
    currentView = 'dashboard';
    document.getElementById('page-title').textContent = 'Ocean Monitoring Dashboard';
    document.getElementById('dashboard-content').style.display = 'block';
    document.getElementById('dynamic-content').style.display = 'none';
    updateActiveNav(0);
    loadDashboard();
}

function showSensorData() {
    currentView = 'sensordata';
    document.getElementById('page-title').textContent = 'All Sensor Data';
    document.getElementById('dashboard-content').style.display = 'none';
    document.getElementById('dynamic-content').style.display = 'block';
    updateActiveNav(1);
    loadSensorDataView();
}

function showWavyDevices() {
    currentView = 'wavy';
    document.getElementById('page-title').textContent = 'Wavy Devices';
    document.getElementById('dashboard-content').style.display = 'none';
    document.getElementById('dynamic-content').style.display = 'block';
    updateActiveNav(2);
    loadWavyDevicesView();
}

function showDataTypes() {
    currentView = 'datatypes';
    document.getElementById('page-title').textContent = 'Data Types';
    document.getElementById('dashboard-content').style.display = 'none';
    document.getElementById('dynamic-content').style.display = 'block';
    updateActiveNav(3);
    loadDataTypesView();
}

function showAggregators() {
    currentView = 'aggregators';
    document.getElementById('page-title').textContent = 'Aggregators';
    document.getElementById('dashboard-content').style.display = 'none';
    document.getElementById('dynamic-content').style.display = 'block';
    updateActiveNav(4);
    loadAggregatorsView();
}

function showAnalysis() {
    currentView = 'analysis';
    document.getElementById('page-title').textContent = 'Data Analysis';
    document.getElementById('dashboard-content').style.display = 'none';
    document.getElementById('dynamic-content').style.display = 'block';
    updateActiveNav(5);
    loadAnalysisView();
}

function updateActiveNav(index) {
    const navLinks = document.querySelectorAll('.nav-link');
    navLinks.forEach(link => link.classList.remove('active'));
    navLinks[index].classList.add('active');
}

// Dashboard functions
async function loadDashboard() {
    try {
        const [statsResponse, dataResponse] = await Promise.all([
            fetch(`${API_BASE}/stats`),
            fetch(`${API_BASE}?pageSize=10`)
        ]);
        
        const stats = await statsResponse.json();
        const data = await dataResponse.json();
        
        updateStatistics(stats);
        updateRecentData(data.data);
        updateCharts(stats);
        
    } catch (error) {
        console.error('Error loading dashboard:', error);
        showError('Failed to load dashboard data');
    }
}

function updateStatistics(stats) {
    document.getElementById('total-records').textContent = formatNumber(stats.totalRecords);
    document.getElementById('aggregator-count').textContent = stats.aggregatorCount;
    document.getElementById('data-type-count').textContent = stats.dataTypeDistribution.length;
    document.getElementById('wavy-device-count').textContent = stats.wavyDeviceStats.length;
}

function updateRecentData(data) {
    const tbody = document.getElementById('recent-data-tbody');
    
    if (!data || data.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" class="text-center">No data available</td></tr>';
        return;
    }
    
    tbody.innerHTML = data.map(item => `
        <tr>
            <td><span class="badge bg-secondary">${item.wavyId}</span></td>
            <td>${item.aggregatorId}</td>
            <td><span class="badge badge-${item.dataType}">${item.dataType}</span></td>
            <td>${item.rawValue}</td>
            <td>${formatDateTime(item.timestamp)}</td>
            <td>${formatDateTime(item.receivedAt)}</td>
        </tr>
    `).join('');
}

function updateCharts(stats) {
    // Data Type Distribution Chart
    if (charts.dataType) {
        charts.dataType.destroy();
    }
    
    const ctx1 = document.getElementById('dataTypeChart').getContext('2d');
    charts.dataType = new Chart(ctx1, {
        type: 'doughnut',
        data: {
            labels: stats.dataTypeDistribution.map(item => item.dataType),
            datasets: [{
                data: stats.dataTypeDistribution.map(item => item.count),
                backgroundColor: [
                    '#ff6b6b', '#4ecdc4', '#45b7d1', '#96ceb4', '#feca57'
                ]
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'bottom'
                }
            }
        }
    });
    
    // Wavy Device Activity Chart
    if (charts.wavyDevice) {
        charts.wavyDevice.destroy();
    }
    
    const ctx2 = document.getElementById('wavyDeviceChart').getContext('2d');
    charts.wavyDevice = new Chart(ctx2, {
        type: 'bar',
        data: {
            labels: stats.wavyDeviceStats.slice(0, 10).map(item => item.wavyId),
            datasets: [{
                label: 'Data Points',
                data: stats.wavyDeviceStats.slice(0, 10).map(item => item.count),
                backgroundColor: '#007bff'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });
}

// Dynamic content loaders
async function loadSensorDataView() {
    const content = `
        <div class="row mb-3">
            <div class="col-md-6">
                <button class="btn btn-outline-primary" data-bs-toggle="modal" data-bs-target="#filterModal">
                    <i class="fas fa-filter"></i> Filter Data
                </button>
            </div>
            <div class="col-md-6 text-end">
                <button class="btn btn-outline-success export-button" onclick="exportData()">
                    <i class="fas fa-download"></i> Export CSV
                </button>
            </div>
        </div>
        <div class="card">
            <div class="card-body">
                <div class="table-responsive">
                    <table class="table table-striped">
                        <thead>
                            <tr>
                                <th>Wavy ID</th>
                                <th>Aggregator</th>
                                <th>Data Type</th>
                                <th>Value</th>
                                <th>Timestamp</th>
                                <th>Received At</th>
                            </tr>
                        </thead>
                        <tbody id="sensor-data-tbody">
                            <tr><td colspan="6" class="text-center">Loading...</td></tr>
                        </tbody>
                    </table>
                </div>
                <nav>
                    <ul class="pagination justify-content-center" id="pagination">
                    </ul>
                </nav>
            </div>
        </div>
    `;
    
    document.getElementById('dynamic-content').innerHTML = content;
    await loadSensorData();
}

async function loadSensorData(page = 1) {
    try {
        let url = `${API_BASE}?page=${page}&pageSize=20`;
        
        if (currentFilters.wavyId) {
            url = `${API_BASE}/wavy/${currentFilters.wavyId}?page=${page}&pageSize=20`;
        } else if (currentFilters.dataType) {
            url = `${API_BASE}/type/${currentFilters.dataType}?page=${page}&pageSize=20`;
        }
        
        const response = await fetch(url);
        const result = await response.json();
        
        updateSensorDataTable(result.data);
        updatePagination(result.pagination, page);
        
    } catch (error) {
        console.error('Error loading sensor data:', error);
        showError('Failed to load sensor data');
    }
}

function updateSensorDataTable(data) {
    const tbody = document.getElementById('sensor-data-tbody');
    
    if (!data || data.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" class="text-center">No data available</td></tr>';
        return;
    }
    
    tbody.innerHTML = data.map(item => `
        <tr>
            <td><span class="badge bg-secondary">${item.wavyId}</span></td>
            <td>${item.aggregatorId}</td>
            <td><span class="badge badge-${item.dataType}">${item.dataType}</span></td>
            <td>${item.rawValue}</td>
            <td>${formatDateTime(item.timestamp)}</td>
            <td>${formatDateTime(item.receivedAt)}</td>
        </tr>
    `).join('');
}

function updatePagination(pagination, currentPage) {
    const paginationEl = document.getElementById('pagination');
    
    if (!pagination || pagination.totalPages <= 1) {
        paginationEl.innerHTML = '';
        return;
    }
    
    let html = '';
    
    // Previous button
    html += `<li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
        <a class="page-link" href="#" onclick="loadSensorData(${currentPage - 1})">Previous</a>
    </li>`;
    
    // Page numbers
    for (let i = Math.max(1, currentPage - 2); i <= Math.min(pagination.totalPages, currentPage + 2); i++) {
        html += `<li class="page-item ${i === currentPage ? 'active' : ''}">
            <a class="page-link" href="#" onclick="loadSensorData(${i})">${i}</a>
        </li>`;
    }
    
    // Next button
    html += `<li class="page-item ${currentPage === pagination.totalPages ? 'disabled' : ''}">
        <a class="page-link" href="#" onclick="loadSensorData(${currentPage + 1})">Next</a>
    </li>`;
    
    paginationEl.innerHTML = html;
}

async function loadWavyDevicesView() {
    try {
        const response = await fetch(`${API_BASE}/stats`);
        const stats = await response.json();
        
        const content = `
            <div class="row">
                ${stats.wavyDeviceStats.map(device => `
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="card">
                            <div class="card-body">
                                <h5 class="card-title">
                                    <i class="fas fa-microchip text-primary"></i> ${device.wavyId}
                                </h5>
                                <p class="card-text">
                                    <strong>Data Points:</strong> ${formatNumber(device.count)}<br>
                                    <span class="realtime-indicator"></span> Active
                                </p>
                                <button class="btn btn-outline-primary btn-sm" onclick="viewWavyDevice('${device.wavyId}')">
                                    View Data
                                </button>
                            </div>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
        
        document.getElementById('dynamic-content').innerHTML = content;
        
    } catch (error) {
        console.error('Error loading Wavy devices:', error);
        showError('Failed to load Wavy devices');
    }
}

async function loadDataTypesView() {
    try {
        const response = await fetch(`${API_BASE}/stats`);
        const stats = await response.json();
        
        const content = `
            <div class="row">
                ${stats.dataTypeDistribution.map(dataType => `
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="card">
                            <div class="card-body">
                                <h5 class="card-title">
                                    <span class="badge badge-${dataType.dataType} fs-6">${dataType.dataType}</span>
                                </h5>
                                <p class="card-text">
                                    <strong>Total Records:</strong> ${formatNumber(dataType.count)}
                                </p>
                                <button class="btn btn-outline-primary btn-sm" onclick="viewDataType('${dataType.dataType}')">
                                    View Data
                                </button>
                            </div>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
        
        document.getElementById('dynamic-content').innerHTML = content;
        
    } catch (error) {
        console.error('Error loading data types:', error);
        showError('Failed to load data types');
    }
}

async function loadAggregatorsView() {
    try {
        const response = await fetch(`${API_BASE}/aggregators`);
        const aggregators = await response.json();
        
        const content = `
            <div class="row">
                ${aggregators.map(agg => `
                    <div class="col-md-6 col-lg-4 mb-3">
                        <div class="card">
                            <div class="card-body">
                                <h5 class="card-title">
                                    <i class="fas fa-server text-success"></i> ${agg.clientId}
                                </h5>
                                <p class="card-text">
                                    <strong>Status:</strong> <span class="status-${agg.status.toLowerCase()}">${agg.status}</span><br>
                                    <strong>Registered:</strong> ${formatDateTime(agg.registeredAt)}<br>
                                    <strong>Last Connected:</strong> ${formatDateTime(agg.lastConnectedAt)}
                                </p>
                            </div>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
        
        document.getElementById('dynamic-content').innerHTML = content;
        
    } catch (error) {
        console.error('Error loading aggregators:', error);
        showError('Failed to load aggregators');
    }
}

// Filter and action functions
function applyFilters() {
    const wavyId = document.getElementById('filterWavyId').value;
    const dataType = document.getElementById('filterDataType').value;
    
    currentFilters = {};
    if (wavyId) currentFilters.wavyId = wavyId;
    if (dataType) currentFilters.dataType = dataType;
    
    if (currentView === 'sensordata') {
        loadSensorData(1);
    }
    
    // Close modal
    const modal = bootstrap.Modal.getInstance(document.getElementById('filterModal'));
    modal.hide();
}

function viewWavyDevice(wavyId) {
    currentFilters = { wavyId: wavyId };
    showSensorData();
}

function viewDataType(dataType) {
    currentFilters = { dataType: dataType };
    showSensorData();
}

async function exportData() {
    try {
        let url = `${API_BASE}/export/csv`;
        const params = new URLSearchParams();
        
        if (currentFilters.wavyId) params.append('wavyId', currentFilters.wavyId);
        if (currentFilters.dataType) params.append('dataType', currentFilters.dataType);
        
        if (params.toString()) {
            url += '?' + params.toString();
        }
        
        window.open(url, '_blank');
        
    } catch (error) {
        console.error('Error exporting data:', error);
        showError('Failed to export data');
    }
}

function refreshData() {
    switch (currentView) {
        case 'dashboard':
            loadDashboard();
            break;
        case 'sensordata':
            loadSensorData(1);
            break;
        case 'wavy':
            loadWavyDevicesView();
            break;
        case 'datatypes':
            loadDataTypesView();
            break;
        case 'aggregators':
            loadAggregatorsView();
            break;
        case 'analysis':
            loadAnalysisView();
            break;
    }
}

// Analysis view functions
async function loadAnalysisView() {
    const content = `
        <div class="row mb-4">
            <div class="col-12">
                <div class="card">
                    <div class="card-header d-flex justify-content-between align-items-center">
                        <h5><i class="fas fa-chart-bar me-2"></i>Ocean Data Analysis</h5>
                        <button class="btn btn-primary" onclick="showAnalysisModal()">
                            <i class="fas fa-play me-2"></i>Run New Analysis
                        </button>
                    </div>
                    <div class="card-body">
                        <div class="row mb-3">
                            <div class="col-md-4">
                                <div class="card bg-light">
                                    <div class="card-body text-center">
                                        <i class="fas fa-thermometer-half fa-2x text-danger mb-2"></i>
                                        <h6>Temperature Analysis</h6>
                                        <small class="text-muted">Statistical analysis of temperature readings</small>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-4">
                                <div class="card bg-light">
                                    <div class="card-body text-center">
                                        <i class="fas fa-tint fa-2x text-info mb-2"></i>
                                        <h6>Humidity & Water Level</h6>
                                        <small class="text-muted">Moisture and water level statistics</small>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-4">
                                <div class="card bg-light">
                                    <div class="card-body text-center">
                                        <i class="fas fa-wind fa-2x text-success mb-2"></i>
                                        <h6>Wind Speed Analysis</h6>
                                        <small class="text-muted">Wind speed patterns and statistics</small>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        
        <div class="row">
            <div class="col-12">
                <div class="card">
                    <div class="card-header">
                        <h5><i class="fas fa-history me-2"></i>Analysis History</h5>
                    </div>
                    <div class="card-body">
                        <div id="analysis-loading" class="text-center p-4">
                            <div class="spinner-border text-primary" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                            <p class="mt-2">Loading analysis results...</p>
                        </div>
                        <div id="analysis-results" style="display: none;">
                            <div class="table-responsive">
                                <table class="table table-striped" id="analysis-table">
                                    <thead>
                                        <tr>
                                            <th>Data Type</th>
                                            <th>Count</th>
                                            <th>Average</th>
                                            <th>Min</th>
                                            <th>Max</th>
                                            <th>Std Dev</th>
                                            <th>Median</th>
                                            <th>Analyzed At</th>
                                            <th>Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody id="analysis-tbody">
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        
        <!-- Analysis Modal -->
        <div class="modal fade" id="analysisModal" tabindex="-1">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title"><i class="fas fa-chart-bar me-2"></i>Run Data Analysis</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <div class="row">
                            <div class="col-md-6">
                                <div class="mb-3">
                                    <label class="form-label">Data Types to Analyze</label>
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="temp-check" value="temperature" checked>
                                        <label class="form-check-label" for="temp-check">
                                            <i class="fas fa-thermometer-half text-danger"></i> Temperature
                                        </label>
                                    </div>
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="humidity-check" value="humidity" checked>
                                        <label class="form-check-label" for="humidity-check">
                                            <i class="fas fa-tint text-info"></i> Humidity
                                        </label>
                                    </div>
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="water-check" value="waterLevel" checked>
                                        <label class="form-check-label" for="water-check">
                                            <i class="fas fa-water text-primary"></i> Water Level
                                        </label>
                                    </div>
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="wind-check" value="windSpeed" checked>
                                        <label class="form-check-label" for="wind-check">
                                            <i class="fas fa-wind text-success"></i> Wind Speed
                                        </label>
                                    </div>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="mb-3">
                                    <label for="analysis-wavy-filter" class="form-label">Filter by Wavy Device (Optional)</label>
                                    <select class="form-select" id="analysis-wavy-filter">
                                        <option value="">All Devices</option>
                                        <option value="wavy1">Wavy 1</option>
                                        <option value="wavy2">Wavy 2</option>
                                        <option value="wavy3">Wavy 3</option>
                                    </select>
                                </div>
                                <div class="mb-3">
                                    <label for="analysis-hours" class="form-label">Time Range (Hours)</label>
                                    <select class="form-select" id="analysis-hours">
                                        <option value="1">Last 1 Hour</option>
                                        <option value="6">Last 6 Hours</option>
                                        <option value="24" selected>Last 24 Hours</option>
                                        <option value="72">Last 3 Days</option>
                                        <option value="168">Last Week</option>
                                    </select>
                                </div>
                            </div>
                        </div>
                        <div class="alert alert-info">
                            <i class="fas fa-info-circle me-2"></i>
                            Analysis will be performed using our Python gRPC service for statistical calculations including mean, median, standard deviation, and more.
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        <button type="button" class="btn btn-primary" onclick="runAnalysis()" id="run-analysis-btn">
                            <i class="fas fa-play me-2"></i>Run Analysis
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;
    
    document.getElementById('dynamic-content').innerHTML = content;
    await loadAnalysisResults();
}

async function loadAnalysisResults() {
    try {
        const response = await fetch('/api/analysis');
        const data = await response.json();
        
        document.getElementById('analysis-loading').style.display = 'none';
        document.getElementById('analysis-results').style.display = 'block';
        
        const tbody = document.getElementById('analysis-tbody');
        tbody.innerHTML = '';
        
        if (data.data && data.data.length > 0) {
            data.data.forEach(result => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td>
                        <span class="badge bg-${getDataTypeBadgeColor(result.dataType)}">
                            ${getDataTypeIcon(result.dataType)} ${result.dataType}
                        </span>
                    </td>
                    <td>${result.count}</td>
                    <td>${result.average.toFixed(2)}</td>
                    <td>${result.min.toFixed(2)}</td>
                    <td>${result.max.toFixed(2)}</td>
                    <td>${result.standardDeviation.toFixed(2)}</td>
                    <td>${result.median.toFixed(2)}</td>
                    <td>${formatDateTime(result.analyzedAt)}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-danger" onclick="deleteAnalysis('${result.id}')">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                `;
                tbody.appendChild(row);
            });
        } else {
            tbody.innerHTML = '<tr><td colspan="9" class="text-center text-muted">No analysis results found. Run your first analysis!</td></tr>';
        }
        
    } catch (error) {
        console.error('Error loading analysis results:', error);
        document.getElementById('analysis-loading').innerHTML = '<div class="alert alert-danger">Failed to load analysis results</div>';
    }
}

function showAnalysisModal() {
    const modal = new bootstrap.Modal(document.getElementById('analysisModal'));
    modal.show();
}

async function runAnalysis() {
    const runBtn = document.getElementById('run-analysis-btn');
    const originalText = runBtn.innerHTML;
    
    try {
        // Disable button and show loading
        runBtn.disabled = true;
        runBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Running...';
        
        // Get selected data types
        const dataTypes = [];
        if (document.getElementById('temp-check').checked) dataTypes.push('temperature');
        if (document.getElementById('humidity-check').checked) dataTypes.push('humidity');
        if (document.getElementById('water-check').checked) dataTypes.push('waterLevel');
        if (document.getElementById('wind-check').checked) dataTypes.push('windSpeed');
        
        if (dataTypes.length === 0) {
            showError('Please select at least one data type to analyze');
            return;
        }
        
        // Get other parameters
        const wavyId = document.getElementById('analysis-wavy-filter').value || null;
        const hoursBack = parseInt(document.getElementById('analysis-hours').value);
        
        // Make API call
        const response = await fetch('/api/analysis/run', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                dataTypes: dataTypes,
                wavyId: wavyId,
                hoursBack: hoursBack
            })
        });
        
        const result = await response.json();
        
        if (response.ok) {
            showSuccess(result.message);
            bootstrap.Modal.getInstance(document.getElementById('analysisModal')).hide();
            await loadAnalysisResults(); // Refresh the results
        } else {
            showError(result.error || 'Failed to run analysis');
        }
        
    } catch (error) {
        console.error('Error running analysis:', error);
        showError('Failed to run analysis');
    } finally {
        // Re-enable button
        runBtn.disabled = false;
        runBtn.innerHTML = originalText;
    }
}

async function deleteAnalysis(id) {
    if (!confirm('Are you sure you want to delete this analysis result?')) {
        return;
    }
    
    try {
        const response = await fetch(`/api/analysis/${id}`, {
            method: 'DELETE'
        });
        
        if (response.ok) {
            showSuccess('Analysis result deleted successfully');
            await loadAnalysisResults(); // Refresh the results
        } else {
            const result = await response.json();
            showError(result.error || 'Failed to delete analysis result');
        }
    } catch (error) {
        console.error('Error deleting analysis:', error);
        showError('Failed to delete analysis result');
    }
}

function getDataTypeBadgeColor(dataType) {
    switch (dataType) {
        case 'temperature': return 'danger';
        case 'humidity': return 'info';
        case 'waterLevel': return 'primary';
        case 'windSpeed': return 'success';
        default: return 'secondary';
    }
}

function getDataTypeIcon(dataType) {
    switch (dataType) {
        case 'temperature': return '<i class="fas fa-thermometer-half"></i>';
        case 'humidity': return '<i class="fas fa-tint"></i>';
        case 'waterLevel': return '<i class="fas fa-water"></i>';
        case 'windSpeed': return '<i class="fas fa-wind"></i>';
        default: return '<i class="fas fa-chart-line"></i>';
    }
}

function showSuccess(message) {
    // Simple success notification - you can enhance this with toast notifications
    const alert = document.createElement('div');
    alert.className = 'alert alert-success alert-dismissible fade show position-fixed';
    alert.style.top = '20px';
    alert.style.right = '20px';
    alert.style.zIndex = '9999';
    alert.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    document.body.appendChild(alert);
    
    setTimeout(() => {
        if (alert.parentNode) {
            alert.parentNode.removeChild(alert);
        }
    }, 5000);
}

function showError(message) {
    // Simple error notification - you can enhance this with toast notifications
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger alert-dismissible fade show position-fixed';
    alert.style.top = '20px';
    alert.style.right = '20px';
    alert.style.zIndex = '9999';
    alert.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    document.body.appendChild(alert);
    
    setTimeout(() => {
        if (alert.parentNode) {
            alert.parentNode.removeChild(alert);
        }
    }, 5000);
}

// Utility functions
function formatNumber(num) {
    if (num === null || num === undefined) return '0';
    return new Intl.NumberFormat().format(num);
}

function formatDateTime(dateString) {
    if (!dateString) return 'N/A';
    
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return 'Invalid Date';
        
        return new Intl.DateTimeFormat('en-US', {
            year: 'numeric',
            month: 'short',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: false
        }).format(date);
    } catch (error) {
        console.error('Error formatting date:', error);
        return 'Invalid Date';
    }
}
