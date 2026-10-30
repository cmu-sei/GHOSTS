import { signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DocumentView, Me, PluginView } from '../../../core/models/builder.model';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { MeService } from '../../../core/services/me.service';
import { WorkspaceStore } from '../workspace.store';
import { FinishPageComponent } from './finish-page.component';

const readIn: DocumentView = {
  library: 'crucible-tabletop-basics', libraryName: 'Crucible tabletop basics', version: 1,
  template: 'participant-read-in', name: 'Participant read-in', audience: 'participants', format: 'markdown'
};

const plugin = (id: string, name: string, url?: string): PluginView =>
  ({ id, name, description: `What ${name} does.`, connected: !!url, ...(url ? { url, connectedBy: 'dev' } : {}) });

const plugins: PluginView[] = [
  plugin('blueprint', 'Blueprint'), plugin('cite', 'CITE'), plugin('gallery', 'Gallery'),
  plugin('player', 'Player'), plugin('steamfitter', 'Steamfitter'), plugin('ghosts', 'GHOSTS', 'http://ghosts.local:5000')
];

async function render(documents: DocumentView[] = [readIn], me: Me | null = { user: 'dev', admin: true }) {
  const api = {
    getPlugins: vi.fn(() => of(plugins)),
    connectPlugin: vi.fn((id: string, url: string) => of({ ...plugins.find(p => p.id === id)!, connected: true, url, connectedBy: 'dev' })),
    disconnectPlugin: vi.fn(() => of(undefined)),
    getDocuments: vi.fn(() => of(documents)),
    pull: vi.fn(() => of({ slug: 'harbor-blackout', name: 'Harbor Blackout' })),
    send: vi.fn((_: number, target: string) => throwError(() => new HttpErrorResponse({
      status: 501, error: { code: 'SEND_NOT_BUILT', message: `${target} is connected, but sending a scenario to it isn't built yet.` }
    }))),
    exportMarkdown: vi.fn(() => of('# Harbor Blackout\n')),
    documentUrl: (id: number, library: string, template: string, download = false) =>
      `/api/scenarios/${id}/documents/${library}/${template}${download ? '?download=true' : ''}`,
    exportUrl: (id: number, format: 'json' | 'markdown' = 'json') =>
      `/api/scenarios/${id}/export${format === 'markdown' ? '?format=markdown' : ''}`
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: BuilderApiService, useValue: api },
      { provide: MeService, useValue: { me: signal(me) } },
      { provide: WorkspaceStore, useValue: { scenarioId: () => 7, progress: signal(null), name: signal('Harbor Blackout'), schemaVersion: signal('0.1.0') } }
    ]
  });
  const fixture = TestBed.createComponent(FinishPageComponent);
  await fixture.whenStable();
  return { fixture, api, root: fixture.nativeElement as HTMLElement };
}

const byText = (root: HTMLElement, selector: string, text: string) =>
  Array.from(root.querySelectorAll<HTMLElement>(selector)).find(e => e.textContent?.trim() === text);

describe('FinishPageComponent', () => {
  it('lists each document with its audience, format and library', async () => {
    const { root } = await render();
    const row = root.querySelector('.document')!;

    expect(row.textContent).toContain('Participant read-in');
    expect(row.textContent).toContain('Participants');
    expect(row.textContent).toContain('Markdown');
    expect(row.textContent).toContain('Crucible tabletop basics, version 1');
  });

  it('links Preview and Download to the document URLs', async () => {
    const { root } = await render();

    const preview = byText(root, 'a', 'Preview') as HTMLAnchorElement;
    const download = byText(root, '.document a', 'Download') as HTMLAnchorElement;
    expect(preview.getAttribute('href')).toBe('/api/scenarios/7/documents/crucible-tabletop-basics/participant-read-in');
    expect(preview.target).toBe('_blank');
    expect(download.getAttribute('href')).toBe('/api/scenarios/7/documents/crucible-tabletop-basics/participant-read-in?download=true');
    expect(download.hasAttribute('download')).toBe(true);
    const json = byText(root, 'a', 'Download JSON') as HTMLAnchorElement;
    expect(json.getAttribute('href')).toBe('/api/scenarios/7/export');
    expect(json.hasAttribute('download')).toBe(true);
  });

  it('names the standard format and version the JSON is in', async () => {
    const { root } = await render();

    expect(root.querySelector('.panel .row .name')!.textContent?.trim()).toBe('Scenario JSON (in Crucible Scenario Format Schema v0.1.0)');
  });

  it('shows the JSON on Show JSON', async () => {
    const { fixture, root } = await render();

    expect(root.querySelector('pre')).toBeNull();
    byText(root, 'button', 'Show JSON')!.click();
    await fixture.whenStable();

    expect(root.querySelector('pre')!.textContent).toContain('"slug": "harbor-blackout"');
    expect(byText(root, 'button', 'Hide JSON')).toBeDefined();
  });

  it('shows and downloads the scenario as Markdown', async () => {
    const { fixture, root } = await render();

    const download = byText(root, 'a', 'Download Markdown') as HTMLAnchorElement;
    expect(download.getAttribute('href')).toBe('/api/scenarios/7/export?format=markdown');
    expect(download.hasAttribute('download')).toBe(true);
    byText(root, 'button', 'Show Markdown')!.click();
    await fixture.whenStable();

    expect(root.querySelector('pre')!.textContent).toContain('# Harbor Blackout');
    expect(byText(root, 'button', 'Hide Markdown')).toBeDefined();
    expect(byText(root, 'button', 'Show JSON')).toBeDefined();
  });

  it('shows each plugin as a card with its name and a short description', async () => {
    const { root } = await render();

    const plugins = Array.from(root.querySelectorAll('h2')).find(h => h.textContent?.trim() === 'Plugins')!;
    const cards = Array.from(plugins.closest('section')!.querySelectorAll<HTMLElement>('.plugin'));
    expect(cards.map(c => c.querySelector('.plugin-name')!.textContent?.trim())).toEqual(['Blueprint', 'CITE', 'Gallery', 'Player', 'Steamfitter', 'GHOSTS']);
    expect(cards[0].querySelector('.plugin-description')!.textContent).toContain('What Blueprint does.');
  });

  it('keeps Send greyed out until a plugin is connected', async () => {
    const { root } = await render();
    const send = (name: string) => root.querySelector<HTMLButtonElement>(`button[aria-label="Send to ${name}"]`)!;

    expect(send('Blueprint').disabled).toBe(true);
    expect(send('GHOSTS').disabled).toBe(false);
    expect(root.querySelector('.plugin:last-child')!.textContent).toContain('ghosts.local');
  });

  it('connects a plugin by its API URL from its + button', async () => {
    const { fixture, root, api } = await render();

    root.querySelector<HTMLButtonElement>('button[aria-label="Connect CITE"]')!.click();
    await fixture.whenStable();
    const url = root.querySelector<HTMLInputElement>('input[aria-label="CITE API URL"]')!;
    url.value = 'https://cite.local/api';
    url.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    byText(root.querySelector('.plugin:nth-child(2)') as HTMLElement, 'button', 'Connect')!.click();
    await fixture.whenStable();

    expect(api.connectPlugin).toHaveBeenCalledWith('cite', 'https://cite.local/api');
    expect(root.querySelector<HTMLButtonElement>('button[aria-label="Send to CITE"]')!.disabled).toBe(false);
    expect(root.querySelector('input[aria-label="CITE API URL"]')).toBeNull();
  });

  it('offers + only to admins', async () => {
    const { root } = await render([readIn], { user: 'lee', admin: false });

    expect(root.querySelector('button[aria-label="Connect CITE"]')).toBeNull();
    expect(root.querySelector('.plugin:nth-child(2)')!.textContent).toContain('Not connected');
  });

  it("shows what the API says when a connected plugin's Send is pressed", async () => {
    const { fixture, root, api } = await render();

    root.querySelector<HTMLButtonElement>('button[aria-label="Send to GHOSTS"]')!.click();
    await fixture.whenStable();

    expect(api.send).toHaveBeenCalledWith(7, 'ghosts');
    expect(root.querySelector('[role="status"]')!.textContent).toContain("isn't built yet");
  });

  it('points admins to the admin area when no library serves the kind', async () => {
    const admin = await render([]);
    expect(admin.root.textContent).toContain('No template library serves this kind yet.');
    expect(byText(admin.root, 'a', 'Make one in Admin')!.getAttribute('href')).toBe('/admin');

    TestBed.resetTestingModule();
    const person = await render([], { user: 'lee', admin: false });
    expect(person.root.textContent).toContain('Ask an admin to make one.');
    expect(byText(person.root, 'a', 'Make one in Admin')).toBeUndefined();
  });
});
