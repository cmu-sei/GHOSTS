import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ScenarioType, ScenarioTypeDetail, ScenarioTypeStage } from '../../../core/models/builder.model';
import { AdminApiService } from '../../../core/services/admin-api.service';
import { ScenarioTypePageComponent } from './scenario-type-page.component';

const stage = (number: number, title: string, texts: string[] = []): ScenarioTypeStage => ({
  number, title, purpose: `${title} settles things.`,
  questions: texts.map((text, i) => ({ id: `q${number}${i}`, label: `Q${number}${i}`, text, why: '', answer: 'text', path: `/scenario/q${number}${i}` }))
});

const scenarioType = (stages: ScenarioTypeStage[]): ScenarioType => ({ id: 'red-team', version: '0.0.0', name: 'Red Team Exercise', description: 'A team attacks.', stages, drafts: [] });

const detail = (k: ScenarioType, extra: Partial<ScenarioTypeDetail> = {}): ScenarioTypeDetail =>
  ({ id: 'red-team', scenarioType: k, retired: false, draftChanged: k.stages.length > 0, scenarios: 0, versions: [], ...extra });

async function render(start: ScenarioTypeDetail, draft?: string) {
  const admin = {
    scenarioType: vi.fn(() => of(start)),
    models: vi.fn(() => of(['gemma4:26b', 'sonnet-5.5'])),
    saveScenarioType: vi.fn((_: string, k: ScenarioType) => of(detail(k))),
    publishScenarioType: vi.fn(),
    retireScenarioType: vi.fn(),
    deleteScenarioType: vi.fn(),
    draftStages: vi.fn(),
    draftStage: vi.fn(),
    scenarioTypeExportUrl: (id: string) => `/api/admin/scenario-types/${id}/export`
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'red-team' }), queryParamMap: convertToParamMap(draft ? { draft } : {}) } } },
      { provide: AdminApiService, useValue: admin }
    ]
  });
  vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  const fixture = TestBed.createComponent(ScenarioTypePageComponent);
  return { fixture, admin, root: fixture.nativeElement as HTMLElement };
}

const button = (root: HTMLElement, label: string) =>
  Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === label)!;

describe('ScenarioTypePageComponent', () => {
  it('shows the draft as an outline of stages and questions', async () => {
    const { fixture, root } = await render(detail(scenarioType([stage(0, 'Target', ['What does the team attack?']), stage(1, 'Story')]), { latestVersion: '0.1.0', draftChanged: false }));
    await fixture.whenStable();

    expect(Array.from(root.querySelectorAll<HTMLInputElement>('.stage-head .title')).map(i => i.value)).toEqual(['Target', 'Story']);
    expect(root.querySelector<HTMLTextAreaElement>('.question textarea')!.value).toBe('What does the team attack?');
    expect(root.textContent).toContain('No questions yet.');
    expect(root.querySelector('.sub')!.textContent).toContain('Version 0.1.0 is what new scenarios get.');
    expect(button(root, 'Publish').disabled).toBe(true);
    expect(root.querySelector<HTMLAnchorElement>('a[download]')!.getAttribute('href')).toBe('/api/admin/scenario-types/red-team/export');
  });

  it('saves edits to the draft, and publishes only once they are saved', async () => {
    const { fixture, root, admin } = await render(detail(scenarioType([stage(0, 'Target', ['What does the team attack?'])])));
    await fixture.whenStable();

    const wording = root.querySelector<HTMLTextAreaElement>('.question textarea')!;
    wording.value = 'Which network does the team attack?';
    wording.dispatchEvent(new Event('input'));
    button(root, 'Add question').click();
    await fixture.whenStable();
    expect(button(root, 'Publish').disabled).toBe(true);
    button(root, 'Save draft').click();
    await fixture.whenStable();

    const saved = admin.saveScenarioType.mock.calls[0][1] as ScenarioType;
    expect(saved.stages[0].questions.map(q => q.text)).toEqual(['Which network does the team attack?', '']);
    expect(saved.stages[0].questions[1]).toMatchObject({ id: '', answer: 'text', path: '' });
    expect(root.querySelector('[role="status"]')!.textContent).toContain('Saved the draft.');
    expect(button(root, 'Publish').disabled).toBe(false);
  });

  it('removing a stage moves a draft due after it to the stage before', async () => {
    const k = { ...scenarioType([stage(0, 'Target'), stage(1, 'Story'), stage(2, 'Range')]), drafts: [{ field: 'executiveSummary', dueAfterStage: 1, path: '/scenario/executiveSummary' }] };
    const { fixture, root, admin } = await render(detail(k));
    await fixture.whenStable();

    Array.from(root.querySelectorAll<HTMLButtonElement>('.stage-head button')).filter(b => b.textContent?.trim() === 'Remove stage')[1].click();
    await fixture.whenStable();
    button(root, 'Save draft').click();

    const saved = admin.saveScenarioType.mock.calls[0][1] as ScenarioType;
    expect(saved.stages.map(s => [s.number, s.title])).toEqual([[0, 'Target'], [1, 'Range']]);
    expect(saved.drafts[0].dueAfterStage).toBe(0);
  });

  it('opened to draft, plans the stages and then writes each one in turn', async () => {
    const planned = scenarioType([stage(0, 'Target'), stage(1, 'Story')]);
    const { fixture, root, admin } = await render(detail(scenarioType([])), 'sonnet-5.5');
    admin.draftStages.mockReturnValue(of(detail(planned)));
    admin.draftStage
      .mockReturnValueOnce(of(detail(scenarioType([stage(0, 'Target', ['What?']), stage(1, 'Story')]))))
      .mockReturnValueOnce(of(detail(scenarioType([stage(0, 'Target', ['What?']), stage(1, 'Story', ['Why?'])]))));

    fixture.detectChanges();
    await vi.waitFor(() => expect(admin.draftStage).toHaveBeenCalledTimes(2));
    await fixture.whenStable();

    expect(admin.draftStages).toHaveBeenCalledWith('red-team', 'sonnet-5.5');
    expect(admin.draftStage.mock.calls).toEqual([['red-team', 0, 'sonnet-5.5'], ['red-team', 1, 'sonnet-5.5']]);
    expect(Array.from(root.querySelectorAll<HTMLTextAreaElement>('.question textarea')).map(t => t.value)).toEqual(['What?', 'Why?']);
    expect(root.querySelector('[role="status"]')!.textContent).toContain('Drafted.');
  });

  it('when a stage fails, says why and retries from it', async () => {
    const { fixture, root, admin } = await render(detail(scenarioType([])), 'gemma4:26b');
    admin.draftStages.mockReturnValue(of(detail(scenarioType([stage(0, 'Target'), stage(1, 'Story')]))));
    admin.draftStage
      .mockReturnValueOnce(of(detail(scenarioType([stage(0, 'Target', ['What?']), stage(1, 'Story')]))))
      .mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 502, error: { code: 'MODEL_FAILED', message: 'The model kept breaking a rule.' } })))
      .mockReturnValueOnce(of(detail(scenarioType([stage(0, 'Target', ['What?']), stage(1, 'Story', ['Why?'])]))));

    fixture.detectChanges();
    await vi.waitFor(() => expect(root.querySelector('[role="alert"]')?.textContent).toContain('The model kept breaking a rule.'));
    await fixture.whenStable();
    button(root, 'Retry from stage 2').click();
    await vi.waitFor(() => expect(admin.draftStage).toHaveBeenCalledTimes(3));

    expect(admin.draftStage.mock.calls[2]).toEqual(['red-team', 1, 'gemma4:26b']);
  });
});
