// RetailTMS Dashboard — SignalR + live updates
let connection = null;

function initDashboard(fyJobId) {
    setupSignalR(fyJobId);
    loadStoreGrid();
    setInterval(loadStoreGrid, 30000); // Refresh grid every 30s as fallback
}

function setupSignalR(fyJobId) {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/dashboard")
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build();

    connection.on("CommandStatusUpdated", (data) => {
        updateStoreRow(data.storeId, data.status, data.stepName, data.progress);
        refreshKPIs();
        updateTimestamp();
    });

    connection.on("TerminalStatusChanged", (data) => {
        if (data.newStatus === "Offline") {
            showOfflineAlert(data.storeId, data.newStatus);
        }
        updateTimestamp();
    });

    connection.on("FYJobProgress", (data) => {
        document.getElementById("kpi-completed").textContent = data.completedCount;
        document.getElementById("kpi-failed").textContent = data.failedCount;
        document.getElementById("kpi-pending").textContent = data.pendingCount;
        document.getElementById("kpi-offline").textContent = data.offlineCount;
        const pct = data.totalCount > 0 ? (data.completedCount / data.totalCount * 100).toFixed(1) : 0;
        const bar = document.getElementById("overall-progress");
        if (bar) bar.style.width = pct + "%";
        const label = document.getElementById("progress-label");
        if (label) label.textContent = `${data.completedCount} / ${data.totalCount} stores completed`;
    });

    connection.on("StoreWentOffline", (data) => {
        showToast(`${data.storeName} went OFFLINE`, "warning");
    });

    connection.on("AlertCreated", (data) => {
        showToast(data.message, data.severity === "ERROR" ? "danger" : "warning");
    });

    connection.onreconnecting(() => {
        document.getElementById("live-indicator").className = "badge bg-warning me-2";
        document.getElementById("live-indicator").textContent = "● Reconnecting";
    });

    connection.onreconnected(async () => {
        document.getElementById("live-indicator").className = "badge bg-success me-2";
        document.getElementById("live-indicator").textContent = "● Live";
        if (fyJobId) await connection.invoke("JoinFYJobGroup", fyJobId);
    });

    connection.start().then(async () => {
        console.log("SignalR connected to DashboardHub");
        if (fyJobId) await connection.invoke("JoinFYJobGroup", fyJobId);
    }).catch(err => console.error("SignalR connection failed:", err));
}

async function refreshKPIs() {
    const resp = await fetch("/Dashboard/Summary");
    if (!resp.ok) return;
    const data = await resp.json();
    document.getElementById("kpi-total").textContent = data.totalStores;
    document.getElementById("kpi-completed").textContent = data.completed;
    document.getElementById("kpi-running").textContent = data.running;
    document.getElementById("kpi-pending").textContent = data.pending;
    document.getElementById("kpi-failed").textContent = data.failed;
    document.getElementById("kpi-offline").textContent = data.offline;
}

async function loadStoreGrid() {
    const status = document.getElementById("filter-status")?.value || "";
    const region = document.getElementById("filter-region")?.value || "";
    const resp = await fetch(`/Store/GridData?status=${status}&region=${region}`);
    if (resp.ok) {
        const html = await resp.text();
        document.getElementById("store-grid").innerHTML = html;
    }
}

function updateStoreRow(storeId, status, stepName, progress) {
    const row = document.getElementById(`store-row-${storeId}`);
    if (!row) return;
    const statusCell = row.querySelector(".status-cell");
    const stepCell = row.querySelector(".step-cell");
    const progBar = row.querySelector(".step-progress");
    if (statusCell) statusCell.innerHTML = renderStatusBadge(status);
    if (stepCell && stepName) stepCell.textContent = stepName;
    if (progBar && progress != null) progBar.style.width = progress + "%";
}

function renderStatusBadge(status) {
    const map = {
        "Success": '<span class="badge" style="background:#EAF3DE;color:#3B6D11">● Completed</span>',
        "Failed": '<span class="badge" style="background:#FCEBEB;color:#A32D2D">● Failed</span>',
        "Running": '<span class="badge" style="background:#FAEEDA;color:#854F0B">● Running</span>',
        "Offline": '<span class="badge" style="background:#F1EFE8;color:#5F5E5A">● Offline</span>',
        "Pending": '<span class="badge bg-secondary">● Pending</span>',
    };
    return map[status] || `<span class="badge bg-secondary">${status}</span>`;
}

function showOfflineAlert(storeId, status) {
    showToast(`Store ${storeId} is now ${status}`, "warning");
}

function showToast(message, type = "info") {
    const toast = document.createElement("div");
    toast.className = `alert alert-${type} position-fixed bottom-0 end-0 m-3`;
    toast.style.zIndex = "9999";
    toast.textContent = message;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 4000);
}

function updateTimestamp() {
    const el = document.getElementById("last-updated");
    if (el) el.textContent = "Updated " + new Date().toLocaleTimeString();
}

// Wire up filter dropdowns
document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("filter-status")?.addEventListener("change", loadStoreGrid);
    document.getElementById("filter-region")?.addEventListener("change", loadStoreGrid);
});
