import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { SalesService } from './sales.service';

describe('SalesService', () => {
  let service: SalesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [SalesService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(SalesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('serializes supported filters and preserves the response envelope', () => {
    service.list({ page: 2, size: 20, order: 'saleDate desc', customerId: 'id', status: 'Active' }).subscribe(response => {
      expect(response.data.totalPages).toBe(1);
    });
    const request = http.expectOne(value => value.url === '/api/sales');
    expect(request.request.params.get('_page')).toBe('2');
    expect(request.request.params.get('_size')).toBe('20');
    expect(request.request.params.get('_order')).toBe('saleDate desc');
    expect(request.request.params.get('customerId')).toBe('id');
    request.flush({ success: true, message: 'ok', data: { items: [], pageNumber: 2, pageSize: 20, totalCount: 0, totalPages: 1 } });
  });
});
