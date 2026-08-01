import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiResponse, ApiResponseWithData, CreateUserRequest, User } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class UsersService {
  constructor(private readonly http: HttpClient) {}

  create(request: CreateUserRequest): Observable<ApiResponseWithData<User>> {
    return this.http.post<ApiResponseWithData<User>>('/api/Users', request);
  }

  get(id: string): Observable<ApiResponseWithData<User>> {
    return this.http.get<ApiResponseWithData<User>>(`/api/Users/${id}`);
  }

  delete(id: string): Observable<ApiResponse> {
    return this.http.delete<ApiResponse>(`/api/Users/${id}`);
  }
}
