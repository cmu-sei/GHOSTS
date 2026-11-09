import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Gate } from '../../../core/models/builder.model';
import { GatePanelComponent } from './gate-panel.component';

async function render(gate: Gate, lastStage = false): Promise<ComponentFixture<GatePanelComponent>> {
  const fixture = TestBed.createComponent(GatePanelComponent);
  fixture.componentRef.setInput('gate', gate);
  fixture.componentRef.setInput('lastStage', lastStage);
  await fixture.whenStable();
  return fixture;
}

const buttons = (f: ComponentFixture<unknown>) =>
  Array.from((f.nativeElement as HTMLElement).querySelectorAll('button')).map(b => b.textContent?.trim());

describe('GatePanelComponent', () => {
  it('counts open questions instead of listing them, and offers nothing yet', async () => {
    const open = (id: string) => ({ severity: 'stop' as const, code: 'QUESTION_OPEN', message: `'${id}' is not answered.`, stage: 0 });
    const fixture = await render({ passes: false, findings: [open('headcount'), open('timeSlot')] });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('2 questions open');
    expect(text).not.toContain("'headcount' is not answered.");
    expect(buttons(fixture)).toEqual([]);
  });

  it('names a missing draft, which no row shows', async () => {
    const fixture = await render({
      passes: false,
      findings: [{ severity: 'stop', code: 'DRAFT_MISSING', message: "The executiveSummary draft isn't written yet.", stage: 2 }]
    });

    expect((fixture.nativeElement as HTMLElement).textContent).toContain("The executiveSummary draft isn't written yet.");
  });

  it('offers Advance when the gate passes, and says so on the last stage', async () => {
    expect(buttons(await render({ passes: true, findings: [] }))).toEqual(['Open the next stage']);

    const last = await render({ passes: true, findings: [] }, true);
    expect(buttons(last)).toEqual([]);
    expect((last.nativeElement as HTMLElement).textContent).toContain('last stage');
  });
});
