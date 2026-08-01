import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-toast',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="toast" [class.toast-error]="error()" role="alert">{{ message() }}</div>`
})
export class ToastComponent {
  readonly message = input.required<string>();
  readonly error = input(false);
}
