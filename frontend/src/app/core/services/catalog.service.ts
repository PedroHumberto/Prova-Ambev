import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponseWithData, CatalogItem } from '../models/api.models';

export type CatalogType = 'customers' | 'branches' | 'products';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  constructor(private readonly http: HttpClient) {}

  search(type: CatalogType, search = '', limit = 10): Observable<ApiResponseWithData<CatalogItem[]>> {
    let params = new HttpParams().set('limit', limit);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<ApiResponseWithData<CatalogItem[]>>(`/api/catalog/${type}`, { params });
  }
}
