import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ScenarioType } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { TopologyPageComponent } from './topology-page.component';

const scenarioType = (topology?: boolean): ScenarioType => ({ id: 'k', version: '1', name: 'K', stages: [], drafts: [], ...(topology === undefined ? {} : { topology }) });

async function render(k: ScenarioType) {
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: WorkspaceStore, useValue: { scenarioType: signal(k) } }] });
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

  it('says so when the scenario type has no topology', async () => {
    const root = await render(scenarioType());

    expect(root.querySelector('.target')).toBeNull();
    expect(root.textContent).toContain('This type of scenario has no topology.');
  });
});
