import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LibraryDetail } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { LibraryPageComponent } from './library-page.component';

const library: LibraryDetail = {
  id: 1, slug: 'crucible-tabletop-basics', name: 'Crucible tabletop basics', description: 'Starter documents.',
  kinds: ['crucible-tabletop'], latestVersion: 1, draftChanged: true,
  templates: [
    { id: 'design-summary', name: 'Design summary', audience: 'designers', format: 'html', body: '<h1>x</h1>' },
    { id: 'participant-read-in', name: 'Participant read-in', audience: 'participants', format: 'markdown', body: '# x' }
  ],
  versions: [{ number: 1, publishedBy: 'system', published: '2026-10-30T09:00:00Z' }]
};

async function render() {
  const published = { ...library, latestVersion: 2, draftChanged: false, versions: [...library.versions, { number: 2, publishedBy: 'dev', published: '2026-10-30T10:00:00Z' }] };
  const admin = {
    library: vi.fn().mockReturnValueOnce(of(library)).mockReturnValue(of(published)),
    publish: vi.fn(() => of({ number: 2, publishedBy: 'dev', published: '2026-10-30T10:00:00Z' })),
    save: vi.fn(),
    exportUrl: (id: number) => `/api/admin/libraries/${id}/export`
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1' }) } } },
      { provide: AdminApiService, useValue: admin },
      { provide: BuilderApiService, useValue: { getKinds: () => of([{ id: 'crucible-tabletop', version: '1.0.0', name: 'Crucible tabletop exercise' }]) } }
    ]
  });
  const fixture = TestBed.createComponent(LibraryPageComponent);
  await fixture.whenStable();
  return { fixture, admin, root: fixture.nativeElement as HTMLElement };
}

const button = (root: HTMLElement, label: string) =>
  Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === label);

describe('LibraryPageComponent', () => {
  it('lists templates with audience and format', async () => {
    const { root } = await render();
    const rows = Array.from(root.querySelectorAll('.template')).map(r => r.textContent ?? '');

    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain('Design summary');
    expect(rows[0]).toContain('Designers');
    expect(rows[0]).toContain('HTML');
    expect(rows[1]).toContain('Participants');
    expect(rows[1]).toContain('Markdown');
  });

  it('removes a template from the draft', async () => {
    const { fixture, root, admin } = await render();
    admin.save.mockReturnValue(of({ ...library, templates: [library.templates[1]] }));
    const row = Array.from(root.querySelectorAll('.template')).find(r => r.textContent?.includes('Design summary'))!;

    button(row as HTMLElement, 'Remove')!.click();
    await fixture.whenStable();

    expect(admin.save).toHaveBeenCalledWith(1, expect.objectContaining({ templates: [library.templates[1]] }));
    expect(root.querySelectorAll('.template')).toHaveLength(1);
    expect(root.querySelector('[role="status"]')!.textContent).toContain('Removed Design summary from the draft.');
  });

  it('publishes and shows the new version', async () => {
    const { fixture, root, admin } = await render();

    button(root, 'Publish')!.click();
    await fixture.whenStable();

    expect(admin.publish).toHaveBeenCalledWith(1);
    expect(root.querySelector('[role="status"]')!.textContent).toContain('Published version 2.');
    expect(Array.from(root.querySelectorAll('.version')).map(v => v.textContent)).toHaveLength(2);
    expect(button(root, 'Publish')!.disabled).toBe(true);
  });
});
