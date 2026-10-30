import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { KindSummary, ScenarioSummary } from '../../../core/models/builder.model';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';

type SortKey = 'name' | 'kind' | 'stage' | 'owner' | 'updated';

/**
 * Every scenario as a grid to search, sort and archive, and a new one from a kind. A new scenario opens on its
 * Context page, so the agent has what the designer already knows before the first question.
 */
@Component({
  selector: 'app-scenarios-list',
  standalone: true,
  imports: [DatePipe, RouterLink, TopBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenarios-list.component.html',
  styleUrls: ['./scenarios-list.component.scss']
})
export class ScenariosListComponent implements OnInit {
  private readonly api = inject(BuilderApiService);
  private readonly router = inject(Router);

  protected readonly columns: { key: SortKey; label: string }[] = [
    { key: 'name', label: 'Name' },
    { key: 'kind', label: 'Kind' },
    { key: 'stage', label: 'Stage' },
    { key: 'owner', label: 'Owner' },
    { key: 'updated', label: 'Updated' }
  ];

  protected readonly scenarios = signal<ScenarioSummary[]>([]);
  protected readonly kinds = signal<KindSummary[]>([]);
  protected readonly name = signal('');
  protected readonly kindId = signal('');
  protected readonly query = signal('');
  protected readonly sort = signal<{ key: SortKey; ascending: boolean }>({ key: 'updated', ascending: false });
  protected readonly showArchived = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly archivedCount = computed(() => this.scenarios().filter(s => s.archived).length);

  protected readonly rows = computed(() => {
    const words = this.query().trim().toLowerCase();
    const { key, ascending } = this.sort();
    return this.scenarios()
      .filter(s => this.showArchived() || !s.archived)
      .filter(s => !words || [s.name, s.slug, s.owner, this.kindName(s.kindId)].some(v => v.toLowerCase().includes(words)))
      .sort((a, b) => (ascending ? 1 : -1) * compare(this.value(a, key), this.value(b, key)));
  });

  ngOnInit(): void {
    this.api.getScenarios().subscribe({
      next: scenarios => this.scenarios.set(scenarios),
      error: e => this.error.set(errorMessage(e))
    });
    this.api.getKinds().subscribe(kinds => {
      this.kinds.set(kinds);
      this.kindId.set(kinds[0]?.id ?? '');
    });
  }

  protected create(): void {
    this.api.createScenario(this.name().trim(), this.kindId()).subscribe({
      next: scenario => this.router.navigate(['/scenarios', scenario.id, 'context']),
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected archive(scenario: ScenarioSummary, archived: boolean): void {
    this.error.set(null);
    this.api.archive(scenario.id, archived).subscribe({
      next: changed => this.scenarios.update(all => all.map(s => s.id === changed.id ? changed : s)),
      error: e => this.error.set(errorMessage(e))
    });
  }

  /** A column sorts ascending first; the same column again reverses it. */
  protected sortBy(key: SortKey): void {
    this.sort.update(sort => ({ key, ascending: sort.key === key ? !sort.ascending : true }));
  }

  protected ariaSort(key: SortKey): string | null {
    const sort = this.sort();
    return sort.key === key ? (sort.ascending ? 'ascending' : 'descending') : null;
  }

  protected kindName(id: string): string {
    return this.kinds().find(k => k.id === id)?.name ?? id;
  }

  protected typed(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected searched(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected picked(event: Event): void {
    this.kindId.set((event.target as HTMLSelectElement).value);
  }

  private value(s: ScenarioSummary, key: SortKey): string | number {
    switch (key) {
      case 'kind': return this.kindName(s.kindId);
      case 'stage': return s.stage;
      case 'owner': return s.owner;
      case 'updated': return s.updated;
      default: return s.name;
    }
  }
}

function compare(a: string | number, b: string | number): number {
  return typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b), undefined, { sensitivity: 'base' });
}
