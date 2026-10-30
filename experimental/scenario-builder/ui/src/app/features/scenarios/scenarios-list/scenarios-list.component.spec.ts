import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ScenarioSummary } from '../../../core/models/builder.model';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { ScenariosListComponent } from './scenarios-list.component';

const scenario = (id: number, name: string, extra: Partial<ScenarioSummary> = {}): ScenarioSummary => ({
  id, slug: name.toLowerCase().replace(/ /g, '-'), name, kindId: 'crucible-tabletop', kindVersion: '0.1.0', owner: 'dev', stage: 0,
  created: '2026-10-01T09:00:00Z', updated: '2026-10-01T09:00:00Z', archived: false, ...extra
});

const harbor = scenario(1, 'Harbor Blackout', { stage: 2, updated: '2026-10-30T09:00:00Z' });
const rail = scenario(2, 'Rail Freeze', { owner: 'lee', stage: 1, updated: '2026-10-20T09:00:00Z' });
const old = scenario(3, 'Old Drill', { archived: true, updated: '2026-09-01T09:00:00Z' });

async function render() {
  const api = {
    getScenarios: vi.fn(() => of([rail, harbor, old])),
    getKinds: () => of([{ id: 'crucible-tabletop', version: '0.1.0', name: 'Crucible tabletop exercise' }]),
    createScenario: vi.fn(() => of(scenario(9, 'New One'))),
    archive: vi.fn((id: number, archived: boolean) => of({ ...[harbor, rail, old].find(s => s.id === id)!, archived }))
  };
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: BuilderApiService, useValue: api }] });
  const fixture = TestBed.createComponent(ScenariosListComponent);
  await fixture.whenStable();
  return { fixture, api, root: fixture.nativeElement as HTMLElement };
}

const names = (root: HTMLElement) => Array.from(root.querySelectorAll('tbody tr .title')).map(t => t.textContent?.trim());
const button = (root: Element, label: string) =>
  Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === label) as HTMLButtonElement | undefined;
const type = (input: HTMLInputElement, value: string) => {
  input.value = value;
  input.dispatchEvent(new Event('input'));
};

describe('ScenariosListComponent', () => {
  it('lists unarchived scenarios, most recently changed first', async () => {
    const { root } = await render();

    expect(names(root)).toEqual(['Harbor Blackout', 'Rail Freeze']);
  });

  it('searches by name, kind or owner', async () => {
    const { fixture, root } = await render();
    const search = root.querySelector<HTMLInputElement>('input[aria-label="Search scenarios"]')!;

    type(search, 'lee');
    await fixture.whenStable();
    expect(names(root)).toEqual(['Rail Freeze']);

    type(search, 'harbor');
    await fixture.whenStable();
    expect(names(root)).toEqual(['Harbor Blackout']);
  });

  it('sorts by a column, and again in reverse', async () => {
    const { fixture, root } = await render();

    button(root, 'Name')!.click();
    await fixture.whenStable();
    expect(names(root)).toEqual(['Harbor Blackout', 'Rail Freeze']);
    expect(root.querySelector('th[aria-sort="ascending"]')!.textContent).toContain('Name');

    button(root, 'Name')!.click();
    await fixture.whenStable();
    expect(names(root)).toEqual(['Rail Freeze', 'Harbor Blackout']);

    button(root, 'Stage')!.click();
    await fixture.whenStable();
    expect(names(root)).toEqual(['Rail Freeze', 'Harbor Blackout']);
  });

  it('shows archived scenarios when asked, and archives and restores one', async () => {
    const { fixture, root, api } = await render();

    const row = Array.from(root.querySelectorAll('tbody tr')).find(r => r.textContent?.includes('Harbor Blackout'))!;
    button(row, 'Archive')!.click();
    await fixture.whenStable();
    expect(api.archive).toHaveBeenCalledWith(1, true);
    expect(names(root)).toEqual(['Rail Freeze']);

    const showArchived = root.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    showArchived.click();
    await fixture.whenStable();
    expect(names(root)).toEqual(['Harbor Blackout', 'Rail Freeze', 'Old Drill']);
    const archived = Array.from(root.querySelectorAll('tbody tr')).find(r => r.textContent?.includes('Old Drill'))!;
    expect(archived.textContent).toContain('Archived');
    button(archived, 'Restore')!.click();
    await fixture.whenStable();
    expect(api.archive).toHaveBeenCalledWith(3, false);
  });

  it("says why when it can't archive someone else's scenario", async () => {
    const { fixture, root, api } = await render();
    api.archive.mockReturnValueOnce(throwError(() => new HttpErrorResponse({
      status: 403, error: { code: 'NOT_OWNER', message: 'Only lee, the scenario\'s owner, can archive it.' }
    })));

    const row = Array.from(root.querySelectorAll('tbody tr')).find(r => r.textContent?.includes('Rail Freeze'))!;
    button(row, 'Archive')!.click();
    await fixture.whenStable();

    expect(root.querySelector('[role="alert"]')!.textContent).toContain("Only lee, the scenario's owner, can archive it.");
    expect(names(root)).toEqual(['Harbor Blackout', 'Rail Freeze']);
  });

  it('opens a new scenario on its Context page', async () => {
    const { fixture, root } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    type(root.querySelector<HTMLInputElement>('input[aria-label="Scenario name"]')!, 'New One');
    await fixture.whenStable();

    button(root, 'Create')!.click();
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/scenarios', 9, 'context']);
  });
});
