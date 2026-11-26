# Đề tài 12 – Hệ thống Quản lý Chuỗi Cửa hàng Trà sữa/Cà phê tại Quầy (Milky Tea Chain)

Dự án được tổ chức thành hai phần **Back-end (`backend/MilkyTeaChain.Api`)** và **Front-end (`frontend/`)**, sử dụng **C#, ASP.NET Core Web API, Entity Framework Core, SQL Server, JWT, Swagger và HTML/CSS/JavaScript (`fetch` API)**.

---

## 1. Cấu trúc thư mục Project

```text
MilkyTeaChain/
├── MilkyTeaChain.sln
├── README.md
│
├── backend/
│   └── MilkyTeaChain.Api/
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── BranchesController.cs
│       │   ├── CustomersController.cs
│       │   ├── InventoryController.cs
│       │   ├── MenuController.cs
│       │   ├── OrdersController.cs
│       │   ├── RecipesController.cs
│       │   ├── ReportsController.cs
│       │   └── UsersController.cs
│       ├── Services/
│       │   ├── AuthService.cs
│       │   ├── BranchAccessService.cs
│       │   ├── BranchService.cs
│       │   ├── CustomerService.cs
│       │   ├── InventoryService.cs
│       │   ├── JwtTokenService.cs
│       │   ├── MenuService.cs
│       │   ├── OrderService.cs
│       │   ├── PaymentService.cs
│       │   ├── RecipeService.cs
│       │   ├── ReportService.cs
│       │   └── UserService.cs
│       ├── Models/
│       │   ├── Branch.cs
│       │   ├── BranchInventory.cs
│       │   ├── BranchToppingPrice.cs
│       │   ├── BranchVariantPrice.cs
│       │   ├── Category.cs
│       │   ├── Customer.cs
│       │   ├── CustomerPointTransaction.cs
│       │   ├── Ingredient.cs
│       │   ├── Order.cs
│       │   ├── OrderItem.cs
│       │   ├── OrderItemTopping.cs
│       │   ├── PriceChangeHistory.cs
│       │   ├── Product.cs
│       │   ├── ProductVariant.cs
│       │   ├── StockReceipt.cs
│       │   ├── StockReceiptItem.cs
│       │   ├── Topping.cs
│       │   ├── ToppingRecipeItem.cs
│       │   ├── User.cs
│       │   └── VariantRecipeItem.cs
│       ├── DTOs/
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   └── DbSeeder.cs
│       ├── Enums/
│       │   ├── OrderStatus.cs
│       │   └── UserRole.cs     ← 4 vai trò: Admin=1, Manager=2, Cashier=3, Barista=4
│       ├── Migrations/
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── Program.cs
│       ├── appsettings.json
│       └── MilkyTeaChain.Api.csproj
│
└── frontend/
    ├── index.html          ← Đăng nhập
    ├── sales.html          ← Bán hàng tại quầy (Tìm/tạo khách, danh mục, thẻ món, modal size/topping/ghi chú, phiếu in, tra cứu đơn CN)
    ├── barista.html        ← Quầy pha chế 3 cột (Chờ pha, Đang pha, Hoàn thành - tự cập nhật mỗi 5s)
    ├── customers.html      ← Khách hàng thân thiết & Lịch sử tích điểm
    ├── inventory.html      ← Quản lý kho (Tồn kho, lập phiếu nhập nhiều nguyên liệu, lịch sử nhập/xuất)
    ├── reports.html        ← Báo cáo doanh thu (Bộ lọc ngày, chi nhánh, tổng doanh thu, số đơn, giá trị TB/đơn, đối chiếu đơn)
    ├── admin.html          ← Quản trị hệ thống 4 mục: Đơn hàng, Nhân viên, Chi nhánh, Thực đơn & Giá bán
    ├── css/
    │   └── site.css        ← Tông nâu cà phê & kem, menu dọc bên trái (desktop) & thu gọn (mobile)
    └── js/
        ├── api.js
        ├── auth.js
        ├── sales.js
        ├── barista.js
        ├── customers.js
        ├── inventory.js
        ├── reports.js
        └── admin.js
```

---

## 2. Phân quyền của 4 Vai trò và Tài khoản Demo

| Vai trò (`UserRole`) | Username | Password | Chi nhánh | Quyền hạn nghiệp vụ |
| :--- | :--- | :--- | :--- | :--- |
| **Admin (`1`)** | `cam` *(hoặc `admin`)* | `cam123` *(hoặc `Admin@123`)* | Toàn chuỗi (`null`) | Quản lý toàn chuỗi, tài khoản, vai trò, chi nhánh (thêm/sửa/tìm/ngừng HĐ), thực đơn, giá chung toàn chuỗi và giá riêng từng chi nhánh |
| **Manager (`2`)** | `manager_a` *(hoặc `manager_b`)* | `Manager@123` | Chi nhánh A *(hoặc B)* | Quản lý kho, xem đơn, danh sách khách hàng và báo cáo doanh thu trong phạm vi chi nhánh được giao; sửa giá riêng hoặc chọn “Dùng lại giá chung” tại chi nhánh đó |
| **Cashier (`3`)** | `cashier_a` *(hoặc `cashier_b`)* | `Cashier@123` | Chi nhánh A *(hoặc B)* | Tự vào chi nhánh được phân công; tra cứu hoặc tạo khách mới tại quầy, lập đơn, thu tiền, in phiếu (có tên & địa chỉ cửa hàng) và tra cứu đơn của chi nhánh |
| **Barista (`4`)** | `barista_a` *(hoặc `barista_b`)* | `Barista@123` | Chi nhánh A *(hoặc B)* | Tự vào chi nhánh được phân công; xem hàng đợi đơn đã thanh toán của chi nhánh (`Chờ pha` -> `Đang pha` -> `Hoàn thành`). Không được đổi giá, xem danh bạ khách hoặc báo cáo doanh thu |

---

## 3. Lệnh chạy ứng dụng

Trong thư mục chứa `MilkyTeaChain.sln`, cấu hình khóa ký JWT riêng cho môi trường phát triển trước lần chạy đầu tiên.

```powershell
$jwtKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Jwt:Key" $jwtKey --project backend\MilkyTeaChain.Api\MilkyTeaChain.Api.csproj
dotnet run --project backend\MilkyTeaChain.Api\MilkyTeaChain.Api.csproj
```
- **Giao diện Web**: `http://localhost:5120/` (hoặc `https://localhost:7120/`)
- **Tài liệu API (Swagger)**: `http://localhost:5120/swagger` (hoặc `https://localhost:7120/swagger`)
