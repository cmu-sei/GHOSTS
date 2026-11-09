import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, signal, viewChild } from '@angular/core';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { TOUR } from '../help-content';
import { HelpService } from '../help.service';

interface Box {
  top: number;
  left: number;
  width: number;
  height: number;
}

const PAD = 6;
const GAP = 14;
const CARD_WIDTH = 340;
const EDGE = 16;

/** Where the caption card goes: below the target, else above, else beside it; always inside the window. */
export function place(target: Box, card: { width: number; height: number }, view: { width: number; height: number }): { top: number; left: number } {
  const clampX = (x: number) => Math.max(EDGE, Math.min(x, view.width - card.width - EDGE));
  const clampY = (y: number) => Math.max(EDGE, Math.min(y, view.height - card.height - EDGE));
  const bottom = target.top + target.height;
  if (bottom + GAP + card.height + EDGE <= view.height) {
    return { top: bottom + GAP, left: clampX(target.left) };
  }
  if (target.top - GAP - card.height >= EDGE) {
    return { top: target.top - GAP - card.height, left: clampX(target.left) };
  }
  const right = target.left + target.width;
  const left = right + GAP + card.width + EDGE <= view.width ? right + GAP : target.left - GAP - card.width;
  return { top: clampY(target.top), left: clampX(left) };
}

/**
 * The tour of a scenario's workspace: each stop dims the window but its target, and explains it. It is offered once,
 * in the corner, and never started for the designer. A stop whose target isn't on screen is passed over.
 */
@Component({
  selector: 'app-tour',
  standalone: true,
  imports: [CdkTrapFocus],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tour.component.html',
  styleUrls: ['./tour.component.scss'],
  host: { '(document:keydown)': 'key($event)', '(window:resize)': 'measure()' }
})
export class TourComponent {
  protected readonly help = inject(HelpService);
  private readonly card = viewChild<ElementRef<HTMLElement>>('card');
  private readonly next = viewChild<ElementRef<HTMLButtonElement>>('nextButton');

  protected readonly stops = TOUR;
  protected readonly stop = computed(() => {
    const step = this.help.tourStep();
    return step === null ? null : TOUR[step];
  });
  protected readonly spot = signal<Box | null>(null);
  protected readonly at = signal<{ top: number; left: number } | null>(null);

  /** The offer shows on a scenario's pages until it's answered, and not while the Guide or tour is open. */
  protected readonly offer = computed(
    () => !this.help.tourOffered() && !!this.help.scenario() && this.help.tourStep() === null && !this.help.open() && this.help.page() !== 'admin'
  );

  /** Which way the designer last stepped, so a missing target is passed over in that direction. */
  private direction = 1;
  private tries = 0;

  constructor() {
    afterRenderEffect(() => {
      if (this.help.tourStep() === null) {
        return;
      }
      this.measure();
      this.next()?.nativeElement.focus({ preventScroll: true });
    });
  }

  protected forward(): void {
    const step = this.help.tourStep();
    if (step === null) {
      return;
    }
    if (step >= TOUR.length - 1) {
      this.help.endTour();
      return;
    }
    this.direction = 1;
    this.go(step + 1);
  }

  protected back(): void {
    const step = this.help.tourStep();
    if (step === null || step === 0) {
      return;
    }
    this.direction = -1;
    this.go(step - 1);
  }

  protected take(): void {
    this.help.startTour();
  }

  protected decline(): void {
    this.help.tourAnswered();
  }

  protected key(event: KeyboardEvent): void {
    if (this.help.tourStep() === null) {
      return;
    }
    if (event.key === 'Escape') {
      event.preventDefault();
      this.help.endTour();
    } else if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.forward();
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.back();
    }
  }

  /** Finds the stop's target and fits the spotlight and card to it. Waits a moment for a page still rendering. */
  protected measure(): void {
    const stop = this.stop();
    if (!stop) {
      return;
    }
    const target = document.querySelector<HTMLElement>(`[data-tour="${stop.target}"]`);
    if (!target) {
      if (this.tries++ < 15) {
        setTimeout(() => this.measure(), 60);
      } else {
        this.skip();
      }
      return;
    }
    this.tries = 0;
    target.scrollIntoView({ block: 'nearest' });
    const r = target.getBoundingClientRect();
    const view = { width: window.innerWidth, height: window.innerHeight };
    const spot = {
      top: Math.max(r.top - PAD, 4),
      left: Math.max(r.left - PAD, 4),
      width: Math.min(r.width + 2 * PAD, view.width - 8),
      height: Math.min(r.height + 2 * PAD, view.height - 8)
    };
    this.spot.set(spot);
    const card = this.card()?.nativeElement;
    this.at.set(place(spot, { width: CARD_WIDTH, height: card?.offsetHeight || 190 }, view));
  }

  private go(step: number): void {
    this.tries = 0;
    this.help.tourStep.set(step);
  }

  private skip(): void {
    const step = this.help.tourStep()!;
    const to = step + this.direction;
    if (to < 0 || to >= TOUR.length) {
      this.help.endTour();
      return;
    }
    this.go(to);
    this.measure();
  }
}
