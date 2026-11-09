import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NEVER, of } from 'rxjs';
import { BuilderApiService } from '../../../core/services/builder-api.service';
import { ScenarioType } from '../../../core/models/builder.model';
import { GraySpaceStatus } from '../../../core/models/grayspace.model';
import { WorkspaceStore } from '../workspace.store';
import { TopologyPageComponent } from './topology-page.component';

const scenarioType = (topology?: boolean): ScenarioType => ({ id: 'k', version: '1', name: 'K', stages: [], drafts: [], ...(topology === undefined ? {} : { topology }) });

async function render(k: ScenarioType, grayspace: GraySpaceStatus | null = { connected: false }, deployments?: { target: string }[]) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      { provide: WorkspaceStore, useValue: { scenarioType: signal(k), scenarioId: signal(1) } },
      { provide: BuilderApiService, useValue: { getGraySpace: () => grayspace ? of(grayspace) : NEVER, pull: () => of(deployments ? { deployments } : {}) } }
    ]
  });
  const fixture = TestBed.createComponent(TopologyPageComponent);
  await fixture.whenStable();
  return fixture.nativeElement as HTMLElement;
}

describe('TopologyPageComponent', () => {
  it('opens GraySpace and Updraft, and offers Crucible and another range, which aren’t built yet', async () => {
    const root = await render(scenarioType(true));

    const names = Array.from(root.querySelectorAll('.target-name')).map(e => e.textContent?.trim());
    expect(names).toEqual(['GraySpace', 'Updraft', 'Crucible', 'Other Range']);
    const opens = Array.from(root.querySelectorAll<HTMLAnchorElement>('.target a'));
    expect(opens.map(a => a.textContent?.trim())).toEqual(['Open', 'Open']);
    expect(opens.map(a => a.getAttribute('href'))).toEqual(['/grayspace', '/updraft']);
    const deploys = Array.from(root.querySelectorAll<HTMLButtonElement>('.target button'));
    expect(deploys.map(b => b.textContent?.trim())).toEqual(['Deploy', 'Deploy']);
    expect(deploys.every(b => b.disabled)).toBe(true);
    expect(root.textContent).toContain('Not built yet');
  });

  it('says whether GraySpace is connected and answering', async () => {
    const states = async (grayspace: GraySpaceStatus | null) =>
      Array.from((await render(scenarioType(true), grayspace)).querySelectorAll('.target-state')).slice(0, 2).map(e => e.textContent?.trim());

    expect(await states({ connected: false })).toEqual(['Not connected', 'Not connected']);
    TestBed.resetTestingModule();
    expect(await states({ connected: true, answering: true })).toEqual(['Connected', 'Not connected']);
    TestBed.resetTestingModule();
    expect(await states({ connected: true, answering: false })).toEqual(['Not answering', 'Not connected']);
    TestBed.resetTestingModule();
    expect(await states(null)).toEqual(['Checking…', 'Not connected']);
  });

  it('grays out the other targets once a deployment is built for one', async () => {
    const root = await render(scenarioType(true), { connected: true, answering: true }, [{ target: 'grayspace' }]);

    const cards = Array.from(root.querySelectorAll('.target'));
    expect(cards.map(c => c.classList.contains('unavailable'))).toEqual([false, true, true, true]);
    expect(Array.from(root.querySelectorAll<HTMLAnchorElement>('.target a')).map(a => a.getAttribute('href'))).toEqual(['/grayspace']);
    const buttons = Array.from(root.querySelectorAll<HTMLButtonElement>('.target button'));
    expect(buttons.map(b => b.textContent?.trim())).toEqual(['Open', 'Deploy', 'Deploy']);
    expect(buttons.every(b => b.disabled)).toBe(true);
    expect(Array.from(root.querySelectorAll('.target-state')).map(e => e.textContent?.trim()))
      .toEqual(['Deployment built', 'Not available', 'Not available', 'Not available']);
    expect(root.querySelector('.sub')?.textContent).toContain('GraySpace');
  });

  it('says so when the scenario type has no topology', async () => {
    const root = await render(scenarioType());

    expect(root.querySelector('.target')).toBeNull();
    expect(root.textContent).toContain('This type of scenario has no topology.');
  });
});
