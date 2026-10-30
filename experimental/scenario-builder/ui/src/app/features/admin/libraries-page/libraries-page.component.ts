import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { KindSummary, LibrarySummary } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';

/** Every template library, a new one, or one carried in from another installation as a .library.json file. */
@Component({
  selector: 'app-libraries-page',
  standalone: true,
  imports: [RouterLink, TopBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './libraries-page.component.html',
  styleUrls: ['./libraries-page.component.scss']
})
export class LibrariesPageComponent implements OnInit {
  private readonly admin = inject(AdminApiService);
  private readonly api = inject(BuilderApiService);
  private readonly router = inject(Router);

  protected readonly libraries = signal<LibrarySummary[] | null>(null);
  protected readonly kinds = signal<KindSummary[]>([]);
  protected readonly name = signal('');
  protected readonly note = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
    this.api.getKinds().subscribe(kinds => this.kinds.set(kinds));
  }

  protected kindNames(ids: string[]): string {
    return ids.length ? ids.map(id => this.kinds().find(k => k.id === id)?.name ?? id).join(', ') : 'No kinds';
  }

  protected typed(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  /** A new library serves every loaded kind until its page says otherwise. */
  protected create(): void {
    this.admin.create(this.name().trim(), '', this.kinds().map(k => k.id)).subscribe({
      next: library => this.router.navigate(['/admin/libraries', library.id]),
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
    this.admin.import(text).subscribe({
      next: library => {
        this.error.set(null);
        this.note.set(`Imported ${library.name} as version ${library.latestVersion}.`);
        this.load();
      },
      error: e => this.error.set(`${file.name}: ${errorMessage(e)}`)
    });
  }

  private load(): void {
    this.admin.libraries().subscribe({ next: l => this.libraries.set(l), error: e => this.error.set(errorMessage(e)) });
  }
}
