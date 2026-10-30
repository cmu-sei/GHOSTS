import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SourceView } from '../../../core/models/builder.model';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { WorkspaceStore } from '../workspace.store';
import { ContextPageComponent } from './context-page.component';

const source = (id: number, name: string, kind: SourceView['kind'], characters: number, origin?: string): SourceView =>
  ({ id, kind, name, origin, characters, addedBy: 'dev', added: '2026-10-30T09:00:00Z' });

async function render(existing: SourceView[] = []) {
  const api = {
    getSources: vi.fn(() => of(existing)),
    addTextSource: vi.fn((_: number, name: string, text: string) => of(source(10, name, 'text', text.length))),
    addUrlSource: vi.fn((_: number, url: string) => of(source(11, 'Port notice', 'url', 900, url))),
    uploadSource: vi.fn((_: number, file: File) => of(source(12, file.name, 'file', 4200, file.name))),
    removeSource: vi.fn(() => of(undefined))
  };
  const store = { scenarioId: () => 7, refresh: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: {} },
      { provide: BuilderApiService, useValue: api },
      { provide: WorkspaceStore, useValue: store }
    ]
  });
  const fixture = TestBed.createComponent(ContextPageComponent);
  await fixture.whenStable();
  return { fixture, api, store, root: fixture.nativeElement as HTMLElement };
}

const button = (root: Element, label: string) =>
  Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === label) as HTMLButtonElement | undefined;
const type = (root: HTMLElement, label: string, value: string) => {
  const field = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(`[aria-label="${label}"]`)!;
  field.value = value;
  field.dispatchEvent(new Event('input'));
};
const sources = (root: HTMLElement) => Array.from(root.querySelectorAll('.source')).map(s => s.textContent ?? '');

describe('ContextPageComponent', () => {
  it('adds pasted text and lists it with its size', async () => {
    const { fixture, root, api, store } = await render();

    type(root, 'Text name', 'Port notice');
    type(root, 'Text', 'The cranes stopped at 14:10.');
    await fixture.whenStable();
    button(root, 'Add text')!.click();
    await fixture.whenStable();

    expect(api.addTextSource).toHaveBeenCalledWith(7, 'Port notice', 'The cranes stopped at 14:10.');
    expect(sources(root)[0]).toContain('Port notice');
    expect(sources(root)[0]).toContain('28 characters');
    expect(store.refresh).toHaveBeenCalled();
    expect((root.querySelector('[aria-label="Text"]') as HTMLTextAreaElement).value).toBe('');
  });

  it("adds a link, and shows why one couldn't be fetched", async () => {
    const { fixture, root, api } = await render();

    type(root, 'Link', 'https://port.example/notice');
    await fixture.whenStable();
    button(root, 'Add link')!.click();
    await fixture.whenStable();
    expect(api.addUrlSource).toHaveBeenCalledWith(7, 'https://port.example/notice', '');
    expect(sources(root)[0]).toContain('https://port.example/notice');

    api.addUrlSource.mockReturnValueOnce(throwError(() => new HttpErrorResponse({
      status: 422, error: { code: 'URL_UNREACHABLE', message: "Couldn't fetch https://closed.range/: no route. If it can't be reached from here, paste its text instead." }
    })));
    type(root, 'Link', 'https://closed.range/');
    await fixture.whenStable();
    button(root, 'Add link')!.click();
    await fixture.whenStable();
    expect(root.querySelector('[role="alert"]')!.textContent).toContain('paste its text instead');
  });

  it('uploads each chosen file', async () => {
    const { fixture, root, api } = await render();
    const input = root.querySelector<HTMLInputElement>('input[type="file"]')!;
    const files = [new File(['%PDF'], 'brief.pdf'), new File(['x'], 'notes.docx')];
    Object.defineProperty(input, 'files', { value: files });

    input.dispatchEvent(new Event('change'));
    await vi.waitFor(() => expect(api.uploadSource).toHaveBeenCalledTimes(2));
    await fixture.whenStable();

    expect(api.uploadSource).toHaveBeenCalledWith(7, files[0]);
    expect(sources(root).map(s => s.includes('brief.pdf') || s.includes('notes.docx'))).toEqual([true, true]);
  });

  it('removes a source', async () => {
    const { fixture, root, api } = await render([source(4, 'Old notes', 'text', 120)]);

    button(root.querySelector('.source')!, 'Remove')!.click();
    await fixture.whenStable();

    expect(api.removeSource).toHaveBeenCalledWith(7, 4);
    expect(sources(root)).toEqual([]);
  });

  it('goes on to the questions', async () => {
    const { root } = await render([source(4, 'Old notes', 'text', 120)]);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    button(root, 'Start the questions')!.click();

    expect(navigate).toHaveBeenCalledWith(['../questions'], expect.anything());
  });
});
