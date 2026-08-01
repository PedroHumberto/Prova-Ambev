import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="empty-state"><span class="empty-mark">{{ icon() }}</span><h3>{{ title() }}</h3><p>{{ text() }}</p></div>`
})
export class EmptyStateComponent {
  readonly icon = input('—');
  readonly title = input.required<string>();
  readonly text = input.required<string>();
}
