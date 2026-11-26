// frontend/js/admin.js
// Màn hình Quản trị Hệ thống với 4 mục riêng biệt:
// 1. Đơn hàng (Orders) - Admin & Manager
// 2. Nhân viên (Users - 4 vai trò chủ yếu) - Chỉ Admin
// 3. Chi nhánh (Branches: mã, tên cửa hàng, địa chỉ cụ thể, số điện thoại, giờ mở cửa, trạng thái hoạt động; thêm, sửa, ngừng hoạt động) - Chỉ Admin
// 4. Thực đơn & Giá bán (Menu & Pricing):
//    - Admin đặt giá chung toàn chuỗi hoặc giá riêng chi nhánh.
//    - Manager đặt giá riêng hoặc chọn "Dùng lại giá chung" cho chi nhánh phụ trách.
//    - Hiển thị rõ Giá chung, Giá riêng (nếu có) và Giá thực tế đang áp dụng.
//    - Trước khi lưu: mở hộp thoại xác nhận hiển thị Tên món, Size/Topping, Giá cũ, Giá mới và Phạm vi ảnh hưởng.
//    - Hiển thị bảng Lịch sử đổi giá (Người sửa, Thời gian, Phạm vi, Giá cũ, Giá mới).

let currentSession = null;
let branchesCache = [];
let menuDataCache = { categories: [], toppings: [] };
let pendingPriceConfirmAction = null;

document.addEventListener('DOMContentLoaded', async () => {
    currentSession = requireAuth(['Manager', 'Admin']);
    if (!currentSession) return;

    branchesCache = await apiRequest('/api/branches');

    if (currentSession.role !== 'Admin') {
        // Manager chỉ được vào mục 1. Đơn hàng và mục 4. Thực đơn & Giá bán (của chi nhánh mình)
        document.getElementById('tabBtnUsers')?.classList.add('hidden');
        document.getElementById('tabBtnBranches')?.classList.add('hidden');
        document.getElementById('adminOnlyMenuCreation')?.classList.add('hidden');
    }

    setupOrdersBranchFilter();
    await loadAdminOrders();

    setupPricingBranchSelector();
    await loadAdminMenuData();

    if (currentSession.role === 'Admin') {
        setupUserBranchSelect();
        await loadUsersList();
        renderBranchesTable();
    }
});

function switchAdminTab(tabName) {
    const tabs = ['Orders', 'Users', 'Branches', 'Menu'];
    for (const t of tabs) {
        const btn = document.getElementById(`tabBtn${t}`);
        const panel = document.getElementById(`panel${t}`);
        if (btn) btn.classList.toggle('active', t === tabName);
        if (panel) panel.classList.toggle('hidden', t !== tabName);
    }
}

// --- 1. MỤC ĐƠN HÀNG ---
function setupOrdersBranchFilter() {
    const sel = document.getElementById('orderFilterBranch');
    if (currentSession.role === 'Admin') {
        sel.innerHTML = ['<option value="">Tất cả chi nhánh (Toàn chuỗi)</option>']
            .concat(branchesCache.map(b => `<option value="${b.id}">${b.name} (${b.code})</option>`))
            .join('');
    } else {
        const myB = branchesCache.find(b => b.id === Number(currentSession.branchId));
        sel.innerHTML = `<option value="${currentSession.branchId}">${myB ? myB.name : 'Chi nhánh #' + currentSession.branchId}</option>`;
        sel.disabled = true;
    }
}

async function loadAdminOrders() {
    const branchVal = document.getElementById('orderFilterBranch').value;
    const statusVal = document.getElementById('orderFilterStatus').value;
    const tbody = document.getElementById('adminOrdersBody');

    try {
        const url = branchVal ? `/api/orders?branchId=${branchVal}` : '/api/orders';
        let orders = await apiRequest(url);
        if (statusVal) {
            orders = orders.filter(o => o.status === statusVal);
        }

        if (orders.length === 0) {
            tbody.innerHTML = '<tr><td colspan="7" style="text-align:center;">Không có đơn hàng nào khớp bộ lọc.</td></tr>';
            return;
        }

        tbody.innerHTML = orders.map(o => {
            const id = o.orderId || o.id;
            const statusBadge = o.status === 'Paid'
                ? '<span class="badge badge-paid">Đã thanh toán</span>'
                : '<span class="badge badge-pending">Chờ thanh toán</span>';

            const prepBadge = (o.isPrepared || o.preparationStatus === 'Completed')
                ? '<span class="badge badge-paid">Hoàn thành</span>'
                : (o.preparationStatus === 'Preparing'
                    ? '<span class="badge badge-preparing">Đang pha</span>'
                    : '<span class="badge badge-pending">Chờ pha</span>');

            const itemsSummary = (o.items || []).map(i => {
                const top = (i.toppings && i.toppings.length > 0) ? ` + ${i.toppings.join(', ')}` : '';
                const note = i.note ? ` (${i.note})` : '';
                return `${i.productName} (${i.size} - ${formatCurrency(i.unitPrice)}) x${i.quantity}${top}${note}`;
            }).join('<br>');

            const custText = o.customerName ? `<br><small style="color:var(--primary);">KH: ${o.customerName} (+${o.pointsEarned || 0}đ)</small>` : '';
            const timeStr = new Date(o.paidAt || o.createdAt).toLocaleString('vi-VN');

            return `
                <tr>
                    <td><strong>#${id}</strong></td>
                    <td>${o.branchName || '#' + o.branchId}</td>
                    <td>${o.cashierName || '-'}${custText}</td>
                    <td>${itemsSummary}</td>
                    <td>${statusBadge}</td>
                    <td>${prepBadge}</td>
                    <td class="text-right"><strong>${formatCurrency(o.totalAmount)}</strong><br><small style="color:var(--muted);">${timeStr}</small></td>
                </tr>
            `;
        }).join('');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

// --- 2. MỤC NHÂN VIÊN (4 VAI TRÒ CHỦ YẾU) ---
function setupUserBranchSelect() {
    const sel = document.getElementById('newUserBranchSelect');
    sel.innerHTML = ['<option value="">-- Không gán chi nhánh (Toàn chuỗi) --</option>']
        .concat(branchesCache.filter(b => b.isActive).map(b => `<option value="${b.id}">${b.name} (${b.code})</option>`))
        .join('');
    onNewUserRoleChange();
}

function onNewUserRoleChange() {
    const role = document.getElementById('newUserRoleSelect').value;
    const branchSel = document.getElementById('newUserBranchSelect');
    if (role === 'Admin') {
        branchSel.value = '';
    } else if (!branchSel.value && branchesCache.length > 0) {
        branchSel.value = branchesCache[0].id;
    }
}

async function loadUsersList() {
    const tbody = document.getElementById('usersTableBody');
    try {
        const users = await apiRequest('/api/users');
        tbody.innerHTML = users.map(u => `
            <tr>
                <td>#${u.id}</td>
                <td><strong>${u.username}</strong></td>
                <td>${u.fullName}</td>
                <td><span class="badge badge-pending">${u.role}</span></td>
                <td>${u.branchName || (u.branchId ? '#' + u.branchId : 'Toàn chuỗi')}</td>
            </tr>
        `).join('');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function handleCreateUser(event) {
    event.preventDefault();
    showMessage('adminMessage', '');

    const username = document.getElementById('newUsername').value.trim();
    const password = document.getElementById('newPassword').value;
    const fullName = document.getElementById('newFullName').value.trim();
    const role = document.getElementById('newUserRoleSelect').value;
    const branchVal = document.getElementById('newUserBranchSelect').value;
    const branchId = branchVal ? Number(branchVal) : null;

    try {
        const created = await apiRequest('/api/users', {
            method: 'POST',
            body: JSON.stringify({ username, password, fullName, role, branchId })
        });
        document.getElementById('createUserForm').reset();
        onNewUserRoleChange();
        await loadUsersList();
        showMessage('adminMessage', `Đã tạo nhân viên '${created.username}' (${created.role}) thành công!`, 'success');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

// --- 3. MỤC CHI NHÁNH (NHÓM THEO TỈNH/THÀNH PHỐ, ĐẾM SỐ CHI NHÁNH ĐANG HOẠT ĐỘNG, TÌM KIẾM, CHỈNH SỬA & HỘP THOẠI XÁC NHẬN) ---
let pendingBranchSavePayload = null;

function populateCityFilterDropdown() {
    const cityFilter = document.getElementById('branchCityFilter');
    if (!cityFilter) return;
    const currentVal = cityFilter.value || 'ALL';
    const cities = [...new Set(branchesCache.map(b => b.city || 'Khác'))];

    const opts = ['<option value="ALL">Tất cả Tỉnh/Thành phố</option>'];
    for (const c of cities) {
        const activeCount = branchesCache.filter(b => (b.city || 'Khác') === c && b.isActive).length;
        opts.push(`<option value="${c}">${c} (${activeCount})</option>`);
    }
    cityFilter.innerHTML = opts.join('');
    if ([...cityFilter.options].some(o => o.value === currentVal)) {
        cityFilter.value = currentVal;
    }
}

function renderCityActiveCountSummary() {
    const bar = document.getElementById('cityActiveCountsBar');
    if (!bar) return;

    const cities = [...new Set(branchesCache.map(b => b.city || 'Khác'))];
    bar.innerHTML = cities.map(city => {
        const activeCount = branchesCache.filter(b => (b.city || 'Khác') === city && b.isActive).length;
        const totalCount = branchesCache.filter(b => (b.city || 'Khác') === city).length;
        return `
            <div class="kpi-box" style="padding:0.55rem 1rem; display:flex; align-items:center; gap:0.6rem;">
                <strong style="font-size:1.05rem; color:var(--primary-dark);">${city} (${activeCount})</strong>
                <small style="color:var(--muted);">đang hoạt động / tổng ${totalCount} cửa hàng</small>
            </div>
        `;
    }).join('');
}

function filterBranchesTable() {
    const kw = (document.getElementById('branchSearchInput')?.value || '').trim().toLowerCase();
    const cityFilter = document.getElementById('branchCityFilter')?.value || 'ALL';
    const statusFilter = document.getElementById('branchStatusFilter')?.value || 'ALL';

    const filtered = branchesCache.filter(b => {
        const matchKw = !kw ||
            (b.code || '').toLowerCase().includes(kw) ||
            (b.name || '').toLowerCase().includes(kw) ||
            (b.city || '').toLowerCase().includes(kw) ||
            (b.address || '').toLowerCase().includes(kw) ||
            (b.phone || '').toLowerCase().includes(kw);

        const matchCity = cityFilter === 'ALL' || (b.city || 'Khác') === cityFilter;

        const matchStatus = statusFilter === 'ALL' ||
            (statusFilter === 'ACTIVE' && b.isActive) ||
            (statusFilter === 'INACTIVE' && !b.isActive);

        return matchKw && matchCity && matchStatus;
    });

    renderBranchesTable(filtered);
}

async function searchBranchesFromApi() {
    showMessage('adminMessage', '');
    const kw = (document.getElementById('branchSearchInput')?.value || '').trim();
    const cityFilter = document.getElementById('branchCityFilter')?.value || 'ALL';
    const statusFilter = document.getElementById('branchStatusFilter')?.value || 'ALL';

    const params = new URLSearchParams();
    if (kw) params.set('search', kw);
    if (cityFilter !== 'ALL') params.set('city', cityFilter);
    if (statusFilter === 'ACTIVE') params.set('isActive', 'true');
    if (statusFilter === 'INACTIVE') params.set('isActive', 'false');

    try {
        const qs = params.toString() ? `?${params.toString()}` : '';
        const list = await apiRequest(`/api/branches${qs}`);
        renderBranchesTable(list);
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function clearBranchSearch() {
    const searchInput = document.getElementById('branchSearchInput');
    const citySel = document.getElementById('branchCityFilter');
    const statusSel = document.getElementById('branchStatusFilter');
    if (searchInput) searchInput.value = '';
    if (citySel) citySel.value = 'ALL';
    if (statusSel) statusSel.value = 'ALL';
    branchesCache = await apiRequest('/api/branches');
    renderBranchesTable(branchesCache);
}

function renderBranchesTable(listToRender = null) {
    populateCityFilterDropdown();
    renderCityActiveCountSummary();

    const list = listToRender || branchesCache;
    const countLabel = document.getElementById('branchCountLabel');
    if (countLabel) countLabel.textContent = list.length;

    const container = document.getElementById('groupedBranchesContainer');
    if (!container) return;

    if (list.length === 0) {
        container.innerHTML = '<div class="card"><p style="text-align:center; color:var(--muted);">Không tìm thấy cửa hàng nào khớp bộ lọc.</p></div>';
        return;
    }

    // Nhóm cửa hàng theo Tỉnh/Thành phố và tự đếm số chi nhánh đang hoạt động từ database
    const citiesInList = [...new Set(list.map(b => b.city || 'Khác'))];

    container.innerHTML = citiesInList.map(city => {
        const cityBranches = list.filter(b => (b.city || 'Khác') === city);
        const activeCountInDb = branchesCache.filter(b => (b.city || 'Khác') === city && b.isActive).length;

        const rowsHtml = cityBranches.map(b => {
            const statusBadge = b.isActive
                ? '<span class="badge badge-paid">Đang hoạt động</span>'
                : '<span class="badge badge-pending">Ngừng hoạt động</span>';

            const toggleBtn = b.isActive
                ? `<button type="button" class="btn btn-danger btn-sm" onclick="toggleBranchStatus(${b.id}, false)">Tạm ngừng</button>`
                : `<button type="button" class="btn btn-success btn-sm" onclick="toggleBranchStatus(${b.id}, true)">Kích hoạt</button>`;

            return `
                <tr>
                    <td><strong>${b.code}</strong></td>
                    <td><strong style="color:var(--primary-dark);">${b.name}</strong></td>
                    <td>${b.city || '-'}</td>
                    <td>${b.address || '<em style="color:var(--muted);">Chưa có địa chỉ</em>'}</td>
                    <td>
                        <div>ĐT: <strong>${b.phone || '-'}</strong></div>
                        <small style="color:var(--muted);">Giờ: ${b.openingHours || '07:00 - 22:30'}</small>
                    </td>
                    <td>${statusBadge}</td>
                    <td class="text-right" style="white-space:nowrap;">
                        <button type="button" class="btn btn-primary btn-sm" onclick="editBranchForm(${b.id})">✏️ Chỉnh sửa</button>
                        ${toggleBtn}
                    </td>
                </tr>
            `;
        }).join('');

        return `
            <div class="card" style="margin-bottom:1rem;">
                <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:0.5rem;">
                    <h4 style="margin-bottom:0; border:none; color:var(--primary-dark);">📍 ${city} (${activeCountInDb})</h4>
                    <span class="badge badge-paid">${activeCountInDb} cửa hàng đang hoạt động</span>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th>Mã</th>
                            <th>Tên cửa hàng</th>
                            <th>Tỉnh/TP</th>
                            <th>Địa chỉ</th>
                            <th>Liên hệ & Giờ mở</th>
                            <th>Trạng thái</th>
                            <th class="text-right">Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        ${rowsHtml}
                    </tbody>
                </table>
            </div>
        `;
    }).join('');
}

function editBranchForm(branchId) {
    const b = branchesCache.find(x => x.id === branchId);
    if (!b) return;

    document.getElementById('editingBranchId').value = b.id;
    document.getElementById('branchCodeInput').value = b.code;
    document.getElementById('branchNameInput').value = b.name;
    document.getElementById('branchCityInput').value = b.city || 'Đà Nẵng';
    document.getElementById('branchAddressInput').value = b.address || '';
    document.getElementById('branchPhoneInput').value = b.phone || '';
    document.getElementById('branchHoursInput').value = b.openingHours || '07:00 - 22:30';
    document.getElementById('branchActiveSelect').value = b.isActive ? 'true' : 'false';
    document.getElementById('branchFormTitle').textContent = `✏️ Chỉnh sửa Cửa hàng #${b.id} – ${b.name}`;
    document.getElementById('btnSaveBranch').textContent = '✓ Kiểm tra & Lưu cập nhật cửa hàng';
    document.getElementById('btnCancelEditBranch').classList.remove('hidden');

    document.getElementById('branchNameInput').focus();
}

function resetBranchForm() {
    document.getElementById('createBranchForm').reset();
    document.getElementById('editingBranchId').value = '';
    document.getElementById('branchCityInput').value = 'Đà Nẵng';
    document.getElementById('branchHoursInput').value = '07:00 - 22:30';
    document.getElementById('branchActiveSelect').value = 'true';
    document.getElementById('branchFormTitle').textContent = 'Thêm chi nhánh / cửa hàng mới';
    document.getElementById('btnSaveBranch').textContent = '✓ Kiểm tra & Lưu chi nhánh';
    document.getElementById('btnCancelEditBranch').classList.add('hidden');
}

function promptConfirmSaveBranch(event) {
    event.preventDefault();
    showMessage('adminMessage', '');

    const editingId = document.getElementById('editingBranchId').value;
    const code = document.getElementById('branchCodeInput').value.trim().toUpperCase();
    const name = document.getElementById('branchNameInput').value.trim();
    const city = document.getElementById('branchCityInput').value.trim();
    const address = document.getElementById('branchAddressInput').value.trim();
    const phone = document.getElementById('branchPhoneInput').value.trim();
    const openingHours = document.getElementById('branchHoursInput').value.trim() || '07:00 - 22:30';
    const isActive = document.getElementById('branchActiveSelect').value === 'true';

    if (!code || !name || !city) {
        showMessage('adminMessage', 'Vui lòng nhập đầy đủ Mã chi nhánh, Tên cửa hàng và Tỉnh/Thành phố.');
        return;
    }

    pendingBranchSavePayload = {
        editingId: editingId ? Number(editingId) : null,
        code,
        name,
        city,
        address,
        phone,
        openingHours,
        isActive
    };

    const existing = editingId ? branchesCache.find(b => b.id === Number(editingId)) : null;

    document.getElementById('branchCfmModalTitle').textContent = editingId
        ? `Xác nhận Cập nhật Cửa hàng #${editingId}`
        : 'Xác nhận Tạo mới Cửa hàng';

    document.getElementById('branchCfmActionType').textContent = editingId
        ? `Chỉnh sửa thông tin quán (Cũ: ${existing?.name || ''})`
        : 'Thêm cửa hàng mới vào chuỗi';

    document.getElementById('branchCfmCode').textContent = existing && existing.code !== code
        ? `${existing.code} → ${code}`
        : code;

    document.getElementById('branchCfmName').textContent = existing && existing.name !== name
        ? `${existing.name} → ${name}`
        : name;

    document.getElementById('branchCfmCity').textContent = existing && existing.city !== city
        ? `${existing.city || '(Trống)'} → ${city}`
        : city;

    document.getElementById('branchCfmAddress').textContent = existing && existing.address !== address
        ? `${existing.address || '(Trống)'} → ${address}`
        : (address || '(Chưa nhập)');

    document.getElementById('branchCfmPhone').textContent = phone || '-';
    document.getElementById('branchCfmHours').textContent = openingHours;
    document.getElementById('branchCfmStatus').innerHTML = isActive
        ? '<span class="badge badge-paid">Đang hoạt động</span>'
        : '<span class="badge badge-pending">Tạm ngừng hoạt động</span>';

    document.getElementById('branchConfirmModal').classList.remove('hidden');
}

function closeBranchConfirmModal() {
    document.getElementById('branchConfirmModal').classList.add('hidden');
    pendingBranchSavePayload = null;
}

async function executeConfirmedSaveBranch() {
    if (!pendingBranchSavePayload) return;
    const payload = pendingBranchSavePayload;
    closeBranchConfirmModal();

    try {
        const bodyObj = {
            code: payload.code,
            name: payload.name,
            city: payload.city,
            address: payload.address,
            phone: payload.phone,
            openingHours: payload.openingHours,
            isActive: payload.isActive
        };

        if (payload.editingId) {
            await apiRequest(`/api/branches/${payload.editingId}`, {
                method: 'PUT',
                body: JSON.stringify(bodyObj)
            });
            showMessage('adminMessage', `Đã lưu cập nhật cửa hàng '${payload.name}' (${payload.code} - ${payload.city}) thành công!`, 'success');
        } else {
            await apiRequest('/api/branches', {
                method: 'POST',
                body: JSON.stringify(bodyObj)
            });
            showMessage('adminMessage', `Đã thêm mới cửa hàng '${payload.name}' (${payload.code} - ${payload.city}) thành công!`, 'success');
        }

        resetBranchForm();
        branchesCache = await apiRequest('/api/branches');
        filterBranchesTable();
        setupOrdersBranchFilter();
        setupUserBranchSelect();
        setupPricingBranchSelector();
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function toggleBranchStatus(branchId, activate) {
    showMessage('adminMessage', '');
    const b = branchesCache.find(x => x.id === branchId);
    const actionLabel = activate ? 'Kích hoạt lại' : 'Tạm ngừng hoạt động';
    if (!confirm(`Bạn có chắc chắn muốn "${actionLabel}" đối với "${b?.name || 'Cửa hàng #' + branchId}" không?\n(Lưu ý: Toàn bộ nhân viên, hóa đơn và báo cáo cũ vẫn được giữ nguyên).`)) {
        return;
    }

    try {
        const endpoint = activate ? `/api/branches/${branchId}/activate` : `/api/branches/${branchId}/deactivate`;
        await apiRequest(endpoint, { method: 'PATCH' });
        branchesCache = await apiRequest('/api/branches');
        filterBranchesTable();
        showMessage('adminMessage', activate ? `Đã kích hoạt lại cửa hàng '${b?.name}'. Số lượng chi nhánh đang hoạt động đã được cập nhật.` : `Đã tạm ngừng cửa hàng '${b?.name}'. Số lượng chi nhánh đang hoạt động đã được cập nhật.`, 'success');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

// --- 4. MỤC THỰC ĐƠN & QUẢN LÝ GIÁ BÁN (GIÁ CHUNG / GIÁ RIÊNG / XÁC NHẬN TRƯỚC KHI LƯU / LỊCH SỬ ĐỔI GIÁ) ---
function setupPricingBranchSelector() {
    const sel = document.getElementById('pricingBranchSelect');
    if (currentSession.role === 'Admin') {
        sel.innerHTML = branchesCache
            .map(b => `<option value="${b.id}">${b.name} (${b.code})</option>`)
            .join('');
    } else {
        const myB = branchesCache.find(b => b.id === Number(currentSession.branchId));
        sel.innerHTML = `<option value="${currentSession.branchId}">${myB ? myB.name : 'Chi nhánh #' + currentSession.branchId}</option>`;
        sel.disabled = true;
    }
}

function getSelectedPricingBranch() {
    const branchId = Number(document.getElementById('pricingBranchSelect').value || currentSession.branchId || 1);
    return branchesCache.find(b => b.id === branchId) || { id: branchId, name: `Chi nhánh #${branchId}` };
}

async function loadAdminMenuData() {
    const branchObj = getSelectedPricingBranch();
    try {
        menuDataCache = await apiRequest(`/api/menu?branchId=${branchObj.id}`);
        const categories = menuDataCache.categories || [];
        const toppings = menuDataCache.toppings || [];

        const catSel = document.getElementById('prodCategorySelect');
        if (catSel) {
            catSel.innerHTML = categories.map(c => `<option value="${c.id}">${c.name}</option>`).join('');
        }

        const allProds = [];
        const allVariants = [];
        for (const c of categories) {
            for (const p of (c.products || [])) {
                allProds.push({ ...p, categoryName: c.name });
                for (const v of (p.variants || [])) {
                    allVariants.push({
                        ...v,
                        productId: p.id,
                        productName: p.name,
                        categoryName: c.name
                    });
                }
            }
        }

        const prodSel = document.getElementById('varProductSelect');
        if (prodSel) {
            prodSel.innerHTML = allProds.map(p => `<option value="${p.id}">${p.name} (${p.categoryName})</option>`).join('');
        }

        // Bảng giá từng món & size
        const varTbody = document.getElementById('pricingVariantsTableBody');
        varTbody.innerHTML = allVariants.map(v => {
            const hasOverride = v.branchOverridePrice !== null && v.branchOverridePrice !== undefined;
            const overrideBadge = hasOverride
                ? `<span class="badge badge-preparing">${formatCurrency(v.branchOverridePrice)} (Giá riêng)</span>`
                : `<span style="color:var(--muted);">Đang dùng giá chung</span>`;

            const adminDefaultBtn = currentSession.role === 'Admin'
                ? `<button type="button" class="btn btn-primary btn-sm" onclick="promptPriceChange('VariantDefault', ${v.id}, '${v.productName}', 'Size ${v.size}', ${v.defaultPrice}, ${v.productId})">Đổi giá chung</button>`
                : '';

            const resetBtn = hasOverride
                ? `<button type="button" class="btn btn-warning btn-sm" onclick="promptResetToDefault('VariantReset', ${v.id}, '${v.productName} (Size ${v.size})', ${v.effectivePrice}, ${v.defaultPrice})">Dùng lại giá chung</button>`
                : '';

            return `
                <tr>
                    <td><strong>${v.productName}</strong> <small style="color:var(--muted);">(${v.categoryName})</small></td>
                    <td><strong>Size ${v.size}</strong></td>
                    <td class="text-right">${formatCurrency(v.defaultPrice)}</td>
                    <td class="text-right">${overrideBadge}</td>
                    <td class="text-right"><strong style="color:var(--primary-dark); font-size:1rem;">${formatCurrency(v.effectivePrice)}</strong></td>
                    <td class="text-right" style="white-space:nowrap;">
                        ${adminDefaultBtn}
                        <button type="button" class="btn btn-secondary btn-sm" onclick="promptPriceChange('VariantBranch', ${v.id}, '${v.productName}', 'Size ${v.size}', ${v.effectivePrice}, ${v.productId})">Đặt giá riêng CN</button>
                        ${resetBtn}
                    </td>
                </tr>
            `;
        }).join('');

        // Bảng giá Topping
        const topTbody = document.getElementById('pricingToppingsTableBody');
        topTbody.innerHTML = toppings.map(t => {
            const hasOverride = t.branchOverridePrice !== null && t.branchOverridePrice !== undefined;
            const overrideBadge = hasOverride
                ? `<span class="badge badge-preparing">${formatCurrency(t.branchOverridePrice)} (Giá riêng)</span>`
                : `<span style="color:var(--muted);">Đang dùng giá chung</span>`;

            const adminDefaultBtn = currentSession.role === 'Admin'
                ? `<button type="button" class="btn btn-primary btn-sm" onclick="promptPriceChange('ToppingDefault', ${t.id}, '${t.name}', 'Topping', ${t.defaultPrice}, 0)">Đổi giá chung</button>`
                : '';

            const resetBtn = hasOverride
                ? `<button type="button" class="btn btn-warning btn-sm" onclick="promptResetToDefault('ToppingReset', ${t.id}, 'Topping: ${t.name}', ${t.effectivePrice}, ${t.defaultPrice})">Dùng lại giá chung</button>`
                : '';

            return `
                <tr>
                    <td><strong>${t.name}</strong></td>
                    <td class="text-right">${formatCurrency(t.defaultPrice)}</td>
                    <td class="text-right">${overrideBadge}</td>
                    <td class="text-right"><strong style="color:var(--primary-dark); font-size:1rem;">${formatCurrency(t.effectivePrice)}</strong></td>
                    <td class="text-right" style="white-space:nowrap;">
                        ${adminDefaultBtn}
                        <button type="button" class="btn btn-secondary btn-sm" onclick="promptPriceChange('ToppingBranch', ${t.id}, '${t.name}', 'Topping', ${t.effectivePrice}, 0)">Đặt giá riêng CN</button>
                        ${resetBtn}
                    </td>
                </tr>
            `;
        }).join('');

        await loadPriceChangeHistory();
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

// Hộp thoại nhập giá mới -> Mở Modal Xác nhận hiển thị rõ Tên món, Size/Topping, Giá cũ, Giá mới, Phạm vi ảnh hưởng
function promptPriceChange(mode, itemId, itemName, sizeOrType, oldPrice, productId) {
    const branchObj = getSelectedPricingBranch();
    const isGlobal = mode.endsWith('Default');
    const scopeText = isGlobal
        ? 'Toàn chuỗi (Giá chung áp dụng cho mọi chi nhánh không đặt giá riêng)'
        : `Giá riêng tại ${branchObj.name}`;

    const rawNew = prompt(`Nhập giá mới (VNĐ) cho "${itemName} (${sizeOrType})"\nPhạm vi: ${scopeText}\nGiá hiện tại: ${formatCurrency(oldPrice)}`, oldPrice);
    if (rawNew === null) return;

    const newPrice = Number(rawNew);
    if (isNaN(newPrice) || newPrice < 0) {
        alert('Giá nhập vào không hợp lệ.');
        return;
    }

    openPriceConfirmModal({
        itemName,
        sizeOrType,
        oldPrice,
        newPrice,
        scopeText,
        executeFn: async () => {
            if (mode === 'VariantDefault') {
                const sizeCode = sizeOrType.replace('Size ', '').trim();
                await apiRequest('/api/menu/variants', {
                    method: 'POST',
                    body: JSON.stringify({ productId, size: sizeCode, price: newPrice })
                });
            } else if (mode === 'VariantBranch') {
                await apiRequest(`/api/branches/${branchObj.id}/menu/variants/${itemId}/price`, {
                    method: 'PUT',
                    body: JSON.stringify({ price: newPrice, useDefaultPrice: false })
                });
            } else if (mode === 'ToppingDefault') {
                await apiRequest('/api/menu/toppings', {
                    method: 'POST',
                    body: JSON.stringify({ name: itemName, price: newPrice })
                });
            } else if (mode === 'ToppingBranch') {
                await apiRequest(`/api/branches/${branchObj.id}/menu/toppings/${itemId}/price`, {
                    method: 'PUT',
                    body: JSON.stringify({ price: newPrice, useDefaultPrice: false })
                });
            }
        }
    });
}

function promptResetToDefault(mode, itemId, fullName, oldPrice, defaultPrice) {
    const branchObj = getSelectedPricingBranch();
    openPriceConfirmModal({
        itemName: fullName,
        sizeOrType: 'Khôi phục về Giá chung toàn chuỗi',
        oldPrice,
        newPrice: defaultPrice,
        scopeText: `${branchObj.name} (Xóa giá riêng, dùng lại giá chung toàn chuỗi)`,
        executeFn: async () => {
            if (mode === 'VariantReset') {
                await apiRequest(`/api/branches/${branchObj.id}/menu/variants/${itemId}/price`, {
                    method: 'PUT',
                    body: JSON.stringify({ useDefaultPrice: true })
                });
            } else {
                await apiRequest(`/api/branches/${branchObj.id}/menu/toppings/${itemId}/price`, {
                    method: 'PUT',
                    body: JSON.stringify({ useDefaultPrice: true })
                });
            }
        }
    });
}

function openPriceConfirmModal(config) {
    pendingPriceConfirmAction = config.executeFn;
    document.getElementById('cfmItemName').textContent = config.itemName;
    document.getElementById('cfmSizeOrType').textContent = config.sizeOrType;
    document.getElementById('cfmOldPrice').textContent = formatCurrency(config.oldPrice);
    document.getElementById('cfmNewPrice').textContent = formatCurrency(config.newPrice);
    document.getElementById('cfmScope').textContent = config.scopeText;
    document.getElementById('priceConfirmModal').classList.remove('hidden');
}

function closePriceConfirmModal() {
    document.getElementById('priceConfirmModal').classList.add('hidden');
    pendingPriceConfirmAction = null;
}

async function confirmAndSavePriceChange() {
    if (!pendingPriceConfirmAction) return;
    const fn = pendingPriceConfirmAction;
    closePriceConfirmModal();

    try {
        await fn();
        await loadAdminMenuData();
        showMessage('adminMessage', 'Đã lưu thay đổi giá bán và ghi nhận vào lịch sử đổi giá!', 'success');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function loadPriceChangeHistory() {
    const branchObj = getSelectedPricingBranch();
    const tbody = document.getElementById('priceHistoryTableBody');
    try {
        const qs = currentSession.role === 'Admin' ? '' : `?branchId=${branchObj.id}`;
        const list = await apiRequest(`/api/menu/price-history${qs}`);

        if (list.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--muted);">Chưa có lịch sử thay đổi giá bán.</td></tr>';
            return;
        }

        tbody.innerHTML = list.map(h => `
            <tr>
                <td>${new Date(h.changedAt).toLocaleString('vi-VN')}</td>
                <td><strong>${h.changedByName}</strong></td>
                <td><strong>${h.itemName}</strong></td>
                <td><span class="badge badge-pending">${h.scopeName}</span></td>
                <td class="text-right">${formatCurrency(h.oldPrice)} &rarr; <strong style="color:var(--primary-dark);">${formatCurrency(h.newPrice)}</strong></td>
            </tr>
        `).join('');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function handleCreateCategory(event) {
    event.preventDefault();
    const name = document.getElementById('catNameInput').value.trim();
    try {
        await apiRequest('/api/menu/categories', {
            method: 'POST',
            body: JSON.stringify({ name })
        });
        document.getElementById('catNameInput').value = '';
        await loadAdminMenuData();
        showMessage('adminMessage', `Đã thêm danh mục '${name}'.`, 'success');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function handleCreateProduct(event) {
    event.preventDefault();
    const categoryId = Number(document.getElementById('prodCategorySelect').value);
    const name = document.getElementById('prodNameInput').value.trim();
    try {
        await apiRequest('/api/menu/products', {
            method: 'POST',
            body: JSON.stringify({ categoryId, name })
        });
        document.getElementById('prodNameInput').value = '';
        await loadAdminMenuData();
        showMessage('adminMessage', `Đã thêm món '${name}'.`, 'success');
    } catch (err) {
        showMessage('adminMessage', err.message, 'error');
    }
}

async function handleSaveVariant(event) {
    event.preventDefault();
    const prodSel = document.getElementById('varProductSelect');
    const productId = Number(prodSel.value);
    const prodName = prodSel.options[prodSel.selectedIndex]?.text || `Món #${productId}`;
    const size = document.getElementById('varSizeInput').value.trim().toUpperCase();
    const price = Number(document.getElementById('varPriceInput').value);

    openPriceConfirmModal({
        itemName: prodName,
        sizeOrType: `Size ${size}`,
        oldPrice: 0,
        newPrice: price,
        scopeText: 'Toàn chuỗi (Giá chung)',
        executeFn: async () => {
            await apiRequest('/api/menu/variants', {
                method: 'POST',
                body: JSON.stringify({ productId, size, price })
            });
            document.getElementById('varSizeInput').value = '';
            document.getElementById('varPriceInput').value = '';
        }
    });
}

async function handleSaveTopping(event) {
    event.preventDefault();
    const name = document.getElementById('topNameInput').value.trim();
    const price = Number(document.getElementById('topPriceInput').value);

    openPriceConfirmModal({
        itemName: name,
        sizeOrType: 'Topping',
        oldPrice: 0,
        newPrice: price,
        scopeText: 'Toàn chuỗi (Giá chung)',
        executeFn: async () => {
            await apiRequest('/api/menu/toppings', {
                method: 'POST',
                body: JSON.stringify({ name, price })
            });
            document.getElementById('topNameInput').value = '';
            document.getElementById('topPriceInput').value = '';
        }
    });
}
