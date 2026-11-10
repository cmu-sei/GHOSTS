import { Injectable, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router } from '@angular/router';
import { filter, map } from 'rxjs';
import { ScenarioType } from '../../core/models/builder.model';
import { PageId } from './help-content';

export type GuideSection = 'page' | 'how' | 'glossary';

/** The scenario the designer is in, as the scenario shell tells it. */
export interface ScenarioHelp {
  id: number;
  scenarioType: ScenarioType;
  /** The stage the Questions page shows. */
  stage: number;
  /** The stage that is open for answers. */
  openStage: number;
}

const WELCOME_KEY = 'sb.help.welcomeDismissed';
const TOUR_KEY = 'sb.help.tourOffered';

const SCENARIO_PAGES: PageId[] = ['context', 'questions', 'summary', 'log', 'topology', 'finish'];

/** The page a URL shows, for the Guide's "On This Page". */
export function pageOf(url: string): PageId {
  const path = url.split(/[?#]/)[0];
  if (path.startsWith('/admin')) {
    return 'admin';
  }
  const rest = /^\/scenarios\/[^/]+\/?(.*)$/.exec(path)?.[1];
  if (rest === undefined) {
    return 'scenarios';
  }
  if (rest.startsWith('topology/grayspace')) {
    return 'grayspace';
  }
  if (rest.startsWith('topology/updraft')) {
    return 'updraft';
  }
  const first = rest.split('/')[0];
  return first === 'json' ? 'finish' : SCENARIO_PAGES.find(p => p === first) ?? 'questions';
}

function stored(key: string): boolean {
  try {
    return localStorage.getItem(key) === '1';
  } catch {
    return false;
  }
}

function store(key: string, on: boolean): void {
  try {
    if (on) {
      localStorage.setItem(key, '1');
    } else {
      localStorage.removeItem(key);
    }
  } catch {
    // Private windows can refuse storage; help then just shows again next time.
  }
}

/** The help layer's state: the Guide, the tour, the welcome, and where the designer is. */
@Injectable({ providedIn: 'root' })
export class HelpService {
  private readonly router = inject(Router);

  readonly open = signal(false);
  readonly section = signal<GuideSection>('page');
  readonly query = signal('');
  /** A concept or term the Guide scrolls to and marks when it opens. */
  readonly focus = signal<string | null>(null);

  readonly page = toSignal(
    this.router.events.pipe(filter(e => e instanceof NavigationEnd), map(e => pageOf(e.urlAfterRedirects))),
    { initialValue: pageOf(this.router.url) }
  );
  readonly scenario = signal<ScenarioHelp | null>(null);

  /** The tour's stop, or null when it isn't running. */
  readonly tourStep = signal<number | null>(null);
  readonly welcomeDismissed = signal(stored(WELCOME_KEY));
  readonly tourOffered = signal(stored(TOUR_KEY));

  private returnFocus: HTMLElement | null = null;

  /** Opens the Guide on a section, at a concept or term if given. */
  show(section: GuideSection = 'page', focus: string | null = null): void {
    if (!this.open()) {
      this.returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    }
    this.section.set(section);
    this.query.set('');
    this.focus.set(focus);
    this.open.set(true);
  }

  close(): void {
    if (!this.open()) {
      return;
    }
    this.open.set(false);
    this.returnFocus?.focus();
    this.returnFocus = null;
  }

  toggle(): void {
    if (this.open()) {
      this.close();
    } else {
      this.show();
    }
  }

  dismissWelcome(): void {
    this.welcomeDismissed.set(true);
    store(WELCOME_KEY, true);
  }

  showWelcome(): void {
    this.welcomeDismissed.set(false);
    store(WELCOME_KEY, false);
    this.close();
    this.router.navigateByUrl('/');
  }

  /** The offer was answered, either way; it isn't made again. */
  tourAnswered(): void {
    this.tourOffered.set(true);
    store(TOUR_KEY, true);
  }

  /** Runs the tour on the Questions page of the scenario the designer is in. */
  async startTour(): Promise<void> {
    const scenario = this.scenario();
    if (!scenario) {
      return;
    }
    this.tourAnswered();
    this.open.set(false);
    this.returnFocus = null;
    if (this.page() !== 'questions') {
      await this.router.navigate(['/scenarios', scenario.id, 'questions']);
    }
    this.tourStep.set(0);
  }

  endTour(): void {
    this.tourStep.set(null);
  }
}
