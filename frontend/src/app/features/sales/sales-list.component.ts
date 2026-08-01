import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SalesService } from '../../core/services/sales.service';
import { ErrorService } from '../../core/services/error.service';
import { SaleSummary, SalesFilters, SalesPage } from '../../core/models/api.models';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { ToastComponent } from '../../shared/ui/toast.component';

@Component({
  selector: 'app-sales-list', standalone: true, imports: [ReactiveFormsModule, RouterLink, EmptyStateComponent, ToastComponent, DatePipe, CurrencyPipe], changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sales-list.component.html'
})
export class SalesListComponent implements OnInit {
  private readonly fb = inject(FormBuilder); private readonly service = inject(SalesService); private readonly errorService = inject(ErrorService);
  readonly page = signal<SalesPage | null>(null); readonly loading = signal(false); readonly message = signal(''); readonly currentPage = signal(1);
  readonly filtersForm = this.fb.nonNullable.group({ saleNumber: '', status: '', customerName: '', branchName: '', saleDateFrom: '', saleDateTo: '' });
  ngOnInit(): void { this.load(); }
   load(): void { this.loading.set(true); this.message.set(''); const value = this.filtersForm.getRawValue(); const filters: SalesFilters = { page: this.currentPage(), size: 10, saleNumber: value.saleNumber, customerName: value.customerName, branchName: value.branchName, status: value.status as 'Active' | 'Cancelled' | undefined, saleDateFrom: value.saleDateFrom ? `${value.saleDateFrom}T00:00:00Z` : undefined, saleDateTo: value.saleDateTo ? `${value.saleDateTo}T23:59:59Z` : undefined, order: 'saleDate desc' }; this.service.list(filters).subscribe({ next: response => { this.page.set(response.data); this.loading.set(false); }, error: error => { this.loading.set(false); this.message.set(this.errorService.message(error, 'Não foi possível carregar as vendas.')); } }); }
  search(): void { this.currentPage.set(1); this.load(); }
  clearFilters(): void { this.filtersForm.reset(); this.search(); }
  goTo(page: number): void { this.currentPage.set(page); this.load(); }
  rangeStart(): number { return (this.currentPage() - 1) * 10 + 1; }
  rangeEnd(): number { return Math.min(this.currentPage() * 10, this.page()?.totalCount ?? 0); }
}
