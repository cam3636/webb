// frontend/js/reports.js
// Màn hình Báo cáo Doanh thu thuần túy (đã tách phần quản lý nhân viên sang admin.html):
// - Bộ lọc ngày (Từ ngày - Đến ngày) và phạm vi Chi nhánh / Toàn chuỗi.
// - Hiển thị Tổng doanh thu, Số đơn đã thanh toán, Giá trị trung bình mỗi đơn (Average Order Value).
// - Bảng thống kê sản lượng theo món và Danh sách đơn hàng đã thanh toán để đối chiếu.

let currentSession = null;
let branchesCache = [];

document.addEventListener('DOMContentLoaded', async () => {
    currentSession = requireAuth(['Manager', 'Admin']);
    if (!currentSession) return;

    initDefaultDates();
    await setupScopeSelector();
    await loadRevenueReport();
});

function initDefaultDates() {
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('fromDateInput').value = today;
    document.getElementById('toDateInput').value = today;
}

async function setupScopeSelector() {
    const scopeSelect = document.getElementById('reportScopeSelect');
    const scopeStatic = document.getElementById('reportScopeStatic');

    branchesCache = await apiRequest('/api/branches');
    if (currentSession.role === 'Admin') {
        const options = ['<option value="ALL">Toàn chuỗi (Tất cả chi nhánh)</option>'];
        for (const b of branchesCache) {
            options.push(`<option value="${b.id}">${b.name} (${b.code})</option>`);
        }
        scopeSelect.innerHTML = options.join('');
        scopeSelect.classList.remove('hidden');
        scopeStatic.classList.add('hidden');
    } else {
        scopeSelect.classList.add('hidden');
        const myBranch = branchesCache.find(b => b.id === Number(currentSession.branchId));
        scopeStatic.textContent = myBranch
            ? `${myBranch.name} (${myBranch.code})`
            : `Chi nhánh #${currentSession.branchId}`;
    }
}

async function loadRevenueReport() {
    showMessage('reportMessage', '');
    const from = document.getElementById('fromDateInput').value;
    const to = document.getElementById('toDateInput').value;

    const queryParams = new URLSearchParams();
    if (from) queryParams.set('from', from);
    if (to) queryParams.set('to', to);
    const qs = queryParams.toString() ? `?${queryParams.toString()}` : '';

    let url = '';
    let selectedBranchId = null;

    if (currentSession.role === 'Admin') {
        const scopeVal = document.getElementById('reportScopeSelect').value;
        if (scopeVal === 'ALL') {
            url = `/api/reports/revenue${qs}`;
        } else {
            selectedBranchId = Number(scopeVal);
            url = `/api/branches/${selectedBranchId}/reports/revenue${qs}`;
        }
    } else {
        selectedBranchId = Number(currentSession.branchId);
        url = `/api/branches/${selectedBranchId}/reports/revenue${qs}`;
    }

    try {
        const report = await apiRequest(url);

        const count = report.paidOrderCount ?? 0;
        const total = Number(report.totalRevenue || 0);
        const avg = report.averageOrderValue !== undefined
            ? Number(report.averageOrderValue)
            : (count > 0 ? Math.round(total / count) : 0);

        document.getElementById('kpiScope').textContent = report.scope || 'Chi nhánh';
        document.getElementById('kpiPaidOrders').textContent = count;
        document.getElementById('kpiTotalRevenue').textContent = formatCurrency(total);
        document.getElementById('kpiAvgOrder').textContent = formatCurrency(avg);

        const tbody = document.getElementById('productSalesBody');
        const sales = report.productSales || [];

        if (sales.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;">Chưa có món nào được thanh toán trong khoảng ngày đã chọn.</td></tr>';
        } else {
            tbody.innerHTML = sales.map(s => `
                <tr>
                    <td><strong>${escapeHtml(s.productName)}</strong></td>
                    <td>Size ${s.size}</td>
                    <td>${s.quantitySold}</td>
                    <td class="text-right"><strong>${formatCurrency(s.totalSalesAmount)}</strong></td>
                </tr>
            `).join('');
        }

        await loadPaidOrdersTable(selectedBranchId, from, to);
    } catch (err) {
        showMessage('reportMessage', err.message, 'error');
    }
}

async function loadPaidOrdersTable(branchId, fromStr, toStr) {
    const tbody = document.getElementById('paidOrdersTableBody');
    if (!tbody) return;

    try {
        const url = branchId ? `/api/orders?branchId=${branchId}` : '/api/orders';
        const orders = await apiRequest(url);

        const fromDate = fromStr ? new Date(fromStr + 'T00:00:00') : null;
        const toDate = toStr ? new Date(toStr + 'T23:59:59') : null;

        const paidOrders = (orders || []).filter(o => {
            if (o.status !== 'Paid') return false;
            if (!o.paidAt) return true;
            const pDate = new Date(o.paidAt);
            if (fromDate && pDate < fromDate) return false;
            if (toDate && pDate > toDate) return false;
            return true;
        });

        if (paidOrders.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;">Chưa có đơn hàng đã thanh toán trong khoảng lọc này.</td></tr>';
            return;
        }

        tbody.innerHTML = paidOrders.map(o => {
            const id = o.orderId || o.id;
            const prepLabel = (o.isPrepared || o.preparationStatus === 'Completed')
                ? '<span class="badge badge-paid">Hoàn thành</span>'
                : (o.preparationStatus === 'Preparing'
                    ? '<span class="badge badge-preparing">Đang pha</span>'
                    : '<span class="badge badge-pending">Chờ pha</span>');

            const summary = (o.items || []).map(i => {
                const top = (i.toppings && i.toppings.length > 0) ? ` [${i.toppings.join(', ')}]` : '';
                const note = i.note ? ` (${i.note})` : '';
                return `${escapeHtml(i.productName)} (${i.size}) x${i.quantity}${top}${note}`;
            }).join('<br>');

            const paidTime = o.paidAt ? new Date(o.paidAt).toLocaleString('vi-VN') : '-';

            return `
                <tr>
                    <td><strong>#${id}</strong></td>
                    <td>${o.branchName || '#' + o.branchId}</td>
                    <td>${o.cashierName || '-'}</td>
                    <td>${summary}</td>
                    <td><span class="badge badge-paid">Đã thanh toán</span></td>
                    <td>${prepLabel}</td>
                    <td class="text-right"><strong>${formatCurrency(o.totalAmount)}</strong><br><small style="color:var(--muted);">${paidTime}</small></td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="7" style="text-align:center;color:red;">${err.message}</td></tr>`;
    }
}
