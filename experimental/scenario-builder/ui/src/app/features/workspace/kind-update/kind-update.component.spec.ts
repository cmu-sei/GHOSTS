import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { Kind, KindUpdatePlan, Progress } from '../../../core/models/builder.model';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { MeService } from '../../../core/services/me.service';
import { WorkspaceStore } from '../workspace.store';
import { KindUpdateComponent } from './kind-update.component';

const plan: KindUpdatePlan = {
  from: '0.1.0', to: '0.2.0', owner: 'dev',
  carried: [{ id: 'target', label: 'Target', fromStage: 0, toStage: 0 }, { id: 'story', label: 'Story', fromStage: 1, toStage: 0 }],
  dropped: [{ id: 'notes', label: 'Notes', why: 'Its question isn’t in version 0.2.0.' }],
  added: [{ id: 'scope', label: 'Scope', stage: 0 }],
  draftsDropped: ['executiveSummary'],
  stageFrom: 1, stageTo: 0
};

async function render(found: KindUpdatePlan, user = 'dev') {
  const api = {
    getKindUpdate: vi.fn(() => of(found)),
    applyKindUpdate: vi.fn((_: number, __: string) => of(found))
  };
  const store = {
    scenarioId: signal(7),
    kind: signal({ id: 'red-team-dream', version: found.from, name: 'Red Team Dream', stages: [], drafts: [] } as Kind),
    progress: signal({ kindVersion: found.from } as Progress)
  };
  TestBed.configureTestingModule({
    providers: [
      { provide: BuilderApiService, useValue: api },
      { provide: WorkspaceStore, useValue: store },
      { provide: MeService, useValue: { me: signal({ user, admin: false }) } }
    ]
  });
  const fixture = TestBed.createComponent(KindUpdateComponent);
  const reload = vi.spyOn(fixture.componentInstance as unknown as { reloadPage: () => void }, 'reloadPage').mockImplementation(() => {});
  await fixture.whenStable();
  return { fixture, api, reload, root: fixture.nativeElement as HTMLElement };
}

const button = (root: HTMLElement, label: string) => Array.from(root.querySelectorAll('button')).find(b => b.textContent?.trim() === label);

describe('KindUpdateComponent', () => {
  it('says nothing when the scenario is on the latest version', async () => {
    const { root } = await render({ ...plan, to: '0.1.0', carried: [], dropped: [], added: [], draftsDropped: [], stageTo: 1 });

    expect(root.textContent?.trim()).toBe('');
  });

  it('tells the owner a newer version is out, and reviews what changes', async () => {
    const { fixture, root } = await render(plan);

    expect(root.textContent).toContain('Red Team Dream 0.2.0 is published; this scenario is on 0.1.0.');
    button(root, 'Review the update')!.click();
    await fixture.whenStable();

    const review = root.querySelector('.review')!.textContent!;
    expect(review).toContain('2 answers carry over, 1 of them to another stage.');
    expect(review).toContain('Notes');
    expect(review).toContain('Its question isn’t in version 0.2.0.');
    expect(review).toContain('Scope');
    expect(review).toContain('Executive Summary');
    expect(review).toContain('The open stage moves from 1 to 0');
  });

  it('updates to the reviewed version, then reloads the scenario', async () => {
    const { fixture, root, api, reload } = await render(plan);
    button(root, 'Review the update')!.click();
    await fixture.whenStable();

    button(root, 'Update to 0.2.0')!.click();
    await fixture.whenStable();

    expect(api.applyKindUpdate).toHaveBeenCalledWith(7, '0.2.0');
    expect(reload).toHaveBeenCalled();
  });

  it('shows why an update was refused', async () => {
    const { fixture, root, api } = await render(plan);
    api.applyKindUpdate.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { code: 'VERSION_CHANGED', message: 'The latest version is 0.3.0.' } })));
    button(root, 'Review the update')!.click();
    await fixture.whenStable();

    button(root, 'Update to 0.2.0')!.click();
    await fixture.whenStable();

    expect(root.querySelector('[role="alert"]')!.textContent).toContain('The latest version is 0.3.0.');
  });

  it('leaves the update to the owner', async () => {
    const { root } = await render(plan, 'lee');

    expect(root.textContent).toContain('Its owner, dev, can update it.');
    expect(button(root, 'Review the update')).toBeUndefined();
  });
});
