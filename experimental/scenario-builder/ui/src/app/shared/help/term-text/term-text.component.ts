import { ChangeDetectionStrategy, Component, ViewEncapsulation, computed, inject, input, signal } from '@angular/core';
import { CdkConnectedOverlay, CdkOverlayOrigin, ConnectedPosition } from '@angular/cdk/overlay';
import { Term } from '../help-content';
import { HelpService } from '../help.service';
import { termRuns } from '../glossary-text';

/**
 * Text with its glossary terms marked: each one a dotted underline that opens its definition, and from there the
 * Guide's Glossary. Inline, so it sits inside headings and question text.
 */
@Component({
  selector: 'app-term-text',
  standalone: true,
  imports: [CdkOverlayOrigin, CdkConnectedOverlay],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // The popover renders in the overlay container, outside this component.
  encapsulation: ViewEncapsulation.None,
  template: `
    @for (run of runs(); track $index; let i = $index) {
      @if (run.term; as term) {
        <button type="button" class="sb-term" cdkOverlayOrigin #origin="cdkOverlayOrigin"
          [attr.aria-expanded]="openAt() === i" aria-haspopup="dialog" (click)="toggle(i)">{{ run.text }}</button>
        <ng-template cdkConnectedOverlay [cdkConnectedOverlayOrigin]="origin" [cdkConnectedOverlayOpen]="openAt() === i"
          [cdkConnectedOverlayPositions]="positions" (overlayOutsideClick)="close()" (detach)="close()" (overlayKeydown)="key($event, origin)"
          (attach)="focusIn()">
          <div class="sb-term-pop" role="dialog" [attr.aria-label]="term.term">
            <span class="sb-term-pop-eyebrow">Glossary</span>
            <span class="sb-term-pop-name">{{ term.term }}</span>
            <p class="sb-term-pop-text">{{ term.definition }}</p>
            <button type="button" class="sb-term-pop-more" (click)="more(term)">More in the Glossary <span aria-hidden="true">→</span></button>
          </div>
        </ng-template>
      } @else {
        <ng-container>{{ run.text }}</ng-container>
      }
    }`,
  styles: [`
    app-term-text { display: inline; }
    .sb-term {
      display: inline; padding: 0; margin: 0; border: 0; background: none; font: inherit; color: inherit; cursor: help;
      text-decoration: underline dotted; text-decoration-color: var(--sb-faint); text-decoration-thickness: 1px; text-underline-offset: 3px;
      border-radius: 2px;
    }
    .sb-term:hover, .sb-term[aria-expanded='true'] { text-decoration-color: var(--sb-person); color: var(--sb-ink); background: var(--sb-person-soft); }
    .sb-term-pop {
      width: 300px; padding: 14px 16px 12px; border-radius: 10px;
      background: var(--sb-ink); color: #e9ecf3;
      box-shadow: 0 18px 40px -12px rgb(20 24 36 / 45%), 0 2px 6px rgb(20 24 36 / 18%);
      animation: sb-term-in 140ms ease-out;
    }
    .sb-term-pop-eyebrow { display: block; font: 500 10px/1 var(--sb-mono); letter-spacing: 0.14em; text-transform: uppercase; color: #9aa6c4; }
    .sb-term-pop-name { display: block; margin-top: 8px; font: 500 18px/1.2 var(--sb-serif); color: #ffffff; }
    .sb-term-pop-text { margin: 6px 0 10px; font-size: var(--sb-text); line-height: 1.5; color: #c9cfdc; }
    .sb-term-pop-more {
      padding: 0; border: 0; background: none; cursor: pointer;
      font: 500 var(--sb-small) var(--sb-font); color: #a9bdff;
      &:hover { color: #ffffff; }
    }
    @keyframes sb-term-in { from { opacity: 0; transform: translateY(-4px); } }
  `]
})
export class TermTextComponent {
  private readonly help = inject(HelpService);

  readonly text = input.required<string>();
  protected readonly runs = computed(() => termRuns(this.text()));
  protected readonly openAt = signal<number | null>(null);

  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 8 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -8 }
  ];

  protected toggle(i: number): void {
    this.openAt.update(at => (at === i ? null : i));
  }

  protected close(): void {
    this.openAt.set(null);
  }

  protected key(event: KeyboardEvent, origin: CdkOverlayOrigin): void {
    if (event.key === 'Escape') {
      event.stopPropagation();
      this.close();
      (origin.elementRef.nativeElement as HTMLElement).focus();
    }
  }

  protected focusIn(): void {
    setTimeout(() => document.querySelector<HTMLElement>('.sb-term-pop-more')?.focus());
  }

  protected more(term: Term): void {
    this.close();
    this.help.show('glossary', term.id);
  }
}
