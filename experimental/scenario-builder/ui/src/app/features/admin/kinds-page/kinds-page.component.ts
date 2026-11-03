import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { KindAdminSummary } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { errorMessage } from '../../../core/services/builder-api.service';
import { TopBarComponent } from '../../../shared/components/top-bar/top-bar.component';
import { AdminNavComponent } from '../admin-nav/admin-nav.component';

/**
 * Every kind (shown as a scenario type), and a new one: named, said in a few sentences what it is, and drafted by a model on its page.
 * Or one carried in from another installation as a .kind.json file.
 */
@Component({
  selector: 'app-kinds-page',
  standalone: true,
  imports: [RouterLink, TopBarComponent, AdminNavComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './kinds-page.component.html',
  styleUrls: ['./kinds-page.component.scss']
})
export class KindsPageComponent implements OnInit {
  private readonly admin = inject(AdminApiService);
  private readonly router = inject(Router);

  protected readonly kinds = signal<KindAdminSummary[] | null>(null);
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
    this.admin.createKind({
      name: this.name().trim(), description: this.description().trim(), topology: this.topology(), basedOn: this.basedOn() || undefined
    }).subscribe({
      next: kind => this.router.navigate(['/admin/scenario-types', kind.id], this.model() ? { queryParams: { draft: this.model() } } : {}),
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
    this.admin.importKind(text).subscribe({
      next: kind => {
        this.error.set(null);
        this.note.set(`Imported ${kind.kind.name} as version ${kind.latestVersion}.`);
        this.load();
      },
      error: e => this.error.set(`${file.name}: ${errorMessage(e)}`)
    });
  }

  private load(): void {
    this.admin.kinds().subscribe({ next: k => this.kinds.set(k), error: e => this.error.set(errorMessage(e)) });
  }
}
