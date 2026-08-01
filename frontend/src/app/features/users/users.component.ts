import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { UserRole, UserStatus } from '../../core/models/api.models';
import { ErrorService } from '../../core/services/error.service';
import { UsersService } from '../../core/services/users.service';
import { ToastComponent } from '../../shared/ui/toast.component';
import { isUuid } from '../../shared/validation/validators';

@Component({
  selector: 'app-users', standalone: true, imports: [FormsModule, ReactiveFormsModule, ToastComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './users.component.html'
})
export class UsersComponent {
  readonly UserRole = UserRole; private readonly fb = inject(FormBuilder); private readonly service = inject(UsersService); private readonly errors = inject(ErrorService);
  readonly loading = signal(false); readonly message = signal(''); readonly isError = signal(false); readonly user = signal<import('../../core/models/api.models').User | null>(null); lookupId = '';
   readonly form = this.fb.nonNullable.group({ username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]], email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]], phone: ['', [Validators.required, Validators.pattern(/^\+[1-9]\d{1,14}$/)]], password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[!?*.@#$%^&+=]).+$/)]], role: [UserRole.Customer] });
  create(): void { this.message.set(''); this.isError.set(false); if (this.form.invalid) { this.form.markAllAsTouched(); return; } this.loading.set(true); const value = this.form.getRawValue(); this.service.create({ ...value, status: UserStatus.Active }).subscribe({ next: ({ data }) => { this.loading.set(false); this.user.set(data); this.message.set('Usuário criado com sucesso.'); this.form.reset({ role: UserRole.Customer }); }, error: error => { this.loading.set(false); this.isError.set(true); this.message.set(this.errors.message(error, 'Não foi possível criar o usuário.')); } }); }
   lookup(): void { this.message.set(''); if (!isUuid(this.lookupId)) { this.isError.set(true); this.message.set('Informe um UUID válido.'); return; } this.service.get(this.lookupId.trim()).subscribe({ next: ({ data }) => { this.user.set(data); }, error: error => { this.isError.set(true); this.message.set(this.errors.message(error, 'Usuário não encontrado.')); } }); }
  remove(id: string): void { if (!confirm('Excluir este usuário?')) return; this.service.delete(id).subscribe({ next: () => { this.user.set(null); this.message.set('Usuário excluído.'); this.isError.set(false); }, error: error => { this.isError.set(true); this.message.set(this.errors.message(error, 'Não foi possível excluir o usuário.')); } }); }
  roleName(role: UserRole): string { return ['Nenhum', 'Cliente', 'Gerente', 'Administrador'][role] ?? 'Desconhecido'; }
  statusName(status: UserStatus): string { return ['Desconhecido', 'Ativo', 'Inativo', 'Suspenso'][status] ?? 'Desconhecido'; }
}
