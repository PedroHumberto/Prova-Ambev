export interface ApiResponse {
  success: boolean;
  message: string;
}

export interface ApiResponseWithData<T> extends ApiResponse {
  data: T;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export interface AuthResponse {
  token: string;
  email: string;
  name: string;
  role: string;
}

export enum UserRole {
  None = 0,
  Customer = 1,
  Manager = 2,
  Admin = 3
}

export enum UserStatus {
  Unknown = 0,
  Active = 1,
  Inactive = 2,
  Suspended = 3
}

export interface User {
  id: string;
  name: string;
  email: string;
  phone: string;
  role: UserRole;
  status: UserStatus;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  phone: string;
  email: string;
  status: UserStatus;
  role: UserRole;
}

export interface SaleItem {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  discountPercentage: number;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  status: 'Active' | 'Cancelled' | string;
  createdAt: string;
  updatedAt: string;
  cancelledAt: string | null;
}

export interface SaleSummary {
  id: string;
  saleNumber: string;
  saleDate: string;
  customerId: string;
  customerName: string;
  branchId: string;
  branchName: string;
  status: 'Active' | 'Cancelled' | string;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  createdAt: string;
  updatedAt: string;
  cancelledAt: string | null;
}

export interface Sale extends SaleSummary {
  items: SaleItem[];
}

export interface SalesPage {
  items: SaleSummary[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface SaleItemRequest {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export interface UpdateSaleItemRequest extends SaleItemRequest {
  id?: string;
}

export interface SaleRequest {
  saleNumber: string;
  saleDate: string;
  customerId: string;
  customerName: string;
  branchId: string;
  branchName: string;
  items: SaleItemRequest[] | UpdateSaleItemRequest[];
}

export interface SalesFilters {
  page: number;
  size: number;
  order?: string;
  saleNumber?: string;
  saleDateFrom?: string;
  saleDateTo?: string;
  customerId?: string;
  customerName?: string;
  branchId?: string;
  branchName?: string;
  status?: 'Active' | 'Cancelled';
}
