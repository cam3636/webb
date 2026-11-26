// frontend/js/sales.js
// Màn hình POS Bán hàng tại quầy:
// 1. Hiển thị rõ "Đang bán tại [Tên chi nhánh]" và địa chỉ cụ thể bên dưới.
// 2. Khách hàng thân thiết: Tìm khách bằng SĐT (hiện tên & điểm hiện có), hoặc Tạo nhanh ngay tại quầy mà vẫn giữ nguyên giỏ hàng, hoặc bán dạng "Khách lẻ".
// 3. Tải thực đơn theo giá thực tế của chi nhánh đang bán (/api/menu?branchId=...).
// 4. Nếu giá đổi trong lúc thu ngân đang mở giỏ (HTTP 409 code=PRICE_CHANGED), hiển thị thông báo giá mới và yêu cầu xác nhận lại trước khi thu tiền.
// 5. Phiếu bán hàng có thể in hiển thị đầy đủ Tên cửa hàng, Địa chỉ cửa hàng, Thông tin khách & điểm tích lũy.

let currentSession = null;
let branchesList = [];
let menuCategories = [];
let menuToppings = [];
let flatProducts = [];
const drinkPhotos = new Map([
    ['trà sữa truyền thống', 'classic-milk-tea'],
    ['trà sữa matcha', 'matcha-milk-tea'],
    ['cà phê sữa đá', 'iced-milk-coffee'],
    ['bạc xỉu', 'bac-xiu']
]);
let selectedCategoryId = 'ALL';
let cart = [];
let editingCartIndex = null;
let modalProduct = null;
let currentOrder = null;
let selectedCustomer = null; // null = Khách lẻ

document.addEventListener('DOMContentLoaded', async () => {
    currentSession = requireAuth(['Cashier', 'Admin']);
    if (!currentSession) return;

    await setupBranchDisplay();
    await loadMenu();
    renderSelectedCustomerBox();
    await loadBranchOrdersAtPos();
});

async function setupBranchDisplay() {
    const adminBox = document.getElementById('adminPosBranchSelectors');
    const citySelect = document.getElementById('posCitySelect');

    try {
        branchesList = await apiRequest('/api/branches');
        if (currentSession.role === 'Admin') {
            adminBox.classList.remove('hidden');
            const activeBranches = branchesList.filter(b => b.isActive);
            const cities = [...new Set(activeBranches.map(b => b.city || 'Khác'))];

            citySelect.innerHTML = cities
                .map(c => {
                    const count = activeBranches.filter(b => (b.city || 'Khác') === c).length;
                    return `<option value="${c}">${c} (${count})</option>`;
                })
                .join('');

            populatePosStoreSelectByCity();
        } else {
            adminBox.classList.add('hidden');
            updateSellingBranchBanner();
        }
    } catch (err) {
        showMessage('salesMessage', err.message, 'error');
    }
}

function populatePosStoreSelectByCity() {
    const citySelect = document.getElementById('posCitySelect');
    const branchSelect = document.getElementById('branchSelect');
    const selectedCity = citySelect.value;

    const storesInCity = branchesList.filter(b => b.isActive && (b.city || 'Khác') === selectedCity);
    branchSelect.innerHTML = storesInCity
        .map(b => `<option value="${b.id}">${b.name} (${b.code})</option>`)
        .join('');

    // Nếu khu vực chỉ có một cửa hàng đang hoạt động thì tự động chọn cửa hàng đó
    if (storesInCity.length === 1) {
        branchSelect.value = String(storesInCity[0].id);
    }

    updateSellingBranchBanner();
}

async function onPosCityChange() {
    populatePosStoreSelectByCity();
    await loadMenu();
    await loadBranchOrdersAtPos();
}

function getSelectedBranchId() {
    if (currentSession.role === 'Admin') {
        return Number(document.getElementById('branchSelect').value || 1);
    }
    return Number(currentSession.branchId);
}

function getSelectedBranchObj() {
    const id = getSelectedBranchId();
    return branchesList.find(b => b.id === id) || null;
}

function updateSellingBranchBanner() {
    const b = getSelectedBranchObj();
    const titleEl = document.getElementById('sellingBranchTitle');
    const addrEl = document.getElementById('sellingBranchAddress');

    if (b) {
        titleEl.textContent = `Đang bán tại ${b.name} (${b.code})`;
        const cityInfo = b.city ? ` • Khu vực: ${b.city}` : '';
        addrEl.textContent = `Địa chỉ: ${b.address || 'Chưa cập nhật địa chỉ'}${cityInfo} | ĐT: ${b.phone || '-'} | Giờ mở cửa: ${b.openingHours || '07:00 - 22:30'}`;
    } else {
        titleEl.textContent = `Đang bán tại Chi nhánh #${getSelectedBranchId()}`;
        addrEl.textContent = '';
    }
}

async function onSellingBranchChange() {
    updateSellingBranchBanner();
    await loadMenu();
    await loadBranchOrdersAtPos();
}

async function loadMenu() {
    const branchId = getSelectedBranchId();
    try {
        const data = await apiRequest(`/api/menu?branchId=${branchId}`);
        menuCategories = data.categories || [];
        menuToppings = data.toppings || [];

        flatProducts = [];
        for (const cat of menuCategories) {
            for (const prod of (cat.products || [])) {
                flatProducts.push({
                    ...prod,
                    categoryId: cat.id,
                    categoryName: cat.name
                });
            }
        }

        renderCategoryTabs();
        renderProductCards();
    } catch (err) {
        showMessage('salesMessage', `Không thể tải thực đơn: ${err.message}`, 'error');
    }
}

// --- KHÁCH HÀNG THÂN THIẾT TẠI QUẦY ---
async function lookupCustomerAtPos() {
    showMessage('salesMessage', '');
    const phoneInput = document.getElementById('posCustomerPhoneInput');
    const phone = (phoneInput.value || '').trim();
    if (!phone) {
        showMessage('salesMessage', 'Vui lòng nhập số điện thoại khách hàng cần tìm.');
        return;
    }

    try {
        const customer = await apiRequest(`/api/customers/lookup?phone=${encodeURIComponent(phone)}`);
        selectedCustomer = customer;
        document.getElementById('quickCreateCustomerBox').classList.add('hidden');
        renderSelectedCustomerBox();
        showMessage('salesMessage', `Đã chọn khách hàng thân thiết: ${customer.fullName} (${customer.points} điểm).`, 'success');
    } catch (err) {
        if (err.status === 404) {
            // Chưa có khách -> mở form tạo nhanh ngay tại quầy, giữ nguyên giỏ hàng
            document.getElementById('quickCustomerPhone').value = phone;
            document.getElementById('quickCustomerName').value = '';
            document.getElementById('quickCreateCustomerBox').classList.remove('hidden');
            showMessage('salesMessage', 'Số điện thoại chưa đăng ký. Bạn có thể tạo nhanh hồ sơ khách mới ngay bên dưới (giỏ hàng vẫn giữ nguyên).', 'warn');
        } else {
            showMessage('salesMessage', err.message, 'error');
        }
    }
}

async function createQuickCustomerAtPos() {
    showMessage('salesMessage', '');
    const fullName = (document.getElementById('quickCustomerName').value || '').trim();
    const phone = (document.getElementById('quickCustomerPhone').value || '').trim();

    if (!fullName || !phone) {
        showMessage('salesMessage', 'Vui lòng nhập đủ họ tên và số điện thoại khách mới.');
        return;
    }

    try {
        const created = await apiRequest('/api/customers', {
            method: 'POST',
            body: JSON.stringify({ fullName, phone })
        });
        selectedCustomer = created;
        document.getElementById('posCustomerPhoneInput').value = created.phone;
        document.getElementById('quickCreateCustomerBox').classList.add('hidden');
        renderSelectedCustomerBox();
        showMessage('salesMessage', `Đã đăng ký nhanh khách hàng '${created.fullName}' (${created.phone}) và gắn vào đơn hiện tại! Giỏ hàng được giữ nguyên.`, 'success');
    } catch (err) {
        showMessage('salesMessage', err.message, 'error');
    }
}

function clearSelectedCustomer() {
    selectedCustomer = null;
    document.getElementById('posCustomerPhoneInput').value = '';
    document.getElementById('quickCreateCustomerBox').classList.add('hidden');
    renderSelectedCustomerBox();
}

function renderSelectedCustomerBox() {
    const infoEl = document.getElementById('selectedCustomerBanner');
    if (!selectedCustomer) {
        infoEl.innerHTML = 'Khách hàng đang chọn: <strong>Khách lẻ (Không tích điểm)</strong>';
        return;
    }

    infoEl.innerHTML = `
        Khách hàng thân thiết: <strong style="color:var(--primary-dark);">${selectedCustomer.fullName}</strong>
        (${selectedCustomer.phone}) • Điểm hiện có: <span class="badge badge-paid">${selectedCustomer.points} điểm</span>
        <button type="button" class="btn btn-secondary btn-sm" style="margin-left:0.5rem;" onclick="clearSelectedCustomer()">Đổi sang Khách lẻ</button>
    `;
}

// --- THỰC ĐƠN & GIỎ HÀNG ---
function renderCategoryTabs() {
    const container = document.getElementById('categoryTabs');
    const tabs = [
        `<button type="button" class="cat-tab ${selectedCategoryId === 'ALL' ? 'active' : ''}" onclick="selectCategory('ALL')">Tất cả món</button>`
    ];

    for (const cat of menuCategories) {
        const active = String(selectedCategoryId) === String(cat.id) ? 'active' : '';
        tabs.push(`<button type="button" class="cat-tab ${active}" onclick="selectCategory(${cat.id})">${cat.name}</button>`);
    }

    container.innerHTML = tabs.join('');
}

function selectCategory(catId) {
    selectedCategoryId = catId;
    renderCategoryTabs();
    renderProductCards();
}

function renderProductCards() {
    const keyword = (document.getElementById('searchProductInput')?.value || '').trim().toLowerCase();
    const grid = document.getElementById('productCardsGrid');

    const filtered = flatProducts.filter(p => {
        const matchCat = selectedCategoryId === 'ALL' || Number(p.categoryId) === Number(selectedCategoryId);
        const matchKw = !keyword || p.name.toLowerCase().includes(keyword) || p.categoryName.toLowerCase().includes(keyword);
        return matchCat && matchKw;
    });

    if (filtered.length === 0) {
        grid.innerHTML = '<div style="grid-column: 1 / -1; text-align:center; padding:1.5rem; color:var(--muted);">Không tìm thấy món phù hợp.</div>';
        return;
    }

    grid.innerHTML = filtered.map(p => {
        const photo = drinkPhotos.get(p.name.trim().toLowerCase());
        const variantsText = (p.variants || [])
            .map(v => {
                const eff = v.effectivePrice ?? v.price;
                const isCustom = v.branchOverridePrice !== null && v.branchOverridePrice !== undefined;
                return `Size ${v.size}: ${formatCurrency(eff)}${isCustom ? ' (Giá CN)' : ''}`;
            })
            .join(' | ');

        return `
            <div class="product-card" onclick="openItemModal(${p.id})">
                <div>
                    ${photo ? `<img class="product-photo" src="/images/drinks/${photo}.jpg" alt="" width="480" height="480" loading="lazy">` : ''}
                    <div class="cat-tag">${escapeHtml(p.categoryName)}</div>
                    <div class="prod-title">${escapeHtml(p.name)}</div>
                    <div class="size-prices">${variantsText || 'Chưa cấu hình giá'}</div>
                </div>
                <button type="button" class="btn btn-primary btn-sm" style="width:100%;">+ Chọn Size & Topping</button>
            </div>
        `;
    }).join('');
}

function openItemModal(productId, cartIdx = null) {
    const product = flatProducts.find(p => p.id === Number(productId));
    if (!product || !product.variants || product.variants.length === 0) {
        showMessage('salesMessage', 'Món này chưa có biến thể kích cỡ/giá bán.');
        return;
    }

    modalProduct = product;
    editingCartIndex = cartIdx;

    document.getElementById('modalProductTitle').textContent =
        cartIdx !== null ? `Sửa dòng món: ${product.name}` : `Chọn món: ${product.name}`;

    const variantSel = document.getElementById('modalVariantSelect');
    variantSel.innerHTML = product.variants
        .map(v => {
            const eff = v.effectivePrice ?? v.price;
            return `<option value="${v.id}" data-size="${v.size}" data-price="${eff}">Size ${v.size} – ${formatCurrency(eff)}</option>`;
        })
        .join('');

    const toppingContainer = document.getElementById('modalToppingContainer');
    toppingContainer.innerHTML = menuToppings.map(t => {
        const eff = t.effectivePrice ?? t.price;
        return `
            <label class="topping-chip">
                <input type="checkbox" name="modalToppingCheck" value="${t.id}" data-name="${t.name}" data-price="${eff}">
                <span>${t.name} (+${formatCurrency(eff)})</span>
            </label>
        `;
    }).join('');

    if (cartIdx !== null && cart[cartIdx]) {
        const existing = cart[cartIdx];
        variantSel.value = String(existing.variantId);
        document.getElementById('modalQtyInput').value = existing.quantity;
        document.getElementById('modalNoteInput').value = existing.note || '';

        const topSet = new Set(existing.toppingIds || []);
        document.querySelectorAll('input[name="modalToppingCheck"]').forEach(cb => {
            cb.checked = topSet.has(Number(cb.value));
        });
    } else {
        document.getElementById('modalQtyInput').value = 1;
        document.getElementById('modalNoteInput').value = '';
    }

    document.getElementById('itemConfigModal').classList.remove('hidden');
}

function closeItemModal() {
    document.getElementById('itemConfigModal').classList.add('hidden');
    modalProduct = null;
    editingCartIndex = null;
}

function buildLineSignature(variantId, toppingIds, note) {
    const normalizedNote = (note || '').trim().toLowerCase();
    const sortedTops = [...(toppingIds || [])].sort((a, b) => a - b).join(',');
    return `${variantId}::${sortedTops}::${normalizedNote}`;
}

function confirmModalItem() {
    if (!modalProduct) return;

    const variantSel = document.getElementById('modalVariantSelect');
    const opt = variantSel.options[variantSel.selectedIndex];
    const variantId = Number(opt.value);
    const size = opt.getAttribute('data-size');
    const unitPrice = Number(opt.getAttribute('data-price'));
    const quantity = Number(document.getElementById('modalQtyInput').value || 1);
    const note = (document.getElementById('modalNoteInput').value || '').trim();

    if (quantity <= 0) {
        alert('Số lượng ly phải lớn hơn 0.');
        return;
    }

    const checkedToppings = Array.from(document.querySelectorAll('input[name="modalToppingCheck"]:checked'))
        .map(cb => ({
            id: Number(cb.value),
            name: cb.getAttribute('data-name'),
            price: Number(cb.getAttribute('data-price'))
        }))
        .sort((a, b) => a.id - b.id);

    const toppingIds = checkedToppings.map(t => t.id);
    const signature = buildLineSignature(variantId, toppingIds, note);

    const newLine = {
        signature,
        productId: modalProduct.id,
        variantId,
        productName: modalProduct.name,
        size,
        unitPrice,
        quantity,
        toppings: checkedToppings,
        toppingIds,
        note
    };

    if (editingCartIndex !== null) {
        cart[editingCartIndex] = newLine;
    } else {
        const duplicateIdx = cart.findIndex(item => item.signature === signature);
        if (duplicateIdx >= 0) {
            cart[duplicateIdx].quantity += quantity;
        } else {
            cart.push(newLine);
        }
    }

    closeItemModal();
    renderCart();
}

function editCartItem(index) {
    const line = cart[index];
    if (!line) return;
    openItemModal(line.productId, index);
}

function removeFromCart(index) {
    cart.splice(index, 1);
    renderCart();
}

function clearCart() {
    cart = [];
    renderCart();
}

function renderCart() {
    const tbody = document.getElementById('cartTableBody');
    const totalEl = document.getElementById('cartEstimatedTotal');

    if (cart.length === 0) {
        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center; padding:1rem; color:var(--muted);">Giỏ hàng đang trống. Bấm chọn món ở danh sách bên trái.</td></tr>';
        totalEl.textContent = formatCurrency(0);
        return;
    }

    let sum = 0;
    tbody.innerHTML = cart.map((item, idx) => {
        const toppingSum = item.toppings.reduce((s, t) => s + t.price, 0);
        const lineTotal = (item.unitPrice + toppingSum) * item.quantity;
        sum += lineTotal;

        const toppingText = item.toppings.length > 0
            ? item.toppings.map(t => `${t.name} (+${formatCurrency(t.price)})`).join(', ')
            : 'Không topping';

        const noteHtml = item.note
            ? `<div style="font-size:0.8rem; color:#92400e;">Ghi chú: <em>${item.note}</em></div>`
            : '';

        return `
            <tr>
                <td>
                    <strong>${escapeHtml(item.productName)}</strong> (Size ${item.size})
                    <div style="font-size:0.8rem; color:var(--muted);">${toppingText}</div>
                    ${noteHtml}
                </td>
                <td>${item.quantity}</td>
                <td class="text-right">${formatCurrency(item.unitPrice + toppingSum)}</td>
                <td class="text-right"><strong>${formatCurrency(lineTotal)}</strong></td>
                <td class="text-right" style="white-space:nowrap;">
                    <button type="button" class="btn btn-secondary btn-sm" onclick="editCartItem(${idx})">Sửa</button>
                    <button type="button" class="btn btn-danger btn-sm" onclick="removeFromCart(${idx})">Xóa</button>
                </td>
            </tr>
        `;
    }).join('');

    totalEl.textContent = formatCurrency(sum);
}

function buildOrderPayload(confirmPriceChange = false) {
    return {
        customerId: selectedCustomer ? selectedCustomer.id : null,
        confirmPriceChange,
        items: cart.map(item => ({
            variantId: item.variantId,
            quantity: item.quantity,
            toppingIds: item.toppingIds,
            note: item.note || '',
            expectedUnitPrice: item.unitPrice,
            expectedToppingTotal: item.toppings.reduce((s, t) => s + t.price, 0)
        }))
    };
}

// Cập nhật lại đơn giá mới cho các món đang nằm trong giỏ khi giá vừa đổi
async function syncCartWithLatestMenu() {
    await loadMenu();
    for (const item of cart) {
        const prod = flatProducts.find(p => p.id === item.productId);
        const variant = prod?.variants?.find(v => v.id === item.variantId);
        if (variant) {
            item.unitPrice = Number(variant.effectivePrice ?? variant.price);
        }
        for (const top of item.toppings) {
            const latestTop = menuToppings.find(t => t.id === top.id);
            if (latestTop) {
                top.price = Number(latestTop.effectivePrice ?? latestTop.price);
            }
        }
    }
    renderCart();
}

async function createOrder(confirmPriceChange = false) {
    showMessage('salesMessage', '');
    if (cart.length === 0) {
        showMessage('salesMessage', 'Vui lòng chọn ít nhất một món vào giỏ hàng trước khi tạo đơn.');
        return;
    }

    const branchId = getSelectedBranchId();
    const payload = buildOrderPayload(confirmPriceChange);

    const btn = document.getElementById('btnCreateOrder');
    btn.disabled = true;

    try {
        const order = await apiRequest(`/api/branches/${branchId}/orders`, {
            method: 'POST',
            body: JSON.stringify(payload)
        });

        currentOrder = order;
        const orderId = order.orderId || order.id;
        document.getElementById('lookupOrderIdInput').value = orderId;

        renderOrderDetails(order);
        clearCart();
        showMessage('salesMessage', `Đã tạo đơn hàng #${orderId} thành công! Tổng tiền: ${formatCurrency(order.totalAmount)}. Hãy bấm "Xác nhận Thanh toán".`, 'success');
    } catch (err) {
        if (err.payload?.code === 'PRICE_CHANGED' || err.message.includes('Bảng giá vừa được cập nhật')) {
            await syncCartWithLatestMenu();
            if (confirm(`${err.message}\n\nGiỏ hàng đã được cập nhật theo giá mới. Bạn có đồng ý tạo đơn với giá mới không?`)) {
                btn.disabled = false;
                return await createOrder(true);
            }
        }
        showMessage('salesMessage', err.message, 'error');
    } finally {
        btn.disabled = false;
    }
}

async function createAndPayImmediately(confirmPriceChange = false) {
    showMessage('salesMessage', '');
    if (cart.length === 0) {
        showMessage('salesMessage', 'Giỏ hàng đang trống. Vui lòng chọn món trước khi thanh toán.');
        return;
    }

    const branchId = getSelectedBranchId();
    const payload = buildOrderPayload(confirmPriceChange);

    const btn = document.getElementById('btnQuickPay');
    btn.disabled = true;

    try {
        const order = await apiRequest(`/api/branches/${branchId}/orders`, {
            method: 'POST',
            body: JSON.stringify(payload)
        });

        currentOrder = order;
        const orderId = order.orderId || order.id;
        document.getElementById('lookupOrderIdInput').value = orderId;
        clearCart();

        const paidOrder = await apiRequest(`/api/orders/${orderId}/payment`, {
            method: 'POST'
        });

        currentOrder = paidOrder;
        if (selectedCustomer && paidOrder.customerTotalPoints !== undefined && paidOrder.customerTotalPoints !== null) {
            selectedCustomer.points = paidOrder.customerTotalPoints;
            renderSelectedCustomerBox();
        }
        renderOrderDetails(paidOrder);

        const pointMsg = paidOrder.pointsEarned > 0
            ? ` Khách hàng '${paidOrder.customerName}' được cộng +${paidOrder.pointsEarned} điểm (Tổng: ${paidOrder.customerTotalPoints} điểm).`
            : '';
        showMessage('salesMessage', `Thanh toán thành công đơn hàng #${orderId}!${pointMsg}`, 'success');
    } catch (err) {
        if (err.payload?.code === 'PRICE_CHANGED' || err.message.includes('Bảng giá vừa được cập nhật')) {
            await syncCartWithLatestMenu();
            if (confirm(`${err.message}\n\nGiỏ hàng đã được cập nhật theo giá mới. Bạn có đồng ý thu tiền với giá mới không?`)) {
                btn.disabled = false;
                return await createAndPayImmediately(true);
            }
        }
        if (currentOrder) {
            renderOrderDetails(currentOrder);
        }
        showMessage('salesMessage', err.message, 'error');
    } finally {
        btn.disabled = false;
    }
}

async function payCurrentOrder() {
    const orderId = Number(document.getElementById('lookupOrderIdInput').value || currentOrder?.orderId || currentOrder?.id);
    if (!orderId) {
        showMessage('salesMessage', 'Chưa có mã đơn hàng để thanh toán.');
        return;
    }

    showMessage('salesMessage', '');
    const btnPay = document.getElementById('btnPayOrder');
    btnPay.disabled = true;

    try {
        const paidOrder = await apiRequest(`/api/orders/${orderId}/payment`, {
            method: 'POST'
        });

        currentOrder = paidOrder;
        if (selectedCustomer && paidOrder.customerTotalPoints !== undefined && paidOrder.customerTotalPoints !== null) {
            selectedCustomer.points = paidOrder.customerTotalPoints;
            renderSelectedCustomerBox();
        }
        renderOrderDetails(paidOrder);

        const pointMsg = paidOrder.pointsEarned > 0
            ? ` Khách hàng '${paidOrder.customerName}' được cộng +${paidOrder.pointsEarned} điểm (Tổng: ${paidOrder.customerTotalPoints} điểm).`
            : '';
        showMessage('salesMessage', `Thanh toán thành công đơn hàng #${orderId}! Trạng thái: ${paidOrder.status}.${pointMsg}`, 'success');
    } catch (err) {
        if (err.isNetworkError) {
            showMessage('salesMessage', `Mất kết nối trong lúc thanh toán. Đang kiểm tra lại trạng thái đơn #${orderId}...`, 'warn');
            await checkOrderStatus();
        } else {
            showMessage('salesMessage', err.message, 'error');
        }
    } finally {
        btnPay.disabled = false;
    }
}

async function checkOrderStatus() {
    const orderId = Number(document.getElementById('lookupOrderIdInput').value || currentOrder?.orderId || currentOrder?.id);
    if (!orderId) {
        showMessage('salesMessage', 'Vui lòng nhập mã đơn hàng cần kiểm tra.');
        return;
    }

    try {
        const order = await apiRequest(`/api/orders/${orderId}`);
        currentOrder = order;
        renderOrderDetails(order);
        showMessage('salesMessage', `Đã tải đơn hàng #${orderId} – Trạng thái thanh toán: ${order.status}.`, 'success');
    } catch (err) {
        showMessage('salesMessage', err.message, 'error');
    }
}

function renderOrderDetails(order) {
    const card = document.getElementById('orderResultCard');
    card.classList.remove('hidden');

    const id = order.orderId || order.id;
    document.getElementById('resOrderId').textContent = `#${id}`;
    document.getElementById('resBranchName').textContent =
        `${order.branchName || 'Chi nhánh #' + order.branchId}${order.branchAddress ? ' (' + order.branchAddress + ')' : ''}`;
    document.getElementById('resTotalAmount').textContent = formatCurrency(order.totalAmount);

    const custInfoEl = document.getElementById('resCustomerInfo');
    if (order.customerName) {
        custInfoEl.innerHTML = `Khách hàng: <strong>${order.customerName}</strong> (${order.customerPhone}) • Điểm cộng hóa đơn này: <strong>+${order.pointsEarned || 0} điểm</strong> (Tổng điểm: ${order.customerTotalPoints ?? '-'})`;
    } else {
        custInfoEl.innerHTML = 'Khách hàng: <strong>Khách lẻ</strong>';
    }

    const statusEl = document.getElementById('resOrderStatus');
    statusEl.textContent = order.status === 'Paid' ? 'Đã thanh toán (Paid)' : 'Chờ thanh toán (Pending)';
    statusEl.className = `badge ${order.status === 'Paid' ? 'badge-paid' : 'badge-pending'}`;

    const itemsBody = document.getElementById('resOrderItemsBody');
    itemsBody.innerHTML = (order.items || []).map(i => `
        <tr>
            <td>
                <strong>${escapeHtml(i.productName)}</strong> (Size ${i.size})
                ${i.note ? `<div style="font-size:0.8rem; color:#92400e;">Ghi chú: ${i.note}</div>` : ''}
            </td>
            <td>${(i.toppings && i.toppings.length > 0) ? i.toppings.join(', ') : 'Không'}</td>
            <td>${i.quantity}</td>
            <td class="text-right">${formatCurrency(i.unitPrice)}</td>
            <td class="text-right"><strong>${formatCurrency(i.lineTotal)}</strong></td>
        </tr>
    `).join('');

    renderPrintableReceipt(order);
    loadBranchOrdersAtPos();
}

async function loadBranchOrdersAtPos() {
    const tbody = document.getElementById('posBranchOrdersBody');
    if (!tbody) return;
    const branchId = getSelectedBranchId();

    try {
        const orders = await apiRequest(`/api/orders?branchId=${branchId}`);
        const topOrders = (orders || []).slice(0, 15);
        if (topOrders.length === 0) {
            tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; color:var(--muted);">Chưa có đơn hàng nào tại chi nhánh này.</td></tr>';
            return;
        }

        tbody.innerHTML = topOrders.map(o => {
            const id = o.orderId || o.id;
            const timeStr = new Date(o.paidAt || o.createdAt).toLocaleString('vi-VN');
            const cust = o.customerName ? `${o.customerName} (${o.customerPhone}) +${o.pointsEarned || 0}đ` : 'Khách lẻ';
            const itemsText = (o.items || []).map(i => `${escapeHtml(i.productName)} (${i.size} - ${formatCurrency(i.unitPrice)}) x${i.quantity}`).join(', ');
            const statusBadge = o.status === 'Paid'
                ? '<span class="badge badge-paid">Paid</span>'
                : '<span class="badge badge-pending">Pending</span>';
            const prepBadge = (o.isPrepared || o.preparationStatus === 'Completed')
                ? '<span class="badge badge-paid">Hoàn thành</span>'
                : (o.preparationStatus === 'Preparing' ? '<span class="badge badge-preparing">Đang pha</span>' : '<span class="badge badge-pending">Chờ pha</span>');

            return `
                <tr>
                    <td><strong>#${id}</strong></td>
                    <td>${timeStr}</td>
                    <td>${cust}</td>
                    <td>${itemsText}</td>
                    <td>${statusBadge}</td>
                    <td>${prepBadge}</td>
                    <td class="text-right"><strong>${formatCurrency(o.totalAmount)}</strong></td>
                    <td class="text-right">
                        <button type="button" class="btn btn-secondary btn-sm" onclick="document.getElementById('lookupOrderIdInput').value=${id}; checkOrderStatus();">Xem / In phiếu</button>
                    </td>
                </tr>
            `;
        }).join('');
    } catch {
        // Bỏ qua nếu không tải được
    }
}

function renderPrintableReceipt(order) {
    const area = document.getElementById('receiptPrintArea');
    if (!area) return;

    const id = order.orderId || order.id;
    const timeStr = new Date(order.paidAt || order.createdAt).toLocaleString('vi-VN');
    // Lấy tên và địa chỉ từ cùng hồ sơ chi nhánh
    const branchObj = branchesList.find(b => b.id === order.branchId);
    const branchName = branchObj?.name || order.branchName || `Chi nhánh #${order.branchId}`;
    const branchAddr = branchObj?.address || order.branchAddress || '';
    const branchCity = branchObj?.city || order.branchCity || '';
    const branchPhone = branchObj?.phone || order.branchPhone || '';

    const lines = (order.items || []).map(i => {
        const topText = (i.toppings && i.toppings.length > 0) ? `<br>&nbsp;&nbsp;+ Topping: ${i.toppings.join(', ')}` : '';
        const noteText = i.note ? `<br>&nbsp;&nbsp;* Ghi chú: ${i.note}` : '';
        return `
            <div style="display:flex; justify-content:space-between; margin:0.35rem 0;">
                <span>${escapeHtml(i.productName)} (${i.size} - ${formatCurrency(i.unitPrice)}) x${i.quantity}${topText}${noteText}</span>
                <strong>${formatCurrency(i.lineTotal)}</strong>
            </div>
        `;
    }).join('');

    const custHtml = order.customerName
        ? `<div style="border-top:1px dashed #333; margin-top:0.4rem; padding-top:0.35rem; font-size:0.85rem;">
               Khách hàng: <strong>${order.customerName}</strong> (${order.customerPhone})<br>
               Điểm cộng đơn này: <strong>+${order.pointsEarned || 0}</strong> | Tổng điểm tích lũy: <strong>${order.customerTotalPoints ?? 0}</strong>
           </div>`
        : `<div style="border-top:1px dashed #333; margin-top:0.4rem; padding-top:0.35rem; font-size:0.85rem;">Khách hàng: Khách lẻ</div>`;

    area.innerHTML = `
        <div style="text-align:center; border-bottom:1px dashed #333; padding-bottom:0.5rem; margin-bottom:0.5rem;">
            <strong style="font-size:1.1rem;">Milky Tea – Trà sữa & Cà phê</strong><br>
            <strong>${branchName}</strong><br>
            <span>Đ/c: ${branchAddr || 'Chưa cập nhật'}${branchCity ? ' (' + branchCity + ')' : ''}</span>${branchPhone ? ` | ĐT: ${branchPhone}` : ''}<br>
            <strong style="display:inline-block; margin-top:0.3rem;">PHIẾU BÁN HÀNG #${id}</strong><br>
            <small>Thời gian: ${timeStr} | Thu ngân: ${order.cashierName || currentSession.fullName}</small><br>
            <small>Trạng thái: ${order.status === 'Paid' ? 'ĐÃ THANH TOÁN' : 'CHỜ THANH TOÁN'}</small>
        </div>
        ${lines}
        ${custHtml}
        <div style="border-top:1px dashed #333; margin-top:0.6rem; padding-top:0.5rem; display:flex; justify-content:space-between; font-size:1.05rem;">
            <strong>TỔNG CỘNG:</strong>
            <strong>${formatCurrency(order.totalAmount)}</strong>
        </div>
        <div style="text-align:center; margin-top:0.6rem; font-size:0.8rem;">
            Cảm ơn quý khách & Hẹn gặp lại!
        </div>
    `;
}

function printSalesReceipt() {
    if (!currentOrder) {
        showMessage('salesMessage', 'Chưa có đơn hàng để in phiếu.');
        return;
    }
    window.print();
}
