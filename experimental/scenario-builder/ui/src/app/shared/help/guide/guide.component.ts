import { ChangeDetectionStrategy, Component, ElementRef, afterRenderEffect, computed, inject, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CONCEPTS, GLOSSARY, JOURNEY, MARKS, PAGES, PageId, Term } from '../help-content';
import { GuideSection, HelpService } from '../help.service';
import { TermTextComponent } from '../term-text/term-text.component';

type StageState = 'done' | 'open' | 'locked';

function editable(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName));
}

/**
 * The Guide: help that slides in beside the work without blocking it. What this page is for, how the builder works,
 * and every term it uses, with one search across all three. `?` opens it from anywhere but a text field.
 */
@Component({
  selector: 'app-guide',
  standalone: true,
  imports: [RouterLink, TermTextComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './guide.component.html',
  styleUrls: ['./guide.component.scss', './guide-reference.scss'],
  host: { '(document:keydown)': 'key($event)' }
})
export class GuideComponent {
  protected readonly help = inject(HelpService);
  private readonly search = viewChild<ElementRef<HTMLInputElement>>('search');
  private readonly body = viewChild<ElementRef<HTMLElement>>('body');

  protected readonly sections: { id: GuideSection; label: string }[] = [
    { id: 'page', label: 'On This Page' },
    { id: 'how', label: 'How It Works' },
    { id: 'glossary', label: 'Glossary' }
  ];
  protected readonly concepts = CONCEPTS;
  protected readonly marks = MARKS;
  protected readonly glossary = [...GLOSSARY].sort((a, b) => a.term.localeCompare(b.term));
  protected readonly letters = this.glossary.reduce<{ letter: string; terms: Term[] }[]>((groups, term) => {
    const letter = term.term[0].toUpperCase();
    const last = groups.at(-1);
    if (last?.letter === letter) {
      last.terms.push(term);
    } else {
      groups.push({ letter, terms: [term] });
    }
    return groups;
  }, []);

  protected readonly pageHelp = computed(() => PAGES[this.help.page()]);

  /** The scenario's journey: its pages in order, Topology only for a scenario type that has one. */
  protected readonly journey = computed(() => {
    const scenario = this.help.scenario();
    const here: PageId = ['grayspace', 'updraft'].includes(this.help.page()) ? 'topology' : this.help.page();
    return JOURNEY.filter(j => j.page !== 'topology' || !scenario || scenario.scenarioType.topology).map(j => ({
      ...j,
      here: !!scenario && j.page === here,
      link: scenario ? ['/scenarios', scenario.id, j.page] : null,
      rangeOnly: !scenario && j.page === 'topology'
    }));
  });

  protected readonly stages = computed(() => {
    const scenario = this.help.scenario();
    if (!scenario) {
      return [];
    }
    return scenario.scenarioType.stages.map(stage => ({
      number: stage.number,
      title: stage.title,
      state: this.state(stage.number, scenario.openStage),
      here: this.help.page() === 'questions' && stage.number === scenario.stage
    }));
  });

  protected readonly eyebrow = computed(() => {
    const page = this.help.page();
    const journey = this.journey();
    const at = journey.findIndex(j => j.page === page);
    if (at >= 0 && this.help.scenario()) {
      const step = `Step ${at + 1} of ${journey.length}`;
      return page === 'questions' ? `${step} · Stage ${this.help.scenario()!.stage}` : step;
    }
    return page === 'scenarios' ? 'Start Here' : page === 'admin' ? 'Admin' : 'Topology';
  });

  /** The stage the Questions page shows, and where it stands. */
  protected readonly stage = computed(() => {
    const scenario = this.help.scenario();
    if (this.help.page() !== 'questions' || !scenario) {
      return null;
    }
    const stage = scenario.scenarioType.stages[scenario.stage];
    if (!stage) {
      return null;
    }
    const state = this.state(stage.number, scenario.openStage);
    const last = stage.number === scenario.scenarioType.stages.at(-1)?.number;
    const line =
      state === 'done' ? 'Its questions are answered. You can still change any of them.'
      : state === 'locked' ? `It opens once stage ${scenario.openStage} is answered.`
      : last ? 'The last stage. Once its questions are answered, the scenario is complete.'
      : 'Answer every question here, and Open the next stage appears above them.';
    const label = state === 'done' ? 'Closed' : state === 'open' ? 'Open now' : 'Locked';
    return { number: stage.number, title: stage.title, purpose: stage.purpose, state, label, line, count: scenario.scenarioType.stages.length };
  });

  protected readonly next = computed(() => {
    const scenario = this.help.scenario();
    let page = this.pageHelp().next;
    if (!page || !scenario) {
      return null;
    }
    if (page === 'topology' && !scenario.scenarioType.topology) {
      page = 'finish';
    }
    return { name: PAGES[page].name, link: ['/scenarios', scenario.id, page] };
  });

  protected readonly results = computed(() => {
    const words = this.help.query().trim().toLowerCase();
    if (!words) {
      return null;
    }
    const has = (...texts: string[]) => texts.some(t => t.toLowerCase().includes(words));
    const terms = this.glossary.filter(t => has(t.term, t.definition, t.where));
    const concepts = this.concepts.filter(c => has(c.title, ...c.body));
    const pages = Object.values(PAGES).filter(p => has(p.name, p.lead, ...p.steps, ...(p.notes ?? [])));
    return { terms, concepts, pages, count: terms.length + concepts.length + pages.length };
  });

  private wasOpen = false;

  constructor() {
    // Opening focuses the search; opening at a concept or term scrolls to it.
    afterRenderEffect(() => {
      const open = this.help.open();
      const focus = this.help.focus();
      this.help.section();
      if (open && !this.wasOpen) {
        this.search()?.nativeElement.focus({ preventScroll: true });
      }
      this.wasOpen = open;
      if (open && focus) {
        this.body()?.nativeElement.querySelector(`[data-id="${focus}"]`)?.scrollIntoView({ block: 'start' });
      } else if (open) {
        this.body()?.nativeElement.scrollTo({ top: 0 });
      }
    });
  }

  protected pick(section: GuideSection): void {
    this.help.focus.set(null);
    this.help.section.set(section);
  }

  protected jump(letter: string): void {
    this.body()?.nativeElement.querySelector(`[data-id="letter-${letter}"]`)?.scrollIntoView({ block: 'start', behavior: 'smooth' });
  }

  protected searched(event: Event): void {
    this.help.query.set((event.target as HTMLInputElement).value);
  }

  protected key(event: KeyboardEvent): void {
    if (this.help.tourStep() !== null) {
      return;
    }
    if (event.key === '?' && !event.ctrlKey && !event.metaKey && !event.altKey && !editable(event.target)) {
      event.preventDefault();
      this.help.toggle();
    } else if (event.key === 'Escape' && this.help.open()) {
      this.help.close();
    }
  }

  private state(stage: number, open: number): StageState {
    return stage < open ? 'done' : stage === open ? 'open' : 'locked';
  }
}
