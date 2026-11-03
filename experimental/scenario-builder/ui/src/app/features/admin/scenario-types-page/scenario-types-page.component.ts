import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ScenarioTypeAdminSummary } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { errorMessage } from '../../../core/services/builder-api.service';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AdminNavComponent } from '../admin-nav/admin-nav.component';

/**
 * Every scenario type (shown as a scenario type), and a new one: named, said in a few sentences what it is, and drafted by a model on its page.
 * Or one carried in from another installation as a .scenario type.json file.
 */
@Component({
  selector: 'app-scenario-types-page',
  standalone: true,
  imports: [RouterLink, TopBarComponent, AdminNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './scenario-types-page.component.html',
  styleUrls: ['./scenario-types-page.component.scss']
})
export class ScenarioTypesPageComponent implements OnInit {
  private readonly admin = inject(AdminApiService);
  private readonly router = inject(Router);

  protected readonly scenarioTypes = signal<ScenarioTypeAdminSummary[] | null>(null);
  protected readonly models = signal<string[]>([]);
  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly basedOn = signal('');
  protected readonly topology = signal(false);
  /** Empty: no model, the admin writes it. */
  protected readonly model = signal('');
  protected readonly note = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
    this.admin.models().subscribe(models => {
      this.models.set(models);
      this.model.set(models[0] ?? '');
    });
  }

  protected value(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  protected checked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  /** Its page drafts it with the chosen model; with none, it opens as it is. */
  protected create(): void {
    this.admin.createScenarioType({
      name: this.name().trim(), description: this.description().trim(), topology: this.topology(), basedOn: this.basedOn() || undefined
    }).subscribe({
      next: scenarioType => this.router.navigate(['/admin/scenario-types', scenarioType.id], this.model() ? { queryParams: { draft: this.model() } } : {}),
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected async chosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    const text = await file.text();
    input.value = '';
    this.note.set(null);
    this.admin.importScenarioType(text).subscribe({
      next: scenarioType => {
        this.error.set(null);
        this.note.set(`Imported ${scenarioType.scenarioType.name} as version ${scenarioType.latestVersion}.`);
        this.load();
      },
      error: e => this.error.set(`${file.name}: ${errorMessage(e)}`)
    });
  }

  private load(): void {
    this.admin.scenarioTypes().subscribe({ next: k => this.scenarioTypes.set(k), error: e => this.error.set(errorMessage(e)) });
  }
}
