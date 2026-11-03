import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ScenarioTypeAdminSummary, ScenarioTypeDetail } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { ScenarioTypesPageComponent } from './scenario-types-page.component';

const tabletop: ScenarioTypeAdminSummary = { id: 'crucible-tabletop', name: 'Tabletop Exercise (TTX)', latestVersion: '0.3.0', draftChanged: true, scenarios: 2, retired: true };
const fresh: ScenarioTypeAdminSummary = { id: 'red-team', name: 'Red Team Exercise', draftChanged: false, scenarios: 0, retired: false };

async function render() {
  const created: ScenarioTypeDetail = {
    id: 'red-team-exercise', scenarioType: { id: 'red-team-exercise', version: '0.0.0', name: 'Red Team Exercise', stages: [], drafts: [] },
    retired: false, draftChanged: false, scenarios: 0, versions: []
  };
  const admin = {
    scenarioTypes: vi.fn(() => of([tabletop, fresh])),
    models: vi.fn(() => of(['gemma4:26b', 'sonnet-5.5'])),
    createScenarioType: vi.fn((_: unknown) => of(created)),
    importScenarioType: vi.fn()
  };
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AdminApiService, useValue: admin }] });
  const fixture = TestBed.createComponent(ScenarioTypesPageComponent);
  const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  await fixture.whenStable();
  return { fixture, admin, navigate, root: fixture.nativeElement as HTMLElement };
}

const type = (el: HTMLInputElement | HTMLTextAreaElement, text: string) => {
  el.value = text;
  el.dispatchEvent(new Event('input'));
};

const choose = (el: HTMLSelectElement, value: string) => {
  el.value = value;
  el.dispatchEvent(new Event('change'));
};

describe('ScenarioTypesPageComponent', () => {
  it('lists scenario types with their version, scenarios and state', async () => {
    const { root } = await render();
    const rows = Array.from(root.querySelectorAll('.scenario-type')).map(r => r.textContent ?? '');

    expect(rows[0]).toContain('Tabletop Exercise (TTX)');
    expect(rows[0]).toContain('Version 0.3.0');
    expect(rows[0]).toContain('2 scenarios');
    expect(rows[0]).toContain('Retired');
    expect(rows[0]).toContain('Draft changes');
    expect(rows[1]).toContain('Not published');
    expect(rows[1]).not.toContain('Retired');
  });

  it('creates a scenario type and opens it for the chosen model to draft', async () => {
    const { fixture, root, admin, navigate } = await render();
    const [start, model] = Array.from(root.querySelectorAll<HTMLSelectElement>('.create select'));
    expect(Array.from(start.options).map(o => o.textContent?.trim())).toEqual(['Nothing', 'Tabletop Exercise (TTX)']);

    type(root.querySelector<HTMLInputElement>('.create input:not([type])')!, ' Red Team Exercise ');
    type(root.querySelector<HTMLTextAreaElement>('.create textarea')!, 'A team attacks a mock enterprise.');
    choose(start, 'crucible-tabletop');
    choose(model, 'sonnet-5.5');
    root.querySelector<HTMLInputElement>('.create input[type="checkbox"]')!.click();
    await fixture.whenStable();
    root.querySelector<HTMLButtonElement>('.create button[type="submit"]')!.click();

    expect(admin.createScenarioType).toHaveBeenCalledWith({
      name: 'Red Team Exercise', description: 'A team attacks a mock enterprise.', topology: true, basedOn: 'crucible-tabletop'
    });
    expect(navigate).toHaveBeenCalledWith(['/admin/scenario-types', 'red-team-exercise'], { queryParams: { draft: 'sonnet-5.5' } });
  });

  it('without a model, creates it and opens it as it is', async () => {
    const { fixture, root, navigate } = await render();
    type(root.querySelector<HTMLInputElement>('.create input:not([type])')!, 'Blank');
    choose(Array.from(root.querySelectorAll<HTMLSelectElement>('.create select'))[1], '');
    await fixture.whenStable();

    const submit = root.querySelector<HTMLButtonElement>('.create button[type="submit"]')!;
    expect(submit.textContent?.trim()).toBe('Create');
    submit.click();

    expect(navigate).toHaveBeenCalledWith(['/admin/scenario-types', 'red-team-exercise'], {});
  });
});
