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
  templateUrl: './login.component.html'
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
