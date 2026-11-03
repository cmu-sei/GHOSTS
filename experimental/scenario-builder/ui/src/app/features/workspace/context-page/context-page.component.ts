import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { SourceKind, SourceView } from '../../../core/models/builder.model';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { WorkspaceStore } from '../workspace.store';

/**
 * What the designer already has, given before the first question: pasted text, a link, documents. The agent
 * searches these before it recommends an answer. A new scenario opens here; any scenario can add more later.
 */
@Component({
  selector: 'app-context-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './context-page.component.html',
  styleUrls: ['./context-page.component.scss']
})
export class ContextPageComponent implements OnInit {
  private readonly api = inject(BuilderApiService);
  private readonly store = inject(WorkspaceStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly kinds: Record<SourceKind, string> = { text: 'Text', url: 'Link', file: 'Document' };

  protected readonly sources = signal<SourceView[]>([]);
  protected readonly textName = signal('');
  protected readonly text = signal('');
  protected readonly url = signal('');
  protected readonly urlName = signal('');
  protected readonly uploading = signal(0);
  protected readonly fetching = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.api.getSources(this.store.scenarioId()).subscribe({ next: s => this.sources.set(s), error: e => this.error.set(errorMessage(e)) });
  }

  protected field(target: { set(value: string): void }, event: Event): void {
    target.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  protected addText(): void {
    this.error.set(null);
    this.api.addTextSource(this.store.scenarioId(), this.textName().trim(), this.text()).subscribe({
      next: source => {
        this.added(source);
        this.textName.set('');
        this.text.set('');
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected addUrl(): void {
    this.error.set(null);
    this.fetching.set(true);
    this.api.addUrlSource(this.store.scenarioId(), this.url().trim(), this.urlName().trim()).subscribe({
      next: source => {
        this.fetching.set(false);
        this.added(source);
        this.url.set('');
        this.urlName.set('');
      },
      error: e => {
        this.fetching.set(false);
        this.error.set(errorMessage(e));
      }
    });
  }

  /** Each file on its own, so one the API can't read doesn't stop the rest. */
  protected chosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    this.error.set(null);
    for (const file of files) {
      this.uploading.update(n => n + 1);
      this.api.uploadSource(this.store.scenarioId(), file).subscribe({
        next: source => {
          this.uploading.update(n => n - 1);
          this.added(source);
        },
        error: e => {
          this.uploading.update(n => n - 1);
          this.error.set(`${file.name}: ${errorMessage(e)}`);
        }
      });
    }
  }

  protected remove(source: SourceView): void {
    this.api.removeSource(this.store.scenarioId(), source.id).subscribe({
      next: () => {
        this.sources.update(all => all.filter(s => s.id !== source.id));
        this.store.refresh();
      },
      error: e => this.error.set(errorMessage(e))
    });
  }

  protected start(): void {
    this.router.navigate(['../questions'], { relativeTo: this.route });
  }

  private added(source: SourceView): void {
    this.sources.update(all => [...all, source]);
    this.store.refresh();
  }
}
