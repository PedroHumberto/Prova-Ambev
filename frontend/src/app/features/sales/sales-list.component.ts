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
  template: `<section class="page-heading"><div><p class="eyebrow">PAINEL OPERACIONAL</p><h1>Vendas <span class="heading-count">{{ page()?.totalCount ?? 0 }}</span></h1><p class="page-subtitle">Acompanhe e gerencie todas as vendas da operação.</p></div><a class="button button-primary" routerLink="/sales/new">＋ Nova venda</a></section>
  @if (message()) { <app-toast [message]="message()" [error]="true" /> }
  <section class="panel filters-panel"><form [formGroup]="filtersForm" (ngSubmit)="search()"><div class="filter-main"><div class="search-field"><span>⌕</span><input formControlName="saleNumber" placeholder="Buscar por número da venda"></div><select formControlName="status"><option value="">Todos os status</option><option value="Active">Ativas</option><option value="Cancelled">Canceladas</option></select><button class="button button-ghost" type="submit">Filtrar</button></div><div class="filter-more"><input formControlName="customerName" placeholder="Cliente"><input formControlName="branchName" placeholder="Filial"><input formControlName="saleDateFrom" type="date" aria-label="Data inicial"><span class="date-separator">até</span><input formControlName="saleDateTo" type="date" aria-label="Data final"><button type="button" class="link-button" (click)="clearFilters()">Limpar filtros</button></div></form></section>
  <section class="panel table-panel"><div class="table-head"><div><h2>Histórico de vendas</h2><p>{{ page()?.totalCount ?? 0 }} registros encontrados</p></div><button type="button" class="icon-button" (click)="load()" aria-label="Atualizar">⟳</button></div>@if (loading()) { <div class="loading-state"><span class="spinner"></span>Carregando vendas...</div> } @else if (page()?.items?.length) { <div class="table-scroll"><table><thead><tr><th>Venda</th><th>Data</th><th>Cliente / Filial</th><th>Status</th><th class="align-right">Total</th><th></th></tr></thead><tbody>@for (sale of page()?.items ?? []; track sale.id) { <tr><td><a class="table-link mono" [routerLink]="['/sales', sale.id]">{{ sale.saleNumber }}</a></td><td class="muted">{{ sale.saleDate | date:'dd MMM yyyy' }}</td><td><strong>{{ sale.customerName }}</strong><small class="cell-sub">{{ sale.branchName }}</small></td><td><span class="status-pill" [class.status-cancelled]="sale.status === 'Cancelled'">{{ sale.status === 'Cancelled' ? 'Cancelada' : 'Ativa' }}</span></td><td class="align-right money">{{ sale.totalAmount | currency:'BRL':'symbol':'1.2-2' }}</td><td class="align-right"><a class="row-action" [routerLink]="['/sales', sale.id]">Ver →</a></td></tr> }</tbody></table></div><div class="pagination"><span>Mostrando {{ rangeStart() }}–{{ rangeEnd() }} de {{ page()?.totalCount ?? 0 }}</span><div><button type="button" class="page-button" [disabled]="currentPage() <= 1" (click)="goTo(currentPage() - 1)">‹</button><span class="page-number">{{ currentPage() }}</span><button type="button" class="page-button" [disabled]="currentPage() >= (page()?.totalPages ?? 0)" (click)="goTo(currentPage() + 1)">›</button></div></div> } @else { <app-empty-state icon="◌" title="Nenhuma venda encontrada" text="Ajuste os filtros ou crie uma nova venda para começar." /> }</section>`
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
