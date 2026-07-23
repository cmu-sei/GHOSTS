import { Component, ChangeDetectionStrategy } from '@angular/core';
import { ShellComponent } from './shared/components/shell/shell.component';

@Component({
  selector: 'app-root',
  imports: [ShellComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-shell />`
})
export class App {}
