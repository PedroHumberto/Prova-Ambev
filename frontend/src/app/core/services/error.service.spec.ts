import { HttpErrorResponse } from '@angular/common/http';
import { ErrorService } from './error.service';

describe('ErrorService', () => {
  it('prioritizes field validation messages from ProblemDetails', () => {
    const service = new ErrorService();
    const error = new HttpErrorResponse({ status: 422, error: { title: 'Invalid', errors: { items: ['Quantity is invalid.'] } } });
    expect(service.message(error)).toBe('Quantity is invalid.');
  });

  it('provides a useful offline message', () => {
    const service = new ErrorService();
    expect(service.message(new HttpErrorResponse({ status: 0 }))).toBe('API indisponível. Verifique o backend.');
  });
});
