import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ErrorService } from '../../core/services/error.service';
import { ToastComponent } from '../../shared/ui/toast.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, ToastComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="auth-page"><div class="auth-art"><a class="brand brand-light" routerLink="/"><span class="brand-mark">A</span><span><strong>ambev</strong><small>sales control</small></span></a><div class="art-copy"><p class="eyebrow">OPERAÇÃO EM FOCO</p><h1>Decisões melhores começam com <em>visibilidade.</em></h1><p>Centralize suas vendas, acompanhe resultados e mantenha o time em movimento.</p></div><div class="art-grid"></div></div>
      <main class="auth-card"><div class="auth-card-inner"><div class="mobile-brand"><a class="brand" routerLink="/"><span class="brand-mark">A</span><span><strong>ambev</strong><small>sales control</small></span></a></div><p class="eyebrow">BEM-VINDO DE VOLTA</p><h2>Entrar na sua conta</h2><p class="form-intro">Acesse o painel para acompanhar suas operações.</p>
        @if (error()) { <app-toast [message]="error()" [error]="true" /> }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <label for="email">E-mail</label><input id="email" type="email" formControlName="email" placeholder="voce@empresa.com" autocomplete="email" [class.invalid]="invalid('email')"><small class="field-error">{{ fieldError('email') }}</small>
          <label for="password">Senha</label><input id="password" type="password" formControlName="password" placeholder="Digite sua senha" autocomplete="current-password" [class.invalid]="invalid('password')"><small class="field-error">{{ fieldError('password') }}</small>
          <button class="button button-primary button-full" type="submit" [disabled]="loading()">{{ loading() ? 'Entrando...' : 'Entrar' }} <span>→</span></button>
        </form><p class="auth-switch">Ainda não tem acesso? <a routerLink="/register">Criar conta</a></p>
      </div></main>
    </div>
  `
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly errors = inject(ErrorService);
  private readonly router = inject(Router);
  readonly loading = signal(false);
  readonly error = signal('');
   readonly form = this.fb.nonNullable.group({ email: ['', [Validators.required, Validators.email]], password: ['', [Validators.required, Validators.minLength(6)]] });

  submit(): void {
    this.error.set('');
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({ next: () => { this.loading.set(false); void this.router.navigate(['/sales']); }, error: error => { this.loading.set(false); this.error.set(this.errors.message(error, 'E-mail ou senha inválidos.')); } });
  }
  invalid(name: 'email' | 'password'): boolean { const control = this.form.controls[name]; return control.invalid && control.touched; }
  fieldError(name: 'email' | 'password'): string { const control = this.form.controls[name]; if (!control.touched || !control.errors) return ''; return name === 'email' ? 'Informe um e-mail válido.' : 'Informe sua senha.'; }
}
