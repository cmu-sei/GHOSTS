import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Gate } from '../../../core/models/builder.model';
import { GatePanelComponent } from './gate-panel.component';

async function render(gate: Gate, lastStage = false, stage = 0): Promise<ComponentFixture<GatePanelComponent>> {
  const fixture = TestBed.createComponent(GatePanelComponent);
  fixture.componentRef.setInput('gate', gate);
  fixture.componentRef.setInput('stage', stage);
  fixture.componentRef.setInput('lastStage', lastStage);
  await fixture.whenStable();
  return fixture;
}

const buttons = (f: ComponentFixture<unknown>) =>
  Array.from((f.nativeElement as HTMLElement).querySelectorAll('button')).map(b => b.textContent?.trim());

describe('GatePanelComponent', () => {
  it('counts open questions instead of listing them, and offers nothing yet', async () => {
    const open = (id: string) => ({ severity: 'stop' as const, code: 'QUESTION_OPEN', message: `'${id}' is not answered.`, stage: 0 });
    const fixture = await render({ passes: false, approved: false, findings: [open('headcount'), open('timeSlot')] });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('2 questions open');
    expect(text).not.toContain("'headcount' is not answered.");
    expect(buttons(fixture)).toEqual([]);
  });

  it('names a missing draft, which no row shows', async () => {
    const fixture = await render({
      passes: false, approved: false,
      findings: [{ severity: 'stop', code: 'DRAFT_MISSING', message: "The executiveSummary draft isn't written yet.", stage: 2 }]
    }, false, 2);

    expect((fixture.nativeElement as HTMLElement).textContent).toContain("The executiveSummary draft isn't written yet.");
  });

  it('offers the approval once it is all that is left', async () => {
    const fixture = await render({
      passes: false, approval: 'Confirm the engagement settings', approved: false,
      findings: [{ severity: 'stop', code: 'APPROVAL_MISSING', message: 'Waiting for the owner: Confirm the engagement settings.', stage: 0 }]
    });
    const approved: number[] = [];
    fixture.componentInstance.approve.subscribe(stage => approved.push(stage));

    expect(buttons(fixture)).toEqual(['Confirm the engagement settings']);
    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    expect(approved).toEqual([0]);
  });

  it('offers to approve a closed stage again when its approval went stale, beside the open stage\'s findings', async () => {
    const fixture = await render({
      passes: false, approved: false,
      findings: [
        { severity: 'stop', code: 'QUESTION_OPEN', message: "'objectives' is not answered.", stage: 1 },
        { severity: 'stop', code: 'APPROVAL_STALE', message: 'Stage 0: Confirm the engagement settings: what was approved has changed since.', stage: 0 }
      ]
    }, false, 1);
    const approved: number[] = [];
    fixture.componentInstance.approve.subscribe(stage => approved.push(stage));

    expect(buttons(fixture)).toEqual(['Approve stage 0 again']);
    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    expect(approved).toEqual([0]);
  });

  it('offers Advance when the gate passes, and says so on the last stage', async () => {
    expect(buttons(await render({ passes: true, approved: true, findings: [] }))).toEqual(['Open the next stage']);

    const last = await render({ passes: true, approved: true, findings: [] }, true);
    expect(buttons(last)).toEqual([]);
    expect((last.nativeElement as HTMLElement).textContent).toContain('last stage');
  });
});
