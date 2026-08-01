import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UserRole, UserStatus } from '../../core/models/api.models';
import { UsersService } from '../../core/services/users.service';
import { ErrorService } from '../../core/services/error.service';
import { ToastComponent } from '../../shared/ui/toast.component';

@Component({
  selector: 'app-register', standalone: true, imports: [ReactiveFormsModule, RouterLink, ToastComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.component.html'
})
export class RegisterComponent {
  readonly UserRole = UserRole;
  private readonly fb = inject(FormBuilder); private readonly users = inject(UsersService); private readonly errorService = inject(ErrorService); private readonly router = inject(Router);
  readonly loading = signal(false); readonly error = signal(''); readonly success = signal('');
   readonly form = this.fb.nonNullable.group({ username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]], email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]], phone: ['', [Validators.required, Validators.pattern(/^\+[1-9]\d{1,14}$/)]], password: ['', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[!?*.@#$%^&+=]).+$/)]], role: [UserRole.Customer] });
  submit(): void { this.error.set(''); this.success.set(''); if (this.form.invalid) { this.form.markAllAsTouched(); return; } this.loading.set(true); const value = this.form.getRawValue(); this.users.create({ ...value, status: UserStatus.Active }).subscribe({ next: () => { this.loading.set(false); this.success.set('Usuário criado. Redirecionando para o login...'); setTimeout(() => void this.router.navigate(['/login']), 900); }, error: error => { this.loading.set(false); this.error.set(this.errorService.message(error, 'Não foi possível criar o usuário.')); } }); }
  errorFor(name: keyof typeof this.form.controls): string { const control = this.form.controls[name]; if (!control.touched || !control.errors) return ''; if (control.hasError('required')) return 'Campo obrigatório.'; if (control.hasError('email')) return 'Informe um e-mail válido.'; if (control.hasError('pattern')) return name === 'phone' ? 'Use formato internacional.' : 'Use maiúscula, minúscula, número e símbolo.'; return 'Valor inválido.'; }
}
