import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isUuid(value: string): boolean {
  return UUID_PATTERN.test(value.trim());
}

export function uuidValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '').trim();
    return value && isUuid(value) ? null : { uuid: true };
  };
}

export function moneyValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '').trim();
    return /^(?:0*[1-9]\d*(?:\.\d{1,2})?|0*\.\d{1,2})$/.test(value) && Number(value) > 0
      ? null
      : { money: true };
  };
}

export function uniqueActiveProductsValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const products = (control.value as Array<{ productId?: string }> | null) ?? [];
    const activeProducts = products.map(item => String(item.productId ?? '').trim().toLowerCase()).filter(Boolean);
    return new Set(activeProducts).size === activeProducts.length ? null : { duplicateProducts: true };
  };
}
