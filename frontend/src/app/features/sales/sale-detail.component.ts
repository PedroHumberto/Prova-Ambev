import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Sale } from '../../core/models/api.models';
import { ErrorService } from '../../core/services/error.service';
import { SalesService } from '../../core/services/sales.service';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { ToastComponent } from '../../shared/ui/toast.component';

@Component({
  selector: 'app-sale-detail', standalone: true, imports: [RouterLink, DatePipe, CurrencyPipe, EmptyStateComponent, ToastComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sale-detail.component.html'
})
export class SaleDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute); private readonly router = inject(Router); private readonly service = inject(SalesService); private readonly errorService = inject(ErrorService);
  readonly sale = signal<Sale | null>(null); readonly loading = signal(false); readonly actionLoading = signal(false); readonly message = signal('');
  private id = '';
  ngOnInit(): void { this.id = this.route.snapshot.paramMap.get('id') ?? ''; this.load(); }
  load(): void { if (!this.id) return; this.loading.set(true); this.service.get(this.id).subscribe({ next: response => { this.sale.set(response.data); this.loading.set(false); }, error: error => { this.loading.set(false); this.message.set(this.errorService.message(error, 'Não foi possível carregar a venda.')); } }); }
  cancelSale(): void { if (!confirm('Cancelar esta venda?')) return; this.actionLoading.set(true); this.service.cancel(this.id).subscribe({ next: () => { this.actionLoading.set(false); this.load(); }, error: error => { this.actionLoading.set(false); this.message.set(this.errorService.message(error, 'Não foi possível cancelar a venda.')); } }); }
  cancelItem(itemId: string): void { if (!confirm('Cancelar este item?')) return; this.actionLoading.set(true); this.service.cancelItem(this.id, itemId).subscribe({ next: () => { this.actionLoading.set(false); this.load(); }, error: error => { this.actionLoading.set(false); this.message.set(this.errorService.message(error, 'Não foi possível cancelar o item.')); } }); }
}
