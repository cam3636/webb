// MilkyTeaChain.Api/wwwroot/js/api.js
// Hàm gọi API dùng fetch() với đường dẫn tương đối, tự động đính kèm JWT và xử lý lỗi rõ ràng.

const AUTH_STORAGE_KEY = 'milkytea_auth';

function getAuthSession() {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!raw) return null;
    try {
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

function setAuthSession(session) {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
}

function clearAuthSession() {
    localStorage.removeItem(AUTH_STORAGE_KEY);
}

function formatCurrency(amount) {
    return Number(amount || 0).toLocaleString('vi-VN') + ' đ';
}

function showMessage(elementId, text, type = 'error') {
    const el = document.getElementById(elementId);
    if (!el) return;
    if (!text) {
        el.className = 'alert hidden';
        el.textContent = '';
        return;
    }
    const typeClass = type === 'success'
        ? 'alert-success'
        : type === 'warn'
            ? 'alert-warn'
            : 'alert-error';
    el.className = `alert ${typeClass}`;
    el.textContent = text;
}

async function apiRequest(url, options = {}) {
    const session = getAuthSession();
    const headers = {
        'Accept': 'application/json',
        ...(options.headers || {})
    };

    if (options.body && !(options.body instanceof FormData)) {
        headers['Content-Type'] = 'application/json';
    }

    const token = session?.accessToken || session?.token;
    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    let response;
    try {
        response = await fetch(url, {
            ...options,
            headers
        });
    } catch (networkErr) {
        const err = new Error('Mất kết nối tới máy chủ API. Vui lòng kiểm tra đường truyền mạng.');
        err.isNetworkError = true;
        throw err;
    }

    let payload = null;
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json')) {
        try {
            payload = await response.json();
        } catch {
            payload = null;
        }
    } else {
        const text = await response.text();
        if (text) payload = { message: text };
    }

    if (!response.ok) {
        let message = payload?.message || payload?.title || `Lỗi HTTP ${response.status}`;

        if (response.status === 401) {
            if (!url.includes('/api/auth/login')) {
                message = 'Phiên đăng nhập đã hết hạn hoặc không hợp lệ (401). Vui lòng đăng nhập lại.';
            } else {
                message = payload?.message || 'Sai tên đăng nhập hoặc mật khẩu.';
            }
        } else if (response.status === 403) {
            message = payload?.message || 'Bạn không đủ quyền thực hiện thao tác này hoặc không thuộc chi nhánh được phép (403).';
        } else if (response.status === 409) {
            message = payload?.message || 'Không thể hoàn tất giao dịch do thiếu nguyên liệu tồn kho (409).';
        }

        const error = new Error(message);
        error.status = response.status;
        error.payload = payload;
        throw error;
    }

    return payload;
}
