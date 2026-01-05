// Dashboard JavaScript Functions

// Global chart instances
let salesChart = null;
let trendChart = null;

// Initialize dashboard
window.initializeDashboard = () => {
    // Initialize tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Initialize popovers
    const popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    popoverTriggerList.map(function (popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });
};

// Sidebar toggle
window.toggleSidebar = () => {
    const sidebar = document.querySelector('.sidebar');
    const navMenu = document.querySelector('.nav-menu');

    if (sidebar && navMenu) {
        sidebar.classList.toggle('collapsed');
        navMenu.classList.toggle('collapsed');
    }
};

// Modal functions
window.showModal = (modalId) => {
    const modal = new bootstrap.Modal(document.getElementById(modalId));
    modal.show();
};

window.hideModal = (modalId) => {
    const modal = bootstrap.Modal.getInstance(document.getElementById(modalId));
    if (modal) {
        modal.hide();
    }
};

// Chart rendering functions
window.renderSalesChart = (data, chartType) => {
    const chartElement = document.getElementById('salesChart');
    if (!chartElement) return;

    // Destroy existing chart
    if (salesChart) {
        salesChart.destroy();
    }

    const ctx = chartElement.getContext('2d');

    const chartConfig = {
        type: 'line',
        data: {
            labels: data.map(d => d.x),
            datasets: [{
                label: chartType,
                data: data.map(d => d.y),
                borderColor: '#3498db',
                backgroundColor: 'rgba(52, 152, 219, 0.1)',
                borderWidth: 2,
                fill: true,
                tension: 0.4,
                pointBackgroundColor: '#3498db',
                pointBorderColor: '#ffffff',
                pointBorderWidth: 2,
                pointRadius: 4,
                pointHoverRadius: 6
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
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        color: '#6c757d'
                    }
                },
                y: {
                    grid: {
                        color: 'rgba(0, 0, 0, 0.1)'
                    },
                    ticks: {
                        color: '#6c757d',
                        callback: function (value) {
                            if (chartType === 'Revenue') {
                                return '$' + value.toLocaleString();
                            }
                            return value.toLocaleString();
                        }
                    }
                }
            },
            interaction: {
                intersect: false,
                mode: 'index'
            },
            elements: {
                point: {
                    hoverBackgroundColor: '#3498db'
                }
            }
        }
    };

    salesChart = new Chart(ctx, chartConfig);
};

window.renderTrendChart = (data, chartType) => {
    const chartElement = document.getElementById('trendChart');
    if (!chartElement) return;

    // Destroy existing chart
    if (trendChart) {
        trendChart.destroy();
    }

    const ctx = chartElement.getContext('2d');

    const colors = {
        Revenue: '#3498db',
        Transactions: '#2ecc71',
        Items: '#f39c12'
    };

    const chartConfig = {
        type: 'bar',
        data: {
            labels: data.map(d => d.x),
            datasets: [{
                label: chartType,
                data: data.map(d => d.y),
                backgroundColor: colors[chartType] || '#3498db',
                borderColor: colors[chartType] || '#3498db',
                borderWidth: 0,
                borderRadius: 4,
                borderSkipped: false
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
                x: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        color: '#6c757d'
                    }
                },
                y: {
                    grid: {
                        color: 'rgba(0, 0, 0, 0.1)'
                    },
                    ticks: {
                        color: '#6c757d',
                        callback: function (value) {
                            if (chartType === 'Revenue') {
                                return '$' + value.toLocaleString();
                            }
                            return value.toLocaleString();
                        }
                    }
                }
            },
            interaction: {
                intersect: false,
                mode: 'index'
            }
        }
    };

    trendChart = new Chart(ctx, chartConfig);
};

// Utility functions
window.formatCurrency = (amount) => {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
    }).format(amount);
};

window.formatNumber = (number) => {
    return new Intl.NumberFormat('en-US').format(number);
};

window.formatDate = (date) => {
    return new Intl.DateTimeFormat('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    }).format(new Date(date));
};

// Real-time updates using SignalR
window.initializeSignalR = (businessId) => {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/dashboardhub")
        .build();

    connection.start().then(function () {
        console.log("SignalR Connected");

        // Join business group for real-time updates
        connection.invoke("JoinBusinessGroup", businessId);

        // Listen for real-time updates
        connection.on("SalesUpdate", function (data) {
            updateSalesMetrics(data);
        });

        connection.on("InventoryAlert", function (alert) {
            showInventoryAlert(alert);
        });

        connection.on("SystemNotification", function (notification) {
            showNotification(notification);
        });

    }).catch(function (err) {
        console.error("SignalR Connection Error: ", err.toString());
    });

    return connection;
};

// Update sales metrics in real-time
window.updateSalesMetrics = (data) => {
    const revenueElement = document.querySelector('[data-metric="revenue"]');
    const transactionsElement = document.querySelector('[data-metric="transactions"]');
    const aovElement = document.querySelector('[data-metric="aov"]');

    if (revenueElement) {
        revenueElement.textContent = formatCurrency(data.revenue);
    }

    if (transactionsElement) {
        transactionsElement.textContent = formatNumber(data.transactions);
    }

    if (aovElement) {
        aovElement.textContent = formatCurrency(data.averageOrderValue);
    }
};

// Show inventory alerts
window.showInventoryAlert = (alert) => {
    const alertContainer = document.getElementById('alertContainer');
    if (!alertContainer) return;

    const alertElement = document.createElement('div');
    alertElement.className = `alert alert-${alert.priority} alert-dismissible fade show`;
    alertElement.innerHTML = `
        <i class="fas ${getAlertIcon(alert.type)} me-2"></i>
        <strong>${alert.title}</strong> ${alert.message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;

    alertContainer.appendChild(alertElement);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        if (alertElement.parentNode) {
            alertElement.remove();
        }
    }, 5000);
};

// Show notifications
window.showNotification = (notification) => {
    // Create toast notification
    const toastContainer = document.getElementById('toastContainer') || createToastContainer();

    const toastElement = document.createElement('div');
    toastElement.className = 'toast';
    toastElement.setAttribute('role', 'alert');
    toastElement.innerHTML = `
        <div class="toast-header">
            <i class="fas ${getNotificationIcon(notification.type)} me-2"></i>
            <strong class="me-auto">${notification.title}</strong>
            <small class="text-muted">now</small>
            <button type="button" class="btn-close" data-bs-dismiss="toast"></button>
        </div>
        <div class="toast-body">
            ${notification.message}
        </div>
    `;

    toastContainer.appendChild(toastElement);

    const toast = new bootstrap.Toast(toastElement);
    toast.show();

    // Remove element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', () => {
        toastElement.remove();
    });
};

// Create toast container if it doesn't exist
window.createToastContainer = () => {
    const container = document.createElement('div');
    container.id = 'toastContainer';
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '1055';
    document.body.appendChild(container);
    return container;
};

// Helper functions for icons
window.getAlertIcon = (type) => {
    const icons = {
        'LowStock': 'fa-box',
        'OutOfStock': 'fa-exclamation-triangle',
        'ProductExpiry': 'fa-calendar-times',
        'HighSales': 'fa-chart-line',
        'LowSales': 'fa-chart-line-down',
        'SystemError': 'fa-bug',
        'SecurityAlert': 'fa-shield-alt',
        'SyncIssue': 'fa-sync-alt'
    };
    return icons[type] || 'fa-info-circle';
};

window.getNotificationIcon = (type) => {
    return getAlertIcon(type);
};

// Export functions
window.exportToPDF = (elementId, filename) => {
    const element = document.getElementById(elementId);
    if (!element) return;

    // Use html2pdf library if available
    if (typeof html2pdf !== 'undefined') {
        html2pdf()
            .from(element)
            .save(filename || 'dashboard-report.pdf');
    } else {
        console.warn('html2pdf library not loaded');
    }
};

window.exportToExcel = (data, filename) => {
    // Use SheetJS library if available
    if (typeof XLSX !== 'undefined') {
        const ws = XLSX.utils.json_to_sheet(data);
        const wb = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(wb, ws, "Sheet1");
        XLSX.writeFile(wb, filename || 'dashboard-data.xlsx');
    } else {
        console.warn('SheetJS library not loaded');
    }
};

// Initialize dashboard when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    initializeDashboard();
});

// Handle responsive sidebar
window.addEventListener('resize', function () {
    const sidebar = document.querySelector('.sidebar');
    const navMenu = document.querySelector('.nav-menu');

    if (window.innerWidth <= 768) {
        if (sidebar) sidebar.classList.add('collapsed');
        if (navMenu) navMenu.classList.add('collapsed');
    } else {
        if (sidebar) sidebar.classList.remove('collapsed');
        if (navMenu) navMenu.classList.remove('collapsed');
    }
});

// Smooth scrolling for anchor links
document.addEventListener('click', function (e) {
    if (e.target.matches('a[href^="#"]')) {
        e.preventDefault();
        const target = document.querySelector(e.target.getAttribute('href'));
        if (target) {
            target.scrollIntoView({
                behavior: 'smooth',
                block: 'start'
            });
        }
    }
});