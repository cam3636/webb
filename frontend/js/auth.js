// frontend/js/auth.js
// Quản lý đăng nhập, điều hướng 4 vai trò (Admin, Manager, Cashier, Barista),
// dựng thanh menu bên trái (desktop) & thu gọn (mobile), luôn hiển thị chi nhánh làm việc.

function getHomePageByRole(role) {
    switch (role) {
        case 'Cashier':
            return '/sales.html';
        case 'Barista':
            return '/barista.html';
        case 'Manager':
            return '/inventory.html';
        case 'Admin':
            return '/reports.html';
        default:
            return '/index.html';
    }
}

function getRoleVietnameseName(role) {
    switch (role) {
        case 'Admin': return 'Quản trị toàn chuỗi';
        case 'Manager': return 'Quản lý chi nhánh';
        case 'Cashier': return 'Thu ngân bán hàng';
        case 'Barista': return 'Nhân viên pha chế';
        default: return role;
    }
}

function logout() {
    clearAuthSession();
    window.location.href = '/index.html';
}

function toggleMobileSidebar() {
    const sb = document.getElementById('appSidebar');
    if (sb) {
        sb.classList.toggle('open');
    }
}

function requireAuth(allowedRoles = []) {
    const session = getAuthSession();
    const token = session?.accessToken || session?.token;
    if (!session || !token) {
        window.location.href = '/index.html';
        return null;
    }

    if (allowedRoles.length > 0 && !allowedRoles.includes(session.role)) {
        alert(`Vai trò ${session.role} không được phép truy cập màn hình này.`);
        window.location.href = getHomePageByRole(session.role);
        return null;
    }

    renderSidebarAndHeader(session);
    return session;
}

async function renderSidebarAndHeader(session) {
    let branchDisplay = session.branchId ? `Chi nhánh #${session.branchId}` : 'Toàn chuỗi (Tất cả chi nhánh)';
    let branchAddress = '';

    try {
        const branches = await apiRequest('/api/branches');
        window.__branchesList = branches || [];
        if (session.branchId) {
            const found = (branches || []).find(b => b.id === Number(session.branchId));
            if (found) {
                branchDisplay = `${found.name} (${found.code})`;
                branchAddress = found.address || '';
            }
        }
    } catch {
        // Bỏ qua nếu không tải được
    }

    const sidebarBranchEl = document.getElementById('sidebarBranchText');
    if (sidebarBranchEl) {
        sidebarBranchEl.innerHTML = branchAddress
            ? `${branchDisplay}<br><small style="color:#e7d5c5;">${branchAddress}</small>`
            : branchDisplay;
    }

    const topbarBranchEl = document.getElementById('topbarBranchPill');
    if (topbarBranchEl) {
        topbarBranchEl.textContent = `Chi nhánh làm việc: ${branchDisplay}`;
    }

    const userBadge = document.getElementById('navUserBadge');
    if (userBadge) {
        userBadge.textContent = `${session.fullName || session.username} • ${getRoleVietnameseName(session.role)}`;
    }

    const linkSales = document.getElementById('navSales');
    const linkBarista = document.getElementById('navBarista');
    const linkCustomers = document.getElementById('navCustomers');
    const linkInventory = document.getElementById('navInventory');
    const linkReports = document.getElementById('navReports');
    const linkAdmin = document.getElementById('navAdmin');

    if (linkSales) {
        linkSales.style.display = ['Cashier', 'Admin'].includes(session.role) ? 'flex' : 'none';
    }
    if (linkBarista) {
        linkBarista.style.display = ['Barista', 'Admin'].includes(session.role) ? 'flex' : 'none';
    }
    if (linkCustomers) {
        // Barista không được xem danh bạ khách hàng; Cashier, Manager, Admin được phép mở
        linkCustomers.style.display = ['Cashier', 'Manager', 'Admin'].includes(session.role) ? 'flex' : 'none';
    }
    if (linkInventory) {
        linkInventory.style.display = ['Manager', 'Admin'].includes(session.role) ? 'flex' : 'none';
    }
    if (linkReports) {
        linkReports.style.display = ['Manager', 'Admin'].includes(session.role) ? 'flex' : 'none';
    }
    if (linkAdmin) {
        linkAdmin.style.display = ['Manager', 'Admin'].includes(session.role) ? 'flex' : 'none';
    }
}

async function handleLoginSubmit(event) {
    event.preventDefault();
    showMessage('loginMessage', '');

    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;

    if (!username || !password) {
        showMessage('loginMessage', 'Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.');
        return;
    }

    const submitBtn = document.getElementById('btnLogin');
    submitBtn.disabled = true;

    try {
        const data = await apiRequest('/api/auth/login', {
            method: 'POST',
            body: JSON.stringify({ username, password })
        });

        setAuthSession({
            accessToken: data.accessToken || data.token,
            role: data.role,
            branchId: data.branchId,
            username: data.username,
            fullName: data.fullName,
            userId: data.userId
        });

        window.location.href = getHomePageByRole(data.role);
    } catch (err) {
        showMessage('loginMessage', err.message, 'error');
    } finally {
        submitBtn.disabled = false;
    }
}

function fillDemoAccount(username, password) {
    const u = document.getElementById('username');
    const p = document.getElementById('password');
    if (u && p) {
        u.value = username;
        p.value = password;
    }
}
