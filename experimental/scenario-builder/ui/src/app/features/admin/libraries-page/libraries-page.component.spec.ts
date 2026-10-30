import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { LibraryDetail, LibrarySummary } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { LibrariesPageComponent } from './libraries-page.component';

const basics: LibrarySummary = {
  id: 1, slug: 'crucible-tabletop-basics', name: 'Crucible tabletop basics', kinds: ['crucible-tabletop'], latestVersion: 2, draftChanged: true
};
const unpublished: LibrarySummary = { id: 2, slug: 'drafts', name: 'Drafts', kinds: [], draftChanged: false };

async function render() {
  const imported: LibraryDetail = { ...basics, latestVersion: 3, draftChanged: false, templates: [], versions: [] };
  const admin = {
    libraries: vi.fn(() => of([basics, unpublished])),
    import: vi.fn((_: string) => of(imported)),
    create: vi.fn()
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: AdminApiService, useValue: admin },
      { provide: BuilderApiService, useValue: { getKinds: () => of([{ id: 'crucible-tabletop', version: '1.0.0', name: 'Crucible tabletop exercise' }]) } }
    ]
  });
  const fixture = TestBed.createComponent(LibrariesPageComponent);
  await fixture.whenStable();
  return { fixture, admin, root: fixture.nativeElement as HTMLElement };
}

describe('LibrariesPageComponent', () => {
  it('lists libraries with version and draft state', async () => {
    const { root } = await render();
    const rows = Array.from(root.querySelectorAll('.library')).map(r => r.textContent ?? '');

    expect(rows[0]).toContain('Crucible tabletop basics');
    expect(rows[0]).toContain('Crucible tabletop exercise');
    expect(rows[0]).toContain('Version 2');
    expect(rows[0]).toContain('Draft changes');
    expect(rows[1]).toContain('Not published');
    expect(rows[1]).not.toContain('Draft changes');
  });

  it('imports a chosen .library.json file', async () => {
    const { fixture, root, admin } = await render();
    const input = root.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(input, 'files', { value: [new File(['{"format":"scenario-builder-library"}'], 'basics.library.json')] });

    input.dispatchEvent(new Event('change'));
    await vi.waitFor(() => expect(admin.import).toHaveBeenCalledWith('{"format":"scenario-builder-library"}'));
    await fixture.whenStable();

    expect(root.querySelector('[role="status"]')!.textContent).toContain('Imported Crucible tabletop basics as version 3.');
    expect(admin.libraries).toHaveBeenCalledTimes(2);
  });
});
