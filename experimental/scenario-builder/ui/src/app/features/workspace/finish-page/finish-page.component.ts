import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { DocumentView, PluginView } from '../../../core/models/builder.model';
import { BuilderApiService, errorMessage } from '../../../core/services/builder-api.service';
import { MeService } from '../../../core/services/me.service';
import { AUDIENCES, FORMATS } from '../../../shared/labels/document-labels';
import { WorkspaceStore } from '../workspace.store';

/**
 * The scenario leaving the builder: its JSON, the documents its kind's libraries offer, and the apps it will
 * be sent to. Nothing here waits for the last gate; open findings only mark each output as a draft.
 */
@Component({
  selector: 'app-finish-page',
  standalone: true,
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './finish-page.component.html',
  styleUrls: ['./finish-page.component.scss']
})
export class FinishPageComponent {
  protected readonly store = inject(WorkspaceStore);
  protected readonly api = inject(BuilderApiService);
  protected readonly me = inject(MeService).me;

  protected readonly plugins = signal<PluginView[]>([]);
  /** The plugin whose address is being entered, and what's typed so far. */
  protected readonly editing = signal<string | null>(null);
  protected readonly draftUrl = signal('');
  protected readonly pluginError = signal<string | null>(null);
  protected readonly audiences = AUDIENCES;
  protected readonly formats = FORMATS;

  protected readonly documents = signal<DocumentView[] | null>(null);
  /** The export shown in place, one at a time. */
  protected readonly shown = signal<'json' | 'markdown' | null>(null);
  protected readonly text = signal<string | null>(null);
  protected readonly sendNote = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly findings = computed(() => this.store.progress()?.gate.findings.length ?? 0);

  constructor() {
    this.api.getPlugins().subscribe({ next: p => this.plugins.set(p), error: e => this.error.set(errorMessage(e)) });
    effect(() => {
      const id = this.store.scenarioId();
      if (id) {
        this.api.getDocuments(id).subscribe({ next: d => this.documents.set(d), error: e => this.error.set(errorMessage(e)) });
      }
    });
    // While shown, an export follows every change the agent or a page makes.
    effect(() => {
      this.store.progress();
      const id = this.store.scenarioId();
      const shown = this.shown();
      if (!id || !shown) {
        return;
      }
      const text = shown === 'json' ? this.api.pull(id).pipe(map(doc => JSON.stringify(doc, null, 2))) : this.api.exportMarkdown(id);
      text.subscribe({ next: t => this.text.set(t), error: e => this.error.set(errorMessage(e)) });
    });
  }

  protected toggle(format: 'json' | 'markdown'): void {
    this.text.set(null);
    this.shown.update(shown => shown === format ? null : format);
  }

  protected send(plugin: PluginView): void {
    this.sendNote.set(null);
    this.api.send(this.store.scenarioId(), plugin.id).subscribe({ error: e => this.sendNote.set(errorMessage(e)) });
  }

  protected openConnect(plugin: PluginView): void {
    this.pluginError.set(null);
    this.draftUrl.set(plugin.url ?? '');
    this.editing.set(plugin.id);
  }

  protected connect(plugin: PluginView): void {
    this.api.connectPlugin(plugin.id, this.draftUrl().trim()).subscribe({
      next: connected => {
        this.replace(connected);
        this.editing.set(null);
      },
      error: e => this.pluginError.set(errorMessage(e))
    });
  }

  protected disconnect(plugin: PluginView): void {
    this.api.disconnectPlugin(plugin.id).subscribe({
      next: () => {
        this.replace({ id: plugin.id, name: plugin.name, description: plugin.description, connected: false });
        this.editing.set(null);
      },
      error: e => this.pluginError.set(errorMessage(e))
    });
  }

  /** Where a connected plugin points, short enough for its card. */
  protected host(url: string): string {
    try {
      return new URL(url).host;
    } catch {
      return url;
    }
  }

  private replace(plugin: PluginView): void {
    this.plugins.update(all => all.map(p => p.id === plugin.id ? plugin : p));
  }
}
