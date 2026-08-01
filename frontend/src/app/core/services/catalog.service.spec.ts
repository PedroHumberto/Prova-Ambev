import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CatalogService } from './catalog.service';

describe('CatalogService', () => {
  let service: CatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [CatalogService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the first ten catalog items without a search term', () => {
    service.search('customers').subscribe(response => expect(response.data[0].name).toBe('Demo Customer'));
    const request = http.expectOne('/api/catalog/customers?limit=10');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, message: 'ok', data: [{ id: 'id', name: 'Demo Customer' }] });
  });

  it('sends the typed search term', () => {
    service.search('products', ' demo ', 10).subscribe();
    const request = http.expectOne(value => value.url === '/api/catalog/products');
    expect(request.request.params.get('search')).toBe('demo');
    request.flush({ success: true, message: 'ok', data: [] });
  });
});
