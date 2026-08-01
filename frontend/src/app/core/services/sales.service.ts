import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse, ApiResponseWithData, Sale, SaleRequest, SalesFilters, SalesPage } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class SalesService {
  constructor(private readonly http: HttpClient) {}

  list(filters: SalesFilters): Observable<ApiResponseWithData<SalesPage>> {
    let params = new HttpParams().set('_page', filters.page).set('_size', filters.size);
    const query: Record<string, string | undefined> = {
      _order: filters.order, saleNumber: filters.saleNumber, saleDateFrom: filters.saleDateFrom,
      saleDateTo: filters.saleDateTo, customerId: filters.customerId, customerName: filters.customerName,
      branchId: filters.branchId, branchName: filters.branchName, status: filters.status
    };
    Object.entries(query).forEach(([key, value]) => {
      if (value?.trim()) params = params.set(key, value.trim());
    });
    return this.http.get<ApiResponseWithData<SalesPage>>('/api/sales', { params });
  }

  get(id: string): Observable<ApiResponseWithData<Sale>> {
    return this.http.get<ApiResponseWithData<Sale>>(`/api/sales/${id}`);
  }

  create(request: SaleRequest): Observable<ApiResponseWithData<Sale>> {
    return this.http.post<ApiResponseWithData<Sale>>('/api/sales', request);
  }

  update(id: string, request: SaleRequest): Observable<ApiResponseWithData<Sale>> {
    return this.http.put<ApiResponseWithData<Sale>>(`/api/sales/${id}`, request);
  }

  cancel(id: string): Observable<ApiResponse> {
    return this.http.delete<ApiResponse>(`/api/sales/${id}`);
  }

  cancelItem(saleId: string, itemId: string): Observable<ApiResponse> {
    return this.http.delete<ApiResponse>(`/api/sales/${saleId}/items/${itemId}`);
  }
}
