import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  beforeEach(() => { localStorage.clear(); TestBed.configureTestingModule({ providers: [AuthService, provideHttpClient(), provideHttpClientTesting()] }); service = TestBed.inject(AuthService); http = TestBed.inject(HttpTestingController); });
  afterEach(() => { http.verify(); localStorage.clear(); });
  it('stores the API session after login', () => { const response = { success: true, message: 'ok', data: { token: 'header.eyJleHAiOjQ3NDAwMDAwMDB9.signature', email: 'a@b.com', name: 'Ana', role: 'Manager' } }; service.login('a@b.com', 'Secret1!').subscribe(); const request = http.expectOne('/api/Auth'); expect(request.request.method).toBe('POST'); request.flush(response); expect(service.session()?.email).toBe('a@b.com'); expect(service.token()).toBe(response.data.token); });
});
