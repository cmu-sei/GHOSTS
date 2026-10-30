import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, Subscription, debounceTime } from 'rxjs';
import { Audience, LibraryDetail, ScenarioSummary, TemplateFormat, TemplateSpec } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AUDIENCES, FORMATS } from '../../../shared/labels/document-labels';

/**
 * One template's source beside a live preview: the template rendered on a real scenario, filtered exactly as
 * its audience would get it. A template error takes the preview's place, with its line.
 */
@Component({
  selector: 'app-template-editor',
  standalone: true,
  imports: [RouterLink, TopBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './template-editor.component.html',
  styleUrls: ['./template-editor.component.scss']
})
export class TemplateEditorComponent implements OnInit {
  private readonly admin = inject(AdminApiService);
  private readonly api = inject(BuilderApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly sanitizer = inject(DomSanitizer);

  protected readonly audiences = Object.entries(AUDIENCES) as [Audience, string][];
  protected readonly formats = Object.entries(FORMATS) as [TemplateFormat, string][];
  protected readonly libraryId = Number(this.route.snapshot.paramMap.get('id'));
  private readonly templateId = this.route.snapshot.paramMap.get('templateId') ?? '';

  protected readonly library = signal<LibraryDetail | null>(null);
  protected readonly saved = signal<TemplateSpec | null>(null);
  protected readonly name = signal('');
  protected readonly audience = signal<Audience>('participants');
  protected readonly format = signal<TemplateFormat>('markdown');
  protected readonly body = signal('');
  protected readonly scenarios = signal<ScenarioSummary[]>([]);
  protected readonly scenarioId = signal(0);
  protected readonly page = signal<SafeHtml | null>(null);
  protected readonly templateError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly state = signal('');

  protected readonly dirty = computed(() => {
    const saved = this.saved();
    return !!saved && (saved.name !== this.name() || saved.audience !== this.audience() || saved.format !== this.format() || saved.body !== this.body());
  });

  private readonly changes = new Subject<void>();
  private request?: Subscription;

  constructor() {
    this.changes.pipe(debounceTime(400), takeUntilDestroyed()).subscribe(() => this.refresh());
  }

  ngOnInit(): void {
    this.admin.library(this.libraryId).subscribe({
      next: library => {
        const template = library.templates.find(t => t.id === this.templateId);
        if (!template) {
          this.error.set(`${library.name} has no template ${this.templateId}.`);
          return;
        }
        this.library.set(library);
        this.show(template);
        this.api.getScenarios().subscribe(scenarios => {
          const served = scenarios.filter(s => library.kinds.includes(s.kindId));
          this.scenarios.set(served);
          this.scenarioId.set(served[0]?.id ?? 0);
          this.refresh();
        });
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected typedName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
    this.state.set('');
  }

  protected typedBody(event: Event): void {
    this.body.set((event.target as HTMLTextAreaElement).value);
    this.state.set('');
    this.changes.next();
  }

  protected pickedAudience(event: Event): void {
    this.audience.set((event.target as HTMLSelectElement).value as Audience);
    this.state.set('');
    this.refresh();
  }

  protected pickedFormat(event: Event): void {
    this.format.set((event.target as HTMLSelectElement).value as TemplateFormat);
    this.state.set('');
    this.refresh();
  }

  protected pickedScenario(event: Event): void {
    this.scenarioId.set(Number((event.target as HTMLSelectElement).value));
    this.refresh();
  }

  /** Saves the whole draft, with this template as it now stands. */
  protected save(): void {
    const library = this.library()!;
    const template: TemplateSpec = { id: this.templateId, name: this.name().trim(), audience: this.audience(), format: this.format(), body: this.body() };
    const templates = library.templates.map(t => t.id === this.templateId ? template : t);
    this.admin.save(this.libraryId, { name: library.name, description: library.description, kinds: library.kinds, templates }).subscribe({
      next: saved => {
        this.library.set(saved);
        this.saved.set(template);
        this.error.set(null);
        this.state.set('Saved to the draft');
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  private show(template: TemplateSpec): void {
    this.saved.set(template);
    this.name.set(template.name);
    this.audience.set(template.audience);
    this.format.set(template.format);
    this.body.set(template.body);
  }

  private refresh(): void {
    const scenarioId = this.scenarioId();
    if (!scenarioId) {
      return;
    }
    this.request?.unsubscribe();
    this.request = this.admin.preview({
      scenarioId, libraryId: this.libraryId, name: this.name().trim() || 'Preview', audience: this.audience(), format: this.format(), body: this.body()
    }).subscribe({
      // The frame is sandboxed with nothing allowed, so its markup can't run script or reach the builder.
      next: html => {
        this.page.set(this.sanitizer.bypassSecurityTrustHtml(html));
        this.templateError.set(null);
      },
      error: e => {
        this.page.set(null);
        this.templateError.set(errorMessage(e));
      }
    });
  }
}
