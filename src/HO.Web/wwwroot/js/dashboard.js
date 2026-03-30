// RetailTMS Dashboard — SignalR + live store grid
let connection = null;
let currentStatus = '';
let currentRegion = '';

function initDashboard(fyJobId) {
    // Load initial store grid
    loadStoreGrid();

    // Wire up filter buttons
    document.querySelectorAll('.filter-status-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.filter-status-btn').forEach(b => {
                b.className = 'btn btn-sm btn-outline-secondary filter-status-btn';
            });
            this.className = 'btn btn-sm btn-primary filter-status-btn';
            currentStatus = this.dataset.status;
            loadStoreGrid();
        });
    });

    document.querySelectorAll('.filter-region-btn').forEach(btn => {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.filter-region-btn').forEach(b => {
                b.className = 'btn btn-sm btn-outline-secondary filter-region-btn';
            });
            this.className = 'btn btn-sm btn-primary filter-region-btn';
            currentRegion = this.dataset.region;
            loadStoreGrid();
        });
    });

    // Set up SignalR
    setupSignalR(fyJobId);

    // Auto-refresh grid every 30s as fallback
    setInterval(loadStoreGrid, 30000);
}

async function loadStoreGrid() {
    try {
        const params = new URLSearchParams();
        if (currentStatus) params.set('status', currentStatus);
        if (currentRegion) params.set('region', currentRegion);

        const resp = await fetch('/Store/GridData?' + params.toString());
        if (resp.ok) {
            const html = await resp.text();
            const grid = document.getElementById('store-grid');
            if (grid) grid.innerHTML = html;
            updateTimestamp();
        }
    } catch (err) {
        console.error('Grid load error:', err);
    }
}

function setupSignalR(fyJobId) {
    if (typeof signalR === 'undefined') {
        console.warn('SignalR not loaded — live updates disabled');
        updateIndicator(false);
        return;
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/dashboard')
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    connection.on('CommandStatusUpdated', data => {
        refreshKPIs();
        updateTimestamp();
    });

    connection.on('TerminalStatusChanged', data => {
        refreshKPIs();
        updateTimestamp();
    });

    connection.on('FYJobProgress', data => {
        const ids = ['kpi-completed','kpi-failed','kpi-pending','kpi-offline'];
        const vals = [data.completedCount, data.failedCount, data.pendingCount, data.offlineCount];
        ids.forEach((id, i) => {
            const el = document.getElementById(id);
            if (el) el.textContent = vals[i];
        });
        const bar = document.getElementById('overall-progress');
        const lbl = document.getElementById('progress-label');
        if (bar && data.totalCount > 0) {
            bar.style.width = (data.completedCount / data.totalCount * 100) + '%';
        }
        if (lbl) lbl.textContent = data.completedCount + ' / ' + data.totalCount + ' stores';
        loadStoreGrid();
    });

    connection.on('StoreWentOffline', data => {
        showToast(data.storeName + ' went offline', 'warning');
    });

    connection.on('AlertCreated', data => {
        showToast(data.message, data.severity === 'ERROR' ? 'danger' : 'warning');
    });

    connection.onreconnecting(() => updateIndicator(false));
    connection.onreconnected(async () => {
        updateIndicator(true);
        if (fyJobId) await connection.invoke('JoinFYJobGroup', fyJobId);
        loadStoreGrid();
    });

    connection.start().then(async () => {
        updateIndicator(true);
        if (fyJobId) await connection.invoke('JoinFYJobGroup', fyJobId);
    }).catch(err => {
        console.warn('SignalR connect failed:', err.message);
        updateIndicator(false);
    });
}

async function refreshKPIs() {
    try {
        const resp = await fetch('/Dashboard/Summary');
        if (!resp.ok) return;
        const d = await resp.json();
        const map = {
            'kpi-total':     d.totalStores,
            'kpi-completed': d.completed,
            'kpi-running':   d.running,
            'kpi-pending':   d.pending,
            'kpi-failed':    d.failed,
            'kpi-offline':   d.offline,
        };
        Object.entries(map).forEach(([id, val]) => {
            const el = document.getElementById(id);
            if (el) el.textContent = val;
        });
    } catch {}
}

function updateIndicator(live) {
    const el = document.getElementById('live-indicator');
    if (!el) return;
    el.className = 'badge me-2';
    el.style.fontSize = '11px';
    if (live) {
        el.classList.add('bg-success');
        el.textContent = '● Live';
    } else {
        el.classList.add('bg-warning', 'text-dark');
        el.textContent = '● Reconnecting';
    }
}

function updateTimestamp() {
    const el = document.getElementById('last-updated');
    if (el) el.textContent = 'Updated ' + new Date().toLocaleTimeString();
}

function showToast(message, type = 'info') {
    const div = document.createElement('div');
    div.className = 'alert alert-' + type + ' position-fixed bottom-0 end-0 m-3 shadow';
    div.style.cssText = 'z-index:9999;max-width:320px;font-size:13px';
    div.textContent = message;
    document.body.appendChild(div);
    setTimeout(() => div.remove(), 4000);
}

// Called from store grid retry/rollback buttons
window.retryStore = async function (storeId) {
    const token = document.querySelector('meta[name="aft"]')?.content
        || document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    // For now navigate to Store Details
    window.location.href = '/Store/Details/' + storeId;
};

window.rollbackStore = function (storeId) {
    window.location.href = '/Store/Details/' + storeId;
};
