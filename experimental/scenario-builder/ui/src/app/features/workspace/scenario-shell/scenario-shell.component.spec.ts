import { Component, NO_ERRORS_SCHEMA, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RouterLink, RouterLinkActive, RouterOutlet, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ScenarioType } from '../../../core/models/builder.model';
import { WorkspaceStore } from '../workspace.store';
import { ScenarioShellComponent } from './scenario-shell.component';

@Component({ template: '' })
class PageStub {}

const scenarioType: ScenarioType = { id: 'k', version: '1', name: 'K', drafts: [], stages: [{ number: 0, title: 'Stage', questions: [] }] };

async function harness() {
  TestBed.configureTestingModule({
    providers: [provideRouter([{
      path: 'scenarios/:id', component: ScenarioShellComponent,
      children: ['context', 'questions', 'summary', 'log', 'topology', 'topology/grayspace', 'finish'].map(path => ({ path, component: PageStub }))
    }])]
  });
  // The rail, top bar and agent panel stay unrendered elements; the shell decides only whether the rail is there.
  TestBed.overrideComponent(ScenarioShellComponent, {
    set: {
      imports: [RouterOutlet, RouterLink, RouterLinkActive],
      schemas: [NO_ERRORS_SCHEMA],
      providers: [{
        provide: WorkspaceStore,
        useValue: { load: () => {}, scenarioId: signal(0), name: signal('S'), scenarioType: signal(scenarioType), progress: signal({}), selectedStage: signal(0), error: signal(null) }
      }]
    }
  });
  return RouterTestingHarness.create();
}

describe('ScenarioShellComponent', () => {
  it('shows the stage rail on the Questions page only', async () => {
    const h = await harness();
    const rail = () => (h.fixture.nativeElement as HTMLElement).querySelector('aside.rail');

    await h.navigateByUrl('/scenarios/7/questions');
    expect(rail()).not.toBeNull();

    for (const page of ['context', 'summary', 'log', 'topology', 'topology/grayspace', 'finish']) {
      await h.navigateByUrl(`/scenarios/7/${page}`);
      expect(rail(), page).toBeNull();
    }

    await h.navigateByUrl('/scenarios/7/questions');
    expect(rail()).not.toBeNull();
  });
});
