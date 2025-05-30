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
    }
}

// Utility functions
function formatNumber(num) {
    return new Intl.NumberFormat().format(num);
}

function formatDateTime(dateStr) {
    return new Date(dateStr).toLocaleString();
}

function showError(message) {
    console.error(message);
    // You could implement a toast notification here
    alert(message);
}
