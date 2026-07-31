import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UserRole, UserStatus } from '../../core/models/api.models';
import { UsersService } from '../../core/services/users.service';
import { ErrorService } from '../../core/services/error.service';
import { ToastComponent } from '../../shared/ui/toast.component';

@Component({
  selector: 'app-register', standalone: true, imports: [ReactiveFormsModule, RouterLink, ToastComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="auth-page auth-page-simple"><main class="auth-card"><div class="auth-card-inner register-inner"><a class="brand" routerLink="/login"><span class="brand-mark">A</span><span><strong>ambev</strong><small>sales control</small></span></a><p class="eyebrow register-eyebrow">NOVO ACESSO</p><h2>Criar usuário</h2><p class="form-intro">Cadastre um operador para acessar o painel.</p>
    @if (error()) { <app-toast [message]="error()" [error]="true" /> } @if (success()) { <app-toast [message]="success()" /> }
    <form [formGroup]="form" (ngSubmit)="submit()" novalidate><div class="form-grid"><div><label for="username">Nome</label><input id="username" formControlName="username" placeholder="Nome completo"><small class="field-error">{{ errorFor('username') }}</small></div><div><label for="email">E-mail</label><input id="email" type="email" formControlName="email" placeholder="voce@empresa.com"><small class="field-error">{{ errorFor('email') }}</small></div><div><label for="phone">Telefone</label><input id="phone" formControlName="phone" placeholder="+5511999999999"><small class="field-error">{{ errorFor('phone') }}</small></div><div><label for="password">Senha</label><input id="password" type="password" formControlName="password" placeholder="Mínimo de 8 caracteres"><small class="field-error">{{ errorFor('password') }}</small></div><div><label for="role">Perfil</label><select id="role" formControlName="role"><option [ngValue]="UserRole.Customer">Cliente</option><option [ngValue]="UserRole.Manager">Gerente</option><option [ngValue]="UserRole.Admin">Administrador</option></select></div></div><button class="button button-primary button-full" type="submit" [disabled]="loading()">{{ loading() ? 'Criando...' : 'Criar acesso' }} <span>→</span></button></form><p class="auth-switch">Já possui uma conta? <a routerLink="/login">Voltar para login</a></p>
  </div></main></div>`
})
export class RegisterComponent {
  readonly UserRole = UserRole;
  private readonly fb = inject(FormBuilder); private readonly users = inject(UsersService); private readonly errorService = inject(ErrorService); private readonly router = inject(Router);
  readonly loading = signal(false); readonly error = signal(''); readonly success = signal('');
   readonly form = this.fb.nonNullable.group({ username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]], email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]], phone: ['', [Validators.required, Validators.pattern(/^\+[1-9]\d{1,14}$/)]], password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[!?*.@#$%^&+=]).+$/)]], role: [UserRole.Customer] });
  submit(): void { this.error.set(''); this.success.set(''); if (this.form.invalid) { this.form.markAllAsTouched(); return; } this.loading.set(true); const value = this.form.getRawValue(); this.users.create({ ...value, status: UserStatus.Active }).subscribe({ next: () => { this.loading.set(false); this.success.set('Usuário criado. Redirecionando para o login...'); setTimeout(() => void this.router.navigate(['/login']), 900); }, error: error => { this.loading.set(false); this.error.set(this.errorService.message(error, 'Não foi possível criar o usuário.')); } }); }
  errorFor(name: keyof typeof this.form.controls): string { const control = this.form.controls[name]; if (!control.touched || !control.errors) return ''; if (control.hasError('required')) return 'Campo obrigatório.'; if (control.hasError('email')) return 'Informe um e-mail válido.'; if (control.hasError('pattern')) return name === 'phone' ? 'Use formato internacional.' : 'Use maiúscula, minúscula, número e símbolo.'; return 'Valor inválido.'; }
}
