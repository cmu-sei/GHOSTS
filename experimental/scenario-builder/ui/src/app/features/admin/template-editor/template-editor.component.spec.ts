import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LibraryDetail } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { TemplateEditorComponent } from './template-editor.component';

const library: LibraryDetail = {
  id: 1, slug: 'basics', name: 'Basics', kinds: ['crucible-tabletop'], latestVersion: 1, draftChanged: false, versions: [],
  templates: [{ id: 'read-in', name: 'Read-in', audience: 'participants', format: 'markdown', body: '# {{ scenario.name }}\n{% if %}' }]
};

async function render(preview: () => ReturnType<AdminApiService['preview']>) {
  const admin = { library: vi.fn(() => of(library)), preview: vi.fn(preview), save: vi.fn(() => of(library)) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: '1', templateId: 'read-in' }) } } },
      { provide: AdminApiService, useValue: admin },
      {
        provide: BuilderApiService,
        useValue: { getScenarios: () => of([{ id: 4, slug: 'harbor', name: 'Harbor Blackout', kindId: 'crucible-tabletop', kindVersion: '1', owner: 'dev', stage: 2, updated: '' }]) }
      }
    ]
  });
  const fixture = TestBed.createComponent(TemplateEditorComponent);
  await fixture.whenStable();
  return { fixture, admin, root: fixture.nativeElement as HTMLElement };
}

describe('TemplateEditorComponent', () => {
  it('shows a template error with its line instead of the preview', async () => {
    const { root, admin } = await render(() => throwError(() => new HttpErrorResponse({
      status: 400, error: JSON.stringify({ code: 'TEMPLATE_INVALID', message: 'Read-in, line 2: Expecting an expression.' })
    })));

    expect(admin.preview).toHaveBeenCalledWith(expect.objectContaining({ scenarioId: 4, libraryId: 1, name: 'Read-in', audience: 'participants' }));
    expect(root.querySelector('[role="alert"]')!.textContent).toContain('Read-in, line 2: Expecting an expression.');
    expect(root.querySelector('iframe')).toBeNull();
  });

  it('marks unsaved changes', async () => {
    const { fixture, root } = await render(() => of('<p>Harbor</p>'));
    expect(root.querySelector('iframe')!.getAttribute('sandbox')).toBe('');
    expect(root.textContent).not.toContain('Unsaved changes');

    const body = root.querySelector<HTMLTextAreaElement>('textarea')!;
    body.value = '# Changed';
    body.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(root.textContent).toContain('Unsaved changes');
  });
});
