// frontend/js/barista.js
// Màn hình Pha chế (Barista):
// - Hiển thị đơn đã thanh toán theo đúng chi nhánh, chia thành 3 cột: Chờ pha (Waiting), Đang pha (Preparing) và Hoàn thành (Completed).
// - Mỗi đơn hiện mã đơn, giờ nhận (giờ thanh toán) và chi tiết từng ly (size, số lượng, topping, ghi chú).
// - Tự động cập nhật mỗi 5 giây khi thu ngân vừa thanh toán đơn mới.

let currentSession = null;
let autoRefreshTimer = null;

document.addEventListener('DOMContentLoaded', async () => {
    currentSession = requireAuth(['Barista', 'Admin']);
    if (!currentSession) return;

    await setupBranchSelector();
    await loadBaristaOrders();

    // Tự động cập nhật mỗi 5 giây để nhận ngay đơn mới khi thu ngân thanh toán
    autoRefreshTimer = setInterval(() => {
        loadBaristaOrders(true);
    }, 5000);
});

async function setupBranchSelector() {
    const branchSelect = document.getElementById('branchSelect');
    const branchStaticLabel = document.getElementById('branchStaticLabel');

    try {
        const branches = await apiRequest('/api/branches');
        if (currentSession.role === 'Admin') {
            branchSelect.innerHTML = branches
                .filter(b => b.isActive)
                .map(b => `<option value="${b.id}">${b.name} (${b.code})</option>`)
                .join('');
            branchSelect.classList.remove('hidden');
            branchStaticLabel.textContent = 'Chọn quầy pha chế chi nhánh:';
        } else {
            branchSelect.classList.add('hidden');
            const myBranch = branches.find(b => b.id === Number(currentSession.branchId));
            branchStaticLabel.textContent = myBranch
                ? `Quầy pha chế: ${myBranch.name} (${myBranch.code})`
                : `Quầy pha chế Chi nhánh #${currentSession.branchId}`;
        }
    } catch (err) {
        showMessage('baristaMessage', err.message, 'error');
    }
}

function getSelectedBranchId() {
    if (currentSession.role === 'Admin') {
        return Number(document.getElementById('branchSelect').value || 1);
    }
    return Number(currentSession.branchId);
}

async function loadBaristaOrders(silent = false) {
    if (!silent) showMessage('baristaMessage', '');
    const branchId = getSelectedBranchId();

    try {
        const orders = await apiRequest(`/api/branches/${branchId}/barista/orders`);
        renderThreeColumns(orders || []);
        const lastSync = document.getElementById('lastSyncTime');
        if (lastSync) {
            lastSync.textContent = `Cập nhật tự động lúc ${new Date().toLocaleTimeString('vi-VN')}`;
        }
    } catch (err) {
        if (!silent) showMessage('baristaMessage', err.message, 'error');
    }
}

function normalizeStage(order) {
    if (order.isPrepared || order.preparationStatus === 'Completed' || order.preparationStatus === 'Prepared') {
        return 'Completed';
    }
    if (order.preparationStatus === 'Preparing') {
        return 'Preparing';
    }
    return 'Waiting';
}

function renderThreeColumns(orders) {
    const waitingOrders = orders.filter(o => normalizeStage(o) === 'Waiting');
    const preparingOrders = orders.filter(o => normalizeStage(o) === 'Preparing');
    const completedOrders = orders.filter(o => normalizeStage(o) === 'Completed');

    document.getElementById('countWaiting').textContent = waitingOrders.length;
    document.getElementById('countPreparing').textContent = preparingOrders.length;
    document.getElementById('countCompleted').textContent = completedOrders.length;

    document.getElementById('colWaiting').innerHTML = waitingOrders.length > 0
        ? waitingOrders.map(o => renderOrderCard(o, 'Waiting')).join('')
        : '<p style="text-align:center; color:var(--muted); padding:1rem;">Không có đơn chờ pha.</p>';

    document.getElementById('colPreparing').innerHTML = preparingOrders.length > 0
        ? preparingOrders.map(o => renderOrderCard(o, 'Preparing')).join('')
        : '<p style="text-align:center; color:var(--muted); padding:1rem;">Không có đơn đang pha.</p>';

    document.getElementById('colCompleted').innerHTML = completedOrders.length > 0
        ? completedOrders.map(o => renderOrderCard(o, 'Completed')).join('')
        : '<p style="text-align:center; color:var(--muted); padding:1rem;">Chưa có đơn hoàn thành.</p>';
}

function renderOrderCard(order, stage) {
    const orderId = order.orderId || order.id;
    const receivedTime = order.paidAt
        ? new Date(order.paidAt).toLocaleTimeString('vi-VN')
        : new Date(order.createdAt).toLocaleTimeString('vi-VN');

    const completedTime = order.preparedAt
        ? `<div style="font-size:0.78rem; color:#166534;">Hoàn thành lúc: ${new Date(order.preparedAt).toLocaleTimeString('vi-VN')}</div>`
        : '';

    const cupsHtml = (order.items || []).map(item => {
        const topText = (item.toppings && item.toppings.length > 0)
            ? `<div style="font-size:0.82rem; color:var(--primary);">+ Topping: ${item.toppings.join(', ')}</div>`
            : '<div style="font-size:0.82rem; color:var(--muted);">+ Topping: Không</div>';
        const noteText = item.note
            ? `<div style="font-size:0.82rem; color:#92400e;">* Ghi chú: <strong>${item.note}</strong></div>`
            : '';

        return `
            <div style="padding:0.45rem 0; border-bottom:1px dashed var(--cream-border);">
                <div style="display:flex; justify-content:space-between;">
                    <strong>${escapeHtml(item.productName)} (Size ${item.size})</strong>
                    <span class="badge badge-pending">SL: ${item.quantity} ly</span>
                </div>
                ${topText}
                ${noteText}
            </div>
        `;
    }).join('');

    let actionBtn = '';
    if (stage === 'Waiting') {
        actionBtn = `
            <div style="display:flex; gap:0.5rem; margin-top:0.75rem;">
                <button type="button" class="btn btn-warning btn-sm" style="flex:1;" onclick="startPreparing(${orderId})">Nhận pha (Đang pha)</button>
                <button type="button" class="btn btn-success btn-sm" style="flex:1;" onclick="markOrderPrepared(${orderId})">Hoàn thành ngay</button>
            </div>
        `;
    } else if (stage === 'Preparing') {
        actionBtn = `
            <div style="margin-top:0.75rem;">
                <button type="button" class="btn btn-success btn-sm" style="width:100%;" onclick="markOrderPrepared(${orderId})">✓ Hoàn thành pha chế</button>
            </div>
        `;
    } else {
        actionBtn = `
            <div style="margin-top:0.5rem; text-align:right;">
                <span class="badge badge-paid">✓ Đã giao món</span>
            </div>
        `;
    }

    return `
        <div class="card" style="margin-bottom:0.85rem; border-left: 4px solid ${stage === 'Completed' ? '#15803d' : stage === 'Preparing' ? '#2563eb' : '#d97706'};">
            <div style="display:flex; justify-content:space-between; align-items:center;">
                <strong style="font-size:1.05rem; color:var(--primary-dark);">Đơn #${orderId}</strong>
                <span style="font-size:0.82rem; color:var(--muted);">Giờ nhận: <strong>${receivedTime}</strong></span>
            </div>
            ${completedTime}
            <div style="margin-top:0.4rem;">
                ${cupsHtml}
            </div>
            ${actionBtn}
        </div>
    `;
}

async function startPreparing(orderId) {
    showMessage('baristaMessage', '');
    try {
        await apiRequest(`/api/orders/${orderId}/start-prepare`, {
            method: 'POST'
        });
        await loadBaristaOrders(true);
        showMessage('baristaMessage', `Đơn #${orderId} đã chuyển sang cột "Đang pha".`, 'success');
    } catch (err) {
        showMessage('baristaMessage', err.message, 'error');
    }
}

async function markOrderPrepared(orderId) {
    showMessage('baristaMessage', '');
    try {
        await apiRequest(`/api/orders/${orderId}/prepare`, {
            method: 'POST'
        });
        await loadBaristaOrders(true);
        showMessage('baristaMessage', `Đơn #${orderId} đã chuyển sang "Hoàn thành"! Doanh thu đơn hàng vẫn được bảo toàn.`, 'success');
    } catch (err) {
        showMessage('baristaMessage', err.message, 'error');
    }
}
