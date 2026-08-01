import { FormControl, FormArray, FormGroup } from '@angular/forms';
import { moneyValidator, uniqueActiveProductsValidator, uuidValidator } from './validators';

describe('form validators', () => {
  it('accepts canonical UUIDs and rejects malformed values', () => {
    expect(new FormControl('30000000-0000-4000-8000-000000000001', uuidValidator()).valid).toBeTrue();
    expect(new FormControl('not-a-uuid', uuidValidator()).valid).toBeFalse();
  });

  it('accepts positive prices with up to two decimals', () => {
    expect(new FormControl('10.50', moneyValidator()).valid).toBeTrue();
    expect(new FormControl('10.555', moneyValidator()).valid).toBeFalse();
    expect(new FormControl('0', moneyValidator()).valid).toBeFalse();
  });

  it('rejects duplicate product IDs in the item array', () => {
    const items = new FormArray([
      new FormGroup({ productId: new FormControl('A') }),
      new FormGroup({ productId: new FormControl('a') })
    ], uniqueActiveProductsValidator());
    expect(items.hasError('duplicateProducts')).toBeTrue();
  });
});
