import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { ApiResponseWithData, AuthResponse } from '../models/api.models';

const TOKEN_KEY = 'ambev.access_token';
const SESSION_KEY = 'ambev.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly sessionState = signal<AuthResponse | null>(this.readSession());
  readonly session = this.sessionState.asReadonly();

  constructor(private readonly http: HttpClient) {}

  login(email: string, password: string): Observable<ApiResponseWithData<AuthResponse>> {
    return this.http.post<ApiResponseWithData<AuthResponse>>('/api/Auth', { email, password }).pipe(
      tap(({ data }) => {
        localStorage.setItem(TOKEN_KEY, data.token);
        localStorage.setItem(SESSION_KEY, JSON.stringify(data));
        this.sessionState.set(data);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(SESSION_KEY);
    this.sessionState.set(null);
  }

  token(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    const token = this.token();
    if (!token) return false;
    const payload = this.decodePayload(token);
    return typeof payload['exp'] !== 'number' || payload['exp'] * 1000 > Date.now();
  }

  private readSession(): AuthResponse | null {
    try {
      const value = localStorage.getItem(SESSION_KEY);
      return value ? (JSON.parse(value) as AuthResponse) : null;
    } catch {
      return null;
    }
  }

  private decodePayload(token: string): Record<string, unknown> {
    try {
      const encoded = token.split('.')[1];
      return JSON.parse(atob(encoded.replace(/-/g, '+').replace(/_/g, '/'))) as Record<string, unknown>;
    } catch {
      return {};
    }
  }
}
