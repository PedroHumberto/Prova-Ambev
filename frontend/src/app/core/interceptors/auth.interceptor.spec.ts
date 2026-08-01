import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpTestingController; let client: HttpClient;
  beforeEach(() => { localStorage.setItem('ambev.access_token', 'jwt-token'); TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] }); http = TestBed.inject(HttpTestingController); client = TestBed.inject(HttpClient); });
  afterEach(() => { http.verify(); localStorage.clear(); });
  it('adds the bearer token', () => { client.get('/api/sales').subscribe(); const request = http.expectOne('/api/sales'); expect(request.request.headers.get('Authorization')).toBe('Bearer jwt-token'); request.flush({}); });
});
