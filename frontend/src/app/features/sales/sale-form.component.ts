import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ErrorService } from '../../core/services/error.service';
import { Sale, SaleRequest } from '../../core/models/api.models';
import { SalesService } from '../../core/services/sales.service';
import { ToastComponent } from '../../shared/ui/toast.component';
import { moneyValidator, uniqueActiveProductsValidator, uuidValidator } from '../../shared/validation/validators';

@Component({
  selector: 'app-sale-form', standalone: true, imports: [ReactiveFormsModule, RouterLink, ToastComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sale-form.component.html'
})
export class SaleFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder); private readonly service = inject(SalesService); private readonly errorService = inject(ErrorService); private readonly route = inject(ActivatedRoute); private readonly router = inject(Router);
  readonly loading = signal(false); readonly message = signal(''); readonly editMode = this.route.snapshot.paramMap.has('id'); private id = '';
  readonly form = this.fb.group({ saleNumber: ['', [Validators.required, Validators.maxLength(50)]], saleDate: ['', Validators.required], customerId: ['', [Validators.required, uuidValidator()]], customerName: ['', [Validators.required, Validators.maxLength(200)]], branchId: ['', [Validators.required, uuidValidator()]], branchName: ['', [Validators.required, Validators.maxLength(200)]], items: this.fb.array([this.newItem()], uniqueActiveProductsValidator()) });
  get items(): FormArray { return this.form.controls.items; }
  ngOnInit(): void { this.id = this.route.snapshot.paramMap.get('id') ?? ''; if (this.editMode) this.load(); }
  private newItem(id?: string) { return this.fb.group({ id: [id ?? ''], productId: ['', [Validators.required, uuidValidator()]], productName: ['', [Validators.required, Validators.maxLength(200)]], quantity: [1, [Validators.required, Validators.min(1), Validators.max(20)]], unitPrice: [0.01, [Validators.required, moneyValidator()]] }); }
  addItem(): void { this.items.push(this.newItem()); }
  removeItem(index: number): void { if (this.items.length > 1) this.items.removeAt(index); }
  load(): void { this.loading.set(true); this.service.get(this.id).subscribe({ next: ({ data }) => { this.form.patchValue({ saleNumber: data.saleNumber, saleDate: this.toLocalInput(data.saleDate), customerId: data.customerId, customerName: data.customerName, branchId: data.branchId, branchName: data.branchName }); this.items.clear(); data.items.filter(item => item.status !== 'Cancelled').forEach(item => { const control = this.newItem(item.id); control.patchValue({ id: item.id, productId: item.productId, productName: item.productName, quantity: item.quantity, unitPrice: item.unitPrice }); this.items.push(control); }); this.loading.set(false); }, error: error => { this.loading.set(false); this.message.set(this.errorService.message(error, 'Não foi possível carregar a venda.')); } }); }
  submit(): void { this.message.set(''); if (this.form.invalid || !this.items.length) { this.form.markAllAsTouched(); return; } this.loading.set(true); const raw = this.form.getRawValue(); const request: SaleRequest = { saleNumber: (raw.saleNumber ?? '').trim(), saleDate: new Date(raw.saleDate ?? '').toISOString(), customerId: (raw.customerId ?? '').trim(), customerName: (raw.customerName ?? '').trim(), branchId: (raw.branchId ?? '').trim(), branchName: (raw.branchName ?? '').trim(), items: (raw.items ?? []).map(item => ({ ...(this.editMode && item.id ? { id: item.id } : {}), productId: (item.productId ?? '').trim(), productName: (item.productName ?? '').trim(), quantity: Number(item.quantity), unitPrice: Number(item.unitPrice) })) }; const operation = this.editMode ? this.service.update(this.id, request) : this.service.create(request); operation.subscribe({ next: response => { this.loading.set(false); void this.router.navigate(['/sales', response.data.id]); }, error: error => { this.loading.set(false); this.message.set(this.errorService.message(error, 'Não foi possível salvar a venda.')); } }); }
  errorFor(name: string): string { const control = this.form.get(name); return control?.touched && control.invalid ? 'Campo obrigatório ou inválido.' : ''; }
  private toLocalInput(value: string): string { const date = new Date(value); const offset = date.getTimezoneOffset() * 60000; return new Date(date.getTime() - offset).toISOString().slice(0, 16); }
}
