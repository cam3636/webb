// frontend/js/customers.js
// Trang Khách hàng thân thiết:
// - Hiển thị Họ tên, Điện thoại, Điểm hiện có, Số lần mua và Lần mua gần nhất.
// - Admin xem toàn chuỗi.
// - Manager xem danh sách và lịch sử giao dịch phát sinh tại chi nhánh của mình.
// - Cashier chỉ được tra cứu từng khách bằng số điện thoại để bán hàng và đăng ký khách mới (không xem toàn bộ danh bạ).
// - Barista bị chặn truy cập.

let currentSession = null;

document.addEventListener('DOMContentLoaded', async () => {
    currentSession = requireAuth(['Cashier', 'Manager', 'Admin']);
    if (!currentSession) return;

    if (currentSession.role === 'Cashier') {
        document.getElementById('fullDirectorySection').classList.add('hidden');
        document.getElementById('cashierLookupNotice').classList.remove('hidden');
    } else {
        await loadCustomersDirectory();
    }
});

async function lookupSingleCustomer() {
    showMessage('customerMessage', '');
    const phone = (document.getElementById('lookupPhoneInput').value || '').trim();
    if (!phone) {
        showMessage('customerMessage', 'Vui lòng nhập số điện thoại cần tra cứu.');
        return;
    }

    const resultBox = document.getElementById('lookupSingleResult');
    try {
        const c = await apiRequest(`/api/customers/lookup?phone=${encodeURIComponent(phone)}`);
        const lastBuy = c.lastPurchaseAt ? new Date(c.lastPurchaseAt).toLocaleString('vi-VN') : 'Chưa có giao dịch';
        resultBox.classList.remove('hidden');
        resultBox.innerHTML = `
            <div class="alert alert-success" style="margin-bottom:0;">
                <strong>Tìm thấy khách hàng: ${c.fullName}</strong> • SĐT chuẩn hóa: <strong>${c.phone}</strong><br>
                Điểm tích lũy hiện có: <span class="badge badge-paid">${c.points} điểm</span>
                • Số lần mua: <strong>${c.purchaseCount}</strong>
                • Lần mua gần nhất: <strong>${lastBuy}</strong>
            </div>
        `;
    } catch (err) {
        resultBox.classList.remove('hidden');
        resultBox.innerHTML = `<div class="alert alert-warn" style="margin-bottom:0;">${err.message} Bạn có thể đăng ký khách mới ở biểu mẫu bên phải.</div>`;
    }
}

async function handleCreateCustomer(event) {
    event.preventDefault();
    showMessage('customerMessage', '');

    const fullName = (document.getElementById('newCustName').value || '').trim();
    const phone = (document.getElementById('newCustPhone').value || '').trim();

    if (!fullName || !phone) {
        showMessage('customerMessage', 'Vui lòng nhập đầy đủ họ tên và số điện thoại.');
        return;
    }

    try {
        const created = await apiRequest('/api/customers', {
            method: 'POST',
            body: JSON.stringify({ fullName, phone })
        });
        document.getElementById('createCustomerForm').reset();
        showMessage('customerMessage', `Đã đăng ký khách hàng '${created.fullName}' (SĐT chuẩn hóa: ${created.phone}) thành công!`, 'success');

        if (currentSession.role !== 'Cashier') {
            await loadCustomersDirectory();
        }
    } catch (err) {
        showMessage('customerMessage', err.message, 'error');
    }
}

async function loadCustomersDirectory() {
    if (currentSession.role === 'Cashier') return;
    const kw = (document.getElementById('dirSearchInput')?.value || '').trim();
    const tbody = document.getElementById('customersTableBody');

    const scopeLabel = document.getElementById('directoryScopeLabel');
    if (scopeLabel) {
        scopeLabel.textContent = currentSession.role === 'Admin'
            ? 'Phạm vi: Toàn chuỗi (Tất cả chi nhánh)'
            : `Phạm vi giao dịch: Chi nhánh #${currentSession.branchId}`;
    }

    try {
        const qs = kw ? `?search=${encodeURIComponent(kw)}` : '';
        const list = await apiRequest(`/api/customers${qs}`);

        if (list.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;">Chưa có khách hàng thân thiết nào.</td></tr>';
            return;
        }

        tbody.innerHTML = list.map(c => {
            const lastBuy = c.lastPurchaseAt ? new Date(c.lastPurchaseAt).toLocaleString('vi-VN') : 'Chưa mua';
            return `
                <tr>
                    <td>#${c.id}</td>
                    <td><strong>${c.fullName}</strong></td>
                    <td><strong>${c.phone}</strong></td>
                    <td><span class="badge badge-paid">${c.points} điểm</span></td>
                    <td>${c.purchaseCount} lần</td>
                    <td>${lastBuy}</td>
                    <td class="text-right">
                        <button type="button" class="btn btn-secondary btn-sm" onclick="viewCustomerTransactions(${c.id})">Xem lịch sử tích điểm</button>
                    </td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        showMessage('customerMessage', err.message, 'error');
    }
}

async function viewCustomerTransactions(customerId) {
    const card = document.getElementById('customerTxCard');
    const tbody = document.getElementById('customerTxBody');
    const title = document.getElementById('customerTxTitle');

    try {
        const data = await apiRequest(`/api/customers/${customerId}/transactions`);
        card.classList.remove('hidden');
        const scopeNote = data.branchScope ? ` (Phạm vi Chi nhánh #${data.branchScope})` : ' (Toàn chuỗi)';
        title.textContent = `Lịch sử giao dịch & tích điểm: ${data.customer.fullName} (${data.customer.phone}) – Tổng: ${data.customer.points} điểm${scopeNote}`;

        const txs = data.transactions || [];
        if (txs.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;">Chưa có lịch sử giao dịch tại phạm vi này.</td></tr>';
            return;
        }

        tbody.innerHTML = txs.map(t => `
            <tr>
                <td><strong>Đơn #${t.orderId}</strong></td>
                <td>${t.branchName || 'Chi nhánh #' + t.branchId}</td>
                <td class="text-right">${formatCurrency(t.orderAmount)}</td>
                <td class="text-right"><span class="badge badge-paid">+${t.pointsEarned} điểm</span></td>
                <td>${new Date(t.createdAt).toLocaleString('vi-VN')}</td>
            </tr>
        `).join('');
    } catch (err) {
        showMessage('customerMessage', err.message, 'error');
    }
}
