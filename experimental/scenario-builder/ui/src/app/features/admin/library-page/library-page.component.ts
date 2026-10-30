import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { KindSummary, LibraryDetail, TemplateSpec } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AUDIENCES, FORMATS } from '../../../shared/labels/document-labels';

/** One library: its details and kinds, its draft templates, and the versions publishing has made. */
@Component({
  selector: 'app-library-page',
  standalone: true,
  imports: [DatePipe, RouterLink, TopBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './library-page.component.html',
  styleUrls: ['./library-page.component.scss']
})
export class LibraryPageComponent implements OnInit {
  protected readonly admin = inject(AdminApiService);
  private readonly api = inject(BuilderApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly audiences = AUDIENCES;
  protected readonly formats = FORMATS;
  protected readonly id = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly library = signal<LibraryDetail | null>(null);
  protected readonly kinds = signal<KindSummary[]>([]);
  protected readonly name = signal('');
  protected readonly description = signal('');
  protected readonly kindIds = signal<string[]>([]);
  protected readonly newTemplate = signal('');
  protected readonly note = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly dirty = computed(() => {
    const library = this.library();
    return !!library && (this.name() !== library.name || this.description() !== (library.description ?? '')
      || [...this.kindIds()].sort().join() !== [...library.kinds].sort().join());
  });

  ngOnInit(): void {
    this.load();
    this.api.getKinds().subscribe(kinds => this.kinds.set(kinds));
  }

  protected typedName(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected typedDescription(event: Event): void {
    this.description.set((event.target as HTMLTextAreaElement).value);
  }

  protected typedTemplate(event: Event): void {
    this.newTemplate.set((event.target as HTMLInputElement).value);
  }

  protected toggleKind(id: string, on: boolean): void {
    this.kindIds.update(ids => on ? [...ids, id] : ids.filter(k => k !== id));
  }

  protected saveDetails(): void {
    this.save(this.library()!.templates, () => this.note.set('Saved the draft.'));
  }

  /** A new template starts as a short Markdown read-in, and opens in the editor. */
  protected addTemplate(): void {
    const name = this.newTemplate().trim();
    const templates = this.library()!.templates;
    const stem = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'template';
    let id = stem;
    for (let n = 2; templates.some(t => t.id === id); n++) {
      id = `${stem}-${n}`;
    }
    const template: TemplateSpec = { id, name, audience: 'participants', format: 'markdown', body: '# {{ scenario.name }}\n' };
    this.save([...templates, template], () => this.router.navigate(['/admin/libraries', this.id, 'templates', id]));
  }

  /** Out of the draft only: scenarios keep getting it until the next publish. */
  protected removeTemplate(template: TemplateSpec): void {
    this.save(this.library()!.templates.filter(t => t.id !== template.id),
      () => this.note.set(`Removed ${template.name} from the draft. Publish to take it out of scenarios' documents.`));
  }

  protected publish(): void {
    this.note.set(null);
    this.admin.publish(this.id).subscribe({
      next: version => {
        this.note.set(`Published version ${version.number}.`);
        this.load();
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  private save(templates: TemplateSpec[], then: () => void): void {
    this.note.set(null);
    this.admin.save(this.id, { name: this.name().trim(), description: this.description(), kinds: this.kindIds(), templates }).subscribe({
      next: library => {
        this.show(library);
        then();
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  private load(): void {
    this.admin.library(this.id).subscribe({ next: library => this.show(library), error: e => this.error.set(errorMessage(e)) });
  }

  private show(library: LibraryDetail): void {
    this.library.set(library);
    this.name.set(library.name);
    this.description.set(library.description ?? '');
    this.kindIds.set(library.kinds);
    this.error.set(null);
  }
}
