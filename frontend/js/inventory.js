// frontend/js/inventory.js
// Màn hình Kho nguyên liệu (Manager & Admin):
// - Xem tồn kho theo chi nhánh.
// - Lập phiếu nhập nhiều nguyên liệu cùng lúc.
// - Tra cứu lịch sử Nhập kho (StockReceipts) và Xuất kho bán hàng (từ các đơn đã thanh toán theo công thức).

let currentSession = null;
let ingredientsList = [];
let receiptDraftItems = [];

document.addEventListener('DOMContentLoaded', async () => {
    currentSession = requireAuth(['Manager', 'Admin']);
    if (!currentSession) return;

    await setupBranchSelector();
    await loadIngredients();
    await reloadAllInventoryData();
});

async function setupBranchSelector() {
    const branchSelect = document.getElementById('branchSelect');
    const branchLabel = document.getElementById('branchStaticLabel');

    const branches = await apiRequest('/api/branches');
    if (currentSession.role === 'Admin') {
        branchSelect.innerHTML = branches
            .filter(b => b.isActive)
            .map(b => `<option value="${b.id}">${b.name} (${b.code})</option>`)
            .join('');
        branchSelect.classList.remove('hidden');
        branchLabel.textContent = 'Chọn kho chi nhánh:';
    } else {
        branchSelect.classList.add('hidden');
        const myBranch = branches.find(b => b.id === Number(currentSession.branchId));
        branchLabel.textContent = myBranch
            ? `Kho chi nhánh: ${myBranch.name} (${myBranch.code})`
            : `Kho Chi nhánh #${currentSession.branchId}`;
    }
}

function getSelectedBranchId() {
    if (currentSession.role === 'Admin') {
        return Number(document.getElementById('branchSelect').value || 1);
    }
    return Number(currentSession.branchId);
}

async function reloadAllInventoryData() {
    await loadBranchInventory();
    await loadInventoryHistory();
}

async function loadIngredients() {
    try {
        ingredientsList = await apiRequest('/api/inventory/ingredients');
        const sel = document.getElementById('ingredientSelect');
        sel.innerHTML = ingredientsList
            .map(i => `<option value="${i.id}" data-name="${i.name}" data-unit="${i.unit}">${i.name} (${i.unit})</option>`)
            .join('');
    } catch (err) {
        showMessage('inventoryMessage', err.message, 'error');
    }
}

async function loadBranchInventory() {
    showMessage('inventoryMessage', '');
    const branchId = getSelectedBranchId();

    try {
        const data = await apiRequest(`/api/branches/${branchId}/inventory`);
        document.getElementById('inventoryBranchTitle').textContent =
            `Số lượng tồn kho – ${data.branchName} (${data.branchCode})`;

        const tbody = document.getElementById('inventoryTableBody');
        const items = data.inventories || [];

        if (items.length === 0) {
            tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;">Chưa có dữ liệu tồn kho.</td></tr>';
            return;
        }

        tbody.innerHTML = items.map(item => `
            <tr>
                <td>#${item.ingredientId}</td>
                <td><strong>${item.ingredientName}</strong></td>
                <td>${item.unit}</td>
                <td class="text-right"><strong>${Number(item.quantity).toLocaleString('vi-VN')}</strong></td>
            </tr>
        `).join('');
    } catch (err) {
        showMessage('inventoryMessage', err.message, 'error');
    }
}

async function loadInventoryHistory() {
    const branchId = getSelectedBranchId();
    const inBody = document.getElementById('historyInBody');
    const outBody = document.getElementById('historyOutBody');

    try {
        const history = await apiRequest(`/api/branches/${branchId}/inventory/history`);
        const receipts = history.receipts || [];
        const stockOuts = history.stockOuts || [];

        if (receipts.length === 0) {
            inBody.innerHTML = '<tr><td colspan="4" style="text-align:center; color:var(--muted);">Chưa có phiếu nhập kho nào tại chi nhánh này.</td></tr>';
        } else {
            inBody.innerHTML = receipts.map(r => {
                const timeStr = new Date(r.timestamp).toLocaleString('vi-VN');
                const detailsText = (r.details || [])
                    .map(d => `${d.ingredientName}: +${Number(d.quantityChange).toLocaleString('vi-VN')} ${d.unit}`)
                    .join(', ');
                return `
                    <tr>
                        <td><strong>${r.title}</strong><br><small style="color:var(--muted);">${timeStr}</small></td>
                        <td>${r.actor}</td>
                        <td>${detailsText}</td>
                        <td>${r.note || '-'}</td>
                    </tr>
                `;
            }).join('');
        }

        if (stockOuts.length === 0) {
            outBody.innerHTML = '<tr><td colspan="4" style="text-align:center; color:var(--muted);">Chưa có giao dịch xuất kho bán hàng nào.</td></tr>';
        } else {
            outBody.innerHTML = stockOuts.map(o => {
                const timeStr = new Date(o.timestamp).toLocaleString('vi-VN');
                const detailsText = (o.details || [])
                    .map(d => `${d.ingredientName}: ${Number(d.quantityChange).toLocaleString('vi-VN')} ${d.unit}`)
                    .join(', ');
                return `
                    <tr>
                        <td><strong>${o.title}</strong><br><small style="color:var(--muted);">${timeStr}</small></td>
                        <td>${o.actor}</td>
                        <td>${detailsText}</td>
                        <td>${o.note || '-'}</td>
                    </tr>
                `;
            }).join('');
        }
    } catch (err) {
        showMessage('inventoryMessage', err.message, 'error');
    }
}

function addReceiptLine() {
    showMessage('inventoryMessage', '');
    const sel = document.getElementById('ingredientSelect');
    const opt = sel.options[sel.selectedIndex];
    const quantity = Number(document.getElementById('receiptQtyInput').value || 0);

    if (!opt || quantity <= 0) {
        showMessage('inventoryMessage', 'Vui lòng chọn nguyên liệu và nhập số lượng lớn hơn 0.');
        return;
    }

    const ingredientId = Number(opt.value);
    const ingredientName = opt.getAttribute('data-name');
    const unit = opt.getAttribute('data-unit');

    const existing = receiptDraftItems.find(x => x.ingredientId === ingredientId);
    if (existing) {
        existing.quantity += quantity;
    } else {
        receiptDraftItems.push({
            ingredientId,
            ingredientName,
            unit,
            quantity
        });
    }

    document.getElementById('receiptQtyInput').value = 500;
    renderReceiptDraft();
}

function removeReceiptLine(index) {
    receiptDraftItems.splice(index, 1);
    renderReceiptDraft();
}

function renderReceiptDraft() {
    const tbody = document.getElementById('receiptDraftBody');
    if (receiptDraftItems.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" style="text-align:center;">Chưa có nguyên liệu trong phiếu nhập.</td></tr>';
        return;
    }

    tbody.innerHTML = receiptDraftItems.map((item, idx) => `
        <tr>
            <td>${item.ingredientName}</td>
            <td>${item.unit}</td>
            <td class="text-right">+${Number(item.quantity).toLocaleString('vi-VN')}</td>
            <td class="text-right">
                <button type="button" class="btn btn-danger btn-sm" onclick="removeReceiptLine(${idx})">Xóa</button>
            </td>
        </tr>
    `).join('');
}

async function submitStockReceipt() {
    showMessage('inventoryMessage', '');
    if (receiptDraftItems.length === 0) {
        showMessage('inventoryMessage', 'Phiếu nhập phải có ít nhất một dòng nguyên liệu.');
        return;
    }

    const branchId = getSelectedBranchId();
    const note = document.getElementById('receiptNoteInput').value.trim();

    const btn = document.getElementById('btnSubmitReceipt');
    btn.disabled = true;

    try {
        const result = await apiRequest(`/api/branches/${branchId}/stock-receipts`, {
            method: 'POST',
            body: JSON.stringify({
                note,
                items: receiptDraftItems.map(i => ({
                    ingredientId: i.ingredientId,
                    quantity: i.quantity
                }))
            })
        });

        receiptDraftItems = [];
        document.getElementById('receiptNoteInput').value = '';
        renderReceiptDraft();

        await reloadAllInventoryData();
        showMessage('inventoryMessage', `Đã lập phiếu nhập kho #${result.receiptId} thành công và cập nhật tồn kho & lịch sử!`, 'success');
    } catch (err) {
        showMessage('inventoryMessage', err.message, 'error');
    } finally {
        btn.disabled = false;
    }
}
